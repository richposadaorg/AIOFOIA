<p align="center">
  <img src="aiofoia.png" alt="AIOFOIA logo" width="500" />
</p>

# AIOFOIA — AI Orchestrated Freedom of Information Assistant

A multi-agent FOIA (Freedom of Information Act) request processing proof-of-concept built with
**Microsoft Agent Framework**, **MCP-shaped tools**, **.NET 8**, **React + Vite + TypeScript**, and
**Azure** (Container Apps, AI Search, Blob Storage, OpenAI).

A submitted FOIA request flows through five orchestrated agents — Intake → Search → Redaction →
Human Review (paused) → Packaging — and ends with a downloadable, SAS-protected ZIP of redacted
documents.

See the [quickstart](specs/001-foia-agent-poc/quickstart.md) for setup, deploy, and demo flow.
See the [feature spec](specs/001-foia-agent-poc/spec.md), [plan](specs/001-foia-agent-poc/plan.md),
and [tasks](specs/001-foia-agent-poc/tasks.md) for design details.

> ⚠️ This is a **proof of concept**. Production concerns (auth, SLAs, retention, scaling) are
> intentionally out of scope per the [project constitution](.specify/memory/constitution.md).

## Workflow

```mermaid
flowchart TD
    U([Requestor submits<br/>FOIA request via SPA]) --> A1
    A1[**1. Intake Validation Agent**<br/>validates required fields<br/>tools: SetValidated / SetRejected]
    A1 -->|Validated| A2[**2. Search Agent**<br/>queries Azure AI Search,<br/>fetches content, saves matches<br/>tools: SearchDocuments,<br/>GetDocumentContent, SaveDocuments]
    A1 -->|Rejected| X([Status: Rejected])
    A2 -->|Documents found| A3[**3. Redaction Agent**<br/>per document: detect PII<br/>via Azure OpenAI + regex,<br/>apply redactions, persist<br/>tools: DetectPii, RedactAndSave]
    A2 -->|No documents| Y([Status: NoDocumentsFound])
    A3 --> A4[**4. Human Review Coordinator**<br/>opens a review task if needed<br/>tools: GetReviewStatus,<br/>CreateReviewTask, Acknowledge]
    A4 --> H{{Analyst reviews each<br/>document in the SPA<br/>Approve / Reject / Edit}}
    H -->|Analyst selects approved documents| A5[**5. Packaging & Release Agent**<br/>snapshot included/excluded decisions →<br/>build ZIP → upload to Blob → SAS]
    A5 --> R([Status: ReleasePackageReady<br/>Requestor downloads ZIP via SAS URL])
```

### Step-by-step

1. **Intake Validation Agent.** A `ChatClientAgent` inspects the submitted request payload
   and decides whether it is complete and coherent (subject, requestor name/email, valid
   date range). It calls one of two tools — `SetValidated` or `SetRejected` — both of which
   route through `CaseServer` to update the request status in the database.

2. **Search Agent.** Builds a query from the request subject/description, calls
   `SearchDocuments` (Azure AI Search hybrid query, date-filtered), then `GetDocumentContent`
   for each hit. Finally calls `SaveDocuments` to persist matches and advance the request to
   `DocumentsFound` (or `MarkNoDocumentsFound` if the index returned nothing).

3. **Redaction Agent.** For every newly-found document, a per-document `ChatClientAgent`
   calls `DetectPii` (Azure OpenAI structured-output detection augmented by regex for
   SSN/email/phone/credit card), then `RedactAndSave` to apply the redactions and persist
   the redacted text. The request moves to `PendingHumanReview`.

4. **Human Review Coordinator.** Decides whether a review task is needed. If documents are
   pending, it calls `CreateReviewTask`; otherwise `Acknowledge`. The workflow then **pauses**
   while a FOIA analyst opens the SPA, reviews each document, approves or rejects it, and selects
   which redacted and approved documents belong in the release. Documents excluded from release may
   remain in review or manual handling.

5. **Packaging & Release Agent.** Once every selected document is redacted and approved, the workflow resumes.
   Each package version records an immutable included/excluded document snapshot; included PDFs
   also have SHA-256 hashes. The agent builds the ZIP from only selected documents, uploads it to
   Blob Storage, generates a time-limited SAS URL, and saves the version. Analysts can curate the
   selection and create later package variants; prior snapshots and download links remain available.

All side effects — DB writes, Azure AI Search calls, Azure OpenAI calls, Blob Storage I/O —
are reached **only through the MCP-shaped servers** in `FoiaProcessor.McpTools` (CaseServer,
SearchServer, RedactionServer, ReviewServer, BlobStorageServer), exposed to the LLM as
`Microsoft.Extensions.AI.AIFunction` tools on each `ChatClientAgent`.

## Authentication (Microsoft Entra ID)

The SPA and API are protected by a single Entra ID app registration. Sign-in uses MSAL.js (PKCE
auth-code flow) in the browser; the API validates JWT bearer tokens with
`Microsoft.Identity.Web`. There is no role-based authorization — any signed-in user in the
configured tenant can use the app.

### One-time setup

1. **Create the app registration** in the Microsoft Entra admin center:
   - **Supported account types:** Accounts in this organizational directory only (single tenant).
   - **Platform configuration:** add a **Single-page application** redirect URI for each origin
     you sign in from. For local dev that is `http://localhost:5173` (Vite) and
     `http://localhost:5080` (when the API serves the built SPA). For Azure, add the Container
     App URL (e.g. `https://ca-foia-api-<token>.<region>.azurecontainerapps.io`).
2. **Expose an API:** under **Expose an API** click **Add a scope**, accept the proposed
   `api://{clientId}` URI, then add a scope named `access_as_user` (admin and user consent
   enabled). The full scope value the SPA requests is `api://{clientId}/access_as_user`.
3. **Capture IDs:** copy the **Application (client) ID** and **Directory (tenant) ID**.

### Local development

Set `AzureAd:TenantId` and `AzureAd:ClientId` in
`src/FoiaProcessor.Api/appsettings.Development.json`. Restart the API; the SPA will fetch the
config from `GET /api/config` on first load and trigger a redirect sign-in.

### CI / Azure deployment

Add two **GitHub Actions repository variables** (Settings → Secrets and variables → Actions →
Variables):

- `ENTRA_TENANT_ID` — the Directory (tenant) ID
- `ENTRA_API_CLIENT_ID` — the Application (client) ID

These flow through the deploy workflow into Bicep parameters and end up as
`AzureAd__TenantId` / `AzureAd__ClientId` environment variables on the Container App. Leave
them unset to deploy with auth disabled (the API will then return 401 on every request to an
`[Authorize]` controller).
