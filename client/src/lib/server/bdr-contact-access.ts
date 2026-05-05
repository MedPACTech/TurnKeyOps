const fsModuleName = 'node:fs/promises';
const getCwd = () =>
	(globalThis as typeof globalThis & { process?: { cwd: () => string } }).process?.cwd() ?? '.';
const localStoreDir = `${getCwd()}/.svelte-kit`;
const localStorePath = `${localStoreDir}/local-bdr-contact-access.json`;

export const bdrContactAccessRoles = ['none', 'field', 'office-admin', 'owner'] as const;
export type BdrContactAccessRole = (typeof bdrContactAccessRoles)[number];

type FsPromises = {
	mkdir: (path: string, options: { recursive: boolean }) => Promise<unknown>;
	readFile: (path: string, encoding: 'utf-8') => Promise<string>;
	writeFile: (path: string, data: string) => Promise<unknown>;
};

const getFs = async () => (await import(/* @vite-ignore */ fsModuleName)) as FsPromises;

export const isBdrContactAccessRole = (value: string | null | undefined): value is BdrContactAccessRole =>
	bdrContactAccessRoles.includes(value as BdrContactAccessRole);

export const isBdrAdminContactAccessRole = (value: string | null | undefined): value is 'office-admin' | 'owner' =>
	value === 'office-admin' || value === 'owner';

export const loadBdrContactAccessRoles = async (): Promise<Record<string, BdrContactAccessRole>> => {
	try {
		const fs = await getFs();
		const text = await fs.readFile(localStorePath, 'utf-8');
		const parsed = JSON.parse(text) as Record<string, string>;

		return Object.fromEntries(
			Object.entries(parsed).filter((entry): entry is [string, BdrContactAccessRole] =>
				Boolean(entry[0] && isBdrContactAccessRole(entry[1]))
			)
		);
	} catch {
		return {};
	}
};

export const saveBdrContactAccessRole = async (contactId: string, role: BdrContactAccessRole) => {
	const trimmedContactId = contactId.trim();
	if (!trimmedContactId) {
		throw new Error('Contact id is required.');
	}

	const fs = await getFs();
	const roles = await loadBdrContactAccessRoles();
	const nextRoles = { ...roles, [trimmedContactId]: role };
	await fs.mkdir(localStoreDir, { recursive: true });
	await fs.writeFile(localStorePath, `${JSON.stringify(nextRoles, null, 2)}\n`);

	return nextRoles;
};

export const getPersistedBdrAdminRole = async (contactId: string | undefined) => {
	if (!contactId) return null;
	const roles = await loadBdrContactAccessRoles();
	const role = roles[contactId];
	return isBdrAdminContactAccessRole(role) ? role : null;
};
