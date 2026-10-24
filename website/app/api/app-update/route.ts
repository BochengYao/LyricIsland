import { getFeatureContent } from "@/lib/incentive-store";

export const dynamic = "force-dynamic";

export async function GET() {
  try {
    return Response.json({ update: (await getFeatureContent()).update_prompt });
  } catch {
    return Response.json({ error: "Unable to load app update information" }, { status: 500 });
  }
}
