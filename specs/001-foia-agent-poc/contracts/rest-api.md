# REST API Contract: FOIA Agent PoC

**Feature**: 001-foia-agent-poc
**Base URL (local)**: `http://localhost:5000`
**Base URL (Azure)**: Container App ingress URL (single host serves API + SPA)

All endpoints return `application/json` unless otherwise noted. All timestamps are ISO-8601 UTC. All ids are GUID strings.

---

## Common error shape

`HTTP 400` for validation errors:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "subject": ["Subject is required."],
    "requestorEmail": ["A valid email address is required."]
  }
}
```

`HTTP 404` for unknown resource ids:

```json
{ "title": "Not Found", "status": 404, "detail": "FoiaRequest {id} not found." }
```

`HTTP 409` for state-machine violations (e.g., approving release while a document is rejected):

```json
{ "title": "Conflict", "status": 409, "detail": "Cannot approve release: 1 document(s) not approved." }
```

`HTTP 500` for unexpected failures (workflow / search / blob). The body includes a `traceId`.

---

## 1. Submit FOIA Request

`POST /api/foiarequests`

**Request body**:

```json
{
  "subject": "Communications about Project Alpha",
  "description": "All emails and memos referencing Project Alpha.",
  "requestedStartDate": "2024-01-01",
  "requestedEndDate": "2024-12-31",
  "requestorFullName": "Jane Doe",
  "requestorOrganization": "Acme News",
  "requestorEmail": "jane@example.com",
  "requestorPhone": "+1-555-0100",
  "requestorMailingAddress": "123 Main St, Springfield, IL 62701"
}
```

**Validation**: per [data-model.md](../data-model.md) → "Validation Rules". Returns `400` with the
common error shape on failure.

**Response `200 OK`**:

```json
{
  "id": "8a4e5b1c-1234-4abc-9def-0123456789ab",
  "status": "Submitted",
  "submittedAt": "2026-04-28T14:05:00Z"
}
```

**Side effect**: workflow is enqueued; the response returns immediately.

---

## 2. Get FOIA Request status

`GET /api/foiarequests/{id}`

**Response `200 OK`**:

```json
{
  "id": "8a4e5b1c-...",
  "subject": "Communications about Project Alpha",
  "requestorFullName": "Jane Doe",
  "requestorEmail": "jane@example.com",
  "requestedStartDate": "2024-01-01",
  "requestedEndDate": "2024-12-31",
  "status": "PendingHumanReview",
  "submittedAt": "2026-04-28T14:05:00Z",
  "counts": {
    "documentsFound": 7,
    "documentsPendingReview": 5,
    "documentsApproved": 2,
    "documentsRejected": 0
  },
  "release": {
    "status": "NotReady",
    "sasUrl": null,
    "sasExpiresAt": null
  },
  "auditEvents": [
    { "timestamp": "2026-04-28T14:05:00Z", "eventType": "RequestSubmitted", "message": "Request created.", "relatedDocumentId": null },
    { "timestamp": "2026-04-28T14:05:01Z", "eventType": "RequestValidated", "message": "All required fields present.", "relatedDocumentId": null }
  ]
}
```

`release.status` values: `NotReady`, `Ready`.

---

## 3. Get documents for review

`GET /api/foiarequests/{id}/documents`

**Response `200 OK`**:

```json
{
  "requestId": "8a4e5b1c-...",
  "documents": [
    {
      "id": "doc-guid-1",
      "fileName": "memo-2024-03.txt",
      "fileType": "txt",
      "redactionStatus": "Completed",
      "reviewStatus": "Pending",
      "redactionCount": 4
    }
  ]
}
```

---

## Document PDF preview

`GET /api/documents/{id}/pdf?version=redacted`

Requires the existing `FoiaAnalyst` authorization policy. `version` accepts
`original` or `redacted` (default). Returns `200 OK` with `application/pdf`,
rendered by `PdfRenderer` from the selected stored text with automatic pagination
and page numbers. This is a text rendition, not the original file's layout.
The response is marked `Cache-Control: no-store`.

Returns `400` for an invalid version, `404` for an unknown document, or `409`
when redacted content is not yet available. It never falls back to original
content for a redacted preview.

The document review page fetches the PDF as an authenticated blob for its
original/redacted iframe preview, releases object URLs when replaced or unmounted,
and reloads both review data and preview after adding, editing, or removing a
redaction. The request / release overview link opens `/requests/{requestId}`.
Release package composition is unchanged.

## 4. Get document before/after review

`GET /api/documents/{id}/review`

**Response `200 OK`**:

```json
{
  "id": "doc-guid-1",
  "foiaRequestId": "8a4e5b1c-...",
  "fileName": "memo-2024-03.txt",
  "originalContent": "From: Jane Doe <jane@example.com> ...",
  "redactedContent": "From: [REDACTED NAME] <[REDACTED EMAIL]> ...",
  "redactions": [
    {
      "id": "red-guid-1",
      "piiType": "Name",
      "originalText": "Jane Doe",
      "replacementText": "[REDACTED NAME]",
      "startOffset": 6,
      "endOffset": 14,
      "pageNumber": null,
      "confidence": 0.91,
      "detectionSource": "ai",
      "reviewerApproved": null
    }
  ],
  "reviewStatus": "Pending"
}
```

---

## 5. Approve document redactions

`POST /api/documents/{id}/approve`

**Request body** (optional):

```json
{ "comments": "Looks good." }
```

**Response `200 OK`**:

```json
{ "id": "doc-guid-1", "reviewStatus": "Approved", "approvedAt": "2026-04-28T14:10:00Z" }
```

**Errors**: `404` if document unknown; `409` if document was previously rejected and re-approval
isn't supported (PoC: idempotent — re-approving an already-Approved document is a no-op `200`).

**Side effect**: if this approval makes every document for the request Approved, the request becomes
eligible for `POST /api/foiarequests/{id}/approve-release` (status remains `PendingHumanReview`
until the explicit release-approval call).

---

## 6. Reject document redactions

`POST /api/documents/{id}/reject`

**Request body**:

```json
{ "comments": "Missed an SSN on line 4." }
```

`comments` is required (non-empty trimmed string). `400` with `errors.comments` on violation.

**Response `200 OK`**:

```json
{ "id": "doc-guid-1", "reviewStatus": "ManualHandling", "rejectedAt": "2026-04-28T14:11:00Z" }
```

`reviewStatus` becomes `ManualHandling` (per spec FR-021: "flagged for rework / manual handling and
excluded from the release package").

---

## 7. Approve release

`POST /api/foiarequests/{id}/approve-release`

**Response `200 OK`**:

```json
{ "id": "8a4e5b1c-...", "status": "ApprovedForRelease", "approvedAt": "2026-04-28T14:15:00Z" }
```

**Errors**: `409` if any document is not `Approved`; `404` if request unknown.

**Side effect**: workflow resumes — Packaging & Release agent runs.

---

## 8. Get release package

`GET /api/foiarequests/{id}/release`

**Response `200 OK` (ready)**:

```json
{
  "requestId": "8a4e5b1c-...",
  "status": "Ready",
  "zipBlobName": "foia-request-8a4e5b1c-1234-4abc-9def-0123456789ab-release-package.zip",
  "blobContainerName": "foia-releases",
  "sasUrl": "https://demoacct.blob.core.windows.net/foia-releases/foia-request-...-release-package.zip?sv=...",
  "sasExpiresAt": "2026-05-05T14:16:00Z",
  "createdAt": "2026-04-28T14:16:00Z"
}
```

**Response `200 OK` (not yet ready)**:

```json
{ "requestId": "8a4e5b1c-...", "status": "NotReady" }
```

---

## Notes

- All endpoints are unauthenticated for the PoC (constitution Principle III).
- The SPA polls `GET /api/foiarequests/{id}` every 2 s while a request is in a non-terminal state.
- No batch endpoints, no pagination — demo scale is small.
