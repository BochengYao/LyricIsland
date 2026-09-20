import { isAdminRequest, isSameOrigin } from "@/lib/admin-auth";
import {
  createReleasePreview,
  deleteReleasePreview,
  listReleasePreviews,
  updateReleasePreview
} from "@/lib/incentive-store";
import type { ReleasePreviewFeature } from "@/data/incentives-types";
import { encodeReleasePreviewFeatures } from "@/data/release-preview-content";

class PreviewValidationError extends Error {}

function hasStructuredPreviewInput(body: Record<string, unknown>) {
  return Array.isArray(body.features) || ["note_zh", "note_en", "note_zh_tw", "note_ja"]
    .some((field) => Object.hasOwn(body, field));
}

function previewFeatures(value: unknown): ReleasePreviewFeature[] {
  if (!Array.isArray(value) || !value.length) {
    throw new PreviewValidationError("请至少添加一条功能项");
  }
  if (value.length > 80) throw new PreviewValidationError("功能项不能超过 80 条");
  const usedIds = new Set<string>();
  return value.map((item, index) => {
    if (!item || typeof item !== "object") throw new PreviewValidationError("功能项格式无效");
    const source = item as Record<string, unknown>;
    const id = typeof source.id === "string" ? source.id.trim().slice(0, 120) : "";
    if (!/^[A-Za-z0-9][A-Za-z0-9._:-]{0,119}$/.test(id) || id.startsWith("legacy-") || usedIds.has(id)) {
      throw new PreviewValidationError("功能项 ID 缺失或重复");
    }
    usedIds.add(id);
    const progress = source.progress === null || source.progress === undefined || source.progress === ""
      ? 0
      : source.progress;
    if (typeof progress !== "number" || !Number.isInteger(progress) || progress < 0 || progress > 100) {
      throw new PreviewValidationError("功能进度必须是 0–100 的整数");
    }
    const rawStage = source.stage === undefined || source.stage === null || source.stage === "" ? "development" : source.stage;
    if (rawStage !== "development" && rawStage !== "testing" && rawStage !== "ready") {
      throw new PreviewValidationError("功能状态无效");
    }
    const stage = rawStage;
    if (stage !== "development" && progress !== 100) {
      throw new PreviewValidationError("待测试或待上线状态必须先达到 100%");
    }
    const localizedText = (field: "content_zh" | "content_en" | "content_zh_tw" | "content_ja") =>
      typeof source[field] === "string" ? source[field].trim().slice(0, 2400) : "";
    const contentZh = localizedText("content_zh");
    const contentEn = localizedText("content_en");
    if (!contentZh || !contentEn) throw new PreviewValidationError("每条功能项均需填写中英文内容");
    return {
      id,
      sort_order: index + 1,
      progress,
      stage,
      content_zh: contentZh,
      content_en: contentEn,
      content_zh_tw: localizedText("content_zh_tw"),
      content_ja: localizedText("content_ja")
    };
  });
}

function previewPayload(body: Record<string, unknown>) {
  const status = body.status === "published" ? "published" : "draft";
  const version = typeof body.version === "string" ? body.version.trim().slice(0, 40) : "";
  const structured = hasStructuredPreviewInput(body);
  const sourceField = (noteField: "note_zh" | "note_en" | "note_zh_tw" | "note_ja", legacyField: "body_zh" | "body_en" | "body_zh_tw" | "body_ja") =>
    structured && Object.hasOwn(body, noteField) ? noteField : legacyField;
  const bodyZhField = sourceField("note_zh", "body_zh");
  const bodyEnField = sourceField("note_en", "body_en");
  const bodyZh = typeof body[bodyZhField] === "string" ? body[bodyZhField].trim().slice(0, 2400) : "";
  const bodyEn = typeof body[bodyEnField] === "string" ? body[bodyEnField].trim().slice(0, 2400) : "";
  const optionalLocalizedBody = (noteField: "note_zh_tw" | "note_ja", field: "body_zh_tw" | "body_ja") => {
    const key = sourceField(noteField, field);
    const value = body[key];
    return Object.hasOwn(body, key)
      ? { [field]: typeof value === "string" ? value.trim().slice(0, 2400) : "" }
      : {};
  };
  const features = structured ? previewFeatures(body.features) : null;
  return {
    version,
    title_zh: version,
    title_en: version,
    title_zh_tw: version,
    title_ja: version,
    body_zh: bodyZh,
    body_en: bodyEn,
    highlights_zh: features ? encodeReleasePreviewFeatures(features) : [] as string[],
    highlights_en: features ? features.map((feature) => feature.content_en) : [] as string[],
    ...(features ? {
      highlights_zh_tw: features.map((feature) => feature.content_zh_tw),
      highlights_ja: features.map((feature) => feature.content_ja)
    } : {}),
    ...optionalLocalizedBody("note_zh_tw", "body_zh_tw"),
    ...optionalLocalizedBody("note_ja", "body_ja"),
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
    const records = await listReleasePreviews();
    return Response.json({
      previews: records.filter((preview) => preview.status === "published"),
      drafts: records.filter((preview) => preview.status === "draft")
    });
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
  } catch (error) {
    if (error instanceof PreviewValidationError) return Response.json({ error: error.message }, { status: 400 });
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
    const hasContent = ["version", "body_zh", "body_en", "body_zh_tw", "body_ja", "note_zh", "note_en", "note_zh_tw", "note_ja", "features", "target_date"]
      .some((field) => Object.hasOwn(body, field));
    if (hasContent) {
      if (!hasStructuredPreviewInput(body)) {
        const existing = (await listReleasePreviews()).find((preview) => preview.id === id);
        if (existing?.features.some((feature) => !feature.id.startsWith("legacy-"))) {
          return Response.json({ error: "该预告已使用结构化功能项，请刷新维护者后台后再编辑" }, { status: 409 });
        }
      }
      const payload = previewPayload(body);
      if (!payload.version || !payload.body_zh || !payload.body_en) {
        return Response.json({ error: "版本号、中英文更新内容均为必填项" }, { status: 400 });
      }
      return Response.json({
        preview: await updateReleasePreview(id, payload)
      });
    }
    return Response.json({
      preview: await updateReleasePreview(id, {
        status: body.status as "draft" | "published"
      })
    });
  } catch (error) {
    if (error instanceof PreviewValidationError) return Response.json({ error: error.message }, { status: 400 });
    return Response.json({ error: "更新失败" }, { status: 500 });
  }
}

export async function DELETE(request: Request) {
  if (!isSameOrigin(request) || !(await isAdminRequest(request))) {
    return Response.json({ error: "Unauthorized" }, { status: 401 });
  }
  try {
    const body = (await request.json().catch(() => ({}))) as Record<string, unknown>;
    const id = typeof body.id === "string"
      ? body.id.trim()
      : new URL(request.url).searchParams.get("id")?.trim() ?? "";
    if (!id) return Response.json({ error: "Invalid preview id" }, { status: 400 });
    return Response.json({ preview: await deleteReleasePreview(id) });
  } catch (error) {
    const message = error instanceof Error ? error.message : "删除失败";
    if (message.includes("not found")) return Response.json({ error: "找不到要删除的版本预告" }, { status: 404 });
    if (message.includes("Internal content")) return Response.json({ error: "内部内容不能删除" }, { status: 400 });
    return Response.json({ error: "删除版本预告失败" }, { status: 500 });
  }
}
