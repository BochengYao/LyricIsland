import { getPublicIncentives } from "@/lib/incentive-store";
import { hashVoterToken, readVoterToken } from "@/lib/voter-token";

export const dynamic = "force-dynamic";

export async function GET(request: Request) {
  try {
    const token = readVoterToken(request);
    const data = await getPublicIncentives(token ? await hashVoterToken(token) : undefined);
    return Response.json({ ...data, configured: true });
  } catch (error) {
    const message = error instanceof Error ? error.message : "Unavailable";
    if (message.includes("not configured")) {
      return Response.json({ suggestions: [], previews: [], configured: false });
    }
    return Response.json({ error: "Unable to load community updates" }, { status: 500 });
  }
}
