import { isAdminRequest } from "@/lib/admin-auth";
import { getPromoCodeDetail } from "@/lib/promo-code-store";

export async function GET(
  request: Request,
  { params }: { params: Promise<{ id: string }> }
) {
  if (!(await isAdminRequest(request))) {
    return Response.json({ error: "Unauthorized" }, { status: 401 });
  }
  try {
    const { id } = await params;
    if (!id) {
      return Response.json({ error: "缺少兑换码 ID" }, { status: 400 });
    }
    const { code, logs } = await getPromoCodeDetail(id);
    return Response.json({ code, logs });
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    if (message.includes("not found")) {
      return Response.json({ error: "兑换码不存在" }, { status: 404 });
    }
    return Response.json({ error: "无法读取兑换码详情" }, { status: 500 });
  }
}
