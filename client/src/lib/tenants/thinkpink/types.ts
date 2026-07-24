export type QuoteValues = {
	name: string;
	phone: string;
	email: string;
	address: string;
	acreage: string;
	service: string;
	timeline: string;
};

/** Shape returned by the `?/quote` form action. */
export type QuoteFormResult = {
	success: boolean;
	error?: string | null;
	values?: QuoteValues | null;
} | null;

