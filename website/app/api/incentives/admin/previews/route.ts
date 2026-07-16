import { isAdminRequest, isSameOrigin } from "@/lib/admin-auth";
import {
  createReleasePreview,
  listReleasePreviews,
  updateReleasePreview
} from "@/lib/incentive-store";

function lines(value: unknown) {
  if (Array.isArray(value)) {
    return value.filter((item): item is string => typeof item === "string").map((item) => item.trim()).filter(Boolean).slice(0, 12);
  }
  return [];
}

function previewPayload(body: Record<string, unknown>) {
  const status = body.status === "published" ? "published" : "draft";
  return {
    version: typeof body.version === "string" ? body.version.trim().slice(0, 40) : "",
    title_zh: typeof body.title_zh === "string" ? body.title_zh.trim().slice(0, 160) : "",
    title_en: typeof body.title_en === "string" ? body.title_en.trim().slice(0, 160) : "",
    body_zh: typeof body.body_zh === "string" ? body.body_zh.trim().slice(0, 2400) : "",
    body_en: typeof body.body_en === "string" ? body.body_en.trim().slice(0, 2400) : "",
    highlights_zh: lines(body.highlights_zh),
    highlights_en: lines(body.highlights_en),
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
    if (!payload.version || !payload.title_zh || !payload.body_zh) {
      return Response.json({ error: "版本号、中文标题和中文说明为必填项" }, { status: 400 });
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
