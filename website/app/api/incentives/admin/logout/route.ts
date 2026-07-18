import { clearAdminSessionCookie, isSameOrigin } from "@/lib/admin-auth";

export async function POST(request: Request) {
  if (!isSameOrigin(request)) {
    return Response.json({ error: "Invalid origin" }, { status: 403 });
  }
  const response = Response.json({ ok: true });
  response.headers.set("Set-Cookie", clearAdminSessionCookie());
  return response;
}
