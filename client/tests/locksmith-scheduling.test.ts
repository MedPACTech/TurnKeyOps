import test from 'node:test';
import assert from 'node:assert/strict';
import { requestJobType, scheduleConflicts, schedulingTechnicians, validVisitWindow } from '../src/lib/locksmith-scheduling.ts';
import type { QuoteRequest } from '../src/lib/quote-requests.ts';
import type { ManagedTenantUser } from '../src/lib/server/user-administration.ts';
import type { LocksmithSettings } from '../src/lib/server/locksmith-settings.ts';

test('one staffing pool filters technicians by job type and active membership', () => {
	const users = [
		{ membershipId: 'a', userId: '1', email: 'a@example.test', status: 'Active' },
		{ membershipId: 'b', userId: '2', email: 'b@example.test', status: 'Active' },
		{ membershipId: 'c', userId: '3', email: 'c@example.test', status: 'Inactive' }
	] as ManagedTenantUser[];
	const settings = { techCapabilities: { a: ['residential'], b: ['residential', 'commercial'], c: ['commercial'] } } as LocksmithSettings;
	const available = schedulingTechnicians(users, settings);
	assert.deepEqual(available.map((item) => item.membershipId), ['a', 'b']);
	assert.deepEqual(available.filter((item) => item.jobTypes.includes('commercial')).map((item) => item.membershipId), ['b']);
});

test('assessment conflicts use the shared calendar and overlapping windows', () => {
	const booked = [{ id: 'existing', propertyType: 'residential', siteVisitSchedule: {
		visitDate: '2026-10-05', windowStart: '09:00', windowEnd: '10:30', assignedFieldResource: 'a@example.test'
	} }] as QuoteRequest[];
	assert.equal(scheduleConflicts(booked, 'new', 'a@example.test', '2026-10-05', '10:00', '11:00'), true);
	assert.equal(scheduleConflicts(booked, 'new', 'a@example.test', '2026-10-05', '10:30', '11:30'), false);
	assert.equal(scheduleConflicts(booked, 'new', 'b@example.test', '2026-10-05', '10:00', '11:00'), false);
	assert.equal(requestJobType(booked[0]), 'residential');
	assert.equal(validVisitWindow('2026-10-05', '09:00', '10:30'), true);
	assert.equal(validVisitWindow('2026-10-05', '10:30', '09:00'), false);
});
