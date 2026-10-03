import { draftStorageKey, readFieldDraft, type FieldDraft, type FieldJobType } from './locksmith-drafts.ts';

export type OfflineIdentity = { tenantId: string; userId: string; capabilities: FieldJobType[] };
export type OfflinePhoto = { id: string; openingId: string; name: string; blob: Blob };
export type SavedFieldDraft = {
	scope: string;
	version: 1;
	revision: string;
	savedAt: string;
	catalogVersion: string;
	state: 'pending-office-sync';
	draft: FieldDraft;
	photos: OfflinePhoto[];
};
const databaseName = 'turnkey-locksmith-field-v1';
const storeName = 'drafts';
export const maximumPhotoBytes = 10 * 1024 * 1024;
export const maximumDraftPhotoBytes = 100 * 1024 * 1024;
export const acceptedPhotoTypes = ['image/jpeg', 'image/png', 'image/webp'];

export class DraftConflictError extends Error {
	constructor() { super('This device draft changed in another tab. Load the latest saved draft before saving again.'); this.name = 'DraftConflictError'; }
}
export class InvalidDeviceDraftError extends Error {
	revision: string | null;
	constructor(revision: string | null) { super('The saved draft is incompatible with your current job types or contains invalid data. Clear that device copy to start again.'); this.name = 'InvalidDeviceDraftError'; this.revision = revision; }
}

export function catalogFingerprint(catalog: unknown): string {
	// A deterministic version marker, not a security hash or an authoritative price version.
	const text = JSON.stringify(catalog);
	let value = 2166136261;
	for (let index = 0; index < text.length; index++) value = Math.imul(value ^ text.charCodeAt(index), 16777619);
	return `sample-${(value >>> 0).toString(16)}`;
}

export function isDraftStale(saved: Pick<SavedFieldDraft, 'savedAt' | 'catalogVersion'>, currentCatalogVersion: string, now = Date.now()): boolean {
	const savedAt = Date.parse(saved.savedAt);
	return !Number.isFinite(savedAt) || savedAt > now || now - savedAt > 24 * 60 * 60 * 1000 || saved.catalogVersion !== currentCatalogVersion;
}

export function validateSavedDraft(value: unknown, identity: OfflineIdentity): SavedFieldDraft | null {
	if (!value || typeof value !== 'object') return null;
	const saved = value as SavedFieldDraft;
	if (saved.scope !== draftStorageKey(identity.tenantId, identity.userId) || saved.version !== 1 || typeof saved.revision !== 'string' || !saved.revision || !Number.isFinite(Date.parse(saved.savedAt)) || typeof saved.catalogVersion !== 'string' || saved.state !== 'pending-office-sync') return null;
	if (!readFieldDraft(JSON.stringify(saved.draft), identity.capabilities) || !Array.isArray(saved.photos)) return null;
	let bytes = 0;
	const ids = new Set<string>();
	for (const photo of saved.photos) {
		if (!photo || typeof photo.id !== 'string' || ids.has(photo.id) || typeof photo.name !== 'string' || !saved.draft.openings.some((opening) => opening.id === photo.openingId) || !(photo.blob instanceof Blob) || !acceptedPhotoTypes.includes(photo.blob.type) || photo.blob.size > maximumPhotoBytes) return null;
		ids.add(photo.id); bytes += photo.blob.size;
	}
	return bytes <= maximumDraftPhotoBytes ? saved : null;
}

function openDatabase(): Promise<IDBDatabase> {
	return new Promise((resolve, reject) => {
		const request = indexedDB.open(databaseName, 1);
		request.onupgradeneeded = () => { request.result.createObjectStore(storeName, { keyPath: 'scope' }); };
		request.onerror = () => reject(request.error ?? new Error('Device storage is unavailable.'));
		request.onblocked = () => reject(new Error('Close older field workspace tabs to enable device storage.'));
		request.onsuccess = () => { const database = request.result; database.onversionchange = () => database.close(); resolve(database); };
	});
}

export async function loadDeviceDraft(identity: OfflineIdentity): Promise<SavedFieldDraft | null> {
	const database = await openDatabase();
	return new Promise((resolve, reject) => {
		const transaction = database.transaction(storeName, 'readonly');
		const request = transaction.objectStore(storeName).get(draftStorageKey(identity.tenantId, identity.userId));
		transaction.oncomplete = () => {
			database.close();
			if (request.result === undefined) return resolve(null);
			const saved = validateSavedDraft(request.result, identity);
			if (!saved) return reject(new InvalidDeviceDraftError(typeof request.result?.revision === 'string' ? request.result.revision : null));
			resolve(saved);
		};
		transaction.onabort = transaction.onerror = () => { database.close(); reject(transaction.error ?? new Error('Could not read this device draft.')); };
	});
}

export async function saveDeviceDraft(identity: OfflineIdentity, draft: FieldDraft, photos: OfflinePhoto[], expectedRevision: string | null, catalogVersion: string): Promise<SavedFieldDraft> {
	const saved: SavedFieldDraft = {
		scope: draftStorageKey(identity.tenantId, identity.userId), version: 1, revision: crypto.randomUUID(),
		savedAt: new Date().toISOString(), catalogVersion, state: 'pending-office-sync', draft, photos
	};
	if (!validateSavedDraft(saved, identity)) throw new Error('Check the draft and photos before saving.');
	const database = await openDatabase();
	return new Promise((resolve, reject) => {
		const transaction = database.transaction(storeName, 'readwrite');
		const store = transaction.objectStore(storeName);
		let conflict = false;
		const request = store.get(saved.scope);
		request.onsuccess = () => {
			if ((request.result?.revision ?? null) !== expectedRevision) { conflict = true; transaction.abort(); return; }
			store.put(saved);
		};
		transaction.oncomplete = () => { database.close(); resolve(saved); };
		transaction.onabort = transaction.onerror = () => { database.close(); reject(conflict ? new DraftConflictError() : transaction.error ?? new Error('Could not save this device draft.')); };
	});
}

export async function deleteDeviceDraft(identity: OfflineIdentity, expectedRevision: string | null): Promise<void> {
	const database = await openDatabase();
	return new Promise((resolve, reject) => {
		const transaction = database.transaction(storeName, 'readwrite');
		const store = transaction.objectStore(storeName);
		let conflict = false;
		const request = store.get(draftStorageKey(identity.tenantId, identity.userId));
		request.onsuccess = () => {
			if ((request.result?.revision ?? null) !== expectedRevision) { conflict = true; transaction.abort(); return; }
			store.delete(draftStorageKey(identity.tenantId, identity.userId));
		};
		transaction.oncomplete = () => { database.close(); resolve(); };
		transaction.onabort = transaction.onerror = () => { database.close(); reject(conflict ? new DraftConflictError() : transaction.error ?? new Error('Could not clear this device draft.')); };
	});
}
