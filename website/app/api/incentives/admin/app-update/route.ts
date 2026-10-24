import { isAdminRequest, isSameOrigin } from "@/lib/admin-auth";
import { getFeatureContent, saveAppUpdatePrompt } from "@/lib/incentive-store";

export async function GET(request: Request) {
  if (!(await isAdminRequest(request))) {
    return Response.json({ error: "Unauthorized" }, { status: 401 });
  }
  try {
    return Response.json({ update: (await getFeatureContent()).update_prompt });
  } catch {
    return Response.json({ error: "无法读取更新弹窗设置" }, { status: 500 });
  }
}

export async function PUT(request: Request) {
  if (!isSameOrigin(request) || !(await isAdminRequest(request))) {
    return Response.json({ error: "Unauthorized" }, { status: 401 });
  }
  try {
    const body = await request.json() as { update?: unknown };
    return Response.json({ update: await saveAppUpdatePrompt(body.update) });
  } catch (error) {
    const message = error instanceof Error && error.message.includes("release version")
      ? "启用弹窗前请填写完整版本号（例如 v3.4.1）"
      : error instanceof Error && error.message.includes("Chinese and English")
        ? "启用弹窗前请补全简体中文和英文更新内容"
        : "保存失败";
    return Response.json({ error: message }, { status: 400 });
  }
}
