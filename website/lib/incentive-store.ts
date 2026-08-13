import type {
  FeatureContent,
  IncentiveSubmission,
  FeatureContentVersionOperation,
  PublicReleasePreview,
  PublicSuggestion,
  ReleasePreview,
  SubmissionAttachment,
  SubmissionKind,
  SubmissionStatus,
  RewardStatus
} from "@/data/incentives-types";
import {
  defaultFeatureContent,
  isFeatureReleaseVersion,
  sanitizeFeatureContent
} from "@/data/feature-content";
import {
  defaultReleasePreview,
  releasePreviewFallback,
  type ReleasePreviewInput
} from "@/data/release-preview";
import type { AccessEventSource } from "@/lib/access-log";

type SupabaseConfig = {
  url: string;
  key: string;
  bucket: string;
};

type StoredSubmission = Omit<
  IncentiveSubmission,
  "developer_reply" | "is_flagged" | "is_public"
> & { reviewer_note: string | null };

type StoredFeatureRow = Omit<ReleasePreview, "highlights_zh" | "highlights_en"> & {
  highlights_zh: unknown;
  highlights_en: unknown;
};

function firstText(...values: unknown[]) {
  for (const value of values) {
    if (typeof value === "string" && value.trim()) return value.trim();
  }
  return "";
}

function firstLines(...values: unknown[]) {
  for (const value of values) {
    if (!Array.isArray(value)) continue;
    const lines = value.filter((item): item is string => typeof item === "string")
      .map((item) => item.trim())
      .filter(Boolean);
    if (lines.length) return lines;
  }
  return [] as string[];
}

function normalizeReleasePreview(preview: ReleasePreview): ReleasePreview {
  const titleZh = firstText(preview.title_zh);
  const titleEn = firstText(preview.title_en, titleZh);
  const bodyZh = firstText(preview.body_zh);
  const bodyEn = firstText(preview.body_en, bodyZh);
  const highlightsZh = firstLines(preview.highlights_zh);
  const highlightsEn = firstLines(preview.highlights_en, highlightsZh);
  return {
    ...preview,
    title_zh: titleZh,
    title_en: titleEn,
    title_zh_tw: firstText(preview.title_zh_tw, titleZh),
    title_ja: firstText(preview.title_ja, titleEn, titleZh),
    body_zh: bodyZh,
    body_en: bodyEn,
    body_zh_tw: firstText(preview.body_zh_tw, bodyZh),
    body_ja: firstText(preview.body_ja, bodyEn, bodyZh),
    highlights_zh: highlightsZh,
    highlights_en: highlightsEn,
    highlights_zh_tw: firstLines(preview.highlights_zh_tw, highlightsZh),
    highlights_ja: firstLines(preview.highlights_ja, highlightsEn, highlightsZh)
  };
}

const DEFAULT_PUBLIC_PREVIEW_LIMIT = 20;
const MAX_PUBLIC_PREVIEW_LIMIT = 50;

export type PublicPreviewPageOptions = {
  cursor?: { publishedAt: string; id: string };
  limit?: number;
};

export function parsePublicPreviewPageOptions(request: Request): PublicPreviewPageOptions {
  const url = new URL(request.url);
  const rawLimit = url.searchParams.get("preview_limit");
  const parsedLimit = rawLimit ? Number.parseInt(rawLimit, 10) : DEFAULT_PUBLIC_PREVIEW_LIMIT;
  const limit = Number.isInteger(parsedLimit)
    ? Math.min(Math.max(parsedLimit, 1), MAX_PUBLIC_PREVIEW_LIMIT)
    : DEFAULT_PUBLIC_PREVIEW_LIMIT;
  const rawCursor = url.searchParams.get("preview_cursor");
  if (!rawCursor) return { limit };
  const [rawPublishedAt, rawId, ...extra] = rawCursor.split("~");
  let publishedAt = "";
  let id = "";
  try {
    publishedAt = decodeURIComponent(rawPublishedAt ?? "");
    id = decodeURIComponent(rawId ?? "");
  } catch {
    throw new Error("Invalid preview cursor");
  }
  if (extra.length || !publishedAt || !id || id.length > 128 || Number.isNaN(Date.parse(publishedAt))) {
    throw new Error("Invalid preview cursor");
  }
  return { limit, cursor: { publishedAt, id } };
}

