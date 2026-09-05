# Handoff: KuGou lyrics source removal and public source disclosure

- Date: 2026-09-05
- Domains: Desktop Lyrics & Player Core, Desktop UI, Public Website Frontend, Quality & Integration
- Baseline commit: `06a88fd02224566ad3f66990ae4e905a9bfe18b4`
- Implementation commit: `27ddbb8e1988a106b230fbda233a1fc882eb9ca6`
- Working state: implementation and release-candidate evidence committed on `main`; public feature-output redaction is pending its release commit
- Release state: local `publish/current` rebuilt and verified; commits through `a64bb861b79e24beeba369375966ff56406f599f` are on GitHub and the first ESA deployment was observed in production

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
- Final public API production verification and its release commit are recorded in the follow-up release evidence below once synchronization completes.

## Legal and product boundary

- This change removes one provider integration and reduces public technical disclosure; it does not establish that the remaining third-party lyric use is licensed.
- Hiding provider names or endpoints does not change API terms, copyright obligations, or the underlying network behavior.
- Musixmatch is not included until its current plan and written display rights are confirmed for this product and distribution model.

## Suggested next owner

- Release, GitHub & Store: push local `main`, wait for ESA synchronization, and verify the production response and expected public copy.
- Legal & Evidence: retain provider terms or written permission for each remaining online lyric source before external release.
