import { isAdminRequest, isSameOrigin } from "@/lib/admin-auth";
import type { FeatureContentVersionOperation } from "@/data/incentives-types";
import {
  applyFeatureContentVersionOperation,
  getFeatureContent,
  saveFeatureContent
} from "@/lib/incentive-store";

export async function GET(request: Request) {
  if (!(await isAdminRequest(request))) {
    return Response.json({ error: "Unauthorized" }, { status: 401 });
  }
  try {
    return Response.json({ content: await getFeatureContent() });
  } catch {
    return Response.json({ error: "无法读取新功能页内容" }, { status: 500 });
  }
}

export async function PUT(request: Request) {
  if (!isSameOrigin(request) || !(await isAdminRequest(request))) {
    return Response.json({ error: "Unauthorized" }, { status: 401 });
  }
  try {
    const body = await request.json() as { content?: unknown; operation?: FeatureContentVersionOperation };
    if (body.operation) {
      return Response.json({ content: await applyFeatureContentVersionOperation(body.operation) });
    }
    return Response.json({ content: await saveFeatureContent(body.content) });
  } catch (error) {
    const message = error instanceof Error && error.message.includes("bilingual")
        ? "前台显示的条目必须补全中英文标题和描述"
        : error instanceof Error && error.message.includes("contains")
          ? `该版本仍有功能条目，请确认是否连同 ${error.message.match(/\d+/)?.[0] ?? "全部"} 条功能一起删除`
        : error instanceof Error && error.message.includes("already exists")
          ? "该版本号已存在"
        : error instanceof Error && error.message.includes("not found")
          ? "找不到要操作的版本"
        : error instanceof Error && error.message.includes("No legacy")
          ? "当前没有可迁移的早期更新条目"
        : error instanceof Error && error.message.includes("release version")
          ? "每条新功能必须填写完整版本号（例如 v2.1.8）"
        : "保存失败";
    return Response.json({ error: message }, { status: 400 });
  }
}
