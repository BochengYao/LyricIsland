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
  reviewer_note: string | null;
  created_at: string;
  updated_at: string;
};

export type PublicSuggestion = Pick<
  IncentiveSubmission,
  "id" | "nickname" | "title" | "body" | "created_at" | "like_count"
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