function publicPreviewCursor(preview: ReleasePreview) {
  if (!preview.published_at) return null;
  return `${encodeURIComponent(preview.published_at)}~${encodeURIComponent(preview.id)}`;
}

function majorVersionOf(version: string) {
  const match = version.trim().match(/^v?\s*(\d+)/i);
  return match ? `V${match[1]}` : "OTHER";
}

function toPublicReleasePreview(preview: ReleasePreview): PublicReleasePreview {
  const normalized = normalizeReleasePreview(preview);
  return { ...normalized, major_version: majorVersionOf(normalized.version) };
}

function publicPreviewQuery(options: Required<Pick<PublicPreviewPageOptions, "limit">> & Pick<PublicPreviewPageOptions, "cursor">) {
  const params = new URLSearchParams({
    select: "*",
    status: "eq.published",
    order: "published_at.desc,id.desc",
    limit: String(options.limit + 1)
  });
  if (options.cursor) {
    const { publishedAt, id } = options.cursor;
    params.set("or", `(published_at.lt.${publishedAt},and(published_at.eq.${publishedAt},id.lt.${id}))`);
  }
  return `/rest/v1/release_previews?${params.toString()}`;
}

const REVIEW_META_PREFIX = "[[lyric-island-review:v1]]";
const FEATURE_CONTENT_VERSION = "__FEATURE_CONTENT_V1__";

function decodeReviewMeta(value: string | null | undefined) {
  if (!value?.startsWith(REVIEW_META_PREFIX)) {
    return {
      developer_reply: value || null,
      is_flagged: false,
      is_public: false,
      source: null as AccessEventSource | null
    };
  }
  try {
    const parsed = JSON.parse(value.slice(REVIEW_META_PREFIX.length)) as Record<string, unknown>;
    const submitted = parsed.submitted && typeof parsed.submitted === "object"
      ? parsed.submitted as Record<string, unknown>
      : null;
    return {
      developer_reply: typeof parsed.reply === "string" && parsed.reply ? parsed.reply : null,
      is_flagged: parsed.flagged === true,
      is_public: parsed.public === true,
      source: submitted && typeof submitted.visitor_hash === "string"
        ? {
          visitor_hash: submitted.visitor_hash,
          ip_address: typeof submitted.ip_address === "string" ? submitted.ip_address : null,
          ip_source: typeof submitted.ip_source === "string" ? submitted.ip_source : null,
          country: typeof submitted.country === "string" ? submitted.country : null,
          region: typeof submitted.region === "string" ? submitted.region : null,
          city: typeof submitted.city === "string" ? submitted.city : null,
          user_agent: typeof submitted.user_agent === "string" ? submitted.user_agent : null,
          accept_language: typeof submitted.accept_language === "string" ? submitted.accept_language : null,
          request_id: typeof submitted.request_id === "string" ? submitted.request_id : null,
          forwarded_for: typeof submitted.forwarded_for === "string" ? submitted.forwarded_for : null,
          referrer: typeof submitted.referrer === "string" ? submitted.referrer : null
          }
        : null
    };
  } catch {
    return {
      developer_reply: null,
      is_flagged: false,
      is_public: false,
      source: null as AccessEventSource | null
    };
  }
}

function encodeReviewMeta(meta: {
  developer_reply: string | null;
  is_flagged: boolean;
  is_public: boolean;
  source: AccessEventSource | null;
}) {
  return `${REVIEW_META_PREFIX}${JSON.stringify({
    reply: meta.developer_reply ?? "",
    flagged: meta.is_flagged,
    public: meta.is_public,
    submitted: meta.source
  })}`;
}

function toSubmission(row: StoredSubmission): IncentiveSubmission {
  const { reviewer_note, ...submission } = row;
  const meta = decodeReviewMeta(reviewer_note);
  return {
    ...submission,
    developer_reply: meta.developer_reply,
    is_flagged: meta.is_flagged,
    is_public: meta.is_public
  };
}

function getConfig(): SupabaseConfig {
  const url = process.env.SUPABASE_URL?.replace(/\/$/, "");
  const key = process.env.SUPABASE_SERVICE_ROLE_KEY;
  const bucket = process.env.SUPABASE_STORAGE_BUCKET ?? "lyric-island-submissions";
  if (!url || !key) {
    throw new Error("Submission storage is not configured");
  }
  return { url, key, bucket };
}

