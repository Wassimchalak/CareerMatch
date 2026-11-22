# CareerMatch architecture diagrams — editable source

**Updated:** 22 November 2026. Diagram relationships are grounded in the supplied SQL Server schema and the checked controller/service workflow. These source diagrams complement the legible, vector-rendered illustrations in the 30-page academic PDF.

## Complete SQL Server ERD (14 tables / 17 declared foreign keys)

The **actual UTF-16 SQL Server DDL dated 20 November 2026** was inspected and all 17 `FOREIGN KEY` constraints enumerated, rather than inferring relationships from similar-sounding table names. The report now presents readable **Figure 3a (page 9)** and **Figure 3b (page 10)** connected views; repeated parent boxes are visual references to the same real table. Every child has a PK as well as its named FK attributes, and cardinalities are shown.

```mermaid
erDiagram
  Users ||--o{ CVs : FK_CVs_Users
  Users ||--o{ PasswordResetTokens : FK_PasswordResetTokens_Users
  CVs ||--o{ ExtractedCVSkills : FK_ExtractedCVSkills_CVs
  Skills ||--o{ ExtractedCVSkills : FK_ExtractedCVSkills_Skills
  Jobs ||--o{ JobRequiredSkills : FK_JobRequiredSkills_Jobs
  Skills ||--o{ JobRequiredSkills : FK_JobRequiredSkills_Skills
  Users ||--o{ JobMatches : FK_JobMatches_Users
  CVs ||--o{ JobMatches : FK_JobMatches_CVs
  Jobs ||--o{ JobMatches : FK_JobMatches_Jobs
  Users ||--o{ SavedJobs : FK_SavedJobs_Users
  Jobs ||--o{ SavedJobs : FK_SavedJobs_Jobs
  Users ||--o{ JobApplications : FK_JobApplications_Users
  CVs ||--o{ JobApplications : FK_JobApplications_CVs
  Jobs ||--o{ JobApplications : FK_JobApplications_Jobs
  JobApplications ||--o{ GeneratedCVs : FK_GeneratedCVs_JobApplications
  JobApplications ||--o{ GeneratedCoverLetters : FK_CoverLetters_JobApplications
  JobApplications ||--o| GeneratedInterviewQuestions : FK_GeneratedInterviewQuestions_JobApplications

  Users {
    int UserId PK
    string Email
  }
  CVs {
    int CVId PK
    int UserId FK
    string CVTextHash
  }
  Skills {
    int SkillId PK
    string SkillName
  }
  ExtractedCVSkills {
    int ExtractedCVSkillId PK
    int CVId FK
    int SkillId FK
    decimal YearsOfExperience
  }
  Jobs {
    int JobId PK
    string Title
    string DescriptionHash
  }
  JobRequiredSkills {
    int JobRequiredSkillId PK
    int JobId FK
    int SkillId FK
  }
  JobMatches {
    int JobMatchId PK
    int UserId FK
    int CVId FK
    int JobId FK
    decimal FinalScore
  }
  SavedJobs {
    int SavedJobId PK
    int UserId FK
    int JobId FK
  }
  JobApplications {
    int ApplicationId PK
    int UserId FK
    int CVId FK
    int JobId FK
  }
  GeneratedCVs {
    int GeneratedCVId PK
    int ApplicationId FK
  }
  GeneratedCoverLetters {
    int GeneratedCoverLetterId PK
    int ApplicationId FK
  }
  GeneratedInterviewQuestions {
    int GeneratedInterviewQuestionId PK
    int ApplicationId FK
  }
  PasswordResetTokens {
    int PasswordResetTokenId PK
    int UserId FK
    string TokenHash
  }
  JobSearchCache {
    int CacheId PK
    string CacheKey UK
    string JobIdsJson
    datetime ExpiresAt
  }
```

**Cardinality caveat:** Each SQL FK requires its child row to reference a parent except **JobApplications.CVId**, which is nullable. The generated-interview table has a **UNIQUE ApplicationId** constraint and is therefore **0..1 per application**. The other generated document tables have no matching unique ApplicationId constraint and can contain multiple rows per application. `JobSearchCache.CacheKey` is **unique but not the PK**; `CacheId` is the PK. `JobSearchCache` has no FK to `Jobs`; `JobIdsJson` is serialized IDs.

## Selected UML domain classes

```mermaid
classDiagram
  class User {
    +int UserId
    +string FullName
    +string Email
  }
  class CV {
    +int CVId
    +int UserId
    +string CVTextHash
  }
  class Job {
    +int JobId
    +string Title
    +string DescriptionHash
  }
  class JobMatch {
    +int JobMatchId
    +int UserId
    +int CVId
    +int JobId
    +decimal FinalScore
  }
  class SavedJob {
    +int SavedJobId
    +int UserId
    +int JobId
  }
  class JobApplication {
    +int ApplicationId
    +int UserId
    +int CVId
    +int JobId
  }
  User "1" --> "0..*" CV : owns
  User "1" --> "0..*" SavedJob : saves
  User "1" --> "0..*" JobMatch : receives
  User "1" --> "0..*" JobApplication : records
  Job "1" --> "0..*" JobMatch : assessed
  Job "1" --> "0..*" SavedJob : bookmarked
  Job "1" --> "0..*" JobApplication : opened
  CV "1" --> "0..*" JobMatch : used_in
  CV "1" --> "0..*" JobApplication : used_for
```

## Activity diagram

```mermaid
flowchart TD
  start((Start)) --> search[Enter role, country, city, mode, type]
  search --> cards[Fetch and display job cards]
  cards --> want{Show Best Matches?}
  want -->|No| unscored[Save or open vacancy without score]
  want -->|Yes| cv[Use latest CV or upload PDF]
  cv --> valid{Valid, supported CV?}
  valid -->|No| reject[Show validation error]
  reject --> cv
  valid -->|Yes| cached{Same CV and job hashes as cached score?}
  cached -->|Yes| show[Rank and explain scores >= 60%]
  cached -->|No| ai[Ask AI and persist current score]
  ai --> show
  show --> docs[Optional CV/cover-letter toolkit]
  unscored --> docs
  docs --> handoff[Open employer application website]
  handoff --> finish(((End)))
```

## Sequence diagram (conditional provider/AI calls)

```mermaid
sequenceDiagram
  actor Candidate
  participant UI as React UI
  participant API as ASP.NET API
  participant SQL as SQL Server
  participant Provider as Bebity/Apify
  participant AI as OpenAI

  Candidate->>UI: Set job-search filters
  UI->>API: POST /api/JobSearch/search
  API->>SQL: Lookup normalized 24h cache key
  SQL-->>API: Cache hit or miss
  opt Cache miss
    API->>Provider: Retrieve recent LinkedIn-originated jobs
    Provider-->>API: Vacancy data
    API->>SQL: Deduplicate, upsert, and cache results
  end
  API-->>UI: Display vacancy cards
  Candidate->>UI: Show Best Matches
  UI->>API: Request matching
  API->>SQL: Load CV, vacancy, and score hashes
  SQL-->>API: Valid and stale/missing score entries
  opt Uncached or stale matches exist
    API->>AI: Evaluate relevant CV-job evidence
    AI-->>API: Score, explanation, recommendation
    API->>SQL: Save matching results
  end
  API-->>UI: Display explained scores, filter at 60%
```

The interface-level 60% cutoff is an uncalibrated product decision, not a verified hiring outcome. Application handoffs are not employer-side submission confirmations.
