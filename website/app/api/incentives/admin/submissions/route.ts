import type { RewardStatus, SubmissionStatus } from "@/data/incentives-types";
import { isAdminRequest, isSameOrigin } from "@/lib/admin-auth";
import { listSubmissions, updateSubmission } from "@/lib/incentive-store";

const statuses: SubmissionStatus[] = ["pending", "reviewing", "accepted", "declined"];
const rewards: RewardStatus[] = ["not_eligible", "pending", "issued"];

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
    return Response.json({ error: "Unauthorized" }, { status: 401 });
  }
  try {
    const body = (await request.json()) as Record<string, unknown>;
    const id = typeof body.id === "string" ? body.id : "";
    const status = statuses.includes(body.status as SubmissionStatus)
      ? (body.status as SubmissionStatus)
      : undefined;
    const reward = rewards.includes(body.reward_status as RewardStatus)
      ? (body.reward_status as RewardStatus)
      : undefined;
    const note = typeof body.reviewer_note === "string"
      ? body.reviewer_note.trim().slice(0, 2000)
      : undefined;
    if (!id || (!status && !reward && note === undefined)) {
      return Response.json({ error: "Invalid update" }, { status: 400 });
    }
    const submission = await updateSubmission(id, {
      ...(status ? { status } : {}),
      ...(reward ? { reward_status: reward } : {}),
      ...(note !== undefined ? { reviewer_note: note || null } : {})
    });
    return Response.json({ submission });
  } catch {
    return Response.json({ error: "更新失败" }, { status: 500 });
  }
}