function headers(prefer?: string) {
  const { key } = getConfig();
  return {
    apikey: key,
    Authorization: `Bearer ${key}`,
    "Content-Type": "application/json",
    ...(prefer ? { Prefer: prefer } : {})
  };
}

async function supabase<T>(path: string, init?: RequestInit): Promise<T> {
  const { url } = getConfig();
  const response = await fetch(`${url}${path}`, {
    ...init,
    headers: {
      ...headers(),
      ...(init?.headers ?? {})
    },
    cache: "no-store"
  });
  if (!response.ok) {
    const detail = await response.text();
    throw new Error(`Storage request failed (${response.status}): ${detail.slice(0, 300)}`);
  }
  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

function cleanFileName(name: string) {
  const extension = name.includes(".") ? `.${name.split(".").pop()}` : "";
  return extension.toLowerCase().replace(/[^.a-z0-9]/g, "").slice(0, 12);
}

export async function uploadAttachments(
  files: File[],
  submissionId: string
): Promise<SubmissionAttachment[]> {
  const { url, key, bucket } = getConfig();
  const attachments: SubmissionAttachment[] = [];
  for (const file of files) {
    const path = `${submissionId}/${crypto.randomUUID()}${cleanFileName(file.name)}`;
    const response = await fetch(
      `${url}/storage/v1/object/${encodeURIComponent(bucket)}/${path}`,
      {
        method: "POST",
        headers: {
          apikey: key,
          Authorization: `Bearer ${key}`,
          "Content-Type": file.type,
          "x-upsert": "false"
        },
        body: file,
        cache: "no-store"
      }
    );
    if (!response.ok) {
      throw new Error(`Attachment upload failed (${response.status})`);
    }
    attachments.push({
      path,
      name: file.name.slice(0, 180),
      type: file.type,
      size: file.size
    });
  }
  return attachments;
}

export async function createSubmission(input: {
  id: string;
  kind: SubmissionKind;
  nickname: string;
  email: string;
  title: string;
  body: string;
  attachments: SubmissionAttachment[];
  source?: AccessEventSource | null;
}) {
  const { source = null, ...submission } = input;
  const rows = await supabase<StoredSubmission[]>(
    "/rest/v1/incentive_submissions",
    {
      method: "POST",
      headers: headers("return=representation"),
      body: JSON.stringify({
        ...submission,
        reward_status: "pending",
        reviewer_note: encodeReviewMeta({
          developer_reply: null,
          is_flagged: false,
          is_public: false,
          source
        })
      })
    }
  );
  return toSubmission(rows[0]);
}

export async function getPublicIncentives(voterHash?: string, options: PublicPreviewPageOptions = {}) {
  const rows = await supabase<Array<Pick<StoredSubmission, "id" | "kind" | "nickname" | "title" | "body" | "created_at" | "like_count" | "attachments" | "reviewer_note" | "status">>>(
    "/rest/v1/incentive_submissions?select=id,kind,nickname,title,body,created_at,like_count,attachments,reviewer_note,status&order=updated_at.desc&limit=100"
  );
  const likedRows = voterHash
    ? await supabase<Array<{ submission_id: string }>>(
        `/rest/v1/incentive_likes?select=submission_id&voter_token_hash=eq.${encodeURIComponent(voterHash)}&limit=200`
      )
    : [];
  const likedIds = new Set(likedRows.map((row) => row.submission_id));
  const publicRows = rows.filter((row) => row.status === "accepted" && decodeReviewMeta(row.reviewer_note).is_public).slice(0, 24);
  const suggestions = await Promise.all(publicRows.map(async ({ attachments, reviewer_note, status: _status, ...suggestion }) => {
    const first = attachments?.[0];
    const url = first ? await createSignedUrl(first.path) : undefined;
    return {
      ...suggestion,
      developer_reply: decodeReviewMeta(reviewer_note).developer_reply,
      liked: likedIds.has(suggestion.id),
      ...(first && url
        ? { attachment: { name: first.name, type: first.type, url } }
        : {})
    };
  }));
  const previewLimit = options.limit ?? DEFAULT_PUBLIC_PREVIEW_LIMIT;
  const previewRows = await supabase<ReleasePreview[]>(publicPreviewQuery({
    limit: previewLimit,
    cursor: options.cursor
  }));
  const hasMorePreviews = previewRows.length > previewLimit;
  const previews = previewRows.slice(0, previewLimit).map(toPublicReleasePreview);
  return {
    suggestions,
    previews: previews.length || options.cursor
      ? previews
      : [toPublicReleasePreview(releasePreviewFallback())],
    next_preview_cursor: hasMorePreviews ? publicPreviewCursor(previewRows[previewLimit - 1]) : null
  };
}

export async function toggleSuggestionLike(
  submissionId: string,
  voterTokenHash: string
) {
  const submissions = await supabase<Array<{ id: string; like_count: number; reviewer_note: string | null; status: SubmissionStatus }>>(
    `/rest/v1/incentive_submissions?select=id,like_count,reviewer_note,status&id=eq.${encodeURIComponent(submissionId)}&limit=1`
  );
  const submission = submissions[0];
  if (!submission || submission.status !== "accepted" || !decodeReviewMeta(submission.reviewer_note).is_public) {
    throw new Error("Suggestion is not available for likes");
  }
  const existing = await supabase<Array<{ submission_id: string }>>(
    `/rest/v1/incentive_likes?select=submission_id&submission_id=eq.${encodeURIComponent(submissionId)}&voter_token_hash=eq.${encodeURIComponent(voterTokenHash)}&limit=1`
  );
  if (existing.length > 0) {
    return { liked: true, like_count: submission.like_count, already_liked: true };
  }
  await supabase<Array<{ submission_id: string }>>("/rest/v1/incentive_likes", {
    method: "POST",
    headers: headers("return=representation"),
    body: JSON.stringify({ submission_id: submissionId, voter_token_hash: voterTokenHash })
  });
  const likeCount = submission.like_count + 1;
  await supabase<StoredSubmission[]>(
    `/rest/v1/incentive_submissions?id=eq.${encodeURIComponent(submissionId)}`,
    { method: "PATCH", headers: headers("return=representation"), body: JSON.stringify({ like_count: likeCount }) }
  );
  return { liked: true, like_count: likeCount, already_liked: false };
}

async function createSignedUrl(path: string) {
  const { url, key, bucket } = getConfig();
  const response = await fetch(
    `${url}/storage/v1/object/sign/${encodeURIComponent(bucket)}/${path}`,
    {
      method: "POST",
      headers: {
        apikey: key,
        Authorization: `Bearer ${key}`,
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ expiresIn: 3600 }),
      cache: "no-store"
    }
  );
  if (!response.ok) return undefined;
  const data = (await response.json()) as { signedURL?: string };
  return data.signedURL ? `${url}/storage/v1${data.signedURL}` : undefined;
}

