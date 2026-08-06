"use client";

import { useEffect, useRef, useState } from "react";
import type { SubmissionKind } from "@/data/incentives-types";
import type { Locale } from "@/data/site-copy";

export type SubmissionReceipt = {
  id: string;
  kind: SubmissionKind;
  nickname: string;
  email: string;
  title: string;
  body: string;
  submittedAt: string;
};

function formatDate(value: string, locale: Locale) {
  return new Intl.DateTimeFormat(locale === "zh" ? "zh-CN" : "en-US", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hour12: false
  }).format(new Date(value));
}

function roundedRect(
  context: CanvasRenderingContext2D,
  x: number,
  y: number,
  width: number,
  height: number,
  radius: number
) {
  context.beginPath();
  context.roundRect(x, y, width, height, radius);
  context.closePath();
}

function fitLines(
  context: CanvasRenderingContext2D,
  text: string,
  maxWidth: number,
  maxLines: number
) {
  const lines: string[] = [];
  let line = "";
  for (const character of text.replace(/\s+/g, " ").trim()) {
    const candidate = line + character;
    if (context.measureText(candidate).width > maxWidth && line) {
      lines.push(line);
      line = character;
      if (lines.length === maxLines) break;
    } else {
      line = candidate;
    }
  }
  if (lines.length < maxLines && line) lines.push(line);
  const consumed = lines.join("").length;
  if (consumed < text.replace(/\s+/g, " ").trim().length && lines.length) {
    lines[lines.length - 1] = `${lines[lines.length - 1].replace(/[.,，。…\s]+$/, "")}…`;
  }
  return lines;
}

