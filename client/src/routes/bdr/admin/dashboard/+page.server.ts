import { resolveMvpScaffold } from '$lib/server/mvp';
import { loadQuoteRequests } from '$lib/server/quote-requests';
import { buildQuoteRequestInbox, getQuoteRequestMetrics } from '$lib/quote-requests';
import { formatCurrency } from '$lib/utils/format';

export const load = async ({ fetch }) => {
	const { snapshot, source } = await resolveMvpScaffold(fetch);
	const { requests, source: requestSource } = await loadQuoteRequests(fetch);
	const requestInbox = buildQuoteRequestInbox(requests);

	return {
		source,
		requestSource,
		snapshot,
		requestInbox,
		requestMetrics: getQuoteRequestMetrics(requestInbox),
		metrics: [
			{
				label: 'Open opportunities',
				value: String(snapshot.summary.leadCount),
				detail: `Current scaffold leads worth ${formatCurrency(snapshot.summary.pipelineValue)}`
			},
			{
				label: 'Active estimates',
				value: String(snapshot.summary.estimateCount),
				detail: `${formatCurrency(snapshot.summary.estimateValue)} currently sitting in estimate value`
			},
			{
				label: 'Quote requests',
				value: String(requestInbox.length),
				detail: `${getQuoteRequestMetrics(requestInbox).newCount} new request(s) currently waiting for first response`
			}
		]
	};
};