export async function listSubmissions() {
  const rows = await supabase<StoredSubmission[]>(
    "/rest/v1/incentive_submissions?select=*&order=created_at.desc&limit=200"
  );
  return Promise.all(
    rows.map(async (storedRow) => {
      const row = toSubmission(storedRow);
      return ({
      ...row,
      attachments: await Promise.all(
        (row.attachments ?? []).map(async (attachment) => ({
          ...attachment,
          signedUrl: await createSignedUrl(attachment.path)
        }))
      )
    });})
  );
}

export async function updateSubmission(
  id: string,
  changes: {
    kind?: SubmissionKind;
    nickname?: string;
    email?: string;
    title?: string;
    body?: string;
    status?: SubmissionStatus;
    reward_status?: RewardStatus;
    developer_reply?: string | null;
    is_flagged?: boolean;
    is_public?: boolean;
    like_count?: number;
    created_at?: string;
  }
) {
  const currentRows = await supabase<StoredSubmission[]>(
    `/rest/v1/incentive_submissions?select=*&id=eq.${encodeURIComponent(id)}&limit=1`
  );
  const current = currentRows[0];
  if (!current) throw new Error("Submission not found");
  const previous = toSubmission(current);
  const currentMeta = decodeReviewMeta(current.reviewer_note);
  const { developer_reply, is_flagged, is_public, ...storedChanges } = changes;
  const effectiveStatus = changes.status ?? current.status;
  const rows = await supabase<StoredSubmission[]>(
    `/rest/v1/incentive_submissions?id=eq.${encodeURIComponent(id)}`,
    {
      method: "PATCH",
      headers: headers("return=representation"),
      body: JSON.stringify({
        ...storedChanges,
        reviewer_note: encodeReviewMeta({
          developer_reply: developer_reply !== undefined ? developer_reply : currentMeta.developer_reply,
          is_flagged: is_flagged !== undefined ? is_flagged : currentMeta.is_flagged,
          is_public: effectiveStatus === "accepted"
            ? (is_public !== undefined ? is_public : currentMeta.is_public)
            : false,
          source: currentMeta.source
        }),
        updated_at: new Date().toISOString()
      })
    }
  );
  return { submission: toSubmission(rows[0]), previous };
}