export function SubmissionTicket({
  receipt,
  locale,
  onClose
}: {
  receipt: SubmissionReceipt;
  locale: Locale;
  onClose: () => void;
}) {
  const doneRef = useRef<HTMLButtonElement>(null);
  const [downloading, setDownloading] = useState(false);
  const isZh = locale === "zh";
  const date = formatDate(receipt.submittedAt, locale);

  useEffect(() => {
    doneRef.current?.focus({ preventScroll: true });
    const previousDocumentOverflow = document.documentElement.style.overflow;
    const previousBodyOverflow = document.body.style.overflow;
    document.documentElement.style.overflow = "hidden";
    document.body.style.overflow = "hidden";
    const closeWithEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };
    addEventListener("keydown", closeWithEscape);
    return () => {
      removeEventListener("keydown", closeWithEscape);
      document.documentElement.style.overflow = previousDocumentOverflow;
      document.body.style.overflow = previousBodyOverflow;
    };
  }, [onClose]);

  async function downloadPng() {
    setDownloading(true);
    try {
      const canvas = document.createElement("canvas");
      canvas.width = 1400;
      canvas.height = 720;
      const context = canvas.getContext("2d");
      if (!context) throw new Error("Canvas unavailable");

      context.clearRect(0, 0, canvas.width, canvas.height);
      context.save();
      context.shadowColor = "rgba(20, 20, 19, 0.16)";
      context.shadowBlur = 34;
      context.shadowOffsetY = 16;
      context.fillStyle = "#FCFBFA";
      roundedRect(context, 40, 40, 1320, 640, 54);
      context.fill();
      context.restore();

      context.save();
      roundedRect(context, 40, 40, 1320, 640, 54);
      context.clip();
      context.fillStyle = "#FFD84D";
      context.fillRect(40, 40, 330, 640);
      context.restore();

      context.save();
      context.globalCompositeOperation = "destination-out";
      for (const y of [40, 680]) {
        context.beginPath();
        context.arc(370, y, 26, 0, Math.PI * 2);
        context.fill();
      }
      for (let y = 86; y <= 634; y += 28) {
        for (const x of [40, 1360]) {
          context.beginPath();
          context.arc(x, y, 10, 0, Math.PI * 2);
          context.fill();
        }
      }
      context.restore();

      context.strokeStyle = "rgba(20, 20, 19, 0.36)";
      context.lineWidth = 3;
      context.setLineDash([12, 14]);
      context.beginPath();
      context.moveTo(370, 80);
      context.lineTo(370, 640);
      context.stroke();
      context.setLineDash([]);

      const fontFamily = '"Microsoft YaHei", "PingFang SC", "Arial", sans-serif';
      context.fillStyle = "#141413";
      context.font = `600 28px ${fontFamily}`;
      context.fillText("LYRIC HOVER", 92, 118);
      context.font = `700 56px ${fontFamily}`;
      context.fillText(isZh ? "提交存根" : "SUBMISSION", 92, 210);
      context.font = `500 24px ${fontFamily}`;
      context.fillText(receipt.kind === "feature" ? (isZh ? "新功能提议" : "FEATURE IDEA") : (isZh ? "BUG 提交" : "BUG REPORT"), 92, 270);

      const left = 430;
      context.fillStyle = "#6A6763";
      context.font = `600 20px ${fontFamily}`;
      context.fillText(isZh ? "提交人" : "NAME", left, 116);
      context.fillText(isZh ? "提交时间" : "SUBMITTED", 850, 116);
      context.fillText(isZh ? "邮箱" : "EMAIL", left, 220);
      context.fillText(isZh ? "标题" : "TITLE", left, 326);
      context.fillText(isZh ? "提交内容" : "DETAILS", left, 450);

      context.fillStyle = "#141413";
      context.font = `600 30px ${fontFamily}`;
      context.fillText(receipt.nickname, left, 162);
      context.fillText(date, 850, 162);
      context.font = `500 28px ${fontFamily}`;
      context.fillText(receipt.email, left, 266);
      context.font = `600 32px ${fontFamily}`;
      fitLines(context, receipt.title, 830, 2).forEach((line, index) => {
        context.fillText(line, left, 374 + index * 42);
      });
      context.font = `450 25px ${fontFamily}`;
      fitLines(context, receipt.body, 830, 4).forEach((line, index) => {
        context.fillText(line, left, 494 + index * 36);
      });

      const blob = await new Promise<Blob>((resolve, reject) => {
        canvas.toBlob((value) => value ? resolve(value) : reject(new Error("PNG creation failed")), "image/png");
      });
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `lyric-island-ticket-${receipt.id.slice(0, 8)}.png`;
      anchor.click();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
    } finally {
      setDownloading(false);
    }
  }

  return (
    <div className="ticketModal" role="presentation" onMouseDown={(event) => {
      if (event.target === event.currentTarget) onClose();
    }}>
      <section className="ticketDialog" role="dialog" aria-modal="true" aria-labelledby="ticket-title">
        <div className="ticketThanks">
          <div>
            <p className="eyebrow">{isZh ? "提交成功" : "Submission received"}</p>
            <h2 id="ticket-title">
              {isZh ? (
                <>谢谢你的提交<br />如果被采纳我们将通过邮件联系你❤️</>
              ) : (
                <>Thank you.<br />If it is accepted, we will contact you by email ❤️</>
              )}
            </h2>
            <p>{isZh ? "这张存根可以下载为 PNG 留作纪念。" : "This keepsake can be saved as a PNG."}</p>
          </div>
        </div>

        <div className="ticketIssueStage">
          <div className="ticketDispenser" aria-hidden="true" />
          <div className="ticketFeedWindow">
            <article className="submissionTicket" aria-label={isZh ? "提交存根" : "Submission ticket"}>
              <div className="ticketStub">
                <span>LYRIC HOVER</span>
                <strong>{isZh ? "提交存根" : "SUBMISSION"}</strong>
                <small>{receipt.kind === "feature" ? (isZh ? "新功能提议" : "FEATURE IDEA") : (isZh ? "BUG 提交" : "BUG REPORT")}</small>
              </div>
              <dl className="ticketDetails">
                <div><dt>{isZh ? "姓名" : "Name"}</dt><dd>{receipt.nickname}</dd></div>
                <div><dt>{isZh ? "提交时间" : "Submitted"}</dt><dd><time dateTime={receipt.submittedAt}>{date}</time></dd></div>
                <div className="ticketWide"><dt>{isZh ? "邮箱" : "Email"}</dt><dd>{receipt.email}</dd></div>
                <div className="ticketWide"><dt>{isZh ? "标题" : "Title"}</dt><dd>{receipt.title}</dd></div>
                <div className="ticketWide"><dt>{isZh ? "提交内容" : "Details"}</dt><dd>{receipt.body}</dd></div>
              </dl>
            </article>
          </div>
        </div>

        <div className="ticketActions">
          <button className="button buttonPrimary" type="button" onClick={downloadPng} disabled={downloading}>
            <svg aria-hidden="true" viewBox="0 0 24 24"><path d="M12 3v12m0 0 5-5m-5 5-5-5M5 20h14" /></svg>
            {downloading ? (isZh ? "正在生成…" : "Creating…") : (isZh ? "下载 PNG 存根" : "Download PNG ticket")}
          </button>
          <button ref={doneRef} className="button buttonSecondary" type="button" onClick={onClose}>{isZh ? "完成" : "Done"}</button>
        </div>
      </section>
    </div>
  );
}
