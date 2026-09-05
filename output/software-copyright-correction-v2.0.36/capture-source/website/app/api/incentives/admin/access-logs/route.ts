import { acknowledgeAccessAlerts, listAccessLogs, safeRecordAccessEvent } from "@/lib/access-log";
import { isAdminRequest, isSameOrigin } from "@/lib/admin-auth";

export async function GET(request: Request) {
  if (!(await isAdminRequest(request))) return Response.json({ error: "Unauthorized" }, { status: 401 });
  try {
    return Response.json(await listAccessLogs());
  } catch {
    return Response.json({ error: "无法读取访问日志" }, { status: 500 });
  }
}

export async function PATCH(request: Request) {
  if (!isSameOrigin(request) || !(await isAdminRequest(request))) {
    await safeRecordAccessEvent(request, {
      scope: "admin",
      eventType: "unauthorized_alert_acknowledge",
      severity: "warning",
      statusCode: 401
    });
    return Response.json({ error: "Unauthorized" }, { status: 401 });
  }
  try {
    await acknowledgeAccessAlerts();
    await safeRecordAccessEvent(request, {
      scope: "admin",
      eventType: "security_alerts_acknowledged",
      statusCode: 200
    });
    return Response.json({ ok: true });
  } catch {
    return Response.json({ error: "操作失败" }, { status: 500 });
  }
}