export async function deleteSubmission(id: string) {
  const currentRows = await supabase<StoredSubmission[]>(
    `/rest/v1/incentive_submissions?select=*&id=eq.${encodeURIComponent(id)}&limit=1`
  );
  const current = currentRows[0];
  if (!current) throw new Error("Submission not found");

  const { url, key, bucket } = getConfig();
  await Promise.allSettled(
    (current.attachments ?? []).map((attachment) =>
      fetch(
        `${url}/storage/v1/object/${encodeURIComponent(bucket)}/${attachment.path}`,
        {
          method: "DELETE",
          headers: {
            apikey: key,
            ...(key.startsWith("sb_") ? {} : { Authorization: `Bearer ${key}` })
          },
          cache: "no-store"
        }
      )
    )
  );

  await supabase<StoredSubmission[]>(
    `/rest/v1/incentive_submissions?id=eq.${encodeURIComponent(id)}`,
    { method: "DELETE", headers: headers("return=representation") }
  );
  return toSubmission(current);
}

export async function listReleasePreviews() {
  const rows = await supabase<ReleasePreview[]>(
    "/rest/v1/release_previews?select=*&version=not.in.(__FEATURE_CONTENT_V1__,__AUDIT_LOG_V1__)&order=created_at.desc&limit=50"
  );
  const previews = rows.filter((row) => !row.version.startsWith("__"));
  if (previews.length) return previews.map(normalizeReleasePreview);
  return [await createReleasePreview(defaultReleasePreview)];
}

export async function createReleasePreview(
  input: ReleasePreviewInput
) {
  const now = new Date().toISOString();
  const rows = await supabase<ReleasePreview[]>("/rest/v1/release_previews", {
    method: "POST",
    headers: headers("return=representation"),
    body: JSON.stringify({
      ...input,
      published_at: input.status === "published" ? now : null
    })
  });
  return normalizeReleasePreview(rows[0]);
}

export async function updateReleasePreview(
  id: string,
  input: Partial<
    Omit<ReleasePreview, "id" | "created_at" | "updated_at" | "published_at">
  >
) {
  const rows = await supabase<ReleasePreview[]>(
    `/rest/v1/release_previews?id=eq.${encodeURIComponent(id)}`,
    {
      method: "PATCH",
      headers: headers("return=representation"),
      body: JSON.stringify({
        ...input,
        updated_at: new Date().toISOString(),
        ...(input.status
          ? { published_at: input.status === "published" ? new Date().toISOString() : null }
          : {})
      })
    }
  );
  return normalizeReleasePreview(rows[0]);
}

async function getFeatureContentRow() {
  const rows = await supabase<StoredFeatureRow[]>(
    `/rest/v1/release_previews?select=*&version=eq.${encodeURIComponent(FEATURE_CONTENT_VERSION)}&order=updated_at.desc&limit=1`
  );
  return rows[0];
}

function featureContentRowPayload(content: FeatureContent) {
  return {
    version: FEATURE_CONTENT_VERSION,
    title_zh: "新功能页内容",
    title_en: "Updates page content",
    body_zh: "由维护者后台管理的新功能页内容。",
    body_en: "Updates page content managed in the maintainer console.",
    highlights_zh: content,
    highlights_en: [],
    target_date: null,
    status: "draft" as const,
    published_at: null
  };
}

