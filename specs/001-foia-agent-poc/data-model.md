# Data Model: FOIA Agent Proof-of-Concept

**Feature**: 001-foia-agent-poc
**Date**: 2026-04-28
**Storage**: SQLite via EF Core 8 (`FoiaDbContext`)

All ids are GUIDs unless noted. All timestamps are stored as UTC `DATETIME` (ISO-8601). Enum-like
fields are persisted as strings for human-readability in the SQLite file.

---

## Entities

### FoiaRequest

| Field | Type | Notes |
|---|---|---|
| Id | Guid (PK) | server-assigned |
| Subject | string(200) | required, non-whitespace |
| Description | string(2000) | optional |
| RequestedStartDate | DateOnly | required |
| RequestedEndDate | DateOnly | required, ≥ RequestedStartDate |
| RequestorFullName | string(200) | required |
| RequestorOrganization | string(200) | optional |
| RequestorEmail | string(320) | required, RFC-5322-style format |
| RequestorPhone | string(40) | optional |
| RequestorMailingAddress | string(500) | optional |
| SubmittedAt | DateTime (UTC) | server-assigned |
| Status | string (enum) | see RequestStatus |

**RequestStatus** values: `Submitted`, `Validated`, `Searching`, `DocumentsFound`,
`NoDocumentsFound`, `Redacting`, `PendingHumanReview`, `ApprovedForRelease`, `Packaging`,
`ReleasePackageReady`, `Rejected`, `Error`.

**State transitions**

```text
Submitted → Validated → Searching → (DocumentsFound | NoDocumentsFound)
DocumentsFound → Redacting → PendingHumanReview
PendingHumanReview → (ApprovedForRelease | Rejected)
ApprovedForRelease → Packaging → ReleasePackageReady
Any non-terminal state → Error  (with reason recorded in AuditEvent)
```

Terminal states: `NoDocumentsFound`, `ReleasePackageReady`, `Rejected`, `Error`.

**Relationships**: 1..* `Document`, 0..* `ReviewTask`, 0..* `ReleasePackage`, 0..* `AuditEvent`.

---

### Document

| Field | Type | Notes |
|---|---|---|
| Id | Guid (PK) | |
| FoiaRequestId | Guid (FK → FoiaRequest.Id) | |
| SourceDocumentId | string(200) | id from Azure AI Search |
| FileName | string(300) | from search index |
| FileType | string(50) | e.g., "txt", "pdf" |
| SourceUri | string(2000) | optional |
| OriginalContent | text | UTF-8 text payload (PoC keeps content inline) |
| RedactedContent | text | populated after redaction |
| RedactionStatus | string (enum) | see RedactionStatus |
| ReviewStatus | string (enum) | see ReviewStatus |
| IncludeInRelease | bool | analyst-controlled inclusion decision |
| RetrievedAt | DateTime (UTC) | |

**RedactionStatus**: `NotStarted`, `InProgress`, `Completed`, `Failed`.
**ReviewStatus**: `NotStarted`, `Pending`, `Approved`, `ManualHandling`. (`ManualHandling` is the
terminal state for any document the reviewer rejects — the spec uses the word "Rejected" to describe
the reviewer's action, but the persisted document state is `ManualHandling`. There is no separate
`Rejected` document state for the PoC.)

**Relationships**: 0..* `Redaction`. Only documents with `RedactionStatus = Completed` and
`ReviewStatus = Approved` may be included in a release. Other documents may be explicitly excluded
from a package while remaining under review or in manual handling.

---

### Redaction

| Field | Type | Notes |
|---|---|---|
| Id | Guid (PK) | |
| DocumentId | Guid (FK → Document.Id) | |
| PiiType | string (enum) | see PiiType |
| OriginalText | string(500) | the substring detected |
| ReplacementText | string(60) | e.g., `[REDACTED EMAIL]` |
| StartOffset | int? | char offset in OriginalContent (null for non-text-positional sources) |
| EndOffset | int? | exclusive |
| PageNumber | int? | reserved for future PDF support |
| Confidence | double? | 0..1, populated for AI detections; null for regex |
| DetectionSource | string (enum) | `regex` or `ai` |
| ReviewerApproved | bool? | null until reviewed; true on document approve, false on reject |
| ReviewerComments | string(1000) | optional |

