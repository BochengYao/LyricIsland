export type DistributionStatus = "available" | "assigned" | "revoked" | "expired";

export type PromoCodeLogAction =
  | "TSV_IMPORT"
  | "CREATE"
  | "ASSIGN"
  | "REASSIGN"
  | "REVOKE"
  | "EDIT"
  | "DELETE"
  | "MICROSOFT_STATUS_UPDATE";

export type PromoCodeOrder = {
  id: string;
  microsoft_order_id: string | null;
  order_name: string | null;
  product_name: string | null;
  product_id: string | null;
  source: string;
  code_count: number;
  imported_at: string | null;
  microsoft_synced_at: string | null;
  created_at: string;
  updated_at: string;
};

export type PromoCode = {
  id: string;
  microsoft_code_id: string;
  order_id: string | null;
  raw_order_id: string | null;
  code: string;
  redeem_url: string | null;
  microsoft_available: boolean | null;
  microsoft_redeemed: boolean | null;
  microsoft_start_at: string | null;
  microsoft_expire_at: string | null;
  microsoft_synced_at: string | null;
  distribution_status: DistributionStatus;
  assigned_to_user_id: string | null;
  assigned_to_name: string | null;
  assigned_to_email: string | null;
  assigned_channel: string | null;
  campaign: string | null;
  assigned_at: string | null;
  revoked_at: string | null;
  note: string | null;
  imported_at: string;
  created_at: string;
  updated_at: string;
};

export type PromoCodeLog = {
  id: string;
  promo_code_id: string | null;
  operator_user_id: string | null;
  operator_email: string | null;
  action: PromoCodeLogAction;
  previous_data: Record<string, unknown> | null;
  new_data: Record<string, unknown> | null;
  metadata: Record<string, unknown>;
  created_at: string;
};

export type PromoCodeStats = {
  total: number;
  available: number;
  assigned: number;
  microsoft_redeemed: number;
  expired: number;
  expiring_soon: number;
};

export type PromoCodeFilter = {
  page: number;
  pageSize: number;
  status?: DistributionStatus | "all";
  orderId?: string;
  channel?: string;
  search?: string;
  dateFrom?: string;
  dateTo?: string;
  dateField?: "imported_at" | "assigned_at" | "microsoft_expire_at";
};

export type PromoCodePage = {
  codes: PromoCode[];
  total: number;
  page: number;
  pageSize: number;
};

export type TsvParsedRow = {
  microsoft_code_id: string;
  code: string;
  redeem_url: string | null;
  raw_order_id: string | null;
  order_name: string | null;
  /** Partner Center 中文导出“产品名称”列；无对应列时为 null */
  product_name: string | null;
  microsoft_available: boolean | null;
  microsoft_redeemed: boolean | null;
  microsoft_start_at: string | null;
  microsoft_expire_at: string | null;
  given_to: string | null;
  has_injection_risk: boolean;
  row_number: number;
};

export type TsvParseResult = {
  rows: TsvParsedRow[];
  errors: TsvParseError[];
  warnings: string[];
  total_lines: number;
};

export type TsvParseError = {
  row: number;
  message: string;
};

export type TsvImportResult = {
  new_count: number;
  updated_count: number;
  unchanged_count: number;
  order_id: string;
};

export type TsvImportPreview = {
  filename: string;
  total_detected: number;
  new_count: number;
  existing_count: number;
  microsoft_status_changes: number;
  unchanged_count: number;
  errors: TsvParseError[];
  warnings: string[];
};

export type AssignPromoCodeInput = {
  // All metadata fields are optional (DB columns are nullable); the allocator
  // only needs to know which code to hand out.
  assigned_name?: string;
  assigned_email?: string;
  assigned_channel?: string;
  campaign?: string;
  note?: string;
  /** microsoft_code_id for specific assignment (omit for auto-allocate) */
  specific_code_id?: string;
};

export type AssignPromoCodeResult = {
  id: string;
  code: string;
  redeem_url: string | null;
  microsoft_code_id: string;
};
