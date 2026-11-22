# CareerMatch cache/performance verification

**Prepared:** 22 November 2026 | **Status:** one repeatable local provider-stub integration test passed; live-provider controlled timing study still pending.

## Evidence already obtained

GitHub Actions [run 45021712378](https://github.com/Wassimchalak/CareerMatch/actions/runs/45021712378) passed the disposable SQL Server regression suite (6/6). The test `IdenticalJobSearch_ReusesSqlCacheWithoutSecondProviderCall` used a deterministic synthetic Bebity response through an isolated `HttpMessageHandler` and the **actual JobSearchService SQL cache query/write path**.

- First unique search: provider stub called **once**.
- Ten repeats with identical filter values: provider stub remained at **one total call**.
- Returned job identifier stayed stable; the test validates 10 warm reuses and no duplicate actor requests.
- This proves the **branch logic** for this case, not a 91% real-provider bandwidth saving across actual users, not 24-hour survival over a day, and not actual cache latency in production.

No real third-party request was executed; no external provider bill occurred.

## Live HTTP experiment (requires a staging account and paid/free quota)

```bash
export CAREERMATCH_TEST_JWT='TEST_ACCOUNT_TOKEN'
python scripts/benchmark_api.py \
  --base-url https://YOUR-STAGING-HOST \
  --payload /path/to/your-private-filters.json \
  --runs 10 \
  --output results/staging_cache_2026-11-XX.csv
```

1. Use an authorized *staging account* and a fixed five-field job filter. Record server revision/commit, host region, clock, provider status, and job count.
2. Distinguish **verified cold** and **verified warm** requests by checking SQL `JobSearchCache.CacheKey`, `CreatedAt`, `ExpiresAt`, `JobIdsJson`, and actual provider-call logs. Do not call all repeated requests warm without evidence.
3. Complete at least ten comparable observations per condition if quota and time allow. Keep raw HTTP status and timing, and note rate limiting and failures instead of deleting failed measurements.
4. Report median, minimum, maximum and p95 of latency in milliseconds (method: p95 nearest-rank, sort and select ceiling(0.95*n)). Do not report confidence intervals for a small, uncontrolled sample.
5. For AI score caching, change only CV hash or job-description hash in a **test database**; verify whether recomputation happens. Do not upload real candidates without consent.
6. Stop if provider requests cost money beyond the approved research cap. The basic unit/HTTP/SQL CI suite never calls external provider APIs.

## Caveats

Current `scripts/benchmark_api.py` deliberately marks all cache states `NOT_SERVER_VERIFIED`. Timed requests alone cannot prove cache hits; cross-check SQL and provider-call evidence. Initial production developer observations of ~30-50 s for job search and ~3-5 s for single AI scoring are anecdotal (network load, provider latency, client device and prompts were uncontrolled). Do not relabel them measured benchmarks.