**PiiType**: `Email`, `Phone`, `SSN`, `DateOfBirth`, `Name`, `Address`, `FinancialId`.

**Replacement labels** (one per PiiType): `[REDACTED EMAIL]`, `[REDACTED PHONE]`, `[REDACTED SSN]`,
`[REDACTED DOB]`, `[REDACTED NAME]`, `[REDACTED ADDRESS]`, `[REDACTED FINANCIAL ID]`.

---

### ReviewTask

| Field | Type | Notes |
|---|---|---|
| Id | Guid (PK) | |
| FoiaRequestId | Guid (FK → FoiaRequest.Id) | |
| AssignedReviewer | string(200) | `"demo-reviewer"` for the PoC (single implicit identity) |
| Status | string (enum) | see ReviewTaskStatus |
| CreatedAt | DateTime (UTC) | |
| CompletedAt | DateTime? (UTC) | |
| ReviewerDecision | string (enum) | `Approved` or `Rejected`, populated on completion |
| ReviewerComments | string(1000) | optional |

**ReviewTaskStatus**: `Open`, `Completed`.

A `ReviewTask` represents the request-level review (one per FoiaRequest). Per-document approve/reject
state lives on `Document.ReviewStatus`; the `ReviewTask` tracks the overarching reviewer activity.

---

### ReleasePackage

| Field | Type | Notes |
|---|---|---|
| Id | Guid (PK) | |
| FoiaRequestId | Guid (FK → FoiaRequest.Id) | |
| Version | int | unique within a FOIA request; later curated packages create new versions |
| ZipBlobName | string(300) | `foia-request-{requestId}-release-package.zip` |
| BlobContainerName | string(100) | from configuration |
| SasUrl | string(2000) | full URL with SAS token |
| SasExpiresAt | DateTime (UTC) | now + `SasExpirationDays` (default 7) |
| CreatedAt | DateTime (UTC) | |

**Relationships**: 1..* `ReleasePackageDocument` composition snapshots. Each row captures a source
document ID, source filename, archive filename, included/excluded decision, and (for included
documents) SHA-256 of the PDF bytes added to that package. These snapshot rows do not depend on the
live document row.

---

### AuditEvent

| Field | Type | Notes |
|---|---|---|
| Id | Guid (PK) | |
| FoiaRequestId | Guid (FK → FoiaRequest.Id) | |
| RelatedDocumentId | Guid? (FK → Document.Id) | optional |
| EventType | string (enum) | see AuditEventType |
| Timestamp | DateTime (UTC) | |
| Message | string(1000) | human-readable |

**AuditEventType**: `RequestSubmitted`, `RequestValidated`, `SearchStarted`, `SearchCompleted`,
`DocumentsFound`, `RedactionStarted`, `RedactionCompleted`, `HumanReviewStarted`,
`DocumentApproved`, `DocumentRejected`, `ReleaseApproved`, `PackageCreated`, `SasUrlGenerated`,
`Error`.

---

## Validation Rules (sourced from spec FR-002..FR-004)

| Field | Rule | On violation |
|---|---|---|
| `Subject` | required, trimmed length > 0 | 400 with `errors.subject` |
| `RequestedStartDate` | required | 400 with `errors.requestedStartDate` |
| `RequestedEndDate` | required, ≥ `RequestedStartDate` | 400 with `errors.requestedEndDate` |
| `RequestorFullName` | required, trimmed length > 0 | 400 with `errors.requestorFullName` |
| `RequestorEmail` | required, valid email format | 400 with `errors.requestorEmail` |

Multiple violations are returned in a single response (do not short-circuit).

---

## Indexes (SQLite)

- `FoiaRequest.Status` — supports the status-list view if added later.
- `Document.FoiaRequestId` — supports per-request document listing.
- `AuditEvent.FoiaRequestId, Timestamp` — supports the chronological audit timeline (FR-030).
- `(ReleasePackage.FoiaRequestId, ReleasePackage.Version)` (unique) — versions are ordered per request.

---

## Out-of-scope data concerns

- No soft-delete, no versioning, no row-level audit beyond `AuditEvent`.
- No multi-tenant partition column.
- No encryption-at-rest beyond SQLite's file-level OS protections — out of scope per Principle III.
