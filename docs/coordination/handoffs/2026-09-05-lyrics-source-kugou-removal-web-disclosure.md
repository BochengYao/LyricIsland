# Handoff: KuGou lyrics source removal and public source disclosure

- Date: 2026-09-05
- Domains: Desktop Lyrics & Player Core, Desktop UI, Public Website Frontend, Quality & Integration
- Baseline commit: `06a88fd02224566ad3f66990ae4e905a9bfe18b4`
- Implementation commit: `27ddbb8e1988a106b230fbda233a1fc882eb9ca6`
- Working state: implementation, public feature-output redaction, and release evidence committed on `main`
- Release state: local `publish/current` rebuilt and verified; website redaction commit `9c672ce105c84ec5a3829ff554659e7744ff9d1a` is on GitHub and verified through ESA production responses

## Scope

- Remove the KuGou lyrics provider and all runtime paths that could call it.
- Keep KuGou as a supported SMTC player; player detection is unrelated to the removed lyrics provider.
- Replace named public lyric-provider claims with the generic disclosure: `在线歌词由第三方来源按需获取。`
- Keep the existing QQ Music and NetEase lyrics providers as explicitly requested.
- Research Musixmatch licensing separately; do not integrate it in this change.

## Implementation

- Deleted `LyricHover.Core/KuGouLyricsClient.cs`.
- Removed KuGou from the lyrics preference enum, fallback chain, client factory, settings priority, and settings UI.
- Reserved the retired numeric enum value `3`, bumped the settings schema to `6`, and added a regression test so old persisted KuGou preferences fall back safely instead of selecting another provider.
- Updated public home/update copy in Chinese, Traditional Chinese (derived), English, and Japanese so lyric-provider copy does not name providers or expose endpoints and request parameters.
- Added an output-only disclosure filter to public `/api/features`; matching historical provider-detail items are replaced by the locale's generic disclosure while the stored/admin content remains intact.
- Preserved KuGou player detection, catalog entries, translations, and tests.

## Verification

- `dotnet build LyricHover.sln -c Release`: passed with 0 errors and existing type-conflict warnings.
- `dotnet run --project LyricHover.Tests -c Release --no-build`: passed, including the new retired-source migration test.
- Website TypeScript check: passed via the bundled Node runtime.
- `build:esa` equivalent (`scripts/build-esa-static.mjs`): passed; 17 static routes generated.
- `test:esa-api` equivalent (`scripts/test-esa-api.mjs`): passed.
- `git diff --check`: passed.
- Exact search found no remaining `KuGouLyricsClient`, `LyricsSourcePreference.KuGou`, `lyrics.kugou.com`, or KuGou lyric-response test references.

## Local release candidate

- Command: `publish.ps1 -KeepVersion -NoLaunch` after a successful `win-x64` restore.
- Result: `publish/current`, framework-dependent `win-x64`, `3.2.35-Beta`; 8 files totaling 24,532,283 bytes.
- `LyricHover.App.dll` SHA-256: `D371B6686DCAFD11EC7C2A7171A16EE0A95093D7C58008FD745C9BB15D6CB847`.
- `LyricHover.App.exe` SHA-256: `1FF4A9D1DE3FC104F52BFD3A5C78237CBDAF02B71E679D994C37691BEEB173F7`.
- `LyricHover.Core.dll` SHA-256: `70038808857EB89C923447E0BBD45B9D19F21FC0ED8FAC339F4E33874767DF8F`.
- Published App DLL hash matches the same-build `win-x64` Release output; `.deps.json` and `.runtimeconfig.json` are nonempty; staging is absent.
- The previous current candidate was retained at `publish/archive/v3.2.35-Beta-20260905-171456`.
- The previously running `publish/current` process was stopped by the authoritative script; `-NoLaunch` left the new candidate stopped.
- The release-version transaction and serialization fixture passed separately after packaging.

## GitHub and ESA evidence

- GitHub `origin/main` reached `a64bb861b79e24beeba369375966ff56406f599f`; that push triggered an ESA deployment.
- Production `/`, `/zh-hant/`, `/en/`, and `/ja/` returned HTTP 200 with `Server: ESA`, contained the locale-specific generic disclosure, and did not contain `LRCLIB`.
- The first production check also found that `/api/features` was still serving historical provider-detail text stored in the database.
- The follow-up implementation filters only the public feature response. ESA API tests prove that admin saves retain the original provider-detail text while public output substitutes the generic disclosure and retains unrelated items.
- The public-output follow-up was committed as `9c672ce105c84ec5a3829ff554659e7744ff9d1a` and pushed to `origin/main`, triggering the second ESA deployment.
- After that deployment, `/`, `/zh-hant/`, `/en/`, `/ja/`, and `/api/features` all returned HTTP 200 with `Server: ESA`; every locale page contained its expected generic disclosure, and none of those responses contained `LRCLIB`.
- Production `/api/features` retained the KuGou/Kugou Music player-compatibility wording, confirming that only lyric-provider detail was redacted.

