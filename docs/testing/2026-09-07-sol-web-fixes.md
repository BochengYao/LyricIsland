# Website W01-W05 targeted fixes

- Date: 2026-09-07
- Owner: Website Frontend + Website Admin & Data (Sol implementation)
- Baseline: `7343def786e853f236ea1707f76f3e1ee919368e`
- Worktree: `D:\AppleMusicDesktopLyrics\.worktrees\qa-astra-sol-fixes-20260907`
- Scope: `website/**` plus this report. No production database, external service, real environment file, version, release candidate, historical evidence, commit, push, or deployment was changed.

## Result

W01-W05 are implemented in the TypeScript and ESA paths. The pre-fix failures are recorded in `D:\AppleMusicDesktopLyrics\artifacts\qa-20260907-deep\web-probes.log`; the pre-fix database behavior is recorded in `db-baseline-acceptance.log`. The new executable regression covers all five web findings with synthetic storage/auth data. The independent fixed-schema harness reports 10 PASS / 0 FAIL, and its old-schema-to-migration suite reports 12 PASS / 0 FAIL.

## Fixes

### W03: conditional promo-code deletion

- `website/lib/promo-code-store.ts` and `website/esa/api.js` retain the authenticated pre-read for clear not-found/non-available responses, then delete with both `id` and `distribution_status=available`.
- The DELETE requests `return=representation`. A zero-row result is a conflict and never reports success or creates a successful DELETE audit entry.
- The audit uses the actual row returned by the conditional DELETE, not the stale pre-read row.
- Covered scenarios: normal available deletion; allocation between read and delete; another delete between read and delete; initially assigned code; no false audit on zero rows; same-origin/admin checks remain in place.
- Remaining atomicity boundary: deletion and the separate audit insert are not one database transaction. If the audit insert fails after a successful deletion, the deletion remains committed. This is an existing recoverability boundary and is not represented as fully atomic auditing.

### W01: public suggestion retrieval

- `website/lib/incentive-store.ts` and `website/esa/api.js` filter `status=accepted` in the database query before paging.
- Accepted rows are scanned with a stable keyset ordered by `updated_at desc, id desc`; accepted/private rows no longer consume the old fixed 100-row window. Scanning stops after 24 strict `public === true` rows or exhaustion.
- Public response objects are explicitly allow-listed. They cannot expose `email`, `reviewer_note`, status, or unrelated storage fields even if a storage response contains extra properties.
- Covered scenarios: 100 newer pending plus old public; 100 accepted/private plus old public; 24 public rows spanning pages; exactly one full private page; empty storage.

### W02: transactional, idempotent likes

- Both server implementations call only `POST /rest/v1/rpc/toggle_incentive_like`; the old read-count, insert, and PATCH chain has been removed and there is no unsafe fallback when the RPC is unavailable.
- `website/supabase/schema.sql` contains the canonical function. `website/supabase/2026-09-07-atomic-public-incentive-likes.sql` is the standalone transactional migration.
- The function locks the submission row, requires `accepted`, parses the prefixed review metadata inside the database, and accepts only JSON boolean `true`. Null, malformed metadata, JSON string `"true"`, private, and pending records fail closed.
- The detail insert uses the existing unique key and `ON CONFLICT DO NOTHING`; a successful new detail performs `like_count = like_count + 1` in the same transaction. The response reports `already_liked` from the actual insert result.
- The independent harness executed PostgreSQL 18.3 through PGlite 0.5.8 against the real schema and actual ESA entry: 10 PASS / 0 FAIL. It verified idempotency, queued distinct voters reaching two rows/count 2, private/pending/malformed/null/string-public rejection, update-failure rollback, anonymous EXECUTE denial, and actual two-voter ESA requests.
- Concurrency limit: PGlite used one connection, so the row-lock behavior was exercised with queued requests, not simultaneous independent database connections. A deployed multi-connection Postgres concurrency test remains required before claiming production concurrency acceptance.

### W04: strict Partner Center dates

- `website/lib/promo-code-tsv-parser.ts` now validates year, month, day, hour, minute, and second by exact UTC component round-trip, avoiding JavaScript date rollover and local timezone drift.
- Invalid month/day, non-leap February 29, hour 24, minute 60, and malformed sentinel values fail preview/import parsing.
- Normal ISO dates, leap day, both supported slash orders, optional slash time, and the exact Partner Center never-expire forms `0001/1/1`, `0001/01/01`, optionally at `00:00`, remain valid.

### W05: complete public feature filtering

