export const VOTER_COOKIE = "lyric_island_voter";

export function readVoterToken(request: Request) {
  const cookie = request.headers.get("cookie") ?? "";
  return cookie
    .split(";")
    .map((part) => part.trim())
    .find((part) => part.startsWith(`${VOTER_COOKIE}=`))
    ?.slice(VOTER_COOKIE.length + 1);
}

export function voterCookie(value: string) {
  const secure = process.env.NODE_ENV === "production" ? "; Secure" : "";
  return `${VOTER_COOKIE}=${value}; Path=/; HttpOnly; SameSite=Lax; Max-Age=31536000${secure}`;
}

export async function hashVoterToken(value: string) {
  const digest = await crypto.subtle.digest(
    "SHA-256",
    new TextEncoder().encode(value)
  );
  return Array.from(new Uint8Array(digest), (byte) =>
    byte.toString(16).padStart(2, "0")
  ).join("");
}
