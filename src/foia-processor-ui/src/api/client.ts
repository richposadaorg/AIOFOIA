import type {
    DocumentReview,
    DocumentsList,
    FoiaRequestStatus,
    FoiaRequestSummary,
    PagedFoiaRequests,
    ReleasePackage,
    SubmitFoiaRequest,
    SubmitFoiaResponse,
    SystemHealthReport,
    ValidationProblem,
} from "../types";
import { getAccessToken } from "../auth/msalConfig";

const BASE = (import.meta.env.VITE_API_BASE_URL ?? "").replace(/\/$/, "");

export class ApiValidationError extends Error {
    constructor(public problem: ValidationProblem) {
        super(problem.title);
    }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
    const headers = new Headers(init?.headers ?? {});
    if (!headers.has("Content-Type")) {
        headers.set("Content-Type", "application/json");
    }
    // Anonymous endpoints (config) must not get a bearer header — they may be
    // hit before MSAL is even initialized.
    if (path !== "/api/config") {
        const token = await getAccessToken();
        if (token) {
            headers.set("Authorization", `Bearer ${token}`);
        }
    }
    const res = await fetch(`${BASE}${path}`, {
        ...init,
        headers,
    });
    if (res.status === 400) {
        const problem = (await res.json()) as ValidationProblem;
        throw new ApiValidationError(problem);
    }
    if (!res.ok) {
        const text = await res.text();
        throw new Error(`HTTP ${res.status}: ${text}`);
    }
    if (res.status === 204) return undefined as T;
    return (await res.json()) as T;
}

export const submitFoiaRequest = (dto: SubmitFoiaRequest) =>
    request<SubmitFoiaResponse>("/api/foiarequests", { method: "POST", body: JSON.stringify(dto) });

export const listFoiaRequests = (take = 10) =>
    request<FoiaRequestSummary[]>(`/api/foiarequests?take=${take}`);

export const listAllFoiaRequests = () =>
    request<FoiaRequestSummary[]>(`/api/foiarequests?take=100`);

export const listFoiaRequestsPaged = (skip: number, take: number) =>
    request<PagedFoiaRequests>(
        `/api/foiarequests/paged?skip=${skip}&take=${take}`
    );

export const deleteFoiaRequest = (id: string) =>
    request<void>(`/api/foiarequests/${id}`, { method: "DELETE" });

export const listPendingReviewRequests = () =>
    request<FoiaRequestSummary[]>(`/api/foiarequests/pending-review`);

export const getFoiaRequestStatus = (id: string) =>
    request<FoiaRequestStatus>(`/api/foiarequests/${id}`);

export const getDocuments = (id: string) =>
    request<DocumentsList>(`/api/foiarequests/${id}/documents`);

export const getDocumentReview = (id: string) =>
    request<DocumentReview>(`/api/documents/${id}/review`);

export const approveDocument = (id: string, comments?: string) =>
    request<{ id: string; reviewStatus: string; approvedAt: string }>(`/api/documents/${id}/approve`, {
        method: "POST",
        body: JSON.stringify({ comments: comments ?? null }),
    });

export const rejectDocument = (id: string, comments: string) =>
    request<{ id: string; reviewStatus: string; rejectedAt: string }>(`/api/documents/${id}/reject`, {
        method: "POST",
        body: JSON.stringify({ comments }),
    });

export const deleteDocument = (id: string) =>
    request<void>(`/api/documents/${id}`, { method: "DELETE" });

export const addRedaction = (id: string, redaction: {
    piiType: string;
    originalText: string;
    replacementText: string;
}) =>
    request<DocumentReview["redactions"][number]>(`/api/documents/${id}/redactions`, {
        method: "POST",
        body: JSON.stringify(redaction),
    });

export const updateRedaction = (documentId: string, redactionId: string, replacementText: string) =>
    request<void>(`/api/documents/${documentId}/redactions/${redactionId}`, {
        method: "PATCH",
        body: JSON.stringify({ replacementText, reviewerComments: null }),
    });

export const removeRedaction = (documentId: string, redactionId: string) =>
    request<void>(`/api/documents/${documentId}/redactions/${redactionId}`, { method: "DELETE" });

export const setReleaseSelection = (id: string, includeInRelease: boolean) =>
    request<{ id: string; includeInRelease: boolean }>(`/api/documents/${id}/release-selection`, {
        method: "PUT",
        body: JSON.stringify({ includeInRelease }),
    });

export const approveRelease = (id: string) =>
    request<{ id: string; status: string; approvedAt: string }>(
        `/api/foiarequests/${id}/approve-release`,
        { method: "POST" }
    );

export const getRelease = (id: string) =>
    request<ReleasePackage>(`/api/foiarequests/${id}/release`);

export const getSystemHealth = () =>
    request<SystemHealthReport>(`/api/health/detailed`);
