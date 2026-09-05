import type { Locale } from "@/data/site-copy";

export type ReleaseTimingPreset =
  | "today"
  | "tomorrow"
  | "this-week"
  | "two-weeks"
  | "this-month"
  | "custom-days"
  | "tbd";

export type ReleaseTiming = {
  preset: ReleaseTimingPreset;
  days: number | null;
};

const DAY_IN_MS = 24 * 60 * 60 * 1000;

function dateKey(date: Date) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function addCalendarDays(date: Date, days: number) {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate() + days);
}

function endOfWeek(date: Date) {
  return addCalendarDays(date, (7 - date.getDay()) % 7);
}

function endOfMonth(date: Date) {
  return new Date(date.getFullYear(), date.getMonth() + 1, 0);
}

function parseDateKey(value: string | null) {
  const match = value?.match(/^(\d{4})-(\d{2})-(\d{2})$/);
  if (!match) return null;
  const parsed = new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
  return dateKey(parsed) === value ? parsed : null;
}

function calendarDaysBetween(left: Date, right: Date) {
  const leftUtc = Date.UTC(left.getFullYear(), left.getMonth(), left.getDate());
  const rightUtc = Date.UTC(right.getFullYear(), right.getMonth(), right.getDate());
  return Math.round((leftUtc - rightUtc) / DAY_IN_MS);
}

export function releaseTimingFromTargetDate(targetDate: string | null, now = new Date()): ReleaseTiming {
  const target = parseDateKey(targetDate);
  if (!target) return { preset: "tbd", days: null };

  const days = calendarDaysBetween(target, now);
  if (days < 0) return { preset: "tbd", days: null };
  if (days === 0) return { preset: "today", days };
  if (days === 1) return { preset: "tomorrow", days };
  if (dateKey(target) === dateKey(endOfWeek(now))) return { preset: "this-week", days };
  if (dateKey(target) === dateKey(endOfMonth(now))) return { preset: "this-month", days };
  if (days === 14) return { preset: "two-weeks", days };
  return { preset: "custom-days", days };
}

export function targetDateForReleaseTiming(
  preset: ReleaseTimingPreset,
  customDays: number,
  now = new Date()
) {
  if (preset === "tbd") return null;
  if (preset === "today") return dateKey(now);
  if (preset === "tomorrow") return dateKey(addCalendarDays(now, 1));
  if (preset === "this-week") return dateKey(endOfWeek(now));
  if (preset === "two-weeks") return dateKey(addCalendarDays(now, 14));
  if (preset === "this-month") return dateKey(endOfMonth(now));
  const days = Math.min(365, Math.max(2, Math.round(customDays) || 2));
  return dateKey(addCalendarDays(now, days));
}

export function formatReleaseTiming(targetDate: string | null, locale: Locale, now = new Date()) {
  const timing = releaseTimingFromTargetDate(targetDate, now);
  const days = timing.days ?? 0;

  if (locale === "zh") {
    if (timing.preset === "today") return "今天";
    if (timing.preset === "tomorrow") return "明天";
    if (timing.preset === "this-week") return "本周内";
    if (timing.preset === "two-weeks") return "两周内";
    if (timing.preset === "this-month") return "本月内";
    if (timing.preset === "custom-days") return `${days}天内`;
    return "待定";
  }

  if (locale === "zhHant") {
    if (timing.preset === "today") return "今天";
    if (timing.preset === "tomorrow") return "明天";
    if (timing.preset === "this-week") return "本週內";
    if (timing.preset === "two-weeks") return "兩週內";
    if (timing.preset === "this-month") return "本月內";
    if (timing.preset === "custom-days") return `${days}天內`;
    return "待定";
  }

  if (locale === "ja") {
    if (timing.preset === "today") return "今日中";
    if (timing.preset === "tomorrow") return "明日";
    if (timing.preset === "this-week") return "今週中";
    if (timing.preset === "two-weeks") return "2週間以内";
    if (timing.preset === "this-month") return "今月中";
    if (timing.preset === "custom-days") return `${days}日以内`;
    return "未定";
  }

  if (timing.preset === "today") return "Today";
  if (timing.preset === "tomorrow") return "Tomorrow";
  if (timing.preset === "this-week") return "This week";
  if (timing.preset === "two-weeks") return "Within two weeks";
  if (timing.preset === "this-month") return "This month";
  if (timing.preset === "custom-days") return `Within ${days} days`;
  return "TBD";
}
