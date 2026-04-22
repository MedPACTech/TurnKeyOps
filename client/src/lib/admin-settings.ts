export type AdminSettingField = {
	id: string;
	label: string;
	value: string;
	type: 'currency' | 'percent' | 'days' | 'hours' | 'toggle' | 'select' | 'text';
	help: string;
	options?: string[];
};

export type AdminSettingGroup = {
	id: string;
	label: string;
	description: string;
	outcome: string;
	fields: AdminSettingField[];
};

export const adminSettingsGroups: AdminSettingGroup[] = [
	{
		id: 'estimate-parameters',
		label: 'Estimate parameters',
		description: 'Controls the default commercial assumptions that shape estimate packets before an operator overrides them.',
		outcome: 'Keeps estimate generation consistent across office staff and estimator handoff.',
		fields: [
			{
				id: 'default-overhead',
				label: 'Default overhead rate',
				value: '12%',
				type: 'percent',
				help: 'Applied before final markup so internal costing starts from a visible baseline.'
			},
			{
				id: 'default-profit',
				label: 'Default target margin',
				value: '18%',
				type: 'percent',
				help: 'Used by the office when building standard estimate packets.'
			},
			{
				id: 'minimum-deposit',
				label: 'Minimum deposit request',
				value: '30%',
				type: 'percent',
				help: 'Shown when a job requires material commitment before schedule lock.'
			},
			{
				id: 'insurance-supplement-window',
				label: 'Insurance supplement review window',
				value: '2 days',
				type: 'days',
				help: 'How long supplement review can stay pending before owner escalation.'
			}
		]
	},
	{
		id: 'calculation-handling',
		label: 'Calculation handling',
		description: 'Defines how totals, rounding, and exception rules behave when the estimate desk builds or revises pricing.',
		outcome: 'Reduces pricing drift and makes approval exceptions explicit.',
		fields: [
			{
				id: 'rounding-mode',
				label: 'Estimate rounding mode',
				value: 'Round line items to nearest dollar',
				type: 'select',
				help: 'Controls how customer-facing totals are normalized.',
				options: ['Round line items to nearest dollar', 'Round final total only', 'No rounding']
			},
			{
				id: 'tax-handling',
				label: 'Sales tax handling',
				value: 'Apply by taxable material class',
				type: 'select',
				help: 'Scaffold rule for how the office treats tax in mixed-material scopes.',
				options: ['Apply by taxable material class', 'Apply at estimate total', 'Manual review required']
			},
			{
				id: 'change-order-threshold',
				label: 'Change-order approval threshold',
				value: '$2,500',
				type: 'currency',
				help: 'Changes above this amount require owner signoff before issue.'
			}
		]
	},
	{
		id: 'business-rules',
		label: 'Business rules',
		description: 'Operational guardrails that directly affect release decisions, customer communication, and billing posture.',
		outcome: 'Turns tribal office knowledge into visible policy.',
		fields: [
			{
				id: 'check-hold',
				label: 'Check hold duration',
				value: '3 days',
				type: 'days',
				help: 'Jobs paid by check stay on hold for this period before release.'
			},
			{
				id: 'schedule-release',
				label: 'Schedule release buffer',
				value: '24 hours',
				type: 'hours',
				help: 'Minimum time between readiness approval and hard schedule lock.'
			},
			{
				id: 'signature-required',
				label: 'Require signature before production',
				value: 'Enabled',
				type: 'toggle',
				help: 'Prevents production handoff if the estimate packet is unsigned.'
			},
			{
				id: 'deposit-exception',
				label: 'Deposit exception owner',
				value: 'Owner approval',
				type: 'text',
				help: 'Who can override the default deposit requirement.'
			}
		]
	},
	{
		id: 'operator-defaults',
		label: 'Operator defaults',
		description: 'Default ownership and workflow timing choices for the office team.',
		outcome: 'Makes the queue behave predictably even before full permissions or automation exist.',
		fields: [
			{
				id: 'intake-owner',
				label: 'Default quote intake owner',
				value: 'Office intake',
				type: 'text',
				help: 'New website requests start with this owner unless reassigned.'
			},
			{
				id: 'follow-up-sla',
				label: 'First-response SLA',
				value: '15 minutes',
				type: 'hours',
				help: 'Operational target for emergency or priority web requests.'
			},
			{
				id: 'estimate-review',
				label: 'Estimate internal review required',
				value: 'Enabled',
				type: 'toggle',
				help: 'Keeps quote packets in draft until an internal review pass is complete.'
			}
		]
	}
];
