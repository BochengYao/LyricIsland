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
  const rows = await supabase<IncentiveSubmission[]>(
    "/rest/v1/incentive_submissions",
    {
      method: "POST",
      headers: headers("return=representation"),
      body: JSON.stringify(input)
    }
  );
  return rows[0];
}

export async function getPublicIncentives(voterHash?: string) {
  const rows = await supabase<Array<PublicSuggestion & { attachments?: SubmissionAttachment[] }>>(
    "/rest/v1/incentive_submissions?select=id,nickname,title,body,created_at,like_count,attachments&kind=eq.feature&status=eq.accepted&order=updated_at.desc&limit=12"
  );
  const likedRows = voterHash
    ? await supabase<Array<{ submission_id: string }>>(
        `/rest/v1/incentive_likes?select=submission_id&voter_token_hash=eq.${encodeURIComponent(voterHash)}&limit=200`
      )
    : [];
  const likedIds = new Set(likedRows.map((row) => row.submission_id));
  const suggestions = await Promise.all(rows.map(async ({ attachments, ...suggestion }) => {
    const first = attachments?.[0];
    const url = first ? await createSignedUrl(first.path) : undefined;
    return {
      ...suggestion,
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
  const rows = await supabase<Array<{ liked: boolean; like_count: number }>>(
    "/rest/v1/rpc/toggle_incentive_like",
    {
      method: "POST",
      headers: headers("return=representation"),
      body: JSON.stringify({
        p_submission_id: submissionId,
        p_voter_token_hash: voterTokenHash
      })
    }
  );
  if (!rows[0]) throw new Error("Like update returned no result");
  return rows[0];
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
  const rows = await supabase<IncentiveSubmission[]>(
    "/rest/v1/incentive_submissions?select=*&order=created_at.desc&limit=200"
  );
  return Promise.all(
    rows.map(async (row) => ({
      ...row,
      attachments: await Promise.all(
        (row.attachments ?? []).map(async (attachment) => ({
          ...attachment,
          signedUrl: await createSignedUrl(attachment.path)
        }))
      )
    }))
  );
}

export async function updateSubmission(
  id: string,
  changes: {
    status?: SubmissionStatus;
    reward_status?: RewardStatus;
    reviewer_note?: string | null;
  }
) {
  const rows = await supabase<IncentiveSubmission[]>(
    `/rest/v1/incentive_submissions?id=eq.${encodeURIComponent(id)}`,
    {
      method: "PATCH",
      headers: headers("return=representation"),
      body: JSON.stringify({ ...changes, updated_at: new Date().toISOString() })
    }
  );
  return rows[0];
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
