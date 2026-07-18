import { isSameOrigin } from "@/lib/admin-auth";
import { toggleSuggestionLike } from "@/lib/incentive-store";
import {
  hashVoterToken,
  readVoterToken,
  voterCookie
} from "@/lib/voter-token";

export async function POST(request: Request) {
  if (!isSameOrigin(request)) {
    return Response.json({ error: "Invalid origin" }, { status: 403 });
  }
  try {
    const body = (await request.json()) as { submissionId?: unknown };
    const submissionId = typeof body.submissionId === "string" ? body.submissionId : "";
    if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(submissionId)) {
      return Response.json({ error: "Invalid suggestion" }, { status: 400 });
    }

    const existingToken = readVoterToken(request);
    const token = existingToken ?? crypto.randomUUID();
    const result = await toggleSuggestionLike(
      submissionId,
      await hashVoterToken(token)
    );
    const response = Response.json(result);
    if (!existingToken) response.headers.set("Set-Cookie", voterCookie(token));
    return response;
  } catch (error) {
    const message = error instanceof Error ? error.message : "Like failed";
    return Response.json(
      { error: message.includes("not configured") ? "Like service is not configured yet" : "Like could not be saved" },
      { status: message.includes("not configured") ? 503 : 500 }
    );
  }
}
