import type { AssignPromoCodeInput } from "@/data/promo-code-types";
import { safeRecordAccessEvent } from "@/lib/access-log";
import { isAdminRequest, isSameOrigin } from "@/lib/admin-auth";
import { allocatePromoCode } from "@/lib/promo-code-store";

export async function POST(request: Request) {
  if (!isSameOrigin(request) || !(await isAdminRequest(request))) {
    await safeRecordAccessEvent(request, {
      scope: "admin",
      eventType: "unauthorized_promo_code_update",
      severity: isSameOrigin(request) ? "warning" : "critical",
      statusCode: 401,
    });
    return Response.json({ error: "Unauthorized" }, { status: 401 });
  }
  try {
    const body = (await request.json()) as Partial<AssignPromoCodeInput>;
    // All metadata fields are optional (DB columns are nullable), matching the
    // ESA api.js contract — allocation can happen first, details filled later.
    const assigned_name = typeof body.assigned_name === "string" ? body.assigned_name.trim() : undefined;
    const assigned_email = typeof body.assigned_email === "string" ? body.assigned_email.trim() : undefined;
    const assigned_channel = typeof body.assigned_channel === "string" ? body.assigned_channel.trim() : undefined;
    const campaign = typeof body.campaign === "string" ? body.campaign.trim() : undefined;
    const note = typeof body.note === "string" ? body.note.trim() : undefined;
    const specific_code_id = typeof body.specific_code_id === "string" ? body.specific_code_id.trim() : undefined;

    const input: AssignPromoCodeInput = {
      ...(assigned_name ? { assigned_name } : {}),
      ...(assigned_email ? { assigned_email } : {}),
      ...(assigned_channel ? { assigned_channel } : {}),
      ...(campaign ? { campaign } : {}),
      ...(note !== undefined ? { note } : {}),
      ...(specific_code_id !== undefined ? { specific_code_id } : {}),
    };

    const result = await allocatePromoCode(input);
    if (!result) {
      return Response.json({ error: "没有可用的兑换码" }, { status: 409 });
    }

    await safeRecordAccessEvent(request, {
      scope: "admin",
      eventType: "promo_code_assigned",
      statusCode: 200,
      details: {
        promo_code_id: result.id,
        assigned_to: assigned_name ?? null,
        channel: assigned_channel ?? null,
      },
    });

    // Flat shape, matching the ESA api.js contract ({ id, code, redeem_url, microsoft_code_id }).
    return Response.json(result);
  } catch {
    return Response.json({ error: "分配兑换码失败" }, { status: 500 });
  }
}
