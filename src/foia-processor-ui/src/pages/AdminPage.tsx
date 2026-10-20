import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { deleteFoiaRequest, getCurrentUser, listFoiaRequestsPaged } from "../api/client";
import StatusBadge from "../components/StatusBadge";
import KnowledgeManagementPanel from "../components/KnowledgeManagementPanel";
import type { FoiaRequestSummary } from "../types";

const PAGE_SIZE = 10;

const dateFmt = new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
});

export default function AdminPage() {
    const [items, setItems] = useState<FoiaRequestSummary[] | null>(null);
    const [total, setTotal] = useState(0);
    const [page, setPage] = useState(0); // 0-based
    const [error, setError] = useState<string | null>(null);
    const [deletingId, setDeletingId] = useState<string | null>(null);
    const [loading, setLoading] = useState(false);
    const [isAdmin, setIsAdmin] = useState(false);
    const [authorizationChecked, setAuthorizationChecked] = useState(false);

    function load(targetPage: number) {
        setError(null);
        setLoading(true);
        listFoiaRequestsPaged(targetPage * PAGE_SIZE, PAGE_SIZE)
            .then((res) => {
                setItems(res.items);
                setTotal(res.total);
                setPage(targetPage);
            })
            .catch((e) => setError((e as Error).message))
            .finally(() => setLoading(false));
    }

    useEffect(() => {
        getCurrentUser()
            .then((user) => {
                setIsAdmin(user.capabilities.manageKnowledgeSources);
                if (user.capabilities.manageKnowledgeSources) load(0);
            })
            .catch((e) => setError((e as Error).message))
            .finally(() => setAuthorizationChecked(true));
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
    const canPrev = page > 0;
    const canNext = page < totalPages - 1;
    const startIdx = total === 0 ? 0 : page * PAGE_SIZE + 1;
    const endIdx = Math.min(total, page * PAGE_SIZE + (items?.length ?? 0));

    async function handleDelete(req: FoiaRequestSummary) {
        const ok = window.confirm(
            `Delete FOIA request "${req.subject}" (${req.id})?\n\n` +
            "This permanently removes the request and all associated documents, " +
            "redactions, review tasks, release package, and audit events. " +
            "This cannot be undone."
        );
        if (!ok) return;

        setDeletingId(req.id);
        setError(null);
        try {
            await deleteFoiaRequest(req.id);
            // Reload current page; if it just emptied, step back one.
            const remainingOnPage = (items?.length ?? 1) - 1;
            const nextPage = remainingOnPage === 0 && page > 0 ? page - 1 : page;
            load(nextPage);
        } catch (e) {
            setError(`Failed to delete ${req.id}: ${(e as Error).message}`);
        } finally {
            setDeletingId(null);
        }
    }

    return (
        <div>
            {authorizationChecked && !isAdmin && (
                <div role="alert" className="rounded-lg border border-rose-500/40 bg-rose-500/10 px-4 py-3 text-sm text-rose-200">
                    Admin access is required to manage document sources and requests.
                </div>
            )}
            {!authorizationChecked && (
                <div aria-hidden="true" className="mb-6 h-12 animate-pulse rounded-lg bg-midnight-900/60" />
            )}
            {error && (
                <div
                    role="alert"
                    className="mb-6 rounded-lg border border-rose-500/40 bg-rose-500/10 px-4 py-3 text-sm text-rose-200"
                >
                    {error}
                </div>
            )}

            {isAdmin && <KnowledgeManagementPanel />}

            {isAdmin && !items && !error && (
                <div
                    aria-hidden="true"
                    className="h-64 animate-pulse rounded-2xl border border-midnight-800 bg-midnight-900/40"
                />
            )}

            {isAdmin && items && items.length === 0 && (
                <div className="rounded-2xl border border-midnight-800 bg-midnight-900/60 p-10 text-center">
                    <h2 className="text-lg font-medium text-white">No requests</h2>
                    <p className="mt-2 text-sm text-midnight-300">
                        There are no FOIA requests in the system.
                    </p>
                </div>
            )}

            {isAdmin && items && items.length > 0 && (
                <div className="overflow-hidden rounded-2xl border border-midnight-800 bg-midnight-900/60 shadow-card">
                    <div className="overflow-x-auto">
                        <table className="min-w-full divide-y divide-midnight-800 text-sm">
                            <thead className="bg-midnight-900 text-left text-xs uppercase tracking-wider text-midnight-300">
                                <tr>
                                    <th scope="col" className="px-6 py-3 font-semibold">
                                        Subject
                                    </th>
                                    <th scope="col" className="px-6 py-3 font-semibold">
                                        Requestor
                                    </th>
                                    <th scope="col" className="px-6 py-3 font-semibold">
                                        Request ID
                                    </th>
                                    <th scope="col" className="px-6 py-3 font-semibold">
                                        Status
                                    </th>
                                    <th scope="col" className="px-6 py-3 font-semibold">
                                        Submitted
                                    </th>
                                    <th scope="col" className="px-6 py-3 font-semibold text-right">
                                        Actions
                                    </th>
                                </tr>
                            </thead>
                            <tbody className="divide-y divide-midnight-800">
                                {items.map((r, i) => (
                                    <tr
                                        key={r.id}
                                        className={
                                            i % 2 === 0
                                                ? "bg-midnight-900/40"
                                                : "bg-midnight-900/10"
                                        }
                                    >
                                        <td className="px-6 py-4 font-medium text-white">
                                            <Link
                                                to={`/requests/${r.id}/status`}
                                                className="hover:text-indigo-300"
                                            >
                                                {r.subject}
                                            </Link>
                                        </td>
                                        <td className="px-6 py-4 text-midnight-100">
                                            {r.requestorFullName}
                                        </td>
                                        <td className="px-6 py-4 font-mono text-xs text-midnight-300">
                                            {r.id}
                                        </td>
                                        <td className="px-6 py-4">
                                            <StatusBadge status={r.status} />
                                        </td>
                                        <td className="px-6 py-4 text-midnight-100">
                                            {dateFmt.format(new Date(r.submittedAt))}
                                        </td>
                                        <td className="px-6 py-4 text-right">
                                            <button
                                                type="button"
                                                onClick={() => handleDelete(r)}
                                                disabled={deletingId === r.id}
                                                className="inline-flex items-center rounded-md bg-rose-600/90 px-3 py-1.5 text-xs font-semibold text-white shadow-sm transition-colors hover:bg-rose-600 disabled:cursor-not-allowed disabled:opacity-60"
                                                aria-label={`Delete ${r.subject}`}
                                            >
                                                {deletingId === r.id ? "Deleting…" : "Delete"}
                                            </button>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>

                    <nav
                        aria-label="Pagination"
                        className="flex items-center justify-between gap-4 border-t border-midnight-800 bg-midnight-900/40 px-6 py-3 text-sm text-midnight-300"
                    >
                        <div>
                            Showing <span className="font-medium text-white">{startIdx}</span>
                            &ndash;<span className="font-medium text-white">{endIdx}</span> of{" "}
                            <span className="font-medium text-white">{total}</span>
                        </div>
                        <div className="flex items-center gap-2">
                            <span className="hidden sm:inline">
                                Page <span className="font-medium text-white">{page + 1}</span> of{" "}
                                <span className="font-medium text-white">{totalPages}</span>
                            </span>
                            <button
                                type="button"
                                onClick={() => canPrev && load(page - 1)}
                                disabled={!canPrev || loading}
                                className="rounded-md border border-midnight-700 bg-midnight-900/60 px-3 py-1.5 text-sm font-medium text-midnight-100 transition-colors hover:bg-midnight-800 disabled:cursor-not-allowed disabled:opacity-50"
                            >
                                &larr; Previous
                            </button>
                            <button
                                type="button"
                                onClick={() => canNext && load(page + 1)}
                                disabled={!canNext || loading}
                                className="rounded-md border border-midnight-700 bg-midnight-900/60 px-3 py-1.5 text-sm font-medium text-midnight-100 transition-colors hover:bg-midnight-800 disabled:cursor-not-allowed disabled:opacity-50"
                            >
                                Next &rarr;
                            </button>
                        </div>
                    </nav>
                </div>
            )}
        </div>
    );
}
