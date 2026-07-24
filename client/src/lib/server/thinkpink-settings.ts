import { thinkPinkTenant } from '$lib/config/tenants';

const fsModuleName = 'node:fs/promises';
const getCwd = () =>
	(globalThis as typeof globalThis & { process?: { cwd: () => string } }).process?.cwd() ?? '.';
const storeDir = `${getCwd()}/.svelte-kit`;
const storePath = `${storeDir}/local-thinkpink-settings.json`;

type FsPromises = {
	mkdir: (path: string, options: { recursive: boolean }) => Promise<unknown>;
	readFile: (path: string, encoding: 'utf-8') => Promise<string>;
	writeFile: (path: string, data: string) => Promise<unknown>;
};

export type ThinkPinkSettings = {
	depositPercentRequired: number;
	landClearingPerAcre: number;
	forestryMulchingPerAcre: number;
	brushClearingPerAcre: number;
	treeRemovalSmall: number;
	treeRemovalMedium: number;
	treeRemovalLarge: number;
	stumpGrindingEach: number;
	mobilizationFee: number;
	haulOffPerLoad: number;
	disposalPerLoad: number;
	grindingPerHour: number;
	gradingPerAcre: number;
	restorationPerAcre: number;
	laborRatePerHour: number;
	defaultCrewSize: number;
	overtimeMultiplier: number;
	skidSteerPerDay: number;
	excavatorPerDay: number;
	forestryMulcherPerDay: number;
	chipperPerDay: number;
	dumpTruckPerDay: number;
	overheadPercent: number;
	contingencyPercent: number;
	profitPercent: number;
	taxPercent: number;
	travelCharge: number;
	services: string[];
	estimateInputs: string[];
	jobStages: string[];
};

export const defaultThinkPinkSettings: ThinkPinkSettings = {
	depositPercentRequired: 25,
	landClearingPerAcre: 3200,
	forestryMulchingPerAcre: 2400,
	brushClearingPerAcre: 1800,
	treeRemovalSmall: 350,
	treeRemovalMedium: 750,
	treeRemovalLarge: 1500,
	stumpGrindingEach: 275,
	mobilizationFee: 450,
	haulOffPerLoad: 650,
	disposalPerLoad: 400,
	grindingPerHour: 325,
	gradingPerAcre: 1400,
	restorationPerAcre: 950,
	laborRatePerHour: 72,
	defaultCrewSize: 3,
	overtimeMultiplier: 1.5,
	skidSteerPerDay: 650,
	excavatorPerDay: 950,
	forestryMulcherPerDay: 1800,
	chipperPerDay: 725,
	dumpTruckPerDay: 800,
	overheadPercent: 12,
	contingencyPercent: 10,
	profitPercent: 20,
	taxPercent: 0,
	travelCharge: 125,
	services: [...thinkPinkTenant.services],
	estimateInputs: [...thinkPinkTenant.estimateInputs],
	jobStages: [...thinkPinkTenant.jobStages]
};

const getFs = async () => (await import(/* @vite-ignore */ fsModuleName)) as FsPromises;
const numericKeys = Object.keys(defaultThinkPinkSettings).filter(
	(key) => typeof defaultThinkPinkSettings[key as keyof ThinkPinkSettings] === 'number'
) as Array<keyof ThinkPinkSettings>;
const listKeys = ['services', 'estimateInputs', 'jobStages'] as const;

const normalizeList = (value: unknown, fallback: string[]) =>
	Array.isArray(value)
		? value.map((item) => String(item).trim()).filter(Boolean)
		: [...fallback];

const normalize = (value: unknown): ThinkPinkSettings => {
	const candidate = value && typeof value === 'object' ? (value as Partial<ThinkPinkSettings>) : {};
	const result = structuredClone(defaultThinkPinkSettings);
	for (const key of numericKeys) {
		const number = Number(candidate[key]);
		if (Number.isFinite(number)) (result[key] as number) = Math.max(0, number);
	}
	for (const key of listKeys) result[key] = normalizeList(candidate[key], result[key]);
	result.defaultCrewSize = Math.max(1, Math.round(result.defaultCrewSize));
	result.depositPercentRequired = Math.min(100, result.depositPercentRequired);
	return result;
};

export const loadThinkPinkSettings = async () => {
	try {
		const fs = await getFs();
		return normalize(JSON.parse(await fs.readFile(storePath, 'utf-8')));
	} catch {
		return structuredClone(defaultThinkPinkSettings);
	}
};

export const saveThinkPinkSettings = async (value: unknown) => {
	const settings = normalize(value);
	const fs = await getFs();
	await fs.mkdir(storeDir, { recursive: true });
	await fs.writeFile(storePath, JSON.stringify(settings, null, 2));
	return settings;
};
