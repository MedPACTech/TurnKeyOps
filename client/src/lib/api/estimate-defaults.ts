import { apiRequest } from './client';

export type EstimateDefaults = {
	concreteCostPerYard: number;
	minimumLoadFee: number;
	shortLoadFee: number;
	deliveryFee: number;
	fuelSurcharge: number;
	defaultPumpFee: number;
	additiveCost: number;
	fiberMeshCost: number;
	colorCost: number;
	sealerCost: number;
	demoCostRate: number;
	excavationCostRate: number;
	haulOffFee: number;
	baseMaterialUnitCost: number;
	compactionCost: number;
	vaporBarrierCost: number;
	gradingCost: number;
	accessDifficultyEasyPercent: number;
	accessDifficultyModeratePercent: number;
	accessDifficultyHardPercent: number;
	rebarCostPerFoot: number;
	meshCost: number;
	chairsCost: number;
	dowelsCost: number;
	anchorBoltsCost: number;
	formMaterialCost: number;
	formComplexitySimpleMultiplier: number;
	formComplexityStandardMultiplier: number;
	formComplexityComplexMultiplier: number;
	formLaborHoursPerLinearFoot: number;
	sawCutCost: number;
	jointMaterialCost: number;
	expansionJointCost: number;
	curingCompoundCost: number;
	stampPatternCost: number;
	decorativePremium: number;
	laborRatePerHour: number;
	overtimeMultiplier: number;
	defaultCrewSize: number;
	demoHoursPer100SqFt: number;
	prepHoursPer100SqFt: number;
	formHoursPer100LinearFt: number;
	reinforcementHoursPer100SqFt: number;
	pourHoursPer100SqFt: number;
	finishHoursPer100SqFt: number;
	skidSteerCost: number;
	excavatorCost: number;
	compactorCost: number;
	sawEquipmentCost: number;
	powerTrowelCost: number;
	trailerTruckCost: number;
	generatorCost: number;
	buggyCost: number;
	otherEquipmentCost: number;
	overheadPercent: number;
	contingencyPercent: number;
	profitPercent: number;
	taxPercent: number;
	travelCharge: number;
	rushFee: number;
	weatherRiskAllowance: number;
	/** Legacy estimator aliases retained until calculation inputs move fully into the API. */
	laborRatePerSquareFoot?: number;
	rebarUnitCost?: number;
};

export const getEstimateDefaults = (
	fetcher?: typeof globalThis.fetch,
	accessToken?: string | null
) =>
	apiRequest<EstimateDefaults>(
		'/api/admin/estimate-defaults',
		{ method: 'GET' },
		fetcher,
		accessToken
	);

export const updateEstimateDefaults = (
	defaults: EstimateDefaults,
	fetcher?: typeof globalThis.fetch,
	accessToken?: string | null
) =>
	apiRequest<EstimateDefaults>(
		'/api/admin/estimate-defaults',
		{ method: 'PUT', body: JSON.stringify(defaults) },
		fetcher,
		accessToken
	);
