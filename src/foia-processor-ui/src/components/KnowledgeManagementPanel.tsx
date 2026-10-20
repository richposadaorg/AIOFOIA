import { useEffect, useState, type FormEvent } from "react";
import {
    createKnowledgeProfile,
    createKnowledgeSource,
    deleteKnowledgeSourceDraft,
    listKnowledgeOperations,
    listKnowledgeProfiles,
    listKnowledgeSources,
    listKnowledgeSourceTypes,
} from "../api/client";
import type {
    KnowledgeOperation,
    KnowledgeProfile,
    KnowledgeSource,
    KnowledgeSourceType,
} from "../types";

export default function KnowledgeManagementPanel() {
    const [types, setTypes] = useState<KnowledgeSourceType[]>([]);
    const [sources, setSources] = useState<KnowledgeSource[]>([]);
    const [profiles, setProfiles] = useState<KnowledgeProfile[]>([]);
    const [operations, setOperations] = useState<KnowledgeOperation[]>([]);
    const [name, setName] = useState("");
    const [description, setDescription] = useState("");
    const [kind, setKind] = useState("");
    const [profileName, setProfileName] = useState("");
    const [error, setError] = useState<string | null>(null);
    const [saving, setSaving] = useState(false);
    const selectedType = types.find((sourceType) => sourceType.kind === kind);

    async function load() {
        const [sourceTypes, sourceList, profileList, operationList] = await Promise.all([
            listKnowledgeSourceTypes(),
            listKnowledgeSources(),
            listKnowledgeProfiles(),
            listKnowledgeOperations(),
        ]);
        setTypes(sourceTypes);
        setSources(sourceList);
        setProfiles(profileList);
        setOperations(operationList);
        setKind((current) => current || sourceTypes[0]?.kind || "");
    }

    useEffect(() => {
        load().catch((e) => setError((e as Error).message));
    }, []);

    async function saveSource(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        setSaving(true);
        setError(null);
        try {
            await createKnowledgeSource({ kind, name, description });
            setName("");
            setDescription("");
            await load();
        } catch (e) {
            setError((e as Error).message);
        } finally {
            setSaving(false);
        }
    }

    async function saveProfile(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        setSaving(true);
        setError(null);
        try {
            await createKnowledgeProfile({ name: profileName, description: "" });
            setProfileName("");
            await load();
        } catch (e) {
            setError((e as Error).message);
        } finally {
            setSaving(false);
        }
    }

    async function removeDraft(source: KnowledgeSource) {
        if (!window.confirm(`Remove draft source "${source.name}"?`)) return;
        setError(null);
        try {
            await deleteKnowledgeSourceDraft(source.id);
            await load();
        } catch (e) {
            setError((e as Error).message);
        }
    }

    return (
        <section className="mb-8 space-y-6">
            <div>
                <h2 className="text-xl font-semibold text-white">Document sources</h2>
                <p className="mt-2 rounded-lg border border-amber-500/40 bg-amber-500/10 px-4 py-3 text-sm text-amber-100">
                    Foundry IQ connectors are not verified for this deployment. These records are drafts only;
                    no source is connected, queried, or activated. Do not enter credentials or sensitive content.
                </p>
            </div>

            {error && <div role="alert" className="rounded-lg border border-rose-500/40 bg-rose-500/10 px-4 py-3 text-sm text-rose-200">{error}</div>}

            <form onSubmit={saveSource} className="grid gap-3 rounded-xl border border-midnight-800 bg-midnight-900/60 p-5 md:grid-cols-4">
                <label className="text-sm text-midnight-200">
                    Source type
                    <select value={kind} onChange={(e) => setKind(e.target.value)} required className="mt-1 w-full rounded-md border border-midnight-700 bg-midnight-950 px-3 py-2 text-white">
                        {types.map((sourceType) => <option key={sourceType.kind} value={sourceType.kind}>{sourceType.displayName}{sourceType.preview ? " (preview)" : ""}</option>)}
                    </select>
                </label>
                <label className="text-sm text-midnight-200">
                    Name
                    <input value={name} onChange={(e) => setName(e.target.value)} required maxLength={200} className="mt-1 w-full rounded-md border border-midnight-700 bg-midnight-950 px-3 py-2 text-white" />
                </label>
                <label className="text-sm text-midnight-200 md:col-span-1">
                    Description
                    <input value={description} onChange={(e) => setDescription(e.target.value)} maxLength={1000} className="mt-1 w-full rounded-md border border-midnight-700 bg-midnight-950 px-3 py-2 text-white" />
                </label>
                <button disabled={saving || !kind} className="self-end rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">
                    Save draft
                </button>
                {selectedType && <p className="text-xs text-amber-200 md:col-span-4">
                    {selectedType.preview ? "Preview connector. " : ""}
                    {selectedType.availability}
                </p>}
            </form>

            <div className="overflow-x-auto rounded-xl border border-midnight-800 bg-midnight-900/60">
                <table className="min-w-full divide-y divide-midnight-800 text-left text-sm">
                    <thead className="text-xs uppercase text-midnight-300"><tr><th className="px-4 py-3">Source</th><th className="px-4 py-3">Kind</th><th className="px-4 py-3">State</th><th className="px-4 py-3">Capability</th><th className="px-4 py-3">Action</th></tr></thead>
                    <tbody className="divide-y divide-midnight-800">
                        {sources.map((source) => {
                            const sourceType = types.find((entry) => entry.kind === source.kind);
                            return <tr key={source.id}>
                                <td className="px-4 py-3 text-white">{source.name}<div className="text-xs text-midnight-300">{source.description}</div></td>
                                <td className="px-4 py-3 text-midnight-200">{sourceType?.displayName ?? source.kind}</td>
                                <td className="px-4 py-3 text-midnight-200">{source.state}</td>
                                <td className="px-4 py-3 text-midnight-200">{source.capabilityState}</td>
                                <td className="px-4 py-3">{source.state === "Draft" && <button type="button" onClick={() => removeDraft(source)} className="text-rose-300 hover:text-rose-200">Remove draft</button>}</td>
                            </tr>;
                        })}
                        {sources.length === 0 && <tr><td colSpan={5} className="px-4 py-5 text-midnight-300">No source drafts.</td></tr>}
                    </tbody>
                </table>
            </div>

            <div className="grid gap-6 md:grid-cols-2">
                <div className="rounded-xl border border-midnight-800 bg-midnight-900/60 p-5">
                    <h3 className="font-semibold text-white">Knowledge bases</h3>
                    <form onSubmit={saveProfile} className="mt-3 flex gap-2">
                        <input value={profileName} onChange={(e) => setProfileName(e.target.value)} required maxLength={200} aria-label="Knowledge base draft name" placeholder="Knowledge base draft name" className="min-w-0 flex-1 rounded-md border border-midnight-700 bg-midnight-950 px-3 py-2 text-white" />
                        <button disabled={saving} className="rounded-md border border-midnight-700 px-3 py-2 text-sm text-white disabled:opacity-50">Save draft</button>
                    </form>
                    <ul className="mt-4 space-y-2 text-sm text-midnight-200">
                        {profiles.map((profile) => <li key={profile.id}>{profile.name} <span className="text-midnight-400">({profile.state})</span></li>)}
                        {profiles.length === 0 && <li className="text-midnight-400">No knowledge base drafts.</li>}
                    </ul>
                </div>
                <div className="rounded-xl border border-midnight-800 bg-midnight-900/60 p-5">
                    <h3 className="font-semibold text-white">Recent operations</h3>
                    <ul className="mt-4 space-y-2 text-sm text-midnight-200">
                        {operations.map((operation) => <li key={operation.id}>{operation.operationType} · {operation.status} · {operation.resourceType}</li>)}
                        {operations.length === 0 && <li className="text-midnight-400">No management operations recorded.</li>}
                    </ul>
                </div>
            </div>
        </section>
    );
}
