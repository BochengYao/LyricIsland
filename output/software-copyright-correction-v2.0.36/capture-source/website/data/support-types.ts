export type SupportPaymentProvider = "wechat" | "alipay" | "manual";
export type SupportPaymentStatus = "pending" | "paid" | "refunded" | "closed";
export type SupportVerificationSource = "provider_callback" | "manual_admin";
export type SupportEntitlementCode =
  | "supporter_badge"
  | "ad_free_lifetime"
  | "pro_lifetime";

export type SupportAccount = {
  id: string;
  nickname: string;
  public_thanks: boolean;
  email_verified_at: string | null;
  created_at: string;
  updated_at: string;
};

export type SupportPayment = {
  id: string;
  account_id: string;
  provider: SupportPaymentProvider;
  merchant_order_no: string;
  provider_transaction_hash: string | null;
  amount_fen: number;
  currency: "CNY";
  status: SupportPaymentStatus;
  verification_source: SupportVerificationSource | null;
  verified_at: string | null;
  paid_at: string | null;
  refunded_at: string | null;
  created_at: string;
  updated_at: string;
};

export type SupportEntitlement = {
  id: string;
  account_id: string;
  entitlement_code: SupportEntitlementCode;
  grant_source: "payment_rule" | "manual" | "migration";
  granted_at: string;
  revoked_at: string | null;
  note: string | null;
};
