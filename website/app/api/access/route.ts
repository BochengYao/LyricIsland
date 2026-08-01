import { isAdminRequest, isSameOrigin } from "@/lib/admin-auth";
import { safeRecordAccessEvent } from "@/lib/access-log";

function cleanClientDetails(value: unknown) {
  const source = value && typeof value === "object" ? value as Record<string, unknown> : {};
  const text = (key: string, max: number) =>
    typeof source[key] === "string" ? source[key].slice(0, max) : null;
  const number = (key: string) =>
    typeof source[key] === "number" && Number.isFinite(source[key]) ? source[key] : null;
  const connection = source.connection && typeof source.connection === "object"
    ? source.connection as Record<string, unknown>
    : {};
  return {
    page_title: text("page_title", 200),
    page_url: text("page_url", 1200),
    timezone: text("timezone", 80),
    language: text("language", 50),
    languages: Array.isArray(source.languages)
      ? source.languages.filter((item): item is string => typeof item === "string").slice(0, 12).map((item) => item.slice(0, 50))
      : [],
    platform: text("platform", 120),
    mobile: typeof source.mobile === "boolean" ? source.mobile : null,
    viewport: text("viewport", 30),
    screen: text("screen", 30),
    pixel_ratio: number("pixel_ratio"),
    color_depth: number("color_depth"),
    touch_points: number("touch_points"),
    hardware_concurrency: number("hardware_concurrency"),
    device_memory_gb: number("device_memory_gb"),
    cookies_enabled: typeof source.cookies_enabled === "boolean" ? source.cookies_enabled : null,
    do_not_track: text("do_not_track", 20),
    connection: {
      effective_type: typeof connection.effective_type === "string" ? connection.effective_type.slice(0, 30) : null,
      downlink_mbps: typeof connection.downlink_mbps === "number" && Number.isFinite(connection.downlink_mbps) ? connection.downlink_mbps : null,
      rtt_ms: typeof connection.rtt_ms === "number" && Number.isFinite(connection.rtt_ms) ? connection.rtt_ms : null,
      save_data: typeof connection.save_data === "boolean" ? connection.save_data : null
    },
    navigation_type: text("navigation_type", 30)
  };
}

export async function POST(request: Request) {
  if (!isSameOrigin(request)) return new Response(null, { status: 403 });
  try {
    const body = (await request.json()) as Record<string, unknown>;
    const path = typeof body.path === "string" ? body.path : "/";
    const details = cleanClientDetails(body.details);
    const pathname = new URL(path, request.url).pathname;
    const scope = pathname === "/admin" || pathname.startsWith("/admin/") ? "admin" : "public";
    await safeRecordAccessEvent(request, {
      scope,
      eventType: "page_view",
      path,
      method: "GET",
      statusCode: 200,
      referrer: typeof body.referrer === "string" ? body.referrer : undefined,
      details: {
        ...details,
        ...(scope === "admin" ? { authenticated: await isAdminRequest(request) } : {})
      }
    });
    return new Response(null, { status: 204 });
  } catch {
    return new Response(null, { status: 204 });
  }
}
