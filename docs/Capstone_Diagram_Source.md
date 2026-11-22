# CareerMatch architecture diagrams — editable source

**Updated:** 22 November 2026. Diagram relationships are grounded in the supplied SQL Server schema and the checked controller/service workflow. These source diagrams complement the legible, vector-rendered illustrations in the 30-page academic PDF.

## ER diagram (14-table schema, key relationships)

```mermaid
erDiagram
  Users ||--o{ CVs : owns
  Users ||--o{ JobMatches : receives
  Users ||--o{ SavedJobs : bookmarks
  Users ||--o{ JobApplications : records
  Users ||--o{ PasswordResetTokens : requests
  CVs ||--o{ ExtractedCVSkills : yields
  Skills ||--o{ ExtractedCVSkills : categorizes
  Jobs ||--o{ JobRequiredSkills : requires
  Skills ||--o{ JobRequiredSkills : defines
  CVs ||--o{ JobMatches : assessed_for
  Jobs ||--o{ JobMatches : assessed_for
  Jobs ||--o{ SavedJobs : bookmarked_as
  Jobs ||--o{ JobApplications : handed_off_as
  CVs ||--o{ JobApplications : associated_with
  JobApplications ||--o{ GeneratedCVs : refines
  JobApplications ||--o{ GeneratedCoverLetters : drafts
  JobApplications ||--o{ GeneratedInterviewQuestions : prepares

  Users { int UserId PK string Email }
  CVs { int CVId PK int UserId FK string CVTextHash }
  Skills { int SkillId PK string SkillName }
  ExtractedCVSkills { int CVId FK int SkillId FK decimal YearsOfExperience }
  Jobs { int JobId PK string Title string DescriptionHash }
  JobRequiredSkills { int JobId FK int SkillId FK }
  JobMatches { int JobMatchId PK int UserId FK int CVId FK int JobId FK decimal FinalScore }
  SavedJobs { int SavedJobId PK int UserId FK int JobId FK }
  JobApplications { int ApplicationId PK int UserId FK int CVId FK int JobId FK }
  GeneratedCVs { int ApplicationId FK string GeneratedPdfFileName }
  GeneratedCoverLetters { int ApplicationId FK string GeneratedPdfFileName }
  GeneratedInterviewQuestions { int ApplicationId FK string GeneratedPdfFileName }
  PasswordResetTokens { int UserId FK string TokenHash }
  JobSearchCache { string CacheKey PK string JobIdsJson datetime ExpiresAt }
```

Note that `JobSearchCache` has no direct job FK; it contains serialized cached job identifiers. Direction and multiplicity follow the child-table foreign keys. The exact SQL DDL, not this reduced visual, is the authoritative schema for columns and constraints.

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
