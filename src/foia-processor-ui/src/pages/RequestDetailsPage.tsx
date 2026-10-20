import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { approveRelease, getFoiaRequestStatus, getReleasePackages } from "../api/client";
import type { FoiaRequestStatus, ReleasePackage } from "../types";
import StatusBadge from "../components/StatusBadge";

const TERMINAL_STATUSES = new Set([
    "ReleasePackageReady",
    "Rejected",
    "Error",
    "NoDocumentsFound",
]);

const activeStatuses = new Set<string>([
    "Submitted",
    "Validated",
    "Searching",
    "DocumentsFound",
    "Redacting",
    "Packaging",
    "InProgress",
]);

const dateFmt = new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
});

const dateOnlyFmt = new Intl.DateTimeFormat(undefined, { dateStyle: "medium" });

function formatDate(value: string | null | undefined) {
    if (!value) return "—";
    const d = new Date(value);
    if (isNaN(d.getTime())) return value;
    return dateFmt.format(d);
}

function formatDateOnly(value: string | null | undefined) {
    if (!value) return "—";
    const d = new Date(value);
    if (isNaN(d.getTime())) return value;
    return dateOnlyFmt.format(d);
}

export default function RequestDetailsPage() {
    const { id } = useParams<{ id: string }>();
    const [status, setStatus] = useState<FoiaRequestStatus | null>(null);
    const [releasePackages, setReleasePackages] = useState<ReleasePackage[]>([]);
    const [error, setError] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);
    const [copied, setCopied] = useState(false);

    useEffect(() => {
        if (!id) return;
        let cancelled = false;
        let timer: ReturnType<typeof setTimeout> | null = null;

        async function poll() {
            try {
                const data = await getFoiaRequestStatus(id!);
                if (cancelled) return;
                setStatus(data);
                if (data.release.status === "Ready") {
                    setReleasePackages(await getReleasePackages(id!));
                }
                setError(null);
                if (
                    !TERMINAL_STATUSES.has(data.status) ||
                    activeStatuses.has(data.status)
                ) {
                    timer = setTimeout(poll, 3000);
                }
            } catch (e) {
                if (cancelled) return;
                setError((e as Error).message);
                timer = setTimeout(poll, 5000);
            }
        }

        poll();
        return () => {
            cancelled = true;
            if (timer) clearTimeout(timer);
        };
    }, [id]);

    async function onApproveRelease() {
        if (!id) return;
        setBusy(true);
        try {
            await approveRelease(id);
            const data = await getFoiaRequestStatus(id);
            setStatus(data);
        } catch (e) {
            setError((e as Error).message);
        } finally {
            setBusy(false);
        }
    }

    async function onCopySas() {
        if (!status?.release.sasUrl) return;
        await navigator.clipboard.writeText(status.release.sasUrl);
        setCopied(true);
        window.setTimeout(() => setCopied(false), 1500);
    }

    if (!status && error) {
        return (
            <div>
                <BackLink />
                <div
                    role="alert"
                    className="mt-4 rounded-lg border border-rose-500/40 bg-rose-500/10 px-4 py-3 text-sm text-rose-200"
                >
                    Failed to load request: {error}
                </div>
            </div>
        );
    }

    if (!status) {
        return (
            <div>
                <BackLink />
                <div className="mt-6 h-48 animate-pulse rounded-2xl border border-midnight-800 bg-midnight-900/40" />
            </div>
        );
    }

    const canApproveRelease =
        (status.status === "PendingHumanReview" || status.status === "ReleasePackageReady") &&
        status.counts.documentsIncludedInRelease > 0 &&
        status.counts.documentsIncludedNotReady === 0;

    const sasExpired =
        status.release.status === "Ready" &&
        status.release.sasExpiresAt !== null &&
        new Date(status.release.sasExpiresAt) < new Date();

    return (
        <div className="space-y-6">
            <BackLink />

            {error && (
                <div
                    role="alert"
                    className="rounded-lg border border-rose-500/40 bg-rose-500/10 px-4 py-3 text-sm text-rose-200"
                >
                    {error}
                </div>
            )}

            {/* Header */}
            <header className="rounded-2xl border border-midnight-800 bg-midnight-900/60 p-6 shadow-card">
                <div className="flex flex-wrap items-start justify-between gap-4">
                    <div className="min-w-0">
                        <p className="text-xs uppercase tracking-wide text-midnight-400">
                            Request
                        </p>
                        <h1 className="mt-1 text-2xl font-semibold text-white">
                            {status.subject}
                        </h1>
                        <p className="mt-1 font-mono text-xs text-midnight-400">
                            {status.id}
                        </p>
                    </div>
                    <StatusBadge status={status.status} />
                </div>

                <div className="mt-6 flex flex-wrap gap-3">
                    {status.status === "PendingHumanReview" || status.status === "ReleasePackageReady" ? (
                        <Link
                            to={`/requests/${status.id}/review`}
                            className="inline-flex items-center rounded-md bg-indigo-500 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-400"
                        >
                            {status.status === "ReleasePackageReady" ? "Curate package documents" : "Review Documents"} &rarr;
                        </Link>
                    ) : (
                        status.counts.documentsFound > 0 && (
                            <Link
                                to={`/requests/${status.id}/review`}
                                className="inline-flex items-center rounded-md border border-midnight-700 bg-midnight-900/60 px-4 py-2 text-sm font-semibold text-midnight-100 hover:border-midnight-600 hover:text-white"
                            >
                                View Documents &rarr;
                            </Link>
                        )
                    )}
                    {canApproveRelease && (
                        <button
                            onClick={onApproveRelease}
                            disabled={busy}
                            className="inline-flex items-center rounded-md bg-emerald-500 px-4 py-2 text-sm font-semibold text-white hover:bg-emerald-400 disabled:opacity-60"
                        >
                            {busy ? "Working…" : status.release.status === "Ready" ? "Create Package Variant" : "Approve Release"}
                        </button>
                    )}
                </div>
            </header>

            {/* Counts */}
            <section className="grid grid-cols-2 gap-4 sm:grid-cols-4">
                <CountCard label="Found" value={status.counts.documentsFound} />
                <CountCard
                    label="Pending"
                    value={status.counts.documentsPendingReview}
                    accent="amber"
                />
                <CountCard
                    label="Approved"
                    value={status.counts.documentsApproved}
                    accent="emerald"
                />
                <CountCard
                    label="Rejected"
                    value={status.counts.documentsRejected}
                    accent="rose"
                />
            </section>

            <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
                {/* Request fields */}
                <section className="rounded-2xl border border-midnight-800 bg-midnight-900/60 p-6 shadow-card">
                    <h2 className="text-base font-semibold text-white">
                        Request information
                    </h2>
                    <dl className="mt-4 divide-y divide-midnight-800 text-sm">
                        <Field label="Subject" value={status.subject} />
                        <Field
                            label="Submitted"
                            value={formatDate(status.submittedAt)}
                        />
                        <Field
                            label="Date range"
                            value={`${formatDateOnly(
                                status.requestedStartDate
                            )} — ${formatDateOnly(status.requestedEndDate)}`}
                        />
                        <div className="py-2.5">
                            <dt className="text-midnight-400">Description</dt>
                            <dd className="mt-2 whitespace-pre-wrap text-left text-midnight-100">
                                {status.description ? (
                                    status.description
                                ) : (
                                    <span className="text-midnight-400">—</span>
                                )}
                            </dd>
                        </div>
                    </dl>
                </section>

                {/* Requestor */}
                <section className="rounded-2xl border border-midnight-800 bg-midnight-900/60 p-6 shadow-card">
                    <h2 className="text-base font-semibold text-white">
                        Requestor
                    </h2>
                    <dl className="mt-4 divide-y divide-midnight-800 text-sm">
                        <Field
                            label="Full name"
                            value={status.requestorFullName}
                        />
                        <Field
                            label="Email"
                            value={
                                <a
                                    href={`mailto:${status.requestorEmail}`}
                                    className="text-indigo-300 hover:text-indigo-200"
                                >
                                    {status.requestorEmail}
                                </a>
                            }
                        />
                        <Field
                            label="Organization"
                            value={
                                status.requestorOrganization ? (
                                    status.requestorOrganization
                                ) : (
                                    <span className="text-midnight-400">—</span>
                                )
                            }
                        />
                        <Field
                            label="Phone"
                            value={
                                status.requestorPhone ? (
                                    <a
                                        href={`tel:${status.requestorPhone}`}
                                        className="text-indigo-300 hover:text-indigo-200"
                                    >
                                        {status.requestorPhone}
                                    </a>
                                ) : (
                                    <span className="text-midnight-400">—</span>
                                )
                            }
                        />
                        <Field
                            label="Mailing address"
                            value={
                                status.requestorMailingAddress ? (
                                    <span className="whitespace-pre-wrap text-left">
                                        {status.requestorMailingAddress}
                                    </span>
                                ) : (
                                    <span className="text-midnight-400">—</span>
                                )
                            }
                        />
                    </dl>
                </section>
            </div>

            {/* Release */}
            {status.release.status === "Ready" && status.release.sasUrl && (
                <section className="rounded-2xl border border-emerald-500/30 bg-emerald-500/5 p-6 shadow-card">
                    <h2 className="text-base font-semibold text-white">
                        Release package {status.release.packageVersion ? `v${status.release.packageVersion}` : ""}
                    </h2>
                    {!sasExpired ? (
                        <div className="mt-4 space-y-3 text-sm">
                            <div className="flex flex-wrap items-center gap-3">
                                <a
                                    href={status.release.sasUrl}
                                    target="_blank"
                                    rel="noreferrer"
                                    className="inline-flex items-center rounded-md bg-emerald-500 px-4 py-2 text-sm font-semibold text-white hover:bg-emerald-400"
                                >
                                    Download release package
                                </a>
                                <button
                                    onClick={onCopySas}
                                    className="inline-flex items-center rounded-md border border-midnight-700 bg-midnight-900/60 px-3 py-2 text-sm font-medium text-midnight-100 hover:border-midnight-600"
                                >
                                    Copy URL
                                </button>
                                {copied && (
                                    <span className="text-xs text-emerald-300">
                                        Copied!
                                    </span>
                                )}
                            </div>
                            <p className="text-xs text-midnight-400">
                                SAS expires:{" "}
                                {formatDate(status.release.sasExpiresAt)}
                            </p>
                        </div>
                    ) : (
                        <p className="mt-4 text-sm text-rose-300">
                            The download link expired on{" "}
                            {formatDate(status.release.sasExpiresAt)}. Please
                            contact the administrator to regenerate the release
                            package.
                        </p>
                    )}
                </section>
            )}

            {releasePackages.length > 0 && (
                <section className="rounded-2xl border border-midnight-800 bg-midnight-900/60 p-6 shadow-card">
                    <h2 className="text-base font-semibold text-white">Package history</h2>
                    <ol className="mt-4 space-y-4">
                        {releasePackages.map((pkg) => (
                            <li key={pkg.version} className="rounded-xl border border-midnight-800 bg-midnight-950/40 p-4">
                                <div className="flex flex-wrap items-center justify-between gap-3">
                                    <div>
                                        <p className="font-medium text-white">Version {pkg.version}</p>
                                        <p className="mt-1 text-xs text-midnight-400">
                                            Created {formatDate(pkg.createdAt)} · {pkg.documents.filter((d) => d.included).length} included · {pkg.documents.filter((d) => !d.included).length} excluded
                                        </p>
                                    </div>
                                    {pkg.sasUrl && (
                                        <a
                                            href={pkg.sasUrl}
                                            target="_blank"
                                            rel="noreferrer"
                                            className="text-sm font-medium text-indigo-300 hover:text-indigo-200"
                                        >
                                            Download v{pkg.version}
                                        </a>
                                    )}
                                </div>
                                <details className="mt-3 text-sm">
                                    <summary className="cursor-pointer text-midnight-300">View composition snapshot</summary>
                                    <ul className="mt-2 space-y-1">
                                        {pkg.documents.map((document) => (
                                            <li key={document.documentId} className="flex flex-wrap justify-between gap-2 text-midnight-300">
                                                <span>
                                                    {document.included ? "Included" : "Excluded"}: {document.fileName}
                                                    {document.packageFileName && document.packageFileName !== document.fileName && ` → ${document.packageFileName}`}
                                                </span>
                                                {document.contentSha256 && (
                                                    <code className="break-all text-xs text-midnight-400" title="SHA-256 of packaged PDF">
                                                        SHA-256 {document.contentSha256}
                                                    </code>
                                                )}
                                            </li>
                                        ))}
                                    </ul>
                                </details>
                            </li>
                        ))}
                    </ol>
                </section>
            )}

            {/* Audit timeline */}
            <section className="rounded-2xl border border-midnight-800 bg-midnight-900/60 p-6 shadow-card">
                <div className="flex items-center justify-between">
                    <h2 className="text-base font-semibold text-white">
                        Audit log
                    </h2>
                    <span className="text-xs text-midnight-400">
                        {status.auditEvents.length} event
                        {status.auditEvents.length === 1 ? "" : "s"}
                    </span>
                </div>
                {status.auditEvents.length === 0 ? (
                    <p className="mt-4 text-sm text-midnight-400">
                        No audit events recorded yet.
                    </p>
                ) : (
                    <ol className="mt-4 space-y-4">
                        {status.auditEvents.map((e, i) => (
                            <li
                                key={i}
                                className="relative rounded-xl border border-midnight-800 bg-midnight-950/40 p-4"
                            >
                                <div className="flex flex-wrap items-baseline justify-between gap-2">
                                    <span className="text-sm font-medium text-white">
                                        {e.eventType}
                                    </span>
                                    <span className="text-xs text-midnight-400">
                                        {formatDate(e.timestamp)}
                                    </span>
                                </div>
                                <p className="mt-1 text-sm text-midnight-200">
                                    {e.message}
                                </p>
                                {e.relatedDocumentFileName && (
                                    <p className="mt-1 text-xs text-midnight-400">
                                        Document: {e.relatedDocumentFileName}
                                    </p>
                                )}
                            </li>
                        ))}
                    </ol>
                )}
            </section>
        </div>
    );
}

