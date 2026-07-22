"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { Eyebrow, LogoLockup } from "@/components/SitePage";
import { SelectiveTextReveal } from "@/components/SelectiveTextReveal";
import {
  SubmissionTicket,
  type SubmissionReceipt
} from "@/components/SubmissionTicket";
import type {
  PublicSuggestion,
  ReleasePreview,
  SubmissionKind
} from "@/data/incentives-types";
import { incentivesByLocale } from "@/data/incentives-copy";
import type { Locale } from "@/data/site-copy";
import { v2Suggestions } from "@/data/v2-suggestions";

const IDENTITY_COOKIE = "lyric_island_contributor";
const LOCAL_LIKES_KEY = "lyric_island_preview_likes";

type Identity = { nickname: string; email: string };

function splitPreviewItems(value: string): string[] {
  return value
    .split(/\r?\n/)
    .map((line) => line.trim().replace(/^(?:[-–—•·*]+|\d+[.)、])\s*/, "").trim())
    .filter(Boolean);
}

function readIdentity(): Identity {
  if (typeof document === "undefined") return { nickname: "", email: "" };
  const raw = document.cookie
    .split(";")
    .map((part) => part.trim())
    .find((part) => part.startsWith(`${IDENTITY_COOKIE}=`))
    ?.slice(IDENTITY_COOKIE.length + 1);
  if (!raw) return { nickname: "", email: "" };
  try {
    const value = JSON.parse(decodeURIComponent(raw)) as Partial<Identity>;
    return {
      nickname: typeof value.nickname === "string" ? value.nickname : "",
      email: typeof value.email === "string" ? value.email : ""
    };
  } catch {
    return { nickname: "", email: "" };
  }
}

function saveIdentity(identity: Identity) {
  const secure = location.protocol === "https:" ? "; Secure" : "";
  document.cookie = `${IDENTITY_COOKIE}=${encodeURIComponent(JSON.stringify(identity))}; Path=/; SameSite=Lax; Max-Age=31536000${secure}`;
}

function ContributionForm({
  kind,
  locale,
  identity,
  onIdentityChange,
  onSuccess
}: {
  kind: SubmissionKind;
  locale: Locale;
  identity: Identity;
  onIdentityChange: (identity: Identity) => void;
  onSuccess: (receipt: SubmissionReceipt) => void;
}) {
  const copy = incentivesByLocale[locale];
  const inputRef = useRef<HTMLInputElement>(null);
  const [files, setFiles] = useState<File[]>([]);
  const [state, setState] = useState<"idle" | "sending" | "success" | "error">("idle");
  const [message, setMessage] = useState("");

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    setState("sending");
    setMessage("");
    saveIdentity(identity);

    try {
      const formData = new FormData(form);
      const title = String(formData.get("title") ?? "");
      const body = String(formData.get("body") ?? "");
      const response = await fetch("/api/incentives/submissions", {
        method: "POST",
        body: formData
      });
      const result = (await response.json()) as { id?: string; error?: string };
      if (!response.ok) throw new Error(result.error ?? "Submission failed");
      const receipt: SubmissionReceipt = {
        id: result.id ?? crypto.randomUUID(),
        kind,
        nickname: identity.nickname,
        email: identity.email,
        title,
        body,
        submittedAt: new Date().toISOString()
      };
      setState("success");
      setMessage(kind === "feature" ? copy.form.successFeature : copy.form.successBug);
      form.reset();
      setFiles([]);
      onSuccess(receipt);
    } catch (error) {
      setState("error");
      setMessage(error instanceof Error ? error.message : "Submission failed");
    }
  }

  function changeFiles(event: React.ChangeEvent<HTMLInputElement>) {
    const next = Array.from(event.target.files ?? []).slice(0, 3);
    setFiles(next);
  }

  function removeFile(index: number) {
    const next = files.filter((_, fileIndex) => fileIndex !== index);
    setFiles(next);
    if (inputRef.current) {
      const transfer = new DataTransfer();
      next.forEach((file) => transfer.items.add(file));
      inputRef.current.files = transfer.files;
    }
  }

  return (
    <form className="contributionForm" onSubmit={submit}>
      <input type="hidden" name="kind" value={kind} />
      <label className="honeypot" aria-hidden="true">
        Company
        <input name="company" tabIndex={-1} autoComplete="off" />
      </label>
      <div className="identityFields">
        <label>
          <span>{copy.form.nickname}</span>
          <input
            name="nickname"
            value={identity.nickname}
            onChange={(event) =>
              onIdentityChange({ ...identity, nickname: event.target.value })
            }
            maxLength={48}
            autoComplete="nickname"
            required
          />
        </label>
        <label>
          <span>{copy.form.email}</span>
          <input
            name="email"
            type="email"
            value={identity.email}
            onChange={(event) =>
              onIdentityChange({ ...identity, email: event.target.value })
            }
            maxLength={180}
            autoComplete="email"
            required
          />
        </label>
      </div>
      <p className="identityHint">{copy.form.identityHint}</p>
      <label>
        <span>{copy.form.title}</span>
        <input
          name="title"
          minLength={4}
          maxLength={120}
          placeholder={kind === "feature" ? copy.form.featureTitlePlaceholder : copy.form.bugTitlePlaceholder}
          required
        />
      </label>
      <label>
        <span>{copy.form.description}</span>
        <textarea
          name="body"
          minLength={12}
          maxLength={4000}
          rows={8}
          placeholder={kind === "feature" ? copy.form.featureDescriptionPlaceholder : copy.form.bugDescriptionPlaceholder}
          required
        />
      </label>
      <div className="attachmentField">
        <div>
          <strong>{copy.form.attachments}</strong>
          <small>{copy.form.attachmentHint}</small>
        </div>
        <label className="attachmentButton">
          <span aria-hidden="true">＋</span>
          {locale === "zh" ? "选择文件" : "Choose files"}
          <input
            ref={inputRef}
            name="attachments"
            type="file"
            accept="image/*,video/*"
            multiple
            onChange={changeFiles}
          />
        </label>
      </div>
      {files.length > 0 && (
        <ul className="attachmentList" aria-label={copy.form.attachments}>
          {files.map((file, index) => (
            <li key={`${file.name}-${file.lastModified}`}>
              <span>{file.name}</span>
              <small>{(file.size / 1024 / 1024).toFixed(1)} MB</small>
              <button type="button" onClick={() => removeFile(index)}>
                {copy.form.removeAttachment}
              </button>
            </li>
          ))}
        </ul>
      )}
      <div className="formSubmitRow">
        <button className="button buttonPrimary" type="submit" disabled={state === "sending"}>
          {state === "sending"
            ? copy.form.submitting
            : kind === "feature"
              ? copy.form.submitFeature
              : copy.form.submitBug}
        </button>
        <p className={`formMessage ${state}`} role="status" aria-live="polite">
          {message}
        </p>
      </div>
    </form>
  );
}

