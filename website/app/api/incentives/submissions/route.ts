import type { SubmissionKind } from "@/data/incentives-types";
import { safeAccessEventSource } from "@/lib/access-log";
import { createSubmission, uploadAttachments } from "@/lib/incentive-store";

const MAX_FILES = 3;
const MAX_FILE_SIZE = 15 * 1024 * 1024;
const MAX_TOTAL_SIZE = 30 * 1024 * 1024;

function text(form: FormData, key: string, max: number) {
  const value = form.get(key);
  return typeof value === "string" ? value.trim().slice(0, max) : "";
}

function validEmail(value: string) {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value) && value.length <= 180;
}

function jsonError(message: string, status = 400) {
  return Response.json({ error: message }, { status });
}

export async function POST(request: Request) {
  try {
    const form = await request.formData();
    if (text(form, "company", 200)) return jsonError("Invalid submission");

    const kind = text(form, "kind", 20) as SubmissionKind;
    const nickname = text(form, "nickname", 48);
    const email = text(form, "email", 180).toLowerCase();
    const title = text(form, "title", 120);
    const body = text(form, "body", 4000);

    if (kind !== "feature" && kind !== "bug") return jsonError("Invalid submission type");
    if (nickname.length < 1) return jsonError("Nickname is required");
    if (!validEmail(email)) return jsonError("A valid email is required");
    if (title.length < 4) return jsonError("Please add a more specific title");
    if (body.length < 12) return jsonError("Please add a little more detail");

    const files = form
      .getAll("attachments")
      .filter((value): value is File => value instanceof File && value.size > 0);
    if (files.length > MAX_FILES) return jsonError("Up to 3 attachments are allowed");
    if (files.some((file) => !/^(image|video)\//.test(file.type))) {
      return jsonError("Only image and video attachments are allowed");
    }
    if (files.some((file) => file.size > MAX_FILE_SIZE)) {
      return jsonError("Each attachment must be 15 MB or smaller");
    }
    if (files.reduce((total, file) => total + file.size, 0) > MAX_TOTAL_SIZE) {
      return jsonError("Attachments must total 30 MB or less");
    }

    const id = crypto.randomUUID();
    const attachments = await uploadAttachments(files, id);
    const submission = await createSubmission({
      id,
      kind,
      nickname,
      email,
      title,
      body,
      attachments,
      source: await safeAccessEventSource(request)
    });

    return Response.json({ id: submission.id, status: submission.status }, { status: 201 });
  } catch (error) {
    const message = error instanceof Error ? error.message : "Submission failed";
    if (message.includes("not configured")) {
      return jsonError("Submission service is not configured yet", 503);
    }
    return jsonError("Submission could not be saved. Please try again later.", 500);
  }
}