## Follow-up correction

- A user screenshot showed that the updates overview could still display `LRCLIB`, Tencent Music, and NetEase Music as lyric providers.
- Root cause: the first public-output filter covered version-detail `sections` but omitted the overview `summary`; the earlier HTTP checks did not constitute rendered-browser evidence for every visible content region.
- The correction applies the same locale-aware disclosure replacement to `summary` and re-applies `publicFeatureContent(...)` in `ManagedFeatureContent` immediately before rendering, providing a client-side fail-closed boundary as well as the ESA API boundary.
- Regression coverage now stores the screenshot-equivalent sentence in the admin summary, proves the admin response retains it, and proves the public summary replaces it with the generic disclosure in all four locales.
- The correction was committed as `9d333145fa7e9c6f97441d11e21276160f1dc80f` and pushed to `origin/main`.
- Production `/updates/` returned HTTP 200 with `Server: ESA` and referenced the new `21crtjarm_on0.js` chunk; the downloaded production chunk matched the locally verified build byte-for-byte.
- Production `/api/features` returned HTTP 200 with `Server: ESA`; its summary and complete public payload contained neither `LRCLIB`, Tencent Music, NetEase Music, nor the screenshot sentence.

## Legal and product boundary

- This change removes one provider integration and reduces public technical disclosure; it does not establish that the remaining third-party lyric use is licensed.
- Hiding provider names or endpoints does not change API terms, copyright obligations, or the underlying network behavior.
- Musixmatch is not included until its current plan and written display rights are confirmed for this product and distribution model.

## Remaining owner

- Legal & Evidence: retain provider terms or written permission for each remaining online lyric source before external release.
- Microsoft Store distribution remains separate and was not uploaded or submitted by this handoff.

## Thread routing — 2026-09-06

### Website & Backend

- Accept the public-content contract: administrator storage may retain historical provider wording, but public `/api/features` and the client render path must filter both `summary` and `sections`.
- Preserve the four-locale generic disclosure and the client-side fail-closed call in `ManagedFeatureContent`.
- For later feature-content changes, run TypeScript, ESA API, and ESA static-build gates serially and verify every visible content region rather than only one response body.

### Desktop Core

- Treat KuGou lyrics-provider support as retired. Preserve numeric preference value `3` as reserved migration history; do not remap it to another provider.
- Continue to distinguish KuGou SMTC player compatibility from lyrics-provider access.
- QQ Music and NetEase lyrics providers remain present by explicit product decision; this handoff does not establish their licensing status.

### Quality & Release

- Use `27ddbb8`, `9c672ce`, and `9d33314` as the implementation chain and `c9ccc59` as the recorded production-verification point for this task.
- Preserve the regression boundary: admin content retains source detail, while public four-language `summary` and `sections` do not expose provider names.
- Do not describe the local `publish/current` candidate as a GitHub Release, Microsoft Store upload/submission, or current release if later desktop-version commits have superseded it.

### Brand & Compliance

- Treat `在线歌词由第三方来源按需获取。` as a limited public disclosure, not as proof of authorization or a transfer of responsibility to users.
- Retain provider terms, written permissions, takedown contacts, and a source-disable procedure for every remaining online lyrics integration.
- Keep Musixmatch unintegrated until the applicable plan and written display/distribution rights are confirmed.

### Architecture

- Preserve three distinct boundaries in future decisions: player detection is not a lyrics provider; administrator evidence is not public copy; technical redaction is not copyright authorization.
- Any proposal to add or restore a provider must route through Desktop Core, Website & Backend, Quality & Release, and Brand & Compliance before external release.

## Open risks and owner actions

- [ ] **Brand & Compliance** — determine and retain the current authorization basis for QQ Music and NetEase lyrics use; record uncertainty rather than inferring rights from technical API accessibility.
- [ ] **Website & Backend** — keep the public redaction regression test aligned with every new locale and every newly introduced public feature-content field.
- [ ] **Quality & Release** — require production-visible-page evidence for future lyric-source copy changes; HTTP/API checks alone are insufficient when the browser renders database-backed content.
- [ ] **Desktop Core** — provide an auditable provider kill switch or equivalent disable path if the existing configuration cannot promptly disable a challenged source.
