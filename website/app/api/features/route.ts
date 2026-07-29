import { getFeatureContent } from "@/lib/incentive-store";

export const dynamic = "force-dynamic";

export async function GET() {
  try {
    return Response.json({ content: await getFeatureContent() });
  } catch {
    return Response.json({ error: "Unable to load feature content" }, { status: 500 });
  }
}
