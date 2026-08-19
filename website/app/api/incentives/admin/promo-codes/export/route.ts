import type { PromoCodeFilter, DistributionStatus } from "@/data/promo-code-types";
import { safeRecordAccessEvent } from "@/lib/access-log";
import { isAdminRequest, isSameOrigin } from "@/lib/admin-auth";
import { exportPromoCodesCsv } from "@/lib/promo-code-store";

const validStatuses: Array<DistributionStatus | "all"> = [
  "available",
  "assigned",
  "revoked",
  "expired",
  "all",
];
const validDateFields: Array<NonNullable<PromoCodeFilter["dateField"]>> = [
  "imported_at",
  "assigned_at",
  "microsoft_expire_at",
];

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
    const body = (await request.json()) as {
      filters?: Record<string, unknown>;
      includeFullCode?: boolean;
    };
    const rawFilters = body.filters ?? {};
    const includeFullCode = body.includeFullCode === true;

    const status = typeof rawFilters.status === "string" && validStatuses.includes(rawFilters.status as DistributionStatus | "all")
      ? (rawFilters.status as DistributionStatus | "all")
      : undefined;
    const orderId = typeof rawFilters.orderId === "string" ? rawFilters.orderId : undefined;
    const channel = typeof rawFilters.channel === "string" ? rawFilters.channel : undefined;
    const search = typeof rawFilters.search === "string" ? rawFilters.search : undefined;
    const dateFrom = typeof rawFilters.dateFrom === "string" ? rawFilters.dateFrom : undefined;
    const dateTo = typeof rawFilters.dateTo === "string" ? rawFilters.dateTo : undefined;
    const dateField = typeof rawFilters.dateField === "string" && (validDateFields as string[]).includes(rawFilters.dateField)
      ? (rawFilters.dateField as PromoCodeFilter["dateField"])
      : undefined;

    const filters = { status, orderId, channel, search, dateFrom, dateTo, dateField };
    const csv = await exportPromoCodesCsv(filters, includeFullCode);

    await safeRecordAccessEvent(request, {
      scope: "admin",
      eventType: "promo_code_export",
      statusCode: 200,
      details: { include_full_code: includeFullCode, filters },
    });

    return new Response(csv, {
      status: 200,
      headers: {
        "Content-Type": "text/csv; charset=utf-8",
        "Content-Disposition": `attachment; filename="promo-codes-${Date.now()}.csv"`,
      },
    });
  } catch {
    return Response.json({ error: "导出兑换码失败" }, { status: 500 });
  }
}
