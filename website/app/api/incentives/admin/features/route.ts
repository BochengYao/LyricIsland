import { isAdminRequest, isSameOrigin } from "@/lib/admin-auth";
import {
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
    const body = await request.json() as { content?: unknown };
    return Response.json({ content: await saveFeatureContent(body.content) });
  } catch (error) {
    const message = error instanceof Error && error.message.includes("At least one")
      ? "至少保留一条新功能内容"
      : error instanceof Error && error.message.includes("bilingual")
        ? "前台显示的条目必须补全中英文标题和描述"
        : error instanceof Error && error.message.includes("release version")
          ? "每条新功能必须填写完整版本号（例如 v2.1.8）"
        : "保存失败";
    return Response.json({ error: message }, { status: 400 });
  }
}
