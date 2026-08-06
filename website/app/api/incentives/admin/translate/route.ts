import { isAdminRequest, isSameOrigin } from "@/lib/admin-auth";
import { translateChineseContent } from "@/lib/content-translation";

export async function POST(request: Request) {
  if (!isSameOrigin(request) || !(await isAdminRequest(request))) {
    return Response.json({ error: "Unauthorized" }, { status: 401 });
  }
  try {
    const body = await request.json() as { entries?: unknown; targetLocales?: unknown };
    return Response.json(await translateChineseContent(body));
  } catch (error) {
    const message = error instanceof Error ? error.message : "翻译失败";
    const status = message.includes("not configured") ? 503 : 400;
    return Response.json({ error: message }, { status });
  }
}
