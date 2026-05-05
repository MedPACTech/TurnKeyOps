const fsModuleName = 'node:fs/promises';
const getCwd = () =>
	(globalThis as typeof globalThis & { process?: { cwd: () => string } }).process?.cwd() ?? '.';
const localStoreDir = `${getCwd()}/.svelte-kit`;
const localStorePath = `${localStoreDir}/local-bdr-estimate-defaults.json`;

type FsPromises = {
	mkdir: (path: string, options: { recursive: boolean }) => Promise<unknown>;
	readFile: (path: string, encoding: 'utf-8') => Promise<string>;
	writeFile: (path: string, data: string) => Promise<unknown>;
};

export type BdrEstimateDefaults = Record<string, number>;

const getFs = async () => (await import(/* @vite-ignore */ fsModuleName)) as FsPromises;

export const defaultBdrEstimateDefaults: BdrEstimateDefaults = {
	concreteCostPerYard: 165,
	minimumLoadFee: 250,
	shortLoadFee: 125,
	deliveryFee: 140,
	fuelSurcharge: 35,
	defaultPumpFee: 650,
	additiveCost: 28,
	fiberMeshCost: 18,
	colorCost: 42,
	sealerCost: 0.85,
	demoCostRate: 4.5,
	excavationCostRate: 6.25,
	haulOffFee: 325,
	baseMaterialUnitCost: 48,
	compactionCost: 180,
	vaporBarrierCost: 0.65,
	gradingCost: 295,
	accessDifficultyEasyPercent: 0,
	accessDifficultyModeratePercent: 7.5,
	accessDifficultyHardPercent: 15,
	rebarCostPerFoot: 2.25,
	meshCost: 180,
	chairsCost: 24,
	dowelsCost: 38,
	anchorBoltsCost: 52,
	formMaterialCost: 115,
	formComplexitySimpleMultiplier: 0.85,
	formComplexityStandardMultiplier: 1,
	formComplexityComplexMultiplier: 1.35,
	formLaborHoursPerLinearFoot: 0.08,
	sawCutCost: 95,
	jointMaterialCost: 34,
	expansionJointCost: 62,
	curingCompoundCost: 110,
	stampPatternCost: 280,
	decorativePremium: 475,
	laborRatePerHour: 68,
	overtimeMultiplier: 1.5,
	defaultCrewSize: 4,
	demoHoursPer100SqFt: 1.25,
	prepHoursPer100SqFt: 1.75,
	formHoursPer100LinearFt: 2.5,
	reinforcementHoursPer100SqFt: 1.2,
	pourHoursPer100SqFt: 1.1,
	finishHoursPer100SqFt: 1.4,
	skidSteerCost: 425,
	excavatorCost: 575,
	compactorCost: 135,
	sawEquipmentCost: 165,
	powerTrowelCost: 210,
	trailerTruckCost: 185,
	generatorCost: 85,
	buggyCost: 145,
	otherEquipmentCost: 100,
	overheadPercent: 12,
	contingencyPercent: 8,
	profitPercent: 18,
	taxPercent: 7.25,
	travelCharge: 75,
	rushFee: 350,
	weatherRiskAllowance: 125,
	laborRatePerSquareFoot: 4,
	rebarUnitCost: 1.5
};

const normalizeDefaults = (value: unknown): BdrEstimateDefaults => {
	const normalized = { ...defaultBdrEstimateDefaults };
	if (!value || typeof value !== 'object') return normalized;

	for (const [key, fallback] of Object.entries(defaultBdrEstimateDefaults)) {
		const raw = (value as Record<string, unknown>)[key];
		const parsed = Number(raw);
		normalized[key] = Number.isFinite(parsed) && parsed >= 0 ? parsed : fallback;
	}

	normalized.rebarUnitCost = Number.isFinite(Number((value as Record<string, unknown>).rebarUnitCost))
		? normalized.rebarUnitCost
		: normalized.rebarCostPerFoot;

	return normalized;
};

const writeBdrEstimateDefaults = async (
	defaults: BdrEstimateDefaults
): Promise<BdrEstimateDefaults> => {
	const fs = await getFs();
	await fs.mkdir(localStoreDir, { recursive: true });
	await fs.writeFile(localStorePath, JSON.stringify(defaults, null, 2));
	return defaults;
};

export const loadBdrEstimateDefaults = async (): Promise<BdrEstimateDefaults> => {
	try {
		const fs = await getFs();
		const contents = await fs.readFile(localStorePath, 'utf-8');
		return normalizeDefaults(JSON.parse(contents) as unknown);
	} catch (cause) {
		if (cause && typeof cause === 'object' && 'code' in cause && cause.code !== 'ENOENT') {
			console.warn('Unable to read BDR estimate defaults store.', cause);
		}

		return normalizeDefaults(defaultBdrEstimateDefaults);
	}
};

export const saveBdrEstimateDefaults = async (
	value: unknown
): Promise<BdrEstimateDefaults> => writeBdrEstimateDefaults(normalizeDefaults(value));
