import type { PromoCode, TsvParsedRow } from "@/data/promo-code-types";
import { safeRecordAccessEvent } from "@/lib/access-log";
import { isAdminRequest, isSameOrigin } from "@/lib/admin-auth";
import {
  listPromoCodes,
  getPromoCodeStats,
  getPromoCodeOrders,
  importPromoCodes,
  updatePromoCode,
  deletePromoCode,
  batchUpdatePromoCodes,
  maskPromoCode,
} from "@/lib/promo-code-store";
import type { PromoCodeFilter, DistributionStatus } from "@/data/promo-code-types";

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

export async function GET(request: Request) {
  if (!(await isAdminRequest(request))) {
    return Response.json({ error: "Unauthorized" }, { status: 401 });
  }
  try {
    const { searchParams } = new URL(request.url);
    const page = Math.max(1, Number(searchParams.get("page")) || 1);
    const pageSize = Math.min(200, Math.max(1, Number(searchParams.get("pageSize")) || 20));
    const status = searchParams.get("status");
    const orderId = searchParams.get("orderId") || undefined;
    const channel = searchParams.get("channel") || undefined;
    const search = searchParams.get("search") || undefined;
    const dateFrom = searchParams.get("dateFrom") || undefined;
    const dateTo = searchParams.get("dateTo") || undefined;
    const dateField = searchParams.get("dateField") || undefined;

    const filter: PromoCodeFilter = {
      page,
      pageSize,
      status: status && validStatuses.includes(status as DistributionStatus | "all")
        ? (status as DistributionStatus | "all")
        : undefined,
      orderId,
      channel,
      search,
      dateFrom,
      dateTo,
      dateField: dateField && (validDateFields as string[]).includes(dateField)
        ? (dateField as PromoCodeFilter["dateField"])
        : undefined,
    };

    const [pageResult, stats, orders] = await Promise.all([
      listPromoCodes(filter),
      getPromoCodeStats(),
      // Order metadata for the filter dropdown; tolerate a missing table so
      // the list stays usable (matches the ESA api.js contract).
      getPromoCodeOrders().catch(() => []),
    ]);

    const maskedCodes = pageResult.codes.map(c => ({
      ...c,
      code: maskPromoCode(c.code),
    }));
    return Response.json({
      codes: maskedCodes,
      total: pageResult.total,
      page: pageResult.page,
      pageSize: pageResult.pageSize,
      stats,
      orders,
    });
  } catch {
    return Response.json({ error: "无法读取兑换码列表" }, { status: 500 });
  }
}

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
      rows?: TsvParsedRow[];
      orderInfo?: { microsoft_order_id?: string; order_name?: string; product_name?: string };
    };
    if (!Array.isArray(body.rows) || body.rows.length === 0) {
      return Response.json({ error: "缺少导入数据" }, { status: 400 });
    }
    const orderInfo = body.orderInfo ?? {};
    const result = await importPromoCodes(body.rows, orderInfo);
    await safeRecordAccessEvent(request, {
      scope: "admin",
      eventType: "promo_code_tsv_import",
      statusCode: 200,
      details: {
        new_count: result.new_count,
        updated_count: result.updated_count,
        order_id: result.order_id,
      },
    });
    // Flat shape, matching the ESA api.js contract ({ new_count, updated_count, ... }).
    return Response.json(result);
  } catch {
    return Response.json({ error: "导入兑换码失败" }, { status: 500 });
  }
}

const ALLOWED_PATCH_FIELDS = new Set([
  "note", "campaign", "assigned_channel", "assigned_to_name", "assigned_to_email",
  "assigned_at",
]);

const ALLOWED_BATCH_FIELDS = new Set(["note", "campaign", "assigned_channel"]);

export async function PATCH(request: Request) {
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
      id?: string;
      ids?: string[];
      changes?: Partial<PromoCode>;
    };

    // Batch update mode
    if (body.ids && Array.isArray(body.ids)) {
      const sanitized: Record<string, unknown> = {};
      for (const [key, value] of Object.entries(body.changes ?? {})) {
        if (ALLOWED_BATCH_FIELDS.has(key)) {
          sanitized[key] = value;
        }
      }
      if (Object.keys(sanitized).length === 0) {
        return Response.json({ error: "没有可修改的字段" }, { status: 400 });
      }
      const result = await batchUpdatePromoCodes(
        body.ids,
        sanitized as { campaign?: string; assigned_channel?: string; note?: string }
      );
      await safeRecordAccessEvent(request, {
        scope: "admin",
        eventType: "promo_code_batch_updated",
        statusCode: 200,
        details: {
          ids: body.ids,
          updated: result.updated,
          changed_fields: Object.keys(sanitized),
        },
      });
      return Response.json(result);
    }

    // Single update mode
    if (!body.id || !body.changes) {
      return Response.json({ error: "缺少兑换码 ID 或修改内容" }, { status: 400 });
    }
    const sanitized: Record<string, unknown> = {};
    for (const [key, value] of Object.entries(body.changes)) {
      if (ALLOWED_PATCH_FIELDS.has(key)) {
        sanitized[key] = value;
      }
    }
    if (Object.keys(sanitized).length === 0) {
      return Response.json({ error: "没有可修改的字段" }, { status: 400 });
    }
    const code = await updatePromoCode(body.id, sanitized as Partial<PromoCode>);
    await safeRecordAccessEvent(request, {
      scope: "admin",
      eventType: "promo_code_updated",
      statusCode: 200,
      details: {
        promo_code_id: body.id,
        changed_fields: Object.keys(sanitized),
      },
    });
    return Response.json({ code });
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    if (message.includes("not found")) {
      return Response.json({ error: "兑换码不存在" }, { status: 404 });
    }
    return Response.json({ error: "更新兑换码失败" }, { status: 500 });
  }
}

export async function DELETE(request: Request) {
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
    const body = (await request.json()) as { id?: string };
    if (!body.id) {
      return Response.json({ error: "缺少兑换码 ID" }, { status: 400 });
    }
    await deletePromoCode(body.id);
    await safeRecordAccessEvent(request, {
      scope: "admin",
      eventType: "promo_code_deleted",
      statusCode: 200,
      details: { promo_code_id: body.id },
    });
    return Response.json({ success: true });
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    if (message.includes("not found")) {
      return Response.json({ error: "兑换码不存在" }, { status: 404 });
    }
    if (message.includes("Cannot delete")) {
      return Response.json({ error: message }, { status: 409 });
    }
    return Response.json({ error: "删除兑换码失败" }, { status: 500 });
  }
}
