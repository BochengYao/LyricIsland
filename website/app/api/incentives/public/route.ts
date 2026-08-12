import {
  getPublicIncentives,
  parsePublicPreviewPageOptions
} from "@/lib/incentive-store";
import { hashVoterToken, readVoterToken } from "@/lib/voter-token";

export const dynamic = "force-dynamic";

export async function GET(request: Request) {
  try {
    const token = readVoterToken(request);
    const data = await getPublicIncentives(
      token ? await hashVoterToken(token) : undefined,
      parsePublicPreviewPageOptions(request)
    );
    return Response.json({ ...data, configured: true });
  } catch (error) {
    const message = error instanceof Error ? error.message : "Unavailable";
    if (message === "Invalid preview cursor") {
      return Response.json({ error: message }, { status: 400 });
    }
    if (message.includes("not configured")) {
      return Response.json({ suggestions: [], previews: [], next_preview_cursor: null, configured: false });
    }
    return Response.json({ error: "Unable to load community updates" }, { status: 500 });
  }
}
