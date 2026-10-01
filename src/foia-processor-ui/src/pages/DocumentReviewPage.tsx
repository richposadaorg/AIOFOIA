import { useEffect, useRef, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
    approveDocument,
    addRedaction,
    deleteDocument,
    getDocumentReview,
    removeRedaction,
    rejectDocument,
    updateRedaction,
} from "../api/client";
import type { DocumentReview } from "../types";
import StatusBadge from "../components/StatusBadge";

function highlight(text: string, ranges: { start: number; end: number; piiType: string }[]) {
    if (!ranges.length) return [text];
    const sorted = [...ranges].sort((a, b) => a.start - b.start);
    const out: (string | JSX.Element)[] = [];
    let cursor = 0;
    sorted.forEach((r, i) => {
        if (r.start > cursor) out.push(text.slice(cursor, r.start));
        out.push(
            <mark
                key={i}
                title={r.piiType}
                className="rounded bg-amber-400/30 px-0.5 text-amber-100"
            >
                {text.slice(r.start, r.end)}
            </mark>
        );
        cursor = r.end;
    });
    if (cursor < text.length) out.push(text.slice(cursor));
    return out;
}

export default function DocumentReviewPage() {
    const { id, documentId } = useParams<{ id: string; documentId: string }>();
    const [doc, setDoc] = useState<DocumentReview | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [comments, setComments] = useState("");
    const [busy, setBusy] = useState(false);
    const [newRedaction, setNewRedaction] = useState({ piiType: "Name", originalText: "", replacementText: "[REDACTED]" });
    const navigate = useNavigate();

    // Synchronized scrolling for the original/redacted panes.
    const originalPaneRef = useRef<HTMLDivElement | null>(null);
    const redactedPaneRef = useRef<HTMLDivElement | null>(null);
    const syncingRef = useRef(false);

    function syncScroll(
        source: HTMLDivElement | null,
        target: HTMLDivElement | null,
    ) {
        if (!source || !target) return;
        if (syncingRef.current) return;
        syncingRef.current = true;
        const sMax = source.scrollHeight - source.clientHeight;
        const tMax = target.scrollHeight - target.clientHeight;
        const ratio = sMax > 0 ? source.scrollTop / sMax : 0;
        target.scrollTop = tMax * ratio;
        requestAnimationFrame(() => {
            syncingRef.current = false;
        });
    }

    useEffect(() => {
        if (!documentId) return;
        getDocumentReview(documentId)
            .then(setDoc)
            .catch((e) => setError((e as Error).message));
    }, [documentId]);

    async function onApprove() {
        if (!documentId) return;
        setBusy(true);
        try {
            await approveDocument(documentId, comments || undefined);
            navigate(`/requests/${id}/review`);
        } catch (e) {
            setError((e as Error).message);
        } finally {
            setBusy(false);
        }
    }

    async function onReject() {
        if (!documentId) return;
        if (!comments.trim()) {
            setError("Comments are required when rejecting a document.");
            return;
        }
        setBusy(true);
        try {
            await rejectDocument(documentId, comments);
            navigate(`/requests/${id}/review`);
        } catch (e) {
            setError((e as Error).message);
        } finally {
            setBusy(false);
        }
    }

    async function onDelete() {
        if (!documentId || !doc) return;
        const ok = window.confirm(
            `Remove document "${doc.fileName}"? This cannot be undone.`
        );
        if (!ok) return;
        setBusy(true);
        try {
            await deleteDocument(documentId);
            navigate(`/requests/${id}/review`);
        } catch (e) {
            setError((e as Error).message);
        } finally {
            setBusy(false);
        }

    }

    async function onAddRedaction() {
        if (!documentId || !newRedaction.originalText.trim() || !newRedaction.replacementText.trim()) return;
        setBusy(true);
        try {
            const added = await addRedaction(documentId, newRedaction);
            setDoc((current) => current && { ...current, redactions: [...current.redactions, added] });
            setNewRedaction({ ...newRedaction, originalText: "" });
        } catch (e) {
            setError((e as Error).message);
        } finally {
            setBusy(false);
        }
    }

    async function onEditRedaction(redactionId: string, currentReplacement: string) {
        if (!documentId) return;
        const replacementText = window.prompt("Replacement text", currentReplacement);
        if (!replacementText?.trim()) return;
        setBusy(true);
        try {
            await updateRedaction(documentId, redactionId, replacementText);
            setDoc((current) => current && {
                ...current,
                redactions: current.redactions.map((r) =>
                    r.id === redactionId ? { ...r, replacementText } : r),
            });
        } catch (e) {
            setError((e as Error).message);
        } finally {
            setBusy(false);
        }
    }

    async function onRemoveRedaction(redactionId: string) {
        if (!documentId || !window.confirm("Remove this redaction?")) return;
        setBusy(true);
        try {
            await removeRedaction(documentId, redactionId);
            setDoc((current) => current && {
                ...current,
                redactions: current.redactions.filter((r) => r.id !== redactionId),
            });
        } catch (e) {
            setError((e as Error).message);
        } finally {
            setBusy(false);
        }
    }

    const backLink = (
        <Link
            to={`/requests/${id}/review`}
            className="inline-flex items-center text-sm font-medium text-indigo-300 transition-colors hover:text-indigo-200"
        >
            &larr; Back to review list
        </Link>
    );

    if (!doc && error) {
        return (
            <div className="space-y-4">
                {backLink}
                <div
                    role="alert"
                    className="rounded-lg border border-rose-500/40 bg-rose-500/10 px-4 py-3 text-sm text-rose-200"
                >
                    {error}
                </div>
            </div>
        );
    }

    if (!doc) {
        return (
            <div className="space-y-4">
                {backLink}
                <div className="h-48 animate-pulse rounded-2xl border border-midnight-800 bg-midnight-900/40" />
            </div>
        );
    }

    const originalRanges = doc.redactions
        .filter((r) => r.startOffset !== null && r.endOffset !== null)
        .map((r) => ({ start: r.startOffset!, end: r.endOffset!, piiType: r.piiType }));

    return (
        <div className="space-y-6">
            {backLink}

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
                            Document
                        </p>
                        <h1 className="mt-1 break-all text-2xl font-semibold text-white">
                            {doc.fileName}
                        </h1>
                        <p className="mt-2 text-sm text-midnight-300">
                            {doc.redactions.length} redaction
                            {doc.redactions.length === 1 ? "" : "s"} detected
                        </p>
                    </div>
                    <StatusBadge status={doc.reviewStatus} />
                </div>
            </header>

            {/* Content panes */}
            <section className="grid grid-cols-1 gap-6 lg:grid-cols-2">
                <article className="rounded-2xl border border-rose-700/40 bg-rose-950/40 p-6 shadow-card">
                    <h2 className="text-base font-semibold text-rose-100">
                        Original (highlighted)
                    </h2>
                    <div
                        ref={originalPaneRef}
                        onScroll={() =>
                            syncScroll(originalPaneRef.current, redactedPaneRef.current)
                        }
                        className="mt-4 max-h-[480px] overflow-auto whitespace-pre-wrap rounded-lg border border-rose-700/40 bg-rose-950/60 p-4 font-mono text-xs leading-relaxed text-rose-50"
                    >
                        {highlight(doc.originalContent, originalRanges)}
                    </div>
                </article>
                <article className="rounded-2xl border border-emerald-700/40 bg-emerald-950/40 p-6 shadow-card">
                    <h2 className="text-base font-semibold text-emerald-100">
                        Redacted
                    </h2>
                    <div
                        ref={redactedPaneRef}
                        onScroll={() =>
                            syncScroll(redactedPaneRef.current, originalPaneRef.current)
                        }
                        className="mt-4 max-h-[480px] overflow-auto whitespace-pre-wrap rounded-lg border border-emerald-700/40 bg-emerald-950/60 p-4 font-mono text-xs leading-relaxed text-emerald-50"
                    >
                        {doc.redactedContent ?? (
                            <span className="text-emerald-300/70">
                                (not yet generated)
                            </span>
                        )}
                    </div>
                </article>
            </section>

            {/* Findings */}
            <section className="rounded-2xl border border-midnight-800 bg-midnight-900/60 shadow-card">
                <header className="flex items-center justify-between border-b border-midnight-800 px-6 py-4">
                    <h2 className="text-base font-semibold text-white">
                        Findings
                    </h2>
                    <span className="text-xs text-midnight-400">
                        {doc.redactions.length} item
                        {doc.redactions.length === 1 ? "" : "s"}
                    </span>
                </header>
                {doc.redactions.length === 0 ? (
                    <p className="px-6 py-6 text-sm text-midnight-400">
                        No redactions detected.
                    </p>
                ) : (
                    <div className="overflow-x-auto">
                        <table className="min-w-full divide-y divide-midnight-800 text-sm">
                            <thead className="bg-midnight-950/60 text-left text-xs uppercase tracking-wide text-midnight-400">
                                <tr>
                                    <th className="px-4 py-3 font-medium">Type</th>
                                    <th className="px-4 py-3 font-medium">Original</th>
                                    <th className="px-4 py-3 font-medium">Replacement</th>
                                    <th className="px-4 py-3 font-medium">Source</th>
                                    <th className="px-4 py-3 font-medium">Confidence</th>
                                    <th className="px-4 py-3 font-medium">Actions</th>
                                </tr>
                            </thead>
                            <tbody className="divide-y divide-midnight-800 text-midnight-100">
                                {doc.redactions.map((r) => (
                                    <tr key={r.id}>
                                        <td className="px-4 py-3 font-medium text-white">
                                            {r.piiType}
                                        </td>
                                        <td className="px-4 py-3">
                                            <code className="rounded bg-midnight-950/60 px-1.5 py-0.5 font-mono text-xs text-amber-200">
                                                {r.originalText}
                                            </code>
                                        </td>
                                        <td className="px-4 py-3">
                                            <code className="rounded bg-midnight-950/60 px-1.5 py-0.5 font-mono text-xs text-emerald-200">
                                                {r.replacementText}
                                            </code>
                                        </td>
                                        <td className="px-4 py-3 text-midnight-300">
                                            {r.detectionSource}
                                        </td>
                                        <td className="px-4 py-3 text-midnight-300">
                                            {r.confidence?.toFixed(2) ?? "—"}
                                        </td>
                                        <td className="space-x-2 px-4 py-3">
                                            <button
                                                onClick={() => onEditRedaction(r.id, r.replacementText)}
                                                disabled={busy}
                                                className="text-indigo-300 hover:text-indigo-200"
                                            >
                                                Edit
                                            </button>
                                            <button
                                                onClick={() => onRemoveRedaction(r.id)}
                                                disabled={busy}
                                                className="text-rose-300 hover:text-rose-200"
                                            >
                                                Remove
                                            </button>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                )}
            </section>

            <section className="rounded-2xl border border-midnight-800 bg-midnight-900/60 p-6 shadow-card">
                <h2 className="text-base font-semibold text-white">Add redaction</h2>
                <div className="mt-4 grid gap-3 md:grid-cols-3">
                    <select
                        value={newRedaction.piiType}
                        onChange={(e) => setNewRedaction({ ...newRedaction, piiType: e.target.value })}
                        className="rounded-md border border-midnight-700 bg-midnight-950/60 px-3 py-2 text-sm text-white"
                    >
                        {["Name", "Address", "Email", "Phone", "SSN", "DateOfBirth", "FinancialId"].map((type) =>
                            <option key={type}>{type}</option>)}
                    </select>
                    <input
                        value={newRedaction.originalText}
                        onChange={(e) => setNewRedaction({ ...newRedaction, originalText: e.target.value })}
                        placeholder="Text to redact"
                        className="rounded-md border border-midnight-700 bg-midnight-950/60 px-3 py-2 text-sm text-white"
                    />
                    <input
                        value={newRedaction.replacementText}
                        onChange={(e) => setNewRedaction({ ...newRedaction, replacementText: e.target.value })}
                        placeholder="Replacement"
                        className="rounded-md border border-midnight-700 bg-midnight-950/60 px-3 py-2 text-sm text-white"
                    />
                </div>
                <button
                    onClick={onAddRedaction}
                    disabled={busy || !newRedaction.originalText.trim()}
                    className="mt-4 rounded-md bg-indigo-500 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-400 disabled:opacity-60"
                >
                    Add redaction
                </button>
            </section>

            {/* Decision */}
            <section className="rounded-2xl border border-midnight-800 bg-midnight-900/60 p-6 shadow-card">
                <h2 className="text-base font-semibold text-white">Decision</h2>
                <label className="mt-4 block text-sm text-midnight-200">
                    <span className="block">
                        Comments{" "}
                        <span className="text-xs text-midnight-400">
                            {doc.reviewStatus === "Approved"
                                ? "(optional)"
                                : "(required if rejecting)"}
                        </span>
                    </span>
                    <textarea
                        value={comments}
                        onChange={(e) => setComments(e.target.value)}
                        rows={4}
                        className="mt-2 block w-full rounded-md border border-midnight-700 bg-midnight-950/60 px-3 py-2 text-sm text-white placeholder-midnight-500 focus:border-indigo-400 focus:outline-none focus:ring-1 focus:ring-indigo-400"
                        placeholder="Add any notes or rejection reason…"
                    />
                </label>
                <div className="mt-6 flex flex-wrap items-center justify-end gap-3 border-t border-midnight-800 pt-6">
                    <button
                        onClick={onDelete}
                        disabled={busy}
                        className="mr-auto inline-flex items-center rounded-md border border-rose-500/40 bg-rose-500/10 px-4 py-2 text-sm font-semibold text-rose-200 hover:bg-rose-500/20 disabled:opacity-60"
                    >
                        Remove document
                    </button>
                    <button
                        onClick={onReject}
                        disabled={busy}
                        className="inline-flex items-center rounded-md border border-rose-500/40 bg-rose-500/10 px-4 py-2 text-sm font-semibold text-rose-200 hover:bg-rose-500/20 disabled:opacity-60"
                    >
                        Reject
                    </button>
                    <button
                        onClick={onApprove}
                        disabled={busy}
                        className="inline-flex items-center rounded-md bg-emerald-500 px-4 py-2 text-sm font-semibold text-white hover:bg-emerald-400 disabled:opacity-60"
                    >
                        {busy ? "Working…" : "Approve"}
                    </button>
                </div>
            </section>
        </div>
    );
}
