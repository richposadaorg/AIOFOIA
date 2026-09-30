// Mirrors REST DTOs from contracts/rest-api.md

export type HealthStatus = "Healthy" | "Degraded" | "Unhealthy";

export type ComponentHealth = {
    name: string;
    category: string;
    status: HealthStatus;
    description?: string | null;
    durationMs: number;
    data?: Record<string, unknown> | null;
    error?: string | null;
};

export type SystemHealthReport = {
    status: HealthStatus;
    timestamp: string;
    components: ComponentHealth[];
};

export type Counts = {
    documentsFound: number;
    documentsPendingReview: number;
    documentsApproved: number;
    documentsRejected: number;
};

export type Release = {
    status: "NotReady" | "Ready";
    sasUrl: string | null;
    sasExpiresAt: string | null;
};

export type AuditEvent = {
    timestamp: string;
    eventType: string;
    message: string;
    relatedDocumentId: string | null;
    relatedDocumentFileName: string | null;
};

export type FoiaRequestStatus = {
    id: string;
    subject: string;
    description?: string | null;
    requestorFullName: string;
    requestorEmail: string;
    requestorOrganization?: string | null;
    requestorPhone?: string | null;
    requestorMailingAddress?: string | null;
    requestedStartDate: string;
    requestedEndDate: string;
    status: string;
    submittedAt: string;
    counts: Counts;
    release: Release;
    auditEvents: AuditEvent[];
};

export type SubmitFoiaRequest = {
    subject: string;
    description?: string;
    requestedStartDate: string;
    requestedEndDate: string;
    requestorFullName: string;
    requestorOrganization?: string;
    requestorEmail: string;
    requestorPhone?: string;
    requestorMailingAddress?: string;
};

export type SubmitFoiaResponse = {
    id: string;
    status: string;
    submittedAt: string;
};

export type FoiaRequestSummary = {
    id: string;
    subject: string;
    requestorFullName: string;
    status: string;
    submittedAt: string;
};

export type PagedFoiaRequests = {
    total: number;
    skip: number;
    take: number;
    items: FoiaRequestSummary[];
};

export type ValidationProblem = {
    title: string;
    status: number;
    errors: Record<string, string[]>;
};

export type DocumentSummary = {
    id: string;
    fileName: string;
    fileType: string;
    redactionStatus: string;
    reviewStatus: string;
    includeInRelease: boolean;
    redactionCount: number;
};

export type DocumentsList = { requestId: string; documents: DocumentSummary[] };

export type DocumentRedaction = {
    id: string;
    piiType: string;
    originalText: string;
    replacementText: string;
    startOffset: number | null;
    endOffset: number | null;
    pageNumber: number | null;
    confidence: number | null;
    detectionSource: string;
    reviewerApproved: boolean | null;
    reviewerComments: string | null;
};

export type DocumentReview = {
    id: string;
    foiaRequestId: string;
    fileName: string;
    originalContent: string;
    redactedContent: string | null;
    redactions: DocumentRedaction[];
    reviewStatus: string;
};

export type ReleasePackage = {
    requestId: string;
    status: "NotReady" | "Ready";
    zipBlobName?: string;
    blobContainerName?: string;
    sasUrl?: string;
    sasExpiresAt?: string;
    createdAt?: string;
};