- `website/data/feature-content.ts` and `website/esa/api.js` apply the same locale-aware filter to summary `label_*` and `items_*`, plus section `title_*`, `body_*`, and `items_*` for simplified Chinese, English, traditional Chinese, and Japanese.
- Public values containing the protected lyric-source wording are replaced with the existing locale-specific generic disclosure. Admin retrieval retains the original stored text.
- `ManagedFeatureContent` still re-applies `publicFeatureContent(sanitizeFeatureContent(...))`, so the client render boundary remains fail closed.
- Synthetic tests inject `LRCLIB` into every public field and language, then execute the actual ESA API and transpiled client implementation. Player compatibility text naming QQ Music, NetEase Music, and Kugou players remains intact in Chinese and English controls.

### Windows ESA build startup

- `website/scripts/build-esa-static.mjs` launches `node_modules/next/dist/bin/next` with `process.execPath` and `shell: false`. This avoids the Windows `next.cmd` child process losing `node` while preserving the same dependency versions.
- The path is assembled with `node:path.resolve`, and direct Node invocation completed 17/17 static routes. After the build, `website/app/api` existed, `website/esa-source-staging/api` did not, and both `out/index.html` and `esa-dist/entry.js` existed.

## Verification

Commands were run serially in `website/` after the final source changes unless stated otherwise.

1. `npm run typecheck`
   - npm printed `'tsc' is not recognized as an internal or external command`. In this environment npm nevertheless returned process exit code 0, so this output is recorded as an environment launcher failure, not a passing typecheck.
2. `node node_modules/typescript/bin/tsc --noEmit`
   - Exit 0; no diagnostics.
3. `node scripts/test-esa-api.mjs`
   - Exit 0; `ESA API tests passed`.
4. `node scripts/check-support-security.mjs`
   - Exit 0; `Support security checks passed`.
5. `node scripts/test-sol-web-fixes.mjs`
   - Exit 0; `Sol website behavior regressions passed: W01 W02 W03 W04 W05`.
6. `npm run build:esa`
   - Failed at the npm command wrapper before the script started: `'node' is not recognized as an internal or external command`.
7. `node scripts/build-esa-static.mjs`
   - Exit 0; Next.js 16.2.10 compiled and typechecked, then generated 17/17 static pages.
8. `git diff --check -- website`
   - Exit 0. Git emitted only line-ending conversion warnings and the sandbox-global ignore read warning.

Independent database evidence from root-owned local artifacts:

- `D:\AppleMusicDesktopLyrics\artifacts\qa-20260907-deep\db-baseline-acceptance.log`: old schema 5 PASS / 3 FAIL; accepted/private, malformed metadata, and null metadata were incorrectly accepted.
- `D:\AppleMusicDesktopLyrics\artifacts\qa-20260907-deep\db-sol-strict-public-verified.log`: fixed schema 10 PASS / 0 FAIL, including actual ESA two-voter requests with consistent detail rows and aggregate count.
- `D:\AppleMusicDesktopLyrics\artifacts\qa-20260907-deep\db-sol-migration-verified.log`: old schema plus existing like upgraded through the standalone migration, history/grants were preserved, reapplying the migration did not lose history, and the complete fixed-schema suite passed; 12 PASS / 0 FAIL.

## SQL deployment order and rollback

The application now requires the three-column RPC response (`liked`, `like_count`, `already_liked`). Having the function in the repository does not prove it exists in production.

1. Apply `website/supabase/2026-09-07-atomic-public-incentive-likes.sql` to the target Supabase/Postgres project in one transaction.
2. Verify the function signature, service-role EXECUTE grant, anonymous denial, public/private negative cases, idempotent repeat, and two independent database connections before deploying the application code.
3. Deploy the website only after the migration checks pass. If migration verification fails, roll back its transaction and do not deploy this application revision.

Do not deploy the application first and do not restore only the old unsafe REST chain. Reverting after both layers are live requires reverting the application and reinstalling the prior two-column RPC definition together; that rollback reintroduces the known race/publicity flaw, so a forward fix is preferred. Root's independent local harness executed the schema and migration against synthetic data; no production migration or production check was executed.

## Remaining acceptance

- Astra medium must independently review W01 keyset paging, W02 database/publicity checks and deployment dependency, W03 zero-row audit behavior, W04 sentinel/date coverage, W05 every render field/language, and the build script's API restoration path.
- Production database migration, multi-connection transaction contention, deployed ESA propagation/cache, and visible browser rendering in all four locales remain external acceptance steps. Local mock/static output cannot establish those states.
- The shared worktree also contains desktop/Core changes owned by the other Sol task. This report and the paths above identify only the website changes made by this task.
