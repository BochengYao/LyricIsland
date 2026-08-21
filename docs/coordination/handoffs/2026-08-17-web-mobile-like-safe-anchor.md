# HANDOFF — 01-web-frontend → 05-quality-integration

- Date: 2026-08-17
- Owner: 01-web-frontend
- Scope: public `/incentives` mobile like-control containment only
- Baseline: `origin/main` `9c8e0aba0dff4e8dd0e364e7d5196f5cae40dc55`
- Candidate branch: `codex/feature/web-mobile-like-safe`
- Candidate commit: see the commit containing this handoff

## Change

- At `<=767px`, anchor the complete `52×44px` like hit box inside each accepted card (`right: 20px; bottom: 25px`) so the hit target, not merely its visual layer, has clearance from the rotated card's clipped rounded edge.
- Keep the visible control a short horizontal `52×26px` capsule through the existing pseudo-element.
- Reserve metadata space so the absolutely positioned control does not change or cover the card information hierarchy.
- Desktop styles, API, admin, data, and authentication are unchanged.

## Verification

- Browser: installed Microsoft Edge, headless Playwright Core runner used only as a temporary local verifier.
- Data: live `https://lyric-island.top/api/incentives/public` payload routed into the local production build.
- Actual transform state: `.acceptedWaterfallTrack` animations were paused at multiple non-zero/current times after layout; screenshots capture the resulting rotated cards rather than untransformed element geometry.
- Viewports captured: `320×720`, `390×844`, `844×1100`, and `1440×900`.
- Key 390px animation samples: `t=0`, `t=12000ms`, `t=26000ms`; these show complete controls on yellow cards and cards approaching both viewport edges.
- Browser measurement: `document.documentElement.scrollWidth - viewportWidth = 0` for every captured viewport/time.
- Evidence directory: `C:\Users\14731\.codex\visualizations\2026\08\16\01a009c2-16ab-7722-9c13-d766a95fcbc6\like-safe-9c8e0ab`

## Commands

- `npm run typecheck` — passed
- `npm run test:esa-api` — passed (`ESA API tests passed`)
- `npm run build:esa` — passed; 16 static pages generated

The first build retry encountered `EPERM` because this task's temporary static server still held `out/404.html`; after stopping only that server, the final build passed. Temporary verifier scripts and the task-local npm cache were removed before commit. No ESA deployment was performed by 01-web-frontend; the coordinator has the user's authorization to trigger ESA after receiving the candidate SHA.
