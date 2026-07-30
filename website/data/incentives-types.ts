export type SubmissionKind = "feature" | "bug";
export type SubmissionStatus = "pending" | "reviewing" | "accepted" | "declined";
export type RewardStatus = "not_eligible" | "pending" | "issued";

export type SubmissionAttachment = {
  path: string;
  name: string;
  type: string;
  size: number;
  signedUrl?: string;
};

export type IncentiveSubmission = {
  id: string;
  kind: SubmissionKind;
  nickname: string;
  email: string;
  title: string;
  body: string;
  attachments: SubmissionAttachment[];
  like_count: number;
  status: SubmissionStatus;
  reward_status: RewardStatus;
  developer_reply: string | null;
  is_flagged: boolean;
  is_public: boolean;
  created_at: string;
  updated_at: string;
};

export type PublicSuggestion = Pick<
  IncentiveSubmission,
  "id" | "kind" | "nickname" | "title" | "body" | "created_at" | "like_count" | "developer_reply"
> & {
  liked: boolean;
  attachment?: {
    name: string;
    type: string;
    url: string;
  };
};

export type ReleasePreview = {
  id: string;
  version: string;
  title_zh: string;
  title_en: string;
  body_zh: string;
  body_en: string;
  highlights_zh: string[];
  highlights_en: string[];
  target_date: string | null;
  status: "draft" | "published";
  created_at: string;
  updated_at: string;
  published_at: string | null;
};

export type FeatureContentSection = {
  id: string;
  title_zh: string;
  title_en: string;
  body_zh: string;
  body_en: string;
  items_zh: string[];
  items_en: string[];
  visible: boolean;
};

export type FeatureContent = {
  summary: {
    label_zh: string;
    label_en: string;
    items_zh: string[];
    items_en: string[];
    visible: boolean;
  };
  sections: FeatureContentSection[];
};

export type AccessSeverity = "normal" | "warning" | "critical";

export type AccessLogEntry = {
  id: number | string;
  scope: "public" | "admin";
  event_type: string;
  path: string;
  method: string;
  status_code: number | null;
  ip_address: string | null;
  ip_source: string | null;
  visitor_hash: string;
  country: string | null;
  region: string | null;
  city: string | null;
  user_agent: string | null;
  accept_language: string | null;
  request_id: string | null;
  forwarded_for: string | null;
  referrer: string | null;
  severity: AccessSeverity;
  details: Record<string, unknown>;
  created_at: string;
  acknowledged_at: string | null;
};
