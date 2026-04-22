export type Customer = {
	id: string;
	displayName: string;
	primaryContactName?: string | null;
	primaryContactEmail?: string | null;
	primaryContactPhone?: string | null;
	status: string;
	lifecycleStage: string;
};

export type Lead = {
	id: string;
	companyName: string;
	contactName: string;
	contactEmail?: string | null;
	contactPhone?: string | null;
	pipelineStage: string;
	estimatedValue?: number | null;
	source: string;
};

export type Estimate = {
	id: string;
	customerId: string;
	jobSiteId?: string | null;
	estimateNumber: string;
	status: string;
	totalAmount: number;
	validUntilUtc?: string | null;
};

export type Invoice = {
	id: string;
	customerId: string;
	jobSiteId?: string | null;
	invoiceNumber: string;
	status: string;
	balanceDue: number;
	dueDateUtc?: string | null;
};

export type MvpScaffoldSnapshot = {
	generatedAtUtc: string;
	summary: {
		customerCount: number;
		estimateCount: number;
		invoiceCount: number;
		leadCount: number;
		estimateValue: number;
		receivablesValue: number;
		pipelineValue: number;
	};
	customers: Customer[];
	estimates: Estimate[];
	invoices: Invoice[];
	leads: Lead[];
};

export type ApiEnvelope<T> = {
	data: T;
	success: boolean;
	traceId?: string;
};

export type MvpScaffoldSource = 'api' | 'fallback';
