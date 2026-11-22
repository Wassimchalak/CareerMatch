# CareerMatch AI matching: independent validation protocol

**Prepared:** 22 Nov 2026 | **Status:** preregistered protocol, **NOT** a completed accuracy study.

## What a score means

CareerMatch returns a heuristic AI-generated job-fit percentage and an explanation/recommendation. This is **not** a validated probability of securing an interview, a guarantee of qualification, or an automated hiring decision. The "Best Matches" UI retains jobs whose matchScore is >= 60 (DashboardPage.tsx), while Saved Jobs can request a score for one job regardless of threshold.

## Existing observational evidence (not a controlled comparison)

Screenshots supplied during project development illustrate the following distinct jobs:

| Example screenshot case | Displayed score | Relevant observation |
|---|---:|---|
| Chedid Capital Group, Software Developer - DevOps | 38% | Explanation notes 3–5 years sought vs roughly 0.1 years of candidate professional experience |
| Modern Electronics, Data Analyst | 45% | Explanation notes 1–2 years and missing Power BI/advanced Excel |
| Software Engineer with .NET requirements | 65% | Explanation notes strong .NET skills but 2–4-year experience gap |
| Recruitable, Junior Consultant | 70% | Junior position; explanation notes relevant analysis/research skills |
| TikTok, Data Analyst Project Intern | 78% | Intern level; explanation notes strong Python/stats alignment |
| Quik Hire Staffing, Backend Software Engineer | 85% | Explanation describes strong alignment of engineering skills |

These are observed **different postings**, not controlled matched pairs. They show face-valid examples of why individual scores differed, but **cannot** establish calibration, accuracy, fairness, repeatability, or a causal seniority penalty. Job descriptions, CV states, AI versions and dates may differ.

## Prospective experiment

1. Recruit two or more independent assessors. Do not show AI scores before the assessors label anonymized CV–job pairs. Obtain permission to use each CV and remove names, addresses, phone/email, dates of birth and identifiable employment details.
2. Define at least 20 consented cases distributed across these conditions: genuinely junior candidate/senior job, senior candidate/junior job, same-role entry position, direct skill match, synonymous technologies, missing explicit skill years, transferable experience, missing mandatory skills, preferred-only missing skills, and unrelated role.
3. **Controlled contrasts:** keep CV, skills and location constant and change only required job experience or one required skill at a time. This isolates seniority/skill effects; simply comparing two unrelated vacancy scores does not.
4. Ask assessors to rate 0–100 role fit based on role/essential skills, professional experience, nonessential qualifications and stated requirements, and record their written rationale. Scores are judgments, not real hiring probabilities. If assessors strongly disagree, report disagreement rather than replacing it with an invented consensus.
5. For repeatability, run each consented case three times under the same documented model, prompt hash and configuration (where budget permits), without exploiting the cache. Log timestamps, response status, tokens/cost if available, temperature where supported and every returned explanation.
6. Compare AI to mean independent ratings via mean absolute disagreement in **percentage points**. Report repeated-score range, high/low mismatch cases, and display-threshold selection at 50%, 60% and 70%, with reviewer-score thresholds only as subjective reference decisions.
7. Do **not** optimize the prompt on a test set and then claim independent confirmation on those same cases. Reserve review examples or explicitly call the study exploratory.
8. Redact raw CVs, JWTs, keys, and identifiable user details from submitted evidence.

## Predeclared evaluation questions

- Does a junior profile receive a meaningfully lower score when an otherwise identical posting changes from entry-level to requiring 5 years?
- Does a synonym or alternate skill spelling leave the score largely unchanged, all else equal?
- Are missing *essential* skills penalized more than missing *preferred* skills?
- For identical inputs, how much do repeated scores and explanations vary?
- Do 50/60/70 cutoffs expose false exclusions or weak inclusions relative to independent reviewers?

The tool `scripts/matching_evaluation.py` accepts a real CSV with
`case_id,human_score,ai_score,seniority_gap,repeat_group` and emits
a real-data-only JSON summary. **No results are prepopulated in the repo.**
Keep all raw actual score runs and assessor ratings with consent in a private research folder.

### Recorded limitation

This study has **not yet been executed** with independently rated cases; no numerical calibration/accuracy claim is made. The report can include this reproducible protocol and explain the score is advisory, but not claim the 60% threshold is objectively optimal.