function AcceptedRail({
  suggestions,
  emptyText,
  locale,
  onLike,
  poppingId
}: {
  suggestions: PublicSuggestion[];
  emptyText: string;
  locale: Locale;
  onLike: (id: string) => void;
  poppingId: string | null;
}) {
  if (!suggestions.length) return <p className="acceptedEmpty">{emptyText}</p>;

  return (
    <div className="acceptedWaterfallViewport">
      <div className="acceptedWaterfall">
        {Array.from({ length: 4 }, (_, columnIndex) => {
          const cycleLength = Math.max(4, suggestions.length);
          const cycle = Array.from(
            { length: cycleLength },
            (_, index) => suggestions[(index + columnIndex) % suggestions.length]
          );
          return (
            <div
              className={`acceptedWaterfallColumn ${columnIndex % 2 === 0 ? "movesUp" : "movesDown"}`}
              key={columnIndex}
            >
              <div className="acceptedWaterfallTrack">
                {[...cycle, ...cycle].map((item, index) => {
                  const duplicate = index >= cycle.length;
                  const accessible = columnIndex === 0 && !duplicate && index < suggestions.length;
                  return (
                    <article
                      className="acceptedCardFrame"
                      data-duplicate={duplicate ? "true" : "false"}
                      key={`${item.id}-${columnIndex}-${index}`}
                      aria-hidden={!accessible}
                    >
                      <div className="acceptedCard">
                        <div className="acceptedCardTop">
                          <time className="acceptedTime" dateTime={item.created_at}>
                            {new Intl.DateTimeFormat(locale === "zh" ? "zh-CN" : "en-US", {
                              year: "numeric",
                              month: locale === "zh" ? "2-digit" : "short",
                              day: "2-digit"
                            }).format(new Date(item.created_at))}
                          </time>
                          <span className={`acceptedKind ${item.kind}`}>{item.kind === "bug" ? "Bug" : locale === "zh" ? "建议" : "Idea"}</span>
                        </div>
                        <h3>{item.title}</h3>
                        <p>{item.body}</p>
                        {item.developer_reply && <div className="acceptedDeveloperReply"><strong>{locale === "zh" ? "开发者回复" : "Developer reply"}</strong><p>{item.developer_reply}</p></div>}
                        {item.attachment && (
                          <a
                            className={`acceptedAttachment ${item.attachment.type.startsWith("video/") ? "isVideo" : "isImage"}`}
                            href={item.attachment.url}
                            target="_blank"
                            rel="noreferrer"
                            tabIndex={accessible ? 0 : -1}
                          >
                            {item.attachment.type.startsWith("image/") ? (
                              <img src={item.attachment.url} alt={accessible ? item.attachment.name : ""} />
                            ) : (
                              <><span aria-hidden="true">▶</span>{locale === "zh" ? "视频附件" : "Video attachment"}</>
                            )}
                          </a>
                        )}
                        <div className="acceptedMeta">
                          <span className="acceptedAuthor">@{item.nickname}</span>
                          <button
                            className={`acceptedLikeButton ${item.liked ? "isLiked" : ""} ${poppingId === item.id ? "isPopping" : ""}`}
                            type="button"
                            aria-label={`${locale === "zh" ? "点赞" : "Like"}：${item.title}`}
                            aria-pressed={item.liked}
                            tabIndex={accessible ? 0 : -1}
                            onClick={() => onLike(item.id)}
                          >
                            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 20.4 4.8 14A4.7 4.7 0 0 1 11.4 7.3l.6.7.6-.7A4.7 4.7 0 0 1 19.2 14Z" /></svg>
                            <span>{item.like_count}</span>
                          </button>
                        </div>
                      </div>
                    </article>
                  );
                })}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

export function IncentivePage({ locale }: { locale: Locale }) {
  const copy = incentivesByLocale[locale];
  const home = locale === "zh" ? "/" : "/en";
  const [tab, setTab] = useState<SubmissionKind>("feature");
  const [identity, setIdentity] = useState<Identity>({ nickname: "", email: "" });
  const [suggestions, setSuggestions] = useState<PublicSuggestion[]>(() => v2Suggestions(locale));
  const [previews, setPreviews] = useState<ReleasePreview[]>([]);
  const [poppingId, setPoppingId] = useState<string | null>(null);
  const [receipt, setReceipt] = useState<SubmissionReceipt | null>(null);
  const [storageConfigured, setStorageConfigured] = useState(false);

  useEffect(() => {
    setIdentity(readIdentity());
    try {
      const localLikes = new Set(JSON.parse(localStorage.getItem(LOCAL_LIKES_KEY) ?? "[]") as string[]);
      setSuggestions((items) => items.map((item) => localLikes.has(item.id) ? {
        ...item,
        liked: true,
        like_count: Math.max(1, item.like_count)
      } : item));
    } catch {
      localStorage.removeItem(LOCAL_LIKES_KEY);
    }
    const syncHash = () => setTab(location.hash === "#bugs" ? "bug" : "feature");
    syncHash();
    addEventListener("hashchange", syncHash);
    fetch("/api/incentives/public")
      .then((response) => response.json())
      .then((data: { suggestions?: PublicSuggestion[]; previews?: ReleasePreview[]; configured?: boolean }) => {
        const configured = Boolean(data.configured);
        setStorageConfigured(configured);
        if (configured) setSuggestions(data.suggestions ?? []);
        else if (data.suggestions?.length) setSuggestions(data.suggestions);
        setPreviews(data.previews ?? []);
      })
      .catch(() => undefined);
    return () => removeEventListener("hashchange", syncHash);
  }, []);

  function selectTab(next: SubmissionKind) {
    setTab(next);
    history.replaceState(null, "", next === "bug" ? "#bugs" : "#features");
    document.getElementById("submission-panel")?.scrollIntoView({ behavior: "smooth", block: "start" });
  }

  async function toggleLike(id: string) {
    const current = suggestions.find((suggestion) => suggestion.id === id);
    if (!current) return;
    const optimisticLiked = !current.liked;
    setSuggestions((items) => items.map((item) => item.id === id ? {
      ...item,
      liked: optimisticLiked,
      like_count: Math.max(0, item.like_count + (optimisticLiked ? 1 : -1))
    } : item));
    setPoppingId(id);
    setTimeout(() => setPoppingId((value) => value === id ? null : value), 560);

    if (!storageConfigured) {
      try {
        const localLikes = new Set(JSON.parse(localStorage.getItem(LOCAL_LIKES_KEY) ?? "[]") as string[]);
        if (optimisticLiked) localLikes.add(id);
        else localLikes.delete(id);
        localStorage.setItem(LOCAL_LIKES_KEY, JSON.stringify([...localLikes]));
      } catch {
        // The visual state still works when browser storage is unavailable.
      }
      return;
    }

    try {
      const response = await fetch("/api/incentives/likes", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ submissionId: id })
      });
      const result = (await response.json()) as { liked?: boolean; like_count?: number };
      if (!response.ok || typeof result.liked !== "boolean" || typeof result.like_count !== "number") {
        throw new Error("Like failed");
      }
      setSuggestions((items) => items.map((item) => item.id === id ? {
        ...item,
        liked: result.liked!,
        like_count: result.like_count!
      } : item));
    } catch {
      setSuggestions((items) => items.map((item) => item.id === id ? current : item));
    }
  }

  return (
    <>
      <a className="skipLink" href="#incentives-main">
        {locale === "zh" ? "跳到用户激励计划" : "Skip to community rewards"}
      </a>
      <SelectiveTextReveal />
      <header className="siteHeader">
        <nav className="floatingNav incentivesNav" aria-label={copy.navLabel}>
          <Link href={home} className="brandLink"><LogoLockup /></Link>
          <div className="incentiveTabs" role="tablist" aria-label={copy.navLabel}>
            <button role="tab" aria-selected={tab === "feature"} onClick={() => selectTab("feature")}>{copy.tabs.feature}</button>
            <button role="tab" aria-selected={tab === "bug"} onClick={() => selectTab("bug")}>{copy.tabs.bug}</button>
          </div>
          <div className="navActions">
            <Link className="languageLink" href={copy.languageHref}>{copy.languageName}</Link>
          </div>
        </nav>
      </header>

      <main id="incentives-main" className="incentivesMain">
        <section className="incentivesHero sectionContainer">
          <Eyebrow reveal>{copy.eyebrow}</Eyebrow>
          <div className="incentivesHeroGrid">
            <h1 data-text-reveal="title" style={{ whiteSpace: "pre-line" }}>
              {copy.title}
            </h1>
            <div>
              <p>{copy.intro}</p>
              <small>{copy.privacyNote}</small>
            </div>
          </div>
        </section>

        <section className={`submissionStage ${tab === "bug" ? "isBug" : "isFeature"}`} id="submission-panel">
          <div className="sectionContainer submissionGrid">
            <div className="submissionPitch">
              <Eyebrow>{tab === "feature" ? copy.feature.eyebrow : copy.bug.eyebrow}</Eyebrow>
              <h2>{tab === "feature" ? copy.feature.title : copy.bug.title}</h2>
              <p>{tab === "feature" ? copy.feature.body : copy.bug.body}</p>
              <div className="rewardPill"><span aria-hidden="true">✦</span>{tab === "feature" ? copy.feature.reward : copy.bug.reward}</div>
            </div>
            <ContributionForm kind={tab} locale={locale} identity={identity} onIdentityChange={setIdentity} onSuccess={setReceipt} />
          </div>
        </section>

        <section className="acceptedSection">
          <div className="sectionContainer acceptedHeading">
            <Eyebrow reveal>{copy.feature.acceptedEyebrow}</Eyebrow>
            <div><h2 data-text-reveal="title">{copy.feature.acceptedTitle}</h2><p>{copy.feature.acceptedSubtitle}</p></div>
          </div>
          <AcceptedRail suggestions={suggestions} emptyText={copy.feature.acceptedEmpty} locale={locale} onLike={toggleLike} poppingId={poppingId} />
        </section>

        <section className="previewSection sectionContainer">
          <div className="previewIntro">
            <Eyebrow reveal>{copy.preview.eyebrow}</Eyebrow>
            <h2 data-text-reveal="title">{copy.preview.title}</h2>
            <p>{copy.preview.body}</p>
          </div>
          <div className="previewList">
            {previews.length ? previews.map((preview) => {
              const title = locale === "zh" ? preview.title_zh : preview.title_en || preview.title_zh;
              const body = locale === "zh" ? preview.body_zh : preview.body_en || preview.body_zh;
              const highlights = locale === "zh" ? preview.highlights_zh : preview.highlights_en.length ? preview.highlights_en : preview.highlights_zh;
              const items = [...splitPreviewItems(body), ...highlights.map((item) => item.trim()).filter(Boolean)];
              return (
                <article className="previewCard" key={preview.id}>
                  <div className="previewCardMeta">
                    <small>{preview.version} · {copy.preview.target} {preview.target_date ?? (locale === "zh" ? "待定" : "TBD")}</small>
                  </div>
                  <div className="previewCardContent">
                    {title !== preview.version && <h3>{title}</h3>}
                    <ol className="previewItems">
                      {items.map((item, itemIndex) => (
                        <li key={`${itemIndex}-${item}`}>
                          <span className="previewItemNumber" aria-hidden="true">{String(itemIndex + 1).padStart(2, "0")}</span>
                          <p>{item}</p>
                        </li>
                      ))}
                    </ol>
                  </div>
                </article>
              );
            }) : <p className="previewEmpty">{copy.preview.empty}</p>}
          </div>
        </section>
      </main>

      <footer className="updatesFooter incentivesFooter">
        <div className="sectionContainer">
          <LogoLockup />
          <p>{copy.footerNote}</p>
          <div><Link href={home}>{copy.backLabel}</Link><span>© 2026 Lyric Island</span></div>
        </div>
      </footer>
      {receipt && <SubmissionTicket receipt={receipt} locale={locale} onClose={() => setReceipt(null)} />}
    </>
  );
}
