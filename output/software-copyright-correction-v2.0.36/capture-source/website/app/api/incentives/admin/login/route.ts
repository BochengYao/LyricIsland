import {
  adminSessionCookie,
  createAdminSession,
  isSameOrigin,
  verifyAdminPassword
} from "@/lib/admin-auth";
import { safeRecordAccessEvent } from "@/lib/access-log";

export async function POST(request: Request) {
  if (!isSameOrigin(request)) {
    await safeRecordAccessEvent(request, {
      scope: "admin",
      eventType: "cross_origin_login_attempt",
      severity: "critical",
      statusCode: 403
    });
    return Response.json({ error: "Invalid origin" }, { status: 403 });
  }
  try {
    const body = (await request.json()) as { password?: unknown };
    const password = typeof body.password === "string" ? body.password : "";
    if (!(await verifyAdminPassword(password))) {
      await safeRecordAccessEvent(request, {
        scope: "admin",
        eventType: "login_failed",
        severity: "warning",
        statusCode: 401
      });
      return Response.json({ error: "密码不正确" }, { status: 401 });
    }
    const response = Response.json({ ok: true });
    response.headers.set("Set-Cookie", adminSessionCookie(await createAdminSession()));
    await safeRecordAccessEvent(request, {
      scope: "admin",
      eventType: "login_succeeded",
      statusCode: 200
    });
    return response;
  } catch (error) {
    const message = error instanceof Error ? error.message : "Login failed";
    return Response.json(
      { error: message.includes("not configured") ? "后台登录尚未配置" : "登录失败" },
      { status: message.includes("not configured") ? 503 : 500 }
    );
  }
}
