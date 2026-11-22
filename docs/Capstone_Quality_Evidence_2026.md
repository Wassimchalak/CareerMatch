# CSC599 verification ledger (22 November 2026)

This is supplementary engineering evidence for the CareerMatch final report.
It distinguishes **pre-existing user-reported work**, **observable Git history**,
**verified new CI execution**, and **work not yet measured**.

## Verified repository timeline (GitHub commit dates, UTC)

| Date | Evidence | Interpretation |
| --- | --- | --- |
| 30–31 Jul 2026 | Commits including `3662473c` (dashboard), `790a88f2` (save-job work) | Repository evidence of UI and saved-job iteration by late July. |
| 26 Aug 2026 | `418a90cc` (cover letter fixes), `7c8ac6aa` (CV upload), `6e239bb7` (API frontend integration) | Continued work on CV, documents and integration. |
| 16 Sep 2026 | `f2122cce` (Bebity integration), `e4a6ce6a` (caching), `823742d3` (production CORS), `6c5a5c7f` (Vercel deploy trigger) | Concrete provider migration, caching and deployment evidence. |
| 30 Sep 2026 | `77d9fa6e` (matching prompt), `2e2505e8` (minimum score) | Matching/prompt calibration iterations. |
| 7 Nov 2026 | `641a9762` (120-second timeout) | Provider latency/reliability adjustment. |
| 22 Nov 2026 | `capstone-quality-2026-11-22` | Additional tests, CI, source/security fixes and documentation. |

Development beginning in **June 2026** is the student's recollection; the
accessible Git commit history used here does not independently establish a
June commit. The original Gantt is a retrospective reconstruction and should
not be described as a historical task-tracking record.

## New verified tests (GitHub Actions)

Workflow [run 45018832603](https://github.com/Wassimchalak/CareerMatch/actions/runs/45018832603)
completed successfully: **14 passed, 0 failed, 0 skipped**, .NET 10.
Test source is in `CareerMatch.API.Tests/`. Test scope: search cache-key
normalization, deduplication/content hashes, search validation, and JWT
generation. The workflow records TRX and coverage artifacts. The reported
count is **not** a measured application-wide coverage percentage.

A subsequent security dependency-upgrade run
[45018942864](https://github.com/Wassimchalak/CareerMatch/actions/runs/45018942864)
also passed the same 14 tests and emitted **no NU1902/NU1903 advisories**.
This checks restore/build/unit-test behavior, not production deployment or
database integration. The latest combined [run 45019305718](https://github.com/Wassimchalak/CareerMatch/actions/runs/45019305718) is **fully successful**: backend xUnit tests 14/14 passed; React TypeScript production build and ESLint checks passed. This run also reports **21 npm audit findings (11 moderate, 10 high)** from the frontend install; their exploitability and direct-vs-transitive origin have **not** been triaged, so the system must not be called dependency-vulnerability-free.

## Source review and fixes on the improvement branch

- `Program.cs`: restrict Swagger/OpenAPI endpoints to Development.
- `SavedJobService.cs`: ensure the saved-job snapshot also matches current
  job-description hash, not only latest CV hash.
- `JobApplicationService.cs`: use one most recent matching row per application
  to prevent duplicate application-history records.
- `CareerMatch.API.csproj`: override vulnerable transitive WebUtilities and
  PKCS dependencies with patched .NET 10 versions.
- `JobSearchService.cs`: correct the timeout error text to match 120 s.
- `README.md`: update public documentation to Bebity via Apify.
- GitHub CI: xUnit .NET tests, plus a React TypeScript build/lint check.

These changes live in an isolated branch until the pull request is merged.
Database integration regression results and production rollout are **not**
asserted. Live authentication secrets, real CVs and production database
dumps must not be committed as test fixtures.

## Evaluation still required for stronger claims

- **AI matching consistency:** obtain consented anonymized CV/job examples
  with independent human labels; include junior/senior mismatches and synonyms;
  repeat inference with fixed configuration. The attached analysis utility
  computes actual differences but is not evidence by itself.
- **Cache/performance:** run documented cold/warm requests with real HTTP
  timings, confirm cache-hit state server-side, record sample size, errors,
  medians/p95, version, location, and provider conditions.
- **User acceptance:** approximately 50 users and positive responses are
  *student-reported informal feedback*. No survey denominator or verified
  satisfaction percentages exist.
- **Costs and project management:** real OpenAI/domain expenses are reported
  without receipts or amounts; Bebity operates on a free tier. GitHub is
  used for version control, but no contemporaneous project-management
  software plan or detailed historical cost ledger is documented.

Do not award a 4/4 rubric score solely on this ledger; the oral and
project-management criteria require faculty judgment and possibly other
evidence.
