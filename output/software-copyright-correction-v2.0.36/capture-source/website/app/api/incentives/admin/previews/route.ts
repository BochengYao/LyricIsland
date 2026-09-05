import { isAdminRequest, isSameOrigin } from "@/lib/admin-auth";
import {
  createReleasePreview,
  listReleasePreviews,
  updateReleasePreview
} from "@/lib/incentive-store";

function previewPayload(body: Record<string, unknown>) {
  const status = body.status === "published" ? "published" : "draft";
  const version = typeof body.version === "string" ? body.version.trim().slice(0, 40) : "";
  const bodyZh = typeof body.body_zh === "string" ? body.body_zh.trim().slice(0, 2400) : "";
  const bodyEn = typeof body.body_en === "string" ? body.body_en.trim().slice(0, 2400) : "";
  return {
    version,
    title_zh: version,
    title_en: version,
    body_zh: bodyZh,
    body_en: bodyEn,
    highlights_zh: [] as string[],
    highlights_en: [] as string[],
    target_date: typeof body.target_date === "string" && /^\d{4}-\d{2}-\d{2}$/.test(body.target_date)
      ? body.target_date
      : null,
    status
  } as const;
}

export async function GET(request: Request) {
  if (!(await isAdminRequest(request))) {
    return Response.json({ error: "Unauthorized" }, { status: 401 });
  }
  try {
    return Response.json({ previews: await listReleasePreviews() });
  } catch {
    return Response.json({ error: "无法读取版本预告" }, { status: 500 });
  }
}

export async function POST(request: Request) {
  if (!isSameOrigin(request) || !(await isAdminRequest(request))) {
    return Response.json({ error: "Unauthorized" }, { status: 401 });
  }
  try {
    const body = (await request.json()) as Record<string, unknown>;
    const payload = previewPayload(body);
    if (!payload.version || !payload.body_zh || !payload.body_en) {
      return Response.json({ error: "版本号、中英文更新内容均为必填项" }, { status: 400 });
    }
    return Response.json({ preview: await createReleasePreview(payload) }, { status: 201 });
  } catch {
    return Response.json({ error: "发布失败" }, { status: 500 });
  }
}

export async function PATCH(request: Request) {
  if (!isSameOrigin(request) || !(await isAdminRequest(request))) {
    return Response.json({ error: "Unauthorized" }, { status: 401 });
  }
  try {
    const body = (await request.json()) as Record<string, unknown>;
    const id = typeof body.id === "string" ? body.id : "";
    if (!id || (body.status !== "draft" && body.status !== "published")) {
      return Response.json({ error: "Invalid update" }, { status: 400 });
    }
    return Response.json({
      preview: await updateReleasePreview(id, { status: body.status })
    });
  } catch {
    return Response.json({ error: "更新失败" }, { status: 500 });
  }
}