export async function getFeatureContent() {
  const existing = await getFeatureContentRow();
  if (existing) return sanitizeFeatureContent(existing.highlights_zh);

  const content = sanitizeFeatureContent(defaultFeatureContent);
  const rows = await supabase<StoredFeatureRow[]>("/rest/v1/release_previews", {
    method: "POST",
    headers: headers("return=representation"),
    body: JSON.stringify(featureContentRowPayload(content))
  });
  return sanitizeFeatureContent(rows[0]?.highlights_zh ?? content);
}

export async function saveFeatureContent(value: unknown) {
  const rawSections = value && typeof value === "object" && Array.isArray((value as { sections?: unknown }).sections)
    ? (value as { sections: unknown[] }).sections
    : [];
  if (rawSections.some((section) => !section || typeof section !== "object" || typeof (section as { release_version?: unknown }).release_version !== "string" || !(section as { release_version: string }).release_version.trim())) {
    throw new Error("Every feature section requires a complete release version");
  }
  const content = sanitizeFeatureContent(value);
  if (content.sections.some((section) =>
    section.visible &&
    (!section.title_zh || !section.title_en || !section.body_zh || !section.body_en)
  )) {
    throw new Error("Visible feature sections require bilingual titles and descriptions");
  }
  if (content.sections.some((section) => !isFeatureReleaseVersion(section.release_version))) {
    throw new Error("Every feature section requires a complete release version");
  }
  if (content.versions.some((version) => !isFeatureReleaseVersion(version) || version === "早期更新")) {
    throw new Error("Feature versions must use complete release versions");
  }
  if (content.sections.some((section) => section.release_version !== "早期更新" && !content.versions.includes(section.release_version))) {
    throw new Error("Every feature section must belong to a managed feature version");
  }
  const existing = await getFeatureContentRow();
  if (!existing) {
    const rows = await supabase<StoredFeatureRow[]>("/rest/v1/release_previews", {
      method: "POST",
      headers: headers("return=representation"),
      body: JSON.stringify(featureContentRowPayload(content))
    });
    return sanitizeFeatureContent(rows[0]?.highlights_zh ?? content);
  }

  const rows = await supabase<StoredFeatureRow[]>(
    `/rest/v1/release_previews?id=eq.${encodeURIComponent(existing.id)}`,
    {
      method: "PATCH",
      headers: headers("return=representation"),
      body: JSON.stringify({
        highlights_zh: content,
        updated_at: new Date().toISOString()
      })
    }
  );
  return sanitizeFeatureContent(rows[0]?.highlights_zh ?? content);
}

export async function applyFeatureContentVersionOperation(operation: FeatureContentVersionOperation) {
  const current = await getFeatureContent();
  const content = sanitizeFeatureContent(current);
  if (operation.type === "create") {
    const releaseVersion = operation.release_version.trim();
    if (!/^v\d+\.\d+\.\d+$/i.test(releaseVersion) || releaseVersion === "早期更新") {
      throw new Error("Feature versions must use complete release versions");
    }
    if (content.versions.includes(releaseVersion)) throw new Error("Feature version already exists");
    return saveFeatureContent({ ...content, versions: [...content.versions, releaseVersion] });
  }
  if (operation.type === "rename") {
    const from = operation.from.trim();
    const to = operation.to.trim();
    if (from === "早期更新" || !content.versions.includes(from)) throw new Error("Feature version not found");
    if (!/^v\d+\.\d+\.\d+$/i.test(to) || to === "早期更新") throw new Error("Feature versions must use complete release versions");
    if (content.versions.includes(to)) throw new Error("Feature version already exists");
    return saveFeatureContent({
      ...content,
      versions: content.versions.map((version) => version === from ? to : version),
      sections: content.sections.map((section) => section.release_version === from
        ? { ...section, release_version: to }
        : section)
    });
  }
  const releaseVersion = operation.release_version.trim();
  if (releaseVersion === "早期更新" || !content.versions.includes(releaseVersion)) throw new Error("Feature version not found");
  const sectionCount = content.sections.filter((section) => section.release_version === releaseVersion).length;
  if (sectionCount > 0 && operation.delete_sections !== true) {
    throw new Error(`Feature version contains ${sectionCount} sections`);
  }
  return saveFeatureContent({
    ...content,
    versions: content.versions.filter((version) => version !== releaseVersion),
    sections: operation.delete_sections === true
      ? content.sections.filter((section) => section.release_version !== releaseVersion)
      : content.sections
  });
}
