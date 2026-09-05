import { getFeatureContent } from "@/lib/incentive-store";
import { publicFeatureContent } from "@/data/feature-content";

export const dynamic = "force-dynamic";

export async function GET() {
  try {
    return Response.json({ content: publicFeatureContent(await getFeatureContent()) });
  } catch {
    return Response.json({ error: "Unable to load feature content" }, { status: 500 });
  }
}
