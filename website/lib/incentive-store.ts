import type {
  IncentiveSubmission,
  PublicSuggestion,
  ReleasePreview,
  SubmissionAttachment,
  SubmissionKind,
  SubmissionStatus,
  RewardStatus
} from "@/data/incentives-types";

type SupabaseConfig = {
  url: string;
  key: string;
  bucket: string;
};

type StoredSubmission = Omit<
  IncentiveSubmission,
  "developer_reply" | "is_flagged" | "is_public"
> & { reviewer_note: string | null };

const REVIEW_META_PREFIX = "[[lyric-island-review:v1]]";

function decodeReviewMeta(value: string | null | undefined) {
  if (!value?.startsWith(REVIEW_META_PREFIX)) {
    return { developer_reply: value || null, is_flagged: false, is_public: false };
  }
  try {
    const parsed = JSON.parse(value.slice(REVIEW_META_PREFIX.length)) as Record<string, unknown>;
    return {
      developer_reply: typeof parsed.reply === "string" && parsed.reply ? parsed.reply : null,
      is_flagged: parsed.flagged === true,
      is_public: parsed.public === true
    };
  } catch {
    return { developer_reply: null, is_flagged: false, is_public: false };
  }
}

function encodeReviewMeta(meta: { developer_reply: string | null; is_flagged: boolean; is_public: boolean }) {
  return `${REVIEW_META_PREFIX}${JSON.stringify({
    reply: meta.developer_reply ?? "",
    flagged: meta.is_flagged,
    public: meta.is_public
  })}`;
}

function toSubmission(row: StoredSubmission): IncentiveSubmission {
  const { reviewer_note, ...submission } = row;
  return { ...submission, ...decodeReviewMeta(reviewer_note) };
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
}) {
  const rows = await supabase<StoredSubmission[]>(
    "/rest/v1/incentive_submissions",
    {
      method: "POST",
      headers: headers("return=representation"),
      body: JSON.stringify(input)
    }
  );
  return toSubmission(rows[0]);
}

export async function getPublicIncentives(voterHash?: string) {
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
  const previews = await supabase<ReleasePreview[]>(
    "/rest/v1/release_previews?select=*&status=eq.published&order=published_at.desc&limit=6"
  );
  return { suggestions, previews };
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
  const liked = existing.length === 0;
  if (liked) {
    await supabase<Array<{ submission_id: string }>>("/rest/v1/incentive_likes", {
      method: "POST",
      headers: headers("return=representation"),
      body: JSON.stringify({ submission_id: submissionId, voter_token_hash: voterTokenHash })
    });
  } else {
    await supabase<Array<{ submission_id: string }>>(
      `/rest/v1/incentive_likes?submission_id=eq.${encodeURIComponent(submissionId)}&voter_token_hash=eq.${encodeURIComponent(voterTokenHash)}`,
      { method: "DELETE", headers: headers("return=representation") }
    );
  }
  const likeCount = Math.max(0, submission.like_count + (liked ? 1 : -1));
  await supabase<StoredSubmission[]>(
    `/rest/v1/incentive_submissions?id=eq.${encodeURIComponent(submissionId)}`,
    { method: "PATCH", headers: headers("return=representation"), body: JSON.stringify({ like_count: likeCount }) }
  );
  return { liked, like_count: likeCount };
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
            : false
        }),
        updated_at: new Date().toISOString()
      })
    }
  );
  return toSubmission(rows[0]);
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
}

export async function listReleasePreviews() {
  return supabase<ReleasePreview[]>(
    "/rest/v1/release_previews?select=*&order=created_at.desc&limit=50"
  );
}

export async function createReleasePreview(
  input: Omit<ReleasePreview, "id" | "created_at" | "updated_at" | "published_at">
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
  return rows[0];
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
  return rows[0];
}
