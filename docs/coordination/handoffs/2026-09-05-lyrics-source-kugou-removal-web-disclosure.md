# Handoff: KuGou lyrics source removal and public source disclosure

- Date: 2026-09-05
- Domains: Desktop Lyrics & Player Core, Desktop UI, Public Website Frontend, Quality & Integration
- Working state: uncommitted changes on `main`; isolated branch creation was attempted twice but permission review timed out
- Release state: validated locally; not committed, pushed, packaged, uploaded, or published

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
- Preserved KuGou player detection, catalog entries, translations, and tests.

## Verification

- `dotnet build LyricHover.sln -c Release`: passed with 0 errors and existing type-conflict warnings.
- `dotnet run --project LyricHover.Tests -c Release --no-build`: passed, including the new retired-source migration test.
- Website TypeScript check: passed via the bundled Node runtime.
- `build:esa` equivalent (`scripts/build-esa-static.mjs`): passed; 17 static routes generated.
- `test:esa-api` equivalent (`scripts/test-esa-api.mjs`): passed.
- `git diff --check`: passed.
- Exact search found no remaining `KuGouLyricsClient`, `LyricsSourcePreference.KuGou`, `lyrics.kugou.com`, or KuGou lyric-response test references.

## Legal and product boundary

- This change removes one provider integration and reduces public technical disclosure; it does not establish that the remaining third-party lyric use is licensed.
- Hiding provider names or endpoints does not change API terms, copyright obligations, or the underlying network behavior.
- Musixmatch is not included until its current plan and written display rights are confirmed for this product and distribution model.

## Suggested next owner

- Quality & Integration: review the working diff and move it to a clean feature branch before commit.
- Legal & Evidence: retain provider terms or written permission for each remaining online lyric source before external release.
