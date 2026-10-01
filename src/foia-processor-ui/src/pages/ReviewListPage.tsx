import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { getDocuments, setReleaseSelection } from "../api/client";
import type { DocumentsList } from "../types";
import StatusBadge from "../components/StatusBadge";

export default function ReviewListPage() {
    const { id } = useParams<{ id: string }>();
    const [data, setData] = useState<DocumentsList | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [busy, setBusy] = useState<string | null>(null);

    async function toggleRelease(id: string, includeInRelease: boolean) {
        setBusy(id);
        try {
            await setReleaseSelection(id, includeInRelease);
            setData((current) => current && {
                ...current,
                documents: current.documents.map((d) =>
                    d.id === id ? { ...d, includeInRelease } : d),
            });
        } catch (e) {
            setError((e as Error).message);
        } finally {
            setBusy(null);
        }
    }

    useEffect(() => {
        if (!id) return;
        getDocuments(id)
            .then(setData)
            .catch((e) => setError((e as Error).message));
    }, [id]);

    return (
        <div className="space-y-6">
            <Link
                to={`/requests/${id}`}
                className="inline-flex items-center text-sm font-medium text-indigo-300 transition-colors hover:text-indigo-200"
            >
                &larr; Back to status
            </Link>

            <header className="flex flex-wrap items-center justify-between gap-4">
                <div>
                    <p className="text-xs uppercase tracking-wide text-midnight-400">
                        Human review
                    </p>
                    <h1 className="mt-1 text-3xl font-semibold text-white">
                        Review documents
                    </h1>
                </div>
            </header>

            {error && (
                <div
                    role="alert"
                    className="rounded-lg border border-rose-500/40 bg-rose-500/10 px-4 py-3 text-sm text-rose-200"
                >
                    {error}
                </div>
            )}

            {!data && !error && (
                <div className="h-48 animate-pulse rounded-2xl border border-midnight-800 bg-midnight-900/40" />
            )}

            {data && data.documents.length === 0 && (
                <div className="rounded-2xl border border-midnight-800 bg-midnight-900/60 p-10 text-center">
                    <h2 className="text-lg font-medium text-white">
                        No documents yet
                    </h2>
                    <p className="mt-2 text-sm text-midnight-300">
                        Documents will appear here once the search and
                        redaction agents have finished processing.
                    </p>
                </div>
            )}

            {data && data.documents.length > 0 && (
                <div className="overflow-hidden rounded-2xl border border-midnight-800 bg-midnight-900/60 shadow-card">
                    <div className="overflow-x-auto">
                        <table className="min-w-full divide-y divide-midnight-800 text-sm">
                            <thead className="bg-midnight-950/60 text-left text-xs uppercase tracking-wide text-midnight-400">
                                <tr>
                                    <th className="px-4 py-3 font-medium">File</th>
                                    <th className="px-4 py-3 font-medium">Type</th>
                                    <th className="px-4 py-3 font-medium">Redactions</th>
                                    <th className="px-4 py-3 font-medium">Redaction status</th>
                                    <th className="px-4 py-3 font-medium">Review status</th>
                                    <th className="px-4 py-3 font-medium">Include in release</th>
                                    <th className="px-4 py-3" />
                                </tr>
                            </thead>
                            <tbody className="divide-y divide-midnight-800 text-midnight-100">
                                {data.documents.map((d) => (
                                    <tr
                                        key={d.id}
                                        className="transition-colors hover:bg-midnight-800/40"
                                    >
                                        <td className="px-4 py-3 font-medium text-white">
                                            {d.fileName}
                                        </td>
                                        <td className="px-4 py-3 text-midnight-300">
                                            {d.fileType}
                                        </td>
                                        <td className="px-4 py-3">
                                            {d.redactionCount}
                                        </td>
                                        <td className="px-4 py-3">
                                            <StatusBadge status={d.redactionStatus} />
                                        </td>
                                        <td className="px-4 py-3">
                                            <StatusBadge status={d.reviewStatus} />
                                        </td>
                                        <td className="px-4 py-3">
                                            <input
                                                type="checkbox"
                                                checked={d.includeInRelease}
                                                disabled={busy === d.id}
                                                onChange={(e) => toggleRelease(d.id, e.target.checked)}
                                                aria-label={`Include ${d.fileName} in release`}
                                            />
                                        </td>
                                        <td className="px-4 py-3 text-right">
                                            <Link
                                                to={`/requests/${id}/documents/${d.id}`}
                                                className="text-sm font-medium text-indigo-300 transition-colors hover:text-indigo-200"
                                            >
                                                Open &rarr;
                                            </Link>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                </div>
            )}
        </div>
    );
}