function BackLink() {
    return (
        <Link
            to="/"
            className="inline-flex items-center text-sm font-medium text-indigo-300 transition-colors hover:text-indigo-200"
        >
            &larr; Back to dashboard
        </Link>
    );
}

function Field({
    label,
    value,
}: {
    label: string;
    value: React.ReactNode;
}) {
    return (
        <div className="flex justify-between gap-4 py-2.5">
            <dt className="text-midnight-400">{label}</dt>
            <dd className="text-right text-midnight-100">{value}</dd>
        </div>
    );
}

type Accent = "default" | "amber" | "emerald" | "rose";

function CountCard({
    label,
    value,
    accent = "default",
}: {
    label: string;
    value: number;
    accent?: Accent;
}) {
    const accentCls: Record<Accent, string> = {
        default: "text-white",
        amber: "text-amber-300",
        emerald: "text-emerald-300",
        rose: "text-rose-300",
    };
    return (
        <div className="rounded-2xl border border-midnight-800 bg-midnight-900/60 p-4 shadow-card">
            <p className="text-xs uppercase tracking-wide text-midnight-400">
                {label}
            </p>
            <p
                className={`mt-2 text-2xl font-semibold tabular-nums ${accentCls[accent]}`}
            >
                {value}
            </p>
        </div>
    );
}
