import { isAdminRequest } from "@/lib/admin-auth";
import { getImportPreview } from "@/lib/promo-code-store";

export async function POST(request: Request) {
  if (!(await isAdminRequest(request))) {
    return Response.json({ error: "Unauthorized" }, { status: 401 });
  }
  try {
    const body = await request.json();
    const preview = await getImportPreview(body.rows);
    return Response.json(preview);
  } catch (error) {
    return Response.json({ error: "导入预览失败" }, { status: 500 });
  }
}
