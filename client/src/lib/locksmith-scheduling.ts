import type { QuoteRequest } from './quote-requests';
import type { ManagedTenantUser } from './server/user-administration';
import type { LocksmithSettings } from './server/locksmith-settings';

export type SchedulingTechnician = { membershipId: string; label: string; jobTypes: Array<'residential' | 'commercial'> };

export function schedulingTechnicians(users: ManagedTenantUser[], settings: LocksmithSettings): SchedulingTechnician[] {
	return users.filter((user) => user.status.toLowerCase() === 'active' && user.userId)
		.map((user) => ({ membershipId: user.membershipId, label: user.email || user.phone || user.userId!, jobTypes: settings.techCapabilities[user.membershipId] ?? [] }))
		.filter((user) => user.jobTypes.length > 0);
}

export function requestJobType(request: QuoteRequest): 'residential' | 'commercial' | null {
	const type = request.propertyType.toLowerCase();
	return type === 'residential' || type === 'commercial' ? type : null;
}

export function scheduleConflicts(requests: QuoteRequest[], requestId: string, techLabel: string, date: string, start: string, end: string): boolean {
	return requests.some((request) => request.id !== requestId && request.siteVisitSchedule?.assignedFieldResource === techLabel &&
		request.siteVisitSchedule.visitDate === date && start < request.siteVisitSchedule.windowEnd && request.siteVisitSchedule.windowStart < end);
}

export function validVisitWindow(date: string, start: string, end: string): boolean {
	return /^\d{4}-\d{2}-\d{2}$/.test(date) && !Number.isNaN(new Date(`${date}T12:00:00Z`).getTime()) && new Date(`${date}T12:00:00Z`).toISOString().slice(0, 10) === date &&
		/^([01]\d|2[0-3]):[0-5]\d$/.test(start) && /^([01]\d|2[0-3]):[0-5]\d$/.test(end) && start < end;
}
