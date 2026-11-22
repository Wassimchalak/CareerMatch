# CareerMatch CSC599 — rubric evidence mapping (22 November 2026)

This is a **self-audit**, not an official grade. Starting independent evaluation: provisional 58/64 assessable rubric points (9.1/10) with five below-Exemplary indicators totaling six missing points.

| Initially below-4 criterion | Original level | New, auditable work | Current evidence limitations |
|---|---:|---|---|
| Technical challenges/design choices | 3/4 | Source-referenced architecture/cache/AI/API tradeoffs; real code fixes; 29 passing tests, including SQL ownership/cache regressions | Full staging schema and deployed system untested; measured live provider/AI performance outstanding |
| Independent prior work comparison | 3/4 | Literature review already compares four identified academic papers and job platforms with CareerMatch. Genuine contribution is integrated workflow, not a novel recommendation algorithm | More source-by-source critical comparison and supervisor judgment may still be needed; no invented novelty claims |
| Effective understanding/use of multiple design methodologies | 3/4 | Source demonstrates relational design, controller-service separation, DI/DTO boundaries and incremental database-first/backend-first development; CI reinforces testing process | These are patterns vs processes, not evidence of Scrum; independent faculty confirmation |
| Graphical communication | 3/4 | UML use case, classes, activity, sequence, state and ER diagrams plus readable actual UI/Swagger evidence retained in 30-page academic PDF; one forward-looking Gantt added | Exact rubric placement/readability and notation quality require final human review; diagrams are not automatically 4/4 |
| Project management | 2/4 | A **real GitHub Issues plan created 22 Nov**, #2–#6, has tasks/owners/acceptance, current Gantt (Nov22-Dec4), due dates, risk register and cost categories. Earlier Git commit dates establish historical milestones | No proof a formal planning board, hours ledger or cost receipts existed June–Nov. Current plan is prospective. Official 4/4 cannot be retrospectively asserted |

### New verified tests
[GitHub Actions run 45021712378](https://github.com/Wassimchalak/CareerMatch/actions/runs/45021712378): **14/14 isolated + 9/9 in-memory HTTP + 6/6 disposable SQL Server = 29/29 passing**; React production build/lint pass. SQL cache test performs 11 identical searches with just one provider **stub** call. No production credential use, external network calls, or real CVs. Tests use a minimal synthetic database schema; a full production migration/integration run remains outstanding.

### Which 4/4 claims are defensible?

- The report is demonstrably better justified and has substantially stronger executed test evidence. A prospective 4/4 for technical analysis and methodologies is a reasonable **candidate**, but only the faculty may award it.
- The independent-research and graphical criteria must be judged on actual comparative depth and figure quality; do not automatically promote them solely because the source is better tested.
- Project management now has genuine project-management **software from Nov 22**, an explicit WBS/Gantt/schedule/cost/risk plan. It does not prove those practices were used over the full June-November implementation. Score remains **unconfirmed** rather than rewriting 2/4 to 4/4.

### Remaining experiment/task tracking
- [Issue #2](https://github.com/Wassimchalak/CareerMatch/issues/2): full staging and deployed-account regression check.
- [Issue #3](https://github.com/Wassimchalak/CareerMatch/issues/3): independently rated real AI scoring cases; repeated scores, seniority and 60% cutoff.
- [Issue #4](https://github.com/Wassimchalak/CareerMatch/issues/4): controlled verified live provider/cached latency, quota and errors.
- [Issue #5](https://github.com/Wassimchalak/CareerMatch/issues/5): figures, page-level proofreading, final PDF source alignment.
- [Issue #6](https://github.com/Wassimchalak/CareerMatch/issues/6): oral demo rehearsal and Dec 4 delivery.

### Final report
The 30-page CSC599 academic report now states 29 verified CI tests, observed (not independently labeled) AI examples, local provider-stub cache data and a current Gantt. It retains screenshots and all mandatory sections. The standalone Design Report is not expressly listed as an additional artifact in the Final Report instructions; course/Blackboard directions still govern.

Do not misrepresent methodology, benchmark latency, user-satisfaction percentages, AI accuracy, or historical spending.
