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

[Run 45021712378](https://github.com/Wassimchalak/CareerMatch/actions/runs/45021712378), **22 November 2026**, completed successfully on .NET 10 with:

| Test category | Passed | Scope |
| --- | ---: | --- |
| Original isolated xUnit tests | **14** | Search-cache key, deduplication/description hashes, input validation, JWT claims and signing-key checks |
| New in-memory ASP.NET Core HTTP tests | **9** | JWT 401/400 paths, health, authenticated malformed requests, Production OpenAPI/Swagger 404 |
| New disposable SQL Server tests | **6** | CV/job description score freshness, ownership, latest application history, foreign delete rejection, one provider-stub call for 11 searches |
| **Total** | **29 / 29** | Zero failed, zero skipped |

These suites use synthetic credentials and data and do **not** access the production database. The SQL regression job uses SQL Server 2022 in a disposable GitHub Actions container with a *minimal test fixture schema*, not the full production schema. Test results do not establish all feature end-to-end behavior or a measured application-wide coverage percentage.

The same run passed the React TypeScript production build and ESLint checks. The earlier frontend lockfile had **21 npm audit findings** (11 moderate, 10 high). The one-time [dependency remediation run 45020077253](https://github.com/Wassimchalak/CareerMatch/actions/runs/45020077253) used `npm audit fix` without forced major upgrades, reported **0 npm audit vulnerabilities after updates**, and passed frontend build and lint. Its temporary workflow was removed. Patched .NET dependencies also removed prior NU1902/NU1903 advisories (initial [run 45018942864](https://github.com/Wassimchalak/CareerMatch/actions/runs/45018942864)). A remaining NU1510 package-pruning warning does not by itself establish a vulnerability.

**Controlled local cache experiment:** one initial search and ten repeats against an isolated deterministic fake provider resulted in **one** total stub request and stable cached job IDs. The run uses actual Dapper SQL caching logic. This demonstrates the cache path and does **not** represent real external Bebity latency, AI scoring accuracy, 24-hour freshness or production cost savings.


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
Disposable SQL integration results are verified in the CI run above; full
production-schema staging and production rollout are **not** asserted. Live authentication secrets, real CVs and production database
dumps must not be committed as test fixtures.

## Evaluation still required for stronger claims

- **AI matching consistency:** obtain consented anonymized CV/job examples
  with independent human labels; include junior/senior mismatches and synonyms;
  repeat inference with fixed configuration. See docs/AI_Matching_Validation_Protocol.md. The attached analysis utility
  computes actual differences but is not evidence by itself.
- **Cache/performance:** run documented cold/warm requests with real HTTP
  timings, confirm cache-hit state server-side, record sample size, errors,
  medians/p95, version, location, and provider conditions. See docs/Cache_Performance_Experiment.md.
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
