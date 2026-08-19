import type {
  AssignPromoCodeInput,
  AssignPromoCodeResult,
  DistributionStatus,
  PromoCode,
  PromoCodeFilter,
  PromoCodeLog,
  PromoCodeOrder,
  PromoCodePage,
  PromoCodeStats,
  TsvImportPreview,
  TsvParsedRow,
} from "@/data/promo-code-types";

// ---------------------------------------------------------------------------
// Configuration & helpers (mirrors incentive-store.ts pattern)
// ---------------------------------------------------------------------------

type SupabaseConfig = {
  url: string;
  key: string;
};

function getConfig(): SupabaseConfig {
  const url = process.env.SUPABASE_URL?.replace(/\/$/, "");
  const key = process.env.SUPABASE_SERVICE_ROLE_KEY;
  if (!url || !key) {
    throw new Error("Promo code storage is not configured");
  }
  return { url, key };
}

function headers(prefer?: string) {
  const { key } = getConfig();
  return {
    apikey: key,
    Authorization: `Bearer ${key}`,
    "Content-Type": "application/json",
    ...(prefer ? { Prefer: prefer } : {}),
  };
}

async function supabase<T>(path: string, init?: RequestInit): Promise<T> {
  const { url } = getConfig();
  const response = await fetch(`${url}${path}`, {
    ...init,
    headers: {
      ...headers(),
      ...(init?.headers ?? {}),
    },
    cache: "no-store",
  });
  if (!response.ok) {
    const detail = await response.text();
    throw new Error(
      `Promo code request failed (${response.status}): ${detail.slice(0, 300)}`
    );
  }
  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

async function supabaseRaw(path: string, init?: RequestInit) {
  const { url } = getConfig();
  return fetch(`${url}${path}`, {
    ...init,
    headers: {
      ...headers(),
      ...(init?.headers ?? {}),
    },
    cache: "no-store",
  });
}

async function rpc<T>(fnName: string, params: Record<string, unknown>): Promise<T> {
  return supabase<T>(`/rest/v1/rpc/${fnName}`, {
    method: "POST",
    body: JSON.stringify(params),
  });
}

// ---------------------------------------------------------------------------
// Code masking helper
// ---------------------------------------------------------------------------

export function maskPromoCode(code: string): string {
  if (code.length <= 8) return code;
  const first5 = code.slice(0, 5);
  const last3 = code.slice(-3);
  const masked = code.slice(5, -3).replace(/./g, "*");
  return `${first5}${masked}${last3}`;
}

// ---------------------------------------------------------------------------
// 1. listPromoCodes
// ---------------------------------------------------------------------------

export async function listPromoCodes(
  options: PromoCodeFilter
): Promise<PromoCodePage> {
  const { page, pageSize, status, orderId, channel, search, dateFrom, dateTo, dateField } =
    options;

  const params = new URLSearchParams({
    select: "*",
    order: "created_at.desc",
    limit: String(pageSize),
    offset: String((page - 1) * pageSize),
  });

  // Status filter
  if (status && status !== "all") {
    params.set("distribution_status", `eq.${status}`);
  }

  // Order ID filter
  if (orderId) {
    params.set("order_id", `eq.${orderId}`);
  }

  // Channel filter
  if (channel) {
    params.set("assigned_channel", `eq.${channel}`);
  }

  // Text search across multiple columns
  if (search && search.trim()) {
    const term = search.trim();
    const searchColumns = [
      "code",
      "microsoft_code_id",
      "assigned_to_name",
      "assigned_to_email",
      "campaign",
      "raw_order_id",
      "note",
    ];
    const orConditions = searchColumns
      .map((col) => `${col}.ilike.*${term}*`)
      .join(",");
    params.set("or", `(${orConditions})`);
  }

  // Date range filters
  if (dateFrom && dateTo && dateField) {
    params.set(dateField, `gte.${dateFrom}`);
    // PostgREST doesn't support two filters on same column via params easily,
    // so we use the `and` operator for the date range
    params.delete(dateField);
    params.set(
      "and",
      `(${dateField}.gte.${dateFrom},${dateField}.lte.${dateTo})`
    );
  } else if (dateFrom && dateField) {
    params.set(dateField, `gte.${dateFrom}`);
  } else if (dateTo && dateField) {
    params.set(dateField, `lte.${dateTo}`);
  }

  const response = await supabaseRaw(
    `/rest/v1/promo_codes?${params.toString()}`,
    {
      headers: {
        ...headers(),
        Prefer: "count=exact",
      },
    }
  );

  if (!response.ok) {
    const detail = await response.text();
    throw new Error(
      `Promo code list failed (${response.status}): ${detail.slice(0, 300)}`
    );
  }

  const codes = (await response.json()) as PromoCode[];

  // Parse Content-Range: X-Y/N
  const contentRange = response.headers.get("Content-Range");
  let total = 0;
  if (contentRange) {
    const match = contentRange.match(/\/(\d+|\*)/);
    if (match && match[1] !== "*") {
      total = Number.parseInt(match[1], 10);
    }
  }

  return { codes, total, page, pageSize };
}

// ---------------------------------------------------------------------------
// 2. getPromoCodeStats
// ---------------------------------------------------------------------------

export async function getPromoCodeStats(): Promise<PromoCodeStats> {
  const rows = await rpc<
    Array<{
      total: number;
      available: number;
      assigned: number;
      microsoft_redeemed: number;
      expired: number;
      expiring_soon: number;
    }>
  >("promo_code_dashboard_stats", {});

  const row = rows[0];
  return {
    total: row.total,
    available: row.available,
    assigned: row.assigned,
    microsoft_redeemed: row.microsoft_redeemed,
    expired: row.expired,
    expiring_soon: row.expiring_soon,
  };
}

// ---------------------------------------------------------------------------
// 3. importPromoCodes
// ---------------------------------------------------------------------------

export async function importPromoCodes(
  rows: TsvParsedRow[],
  orderInfo: {
    microsoft_order_id?: string;
    order_name?: string;
    product_name?: string;
  }
): Promise<{
  new_count: number;
  updated_count: number;
  unchanged_count: number;
  order_id: string;
}> {
  const result = await rpc<{
    new_count: number;
    updated_count: number;
    unchanged_count: number;
    order_id: string;
  }>("bulk_import_promo_codes", {
    p_rows: rows,
    p_order_info: orderInfo,
  });

  return result;
}

// ---------------------------------------------------------------------------
// 4. allocatePromoCode
// ---------------------------------------------------------------------------

export async function allocatePromoCode(
  input: AssignPromoCodeInput
): Promise<AssignPromoCodeResult | null> {
  try {
    const rows = await rpc<
      Array<{
        id: string;
        code: string;
        redeem_url: string | null;
        microsoft_code_id: string;
      }>
    >("allocate_promo_code", {
      p_assigned_name: input.assigned_name,
      p_assigned_email: input.assigned_email,
      p_assigned_channel: input.assigned_channel,
      p_campaign: input.campaign,
      p_note: input.note ?? null,
      p_specific_code_id: input.specific_code_id ?? null,
    });

    if (!rows || rows.length === 0) return null;

    return {
      id: rows[0].id,
      code: rows[0].code,
      redeem_url: rows[0].redeem_url,
      microsoft_code_id: rows[0].microsoft_code_id,
    };
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    if (message.includes("No available promo codes")) {
      return null;
    }
    throw error;
  }
}

// ---------------------------------------------------------------------------
// 5. updatePromoCode
// ---------------------------------------------------------------------------

export async function updatePromoCode(
  id: string,
  changes: Partial<PromoCode>
): Promise<PromoCode> {
  // Fetch current data for audit log
  const existing = await supabase<PromoCode[]>(
    `/rest/v1/promo_codes?select=*&id=eq.${encodeURIComponent(id)}&limit=1`
  );
  if (!existing || existing.length === 0) {
    throw new Error(`Promo code not found: ${id}`);
  }
  const oldData = existing[0];

  const rows = await supabase<PromoCode[]>(
    `/rest/v1/promo_codes?id=eq.${encodeURIComponent(id)}`,
    {
      method: "PATCH",
      headers: headers("return=representation"),
      body: JSON.stringify({
        ...changes,
        updated_at: new Date().toISOString(),
      }),
    }
  );

  if (!rows || rows.length === 0) {
    throw new Error(`Promo code not found: ${id}`);
  }

  // Insert audit log
  await supabase<void>("/rest/v1/promo_code_logs", {
    method: "POST",
    headers: headers("return=minimal"),
    body: JSON.stringify({
      promo_code_id: id,
      action: "EDIT",
      previous_data: oldData,
      new_data: changes,
      metadata: { changed_fields: Object.keys(changes) },
    }),
  });

  return rows[0];
}

// ---------------------------------------------------------------------------
// 5b. batchUpdatePromoCodes
// ---------------------------------------------------------------------------

export async function batchUpdatePromoCodes(
  ids: string[],
  changes: { campaign?: string; assigned_channel?: string; note?: string }
): Promise<{ updated: number }> {
  let updated = 0;
  for (const id of ids) {
    // Fetch current data for audit log
    const existing = await supabase<PromoCode[]>(
      `/rest/v1/promo_codes?select=*&id=eq.${encodeURIComponent(id)}&limit=1`
    );
    if (!existing || existing.length === 0) continue;
    const oldData = existing[0];

    const rows = await supabase<PromoCode[]>(
      `/rest/v1/promo_codes?id=eq.${encodeURIComponent(id)}`,
      {
        method: "PATCH",
        headers: headers("return=representation"),
        body: JSON.stringify({
          ...changes,
          updated_at: new Date().toISOString(),
        }),
      }
    );
    if (rows && rows.length > 0) {
      updated++;
      // Insert audit log
      await supabase<void>("/rest/v1/promo_code_logs", {
        method: "POST",
        headers: headers("return=minimal"),
        body: JSON.stringify({
          promo_code_id: id,
          action: "EDIT",
          previous_data: oldData,
          new_data: changes,
          metadata: { changed_fields: Object.keys(changes) },
        }),
      });
    }
  }
  return { updated };
}

// ---------------------------------------------------------------------------
// 6. revokePromoCode
// ---------------------------------------------------------------------------

export async function revokePromoCode(id: string): Promise<PromoCode> {
  const now = new Date().toISOString();
  const rows = await supabase<PromoCode[]>(
    `/rest/v1/promo_codes?id=eq.${encodeURIComponent(id)}`,
    {
      method: "PATCH",
      headers: headers("return=representation"),
      body: JSON.stringify({
        distribution_status: "revoked" as DistributionStatus,
        revoked_at: now,
        updated_at: now,
      }),
    }
  );

  if (!rows || rows.length === 0) {
    throw new Error(`Promo code not found: ${id}`);
  }

  return rows[0];
}

// ---------------------------------------------------------------------------
// 7. deletePromoCode
// ---------------------------------------------------------------------------

export async function deletePromoCode(id: string): Promise<void> {
  // Fetch first to check status and capture data for audit
  const existing = await supabase<PromoCode[]>(
    `/rest/v1/promo_codes?select=*&id=eq.${encodeURIComponent(id)}&limit=1`
  );

  if (!existing || existing.length === 0) {
    throw new Error(`Promo code not found: ${id}`);
  }

  if (existing[0].distribution_status !== "available") {
    throw new Error(
      `Cannot delete promo code with status "${existing[0].distribution_status}". Only "available" codes can be deleted.`
    );
  }

  const oldData = existing[0];

  await supabase<void>(
    `/rest/v1/promo_codes?id=eq.${encodeURIComponent(id)}`,
    { method: "DELETE" }
  );

  // Insert audit log
  await supabase<void>("/rest/v1/promo_code_logs", {
    method: "POST",
    headers: headers("return=minimal"),
    body: JSON.stringify({
      promo_code_id: id,
      action: "DELETE",
      previous_data: oldData,
      new_data: null,
      metadata: {},
    }),
  });
}

// ---------------------------------------------------------------------------
// 8. getPromoCodeDetail
// ---------------------------------------------------------------------------

export async function getPromoCodeDetail(
  id: string
): Promise<{ code: PromoCode; logs: PromoCodeLog[] }> {
  const [codes, logs] = await Promise.all([
    supabase<PromoCode[]>(
      `/rest/v1/promo_codes?select=*&id=eq.${encodeURIComponent(id)}&limit=1`
    ),
    supabase<PromoCodeLog[]>(
      `/rest/v1/promo_code_logs?promo_code_id=eq.${encodeURIComponent(id)}&order=created_at.desc`
    ),
  ]);

  if (!codes || codes.length === 0) {
    throw new Error(`Promo code not found: ${id}`);
  }

  return { code: codes[0], logs };
}

// ---------------------------------------------------------------------------
// 9. getPromoCodeOrders
// ---------------------------------------------------------------------------

export async function getPromoCodeOrders(): Promise<PromoCodeOrder[]> {
  return supabase<PromoCodeOrder[]>(
    "/rest/v1/promo_code_orders?select=*&order=created_at.desc"
  );
}

// ---------------------------------------------------------------------------
// 10. getImportPreview
// ---------------------------------------------------------------------------

export async function getImportPreview(
  rows: TsvParsedRow[]
): Promise<TsvImportPreview> {
  const microsoftCodeIds = rows.map((r) => r.microsoft_code_id);

  // Query existing codes that match the incoming microsoft_code_ids
  let existingCodes: PromoCode[] = [];
  if (microsoftCodeIds.length > 0) {
    // PostgREST in-filter with quoted values
    const inValues = microsoftCodeIds
      .map((id) => `"${id.replace(/"/g, '\\"')}"`)
      .join(",");
    existingCodes = await supabase<PromoCode[]>(
      `/rest/v1/promo_codes?select=*&microsoft_code_id=in.(${inValues})`
    );
  }

  const existingMap = new Map<string, PromoCode>();
  for (const code of existingCodes) {
    existingMap.set(code.microsoft_code_id, code);
  }

  let newCount = 0;
  let existingCount = 0;
  let microsoftStatusChanges = 0;
  let unchangedCount = 0;

  for (const row of rows) {
    const existing = existingMap.get(row.microsoft_code_id);
    if (!existing) {
      newCount++;
    } else {
      existingCount++;
      // Check if Microsoft-side fields would change
      const hasChanges =
        existing.microsoft_available !== row.microsoft_available ||
        existing.microsoft_redeemed !== row.microsoft_redeemed ||
        existing.microsoft_start_at !== row.microsoft_start_at ||
        existing.microsoft_expire_at !== row.microsoft_expire_at ||
        existing.redeem_url !== row.redeem_url ||
        existing.raw_order_id !== row.raw_order_id;

      if (hasChanges) {
        microsoftStatusChanges++;
      } else {
        unchangedCount++;
      }
    }
  }

  return {
    filename: "",
    total_detected: rows.length,
    new_count: newCount,
    existing_count: existingCount,
    microsoft_status_changes: microsoftStatusChanges,
    unchanged_count: unchangedCount,
    errors: [],
    warnings: [],
  };
}

// ---------------------------------------------------------------------------
// 11. exportPromoCodesCsv
// ---------------------------------------------------------------------------

export async function exportPromoCodesCsv(
  filters: Omit<PromoCodeFilter, "page" | "pageSize">,
  includeFullCode: boolean
): Promise<string> {
  // Build query params (no pagination limit)
  const params = new URLSearchParams({
    select: "*",
    order: "created_at.desc",
    limit: "100000",
  });

  const { status, orderId, channel, search, dateFrom, dateTo, dateField } =
    filters;

  if (status && status !== "all") {
    params.set("distribution_status", `eq.${status}`);
  }
  if (orderId) {
    params.set("order_id", `eq.${orderId}`);
  }
  if (channel) {
    params.set("assigned_channel", `eq.${channel}`);
  }
  if (search && search.trim()) {
    const term = search.trim();
    const searchColumns = [
      "code",
      "microsoft_code_id",
      "assigned_to_name",
      "assigned_to_email",
      "campaign",
      "raw_order_id",
      "note",
    ];
    const orConditions = searchColumns
      .map((col) => `${col}.ilike.*${term}*`)
      .join(",");
    params.set("or", `(${orConditions})`);
  }
  if (dateFrom && dateTo && dateField) {
    params.set(
      "and",
      `(${dateField}.gte.${dateFrom},${dateField}.lte.${dateTo})`
    );
  } else if (dateFrom && dateField) {
    params.set(dateField, `gte.${dateFrom}`);
  } else if (dateTo && dateField) {
    params.set(dateField, `lte.${dateTo}`);
  }

  const codes = await supabase<PromoCode[]>(
    `/rest/v1/promo_codes?${params.toString()}`
  );

  const csvHeaders = [
    "Code",
    "Code ID",
    "Distribution Status",
    "Microsoft Available",
    "Microsoft Redeemed",
    "Order",
    "Campaign",
    "Assigned To",
    "Channel",
    "Assigned At",
    "Expire At",
    "Note",
  ];

  const escapeCsvField = (value: string): string => {
    if (
      value.includes(",") ||
      value.includes('"') ||
      value.includes("\n") ||
      value.includes("\r")
    ) {
      return `"${value.replace(/"/g, '""')}"`;
    }
    return value;
  };

  const sanitizeCsvValue = (value: string): string => {
    if (value.length > 0 && ["=", "+", "-", "@", "\t", "\r"].includes(value[0])) {
      return "'" + value;
    }
    return value;
  };

  const rows = codes.map((c) => {
    const codeValue = includeFullCode ? c.code : maskPromoCode(c.code);
    const assignedTo = [c.assigned_to_name, c.assigned_to_email]
      .filter(Boolean)
      .join(" / ");

    return [
      escapeCsvField(sanitizeCsvValue(codeValue)),
      escapeCsvField(sanitizeCsvValue(c.microsoft_code_id)),
      c.distribution_status,
      c.microsoft_available == null ? "" : String(c.microsoft_available),
      c.microsoft_redeemed == null ? "" : String(c.microsoft_redeemed),
      escapeCsvField(sanitizeCsvValue(c.raw_order_id ?? "")),
      escapeCsvField(sanitizeCsvValue(c.campaign ?? "")),
      escapeCsvField(sanitizeCsvValue(assignedTo)),
      escapeCsvField(sanitizeCsvValue(c.assigned_channel ?? "")),
      c.assigned_at ?? "",
      c.microsoft_expire_at ?? "",
      escapeCsvField(sanitizeCsvValue(c.note ?? "")),
    ].join(",");
  });

  return [csvHeaders.join(","), ...rows].join("\n");
}
