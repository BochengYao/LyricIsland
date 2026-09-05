const COOKIE_NAME = "lyric_island_admin";
const SESSION_SECONDS = 60 * 60 * 24 * 7;

function getSecret() {
  const value = process.env.ADMIN_SESSION_SECRET;
  if (!value || value.length < 24) {
    throw new Error("ADMIN_SESSION_SECRET is not configured");
  }
  return value;
}

function bytesToHex(bytes: ArrayBuffer) {
  return Array.from(new Uint8Array(bytes), (byte) =>
    byte.toString(16).padStart(2, "0")
  ).join("");
}

async function sign(value: string) {
  const encoder = new TextEncoder();
  const key = await crypto.subtle.importKey(
    "raw",
    encoder.encode(getSecret()),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"]
  );
  return bytesToHex(
    await crypto.subtle.sign("HMAC", key, encoder.encode(value))
  );
}

function constantTimeEqual(left: string, right: string) {
  if (left.length !== right.length) return false;
  let mismatch = 0;
  for (let index = 0; index < left.length; index += 1) {
    mismatch |= left.charCodeAt(index) ^ right.charCodeAt(index);
  }
  return mismatch === 0;
}

export async function verifyAdminPassword(password: string) {
  const expected = process.env.ADMIN_PASSWORD;
  if (!expected) throw new Error("ADMIN_PASSWORD is not configured");
  const [left, right] = await Promise.all([
    crypto.subtle.digest("SHA-256", new TextEncoder().encode(password)),
    crypto.subtle.digest("SHA-256", new TextEncoder().encode(expected))
  ]);
  return constantTimeEqual(bytesToHex(left), bytesToHex(right));
}

export async function createAdminSession() {
  const expires = Math.floor(Date.now() / 1000) + SESSION_SECONDS;
  const nonce = crypto.randomUUID();
  const payload = `${expires}.${nonce}`;
  return `${payload}.${await sign(payload)}`;
}

export async function isAdminRequest(request: Request) {
  const cookie = request.headers.get("cookie") ?? "";
  const value = cookie
    .split(";")
    .map((part) => part.trim())
    .find((part) => part.startsWith(`${COOKIE_NAME}=`))
    ?.slice(COOKIE_NAME.length + 1);
  if (!value) return false;

  const [expiresText, nonce, signature] = value.split(".");
  if (!expiresText || !nonce || !signature) return false;
  const expires = Number(expiresText);
  if (!Number.isFinite(expires) || expires <= Date.now() / 1000) return false;
  const expected = await sign(`${expiresText}.${nonce}`);
  return constantTimeEqual(signature, expected);
}

export function adminSessionCookie(value: string) {
  const secure = process.env.NODE_ENV === "production" ? "; Secure" : "";
  return `${COOKIE_NAME}=${value}; Path=/; HttpOnly; SameSite=Strict; Max-Age=${SESSION_SECONDS}${secure}`;
}

export function clearAdminSessionCookie() {
  const secure = process.env.NODE_ENV === "production" ? "; Secure" : "";
  return `${COOKIE_NAME}=; Path=/; HttpOnly; SameSite=Strict; Max-Age=0${secure}`;
}

function firstForwardedValue(value: string | null) {
  return value?.split(",", 1)[0]?.trim() || null;
}

export function isSameOrigin(request: Request) {
  const origin = request.headers.get("origin");
  if (!origin) return true;

  try {
    const requestUrl = new URL(request.url);
    const host = firstForwardedValue(request.headers.get("x-forwarded-host"))
      ?? request.headers.get("host")
      ?? requestUrl.host;
    const forwardedProtocol = firstForwardedValue(request.headers.get("x-forwarded-proto"));
    const protocol = forwardedProtocol
      ? `${forwardedProtocol.replace(/:$/, "")}:`
      : requestUrl.protocol;
    const publicOrigin = new URL(`${protocol}//${host}`).origin;

    return new URL(origin).origin === publicOrigin;
  } catch {
    return false;
  }
}
