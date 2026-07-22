import { isAdminRequest, isSameOrigin } from "@/lib/admin-auth";
import { safeRecordAccessEvent } from "@/lib/access-log";

export async function POST(request: Request) {
  if (!isSameOrigin(request)) return new Response(null, { status: 403 });
  try {
    const body = (await request.json()) as Record<string, unknown>;
    const path = typeof body.path === "string" ? body.path : "/";
    const scope = path === "/admin" || path.startsWith("/admin/") ? "admin" : "public";
    await safeRecordAccessEvent(request, {
      scope,
      eventType: "page_view",
      path,
      statusCode: 200,
      referrer: typeof body.referrer === "string" ? body.referrer : undefined,
      details: scope === "admin" ? { authenticated: await isAdminRequest(request) } : undefined
    });
    return new Response(null, { status: 204 });
  } catch {
    return new Response(null, { status: 204 });
  }
}
