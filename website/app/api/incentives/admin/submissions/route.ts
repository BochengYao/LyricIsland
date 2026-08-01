import type { IncentiveSubmission, RewardStatus, SubmissionStatus } from "@/data/incentives-types";
import { safeRecordAccessEvent } from "@/lib/access-log";
import { isAdminRequest, isSameOrigin } from "@/lib/admin-auth";
import { deleteSubmission, listSubmissions, updateSubmission } from "@/lib/incentive-store";

const statuses: SubmissionStatus[] = ["pending", "reviewing", "accepted", "declined"];
const rewards: RewardStatus[] = ["not_eligible", "pending", "issued"];
const auditedFields: Array<keyof IncentiveSubmission> = [
  "kind",
  "nickname",
  "email",
  "title",
  "body",
  "status",
  "reward_status",
  "developer_reply",
  "is_flagged",
  "is_public",
  "like_count",
  "created_at"
];

function changedFields(previous: IncentiveSubmission, submission: IncentiveSubmission) {
  return auditedFields
    .filter((field) => previous[field] !== submission[field])
    .map((field) => ({
      field,
      before: previous[field] ?? null,
      after: submission[field] ?? null
    }));
}

export async function GET(request: Request) {
  if (!(await isAdminRequest(request))) {
    return Response.json({ error: "Unauthorized" }, { status: 401 });
  }
  try {
    return Response.json({ submissions: await listSubmissions() });
  } catch {
    return Response.json({ error: "无法读取提交记录" }, { status: 500 });
  }
}

export async function PATCH(request: Request) {
  if (!isSameOrigin(request) || !(await isAdminRequest(request))) {
    await safeRecordAccessEvent(request, {
      scope: "admin",
      eventType: "unauthorized_submission_update",
      severity: isSameOrigin(request) ? "warning" : "critical",
      statusCode: 401
    });
    return Response.json({ error: "Unauthorized" }, { status: 401 });
  }
  try {
    const body = (await request.json()) as Record<string, unknown>;
    const id = typeof body.id === "string" ? body.id : "";
    const kind = body.kind === "feature" || body.kind === "bug" ? body.kind : undefined;
    const nickname = typeof body.nickname === "string" ? body.nickname.trim().slice(0, 48) : undefined;
    const email = typeof body.email === "string" ? body.email.trim().toLowerCase().slice(0, 180) : undefined;
    const title = typeof body.title === "string" ? body.title.trim().slice(0, 120) : undefined;
    const content = typeof body.body === "string" ? body.body.trim().slice(0, 4000) : undefined;
    const status = statuses.includes(body.status as SubmissionStatus)
      ? (body.status as SubmissionStatus)
      : undefined;
    const reward = rewards.includes(body.reward_status as RewardStatus)
      ? (body.reward_status as RewardStatus)
      : undefined;
    const reply = typeof body.developer_reply === "string"
      ? body.developer_reply.trim().slice(0, 2000)
      : undefined;
    const isFlagged = typeof body.is_flagged === "boolean" ? body.is_flagged : undefined;
    const isPublic = typeof body.is_public === "boolean" ? body.is_public : undefined;
    const likeCount = typeof body.like_count === "number" && Number.isInteger(body.like_count) && body.like_count >= 0
      ? body.like_count
      : undefined;
    const createdAt = typeof body.created_at === "string" && !Number.isNaN(Date.parse(body.created_at))
      ? new Date(body.created_at).toISOString()
      : undefined;
    if (!id || (nickname !== undefined && !nickname) || (title !== undefined && title.length < 4) ||
      (content !== undefined && content.length < 12) || (email !== undefined && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) ||
      (body.like_count !== undefined && likeCount === undefined) || (body.created_at !== undefined && createdAt === undefined) ||
      (!kind && nickname === undefined && email === undefined && title === undefined && content === undefined && !status && !reward && reply === undefined && isFlagged === undefined && isPublic === undefined && likeCount === undefined && createdAt === undefined)) {
      return Response.json({ error: "Invalid update" }, { status: 400 });
    }
    const { submission, previous } = await updateSubmission(id, {
      ...(kind ? { kind } : {}),
      ...(nickname !== undefined ? { nickname } : {}),
      ...(email !== undefined ? { email } : {}),
      ...(title !== undefined ? { title } : {}),
      ...(content !== undefined ? { body: content } : {}),
      ...(status ? { status } : {}),
      ...(reward ? { reward_status: reward } : {}),
      ...(reply !== undefined ? { developer_reply: reply || null } : {}),
      ...(isFlagged !== undefined ? { is_flagged: isFlagged } : {}),
      ...(isPublic !== undefined ? { is_public: status && status !== "accepted" ? false : isPublic } : {}),
      ...(likeCount !== undefined ? { like_count: likeCount } : {}),
      ...(createdAt !== undefined ? { created_at: createdAt } : {})
    });
    await safeRecordAccessEvent(request, {
      scope: "admin",
      eventType: "submission_updated",
      statusCode: 200,
      details: {
        submissionId: id,
        submissionTitle: submission.title,
        submissionKind: submission.kind,
        changes: changedFields(previous, submission)
      }
    });
    return Response.json({ submission });
  } catch {
    return Response.json({ error: "更新失败" }, { status: 500 });
  }
}

export async function DELETE(request: Request) {
  if (!isSameOrigin(request) || !(await isAdminRequest(request))) {
    await safeRecordAccessEvent(request, {
      scope: "admin",
      eventType: "unauthorized_submission_delete",
      severity: isSameOrigin(request) ? "warning" : "critical",
      statusCode: 401
    });
    return Response.json({ error: "Unauthorized" }, { status: 401 });
  }
  try {
    const body = (await request.json()) as Record<string, unknown>;
    const id = typeof body.id === "string" ? body.id : "";
    if (!id) return Response.json({ error: "Invalid deletion" }, { status: 400 });
    const deleted = await deleteSubmission(id);
    await safeRecordAccessEvent(request, {
      scope: "admin",
      eventType: "submission_deleted",
      statusCode: 200,
      details: {
        submissionId: id,
        submissionTitle: deleted.title,
        submissionKind: deleted.kind,
        snapshot: {
          title: deleted.title,
          body: deleted.body,
          nickname: deleted.nickname,
          status: deleted.status,
          reward_status: deleted.reward_status,
          developer_reply: deleted.developer_reply,
          like_count: deleted.like_count
        }
      }
    });
    return Response.json({ ok: true });
  } catch {
    return Response.json({ error: "删除失败" }, { status: 500 });
  }
}
