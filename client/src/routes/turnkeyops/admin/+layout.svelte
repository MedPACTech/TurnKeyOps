<script lang="ts">
	import { page } from '$app/state';
	import PlatformAdminShell from '$lib/components/admin/PlatformAdminShell.svelte';
	import { getTurnkeyOpsAdminNav } from '$lib/config/platform';
	import type { Snippet } from 'svelte';

	let { children }: { children: Snippet } = $props();

	const activeNav = $derived(getTurnkeyOpsAdminNav(page.url.pathname));

	const shellCopy = $derived.by(() => {
		switch (activeNav.slug) {
			case 'tenants':
				return {
					title: 'Tenant rollout board',
					description: 'Track every tenant as an operating program: who is live, who is onboarding, which blockers are platform-level, and what can be standardized before the next launch.',
					context: {
						label: 'Context',
						title: 'Portfolio sequencing',
						summary: 'Implementation work gets cleaner when tenant status, blockers, and expansion bets are visible in one shared operating rail.',
						metrics: [
							{ label: 'Live tenants', value: '1', detail: 'BDR is the active reference tenant shaping the initial vertical pattern.' },
							{ label: 'Queued launches', value: '2', detail: 'Two follow-on verticals are in scope definition and data mapping.' },
							{ label: 'Critical blockers', value: '4', detail: 'The same four implementation dependencies are slowing repeatable rollout.' }
						]
					},
					focus: {
						label: 'Focus',
						title: 'Protect repeatability',
						summary: 'The goal is not just shipping one tenant. It is proving the next three are easier because the product absorbs the lessons.',
						notes: [
							{ title: 'Keep tenant variance explicit', detail: 'Separate product capabilities, vertical defaults, and client-specific exceptions before they sprawl.' },
							{ title: 'Launch reviews need product owners', detail: 'Every blocker logged here should either become a backlog item or a playbook update.' },
							{ title: 'BDR stays a tenant, not the template', detail: 'Useful patterns can graduate upward, but tenant identity should not bleed into platform navigation or naming.', tone: 'accent' }
						]
					},
					canvas: {
						label: 'Tenant canvas',
						title: 'Portfolio rollout matrix',
						summary: 'Launch status, blockers, and implementation momentum by tenant.',
						actions: [
							{ label: 'Open dashboard', href: '/turnkeyops/admin/dashboard', variant: 'secondary' },
							{ label: 'Review playbooks', href: '/turnkeyops/admin/playbooks' }
						]
					}
				};
			case 'playbooks':
				return {
					title: 'Implementation playbooks',
					description: 'Codify what good rollout looks like across discovery, configuration, migration, training, and launch readiness so new tenants do not restart from scratch.',
					context: {
						label: 'Context',
						title: 'Reusable operating knowledge',
						summary: 'Playbooks convert one-off launch lessons into repeatable implementation assets and product requirements.',
						metrics: [
							{ label: 'Active playbooks', value: '5', detail: 'One per delivery stage from qualification through go-live.' },
							{ label: 'Vertical templates', value: '3', detail: 'Contractor, field services, and white-glove service variants are being shaped.' },
							{ label: 'Recent updates', value: '8', detail: 'Implementation lessons from BDR drove process and product edits this week.' }
						]
					},
					focus: {
						label: 'Focus',
						title: 'Standardize the good parts',
						summary: 'A believable platform console should show how the team captures and reuses operational knowledge, not just status snapshots.',
						notes: [
							{ title: 'Playbooks should map to routes and configuration', detail: 'Every playbook stage should have a matching UI surface, owner, and exit criteria.' },
							{ title: 'Use platform language', detail: 'The copy here should describe controls, templates, integrations, and launch gates rather than tenant-specific office tasks.' },
							{ title: 'Audit for drift', detail: 'When a tenant invents a workaround, decide whether it belongs in the platform or should stay local.' }
						]
					},
					canvas: {
						label: 'Playbook canvas',
						title: 'Launch standards and templates',
						summary: 'Stage ownership, artifacts, and upgrade pressure across the implementation lifecycle.',
						actions: [
							{ label: 'Open health view', href: '/turnkeyops/admin/health', variant: 'secondary' },
							{ label: 'Open control plane', href: '/turnkeyops/admin/access' }
						]
					}
				};
			case 'health':
				return {
					title: 'Platform health and telemetry',
					description: 'Give operators a single read on release confidence, integration reliability, data quality, and operational debt before that debt hits a tenant launch.',
					context: {
						label: 'Context',
						title: 'Shared service confidence',
						summary: 'Platform health matters because every weak integration or brittle workflow is multiplied across tenants.',
						metrics: [
							{ label: 'Critical services', value: '6', detail: 'Auth, documents, billing, notifications, calendar, and CRM sync are on the watch list.' },
							{ label: 'Open incidents', value: '2', detail: 'Both are degraded-mode issues, not full outages.' },
							{ label: 'Release confidence', value: '84%', detail: 'Healthy enough to keep shipping, but integration debt is visible.' }
						]
					},
					focus: {
						label: 'Focus',
						title: 'Catch risk before rollout feels it',
						summary: 'Health reporting should translate technical reliability into launch risk and operator confidence.',
						notes: [
							{ title: 'Instrument by workflow, not only service', detail: 'Operators care whether estimate-to-contract works end to end, not whether one queue is green.' },
							{ title: 'Watch migration quality', detail: 'Data integrity issues are one of the fastest ways to destroy trust during onboarding.' },
							{ title: 'Release windows need launch awareness', detail: 'Avoid shipping risky changes right before tenant cutover unless the blast radius is understood.', tone: 'accent' }
						]
					},
					canvas: {
						label: 'Telemetry canvas',
						title: 'Reliability scorecards',
						summary: 'Incidents, data quality checks, and release posture across the platform.',
						actions: [
							{ label: 'Open access controls', href: '/turnkeyops/admin/access', variant: 'secondary' },
							{ label: 'Review tenants', href: '/turnkeyops/admin/tenants' }
						]
					}
				};
			case 'access':
				return {
					title: 'Access, controls, and governance',
					description: 'Use one route for permission models, environment controls, audit expectations, and launch gates so operators can manage risk without hunting across tenant pages.',
					context: {
						label: 'Context',
						title: 'Control plane discipline',
						summary: 'A serious admin surface needs a clear place for permissions, environments, approvals, and operational safeguards.',
						metrics: [
							{ label: 'Role sets', value: '7', detail: 'Platform, implementation, support, finance, and tenant-scoped access are modeled separately.' },
							{ label: 'Audit events / day', value: '1.2k', detail: 'Enough volume to need summaries and exceptions rather than raw logs.' },
							{ label: 'Launch gates', value: '4', detail: 'Data, training, billing, and admin readiness must pass before go-live.' }
						]
					},
					focus: {
						label: 'Focus',
						title: 'Scale trust intentionally',
						summary: 'Governance should feel operational, not bureaucratic: enough structure to protect the system without slowing delivery to a crawl.',
						notes: [
							{ title: 'Separate platform from tenant access', detail: 'Internal operators need views and controls that tenant staff should never see.' },
							{ title: 'Gate launches with evidence', detail: 'Use checklist-based readiness instead of informal “looks good” approvals.' },
							{ title: 'Review privileged actions', detail: 'High-risk admin capabilities should leave an obvious audit trail and owner record.' }
						]
					},
					canvas: {
						label: 'Policy canvas',
						title: 'Roles, environments, and launch gates',
						summary: 'The working surface for platform governance and operator safeguards.',
						actions: [
							{ label: 'Open health view', href: '/turnkeyops/admin/health', variant: 'secondary' },
							{ label: 'Return to dashboard', href: '/turnkeyops/admin/dashboard' }
						]
					}
				};
			default:
				return {
					title: 'TurnKeyOps operator dashboard',
					description: 'A platform-first control room for launch momentum, shared product health, and the rollout work that turns one tenant implementation into a repeatable operating system.',
					context: {
						label: 'Context',
						title: 'Portfolio command view',
						summary: 'This route sets the tone for the whole console: less “tenant office dashboard,” more “platform operating cockpit.”',
						metrics: [
							{ label: 'Launches in motion', value: '3', detail: '1 active tenant, 2 near-term implementations in discovery and setup.' },
							{ label: 'Platform commitments', value: '9', detail: 'Shared backlog items tied directly to rollout readiness and product integrity.' },
							{ label: 'Exec watchlist', value: '5', detail: 'The top risks that could delay expansion or erode operator trust.' }
						]
					},
					focus: {
						label: 'Focus',
						title: 'Run the product like a platform',
						summary: 'The admin shell should make platform priorities obvious: repeatability, launch confidence, controls, and a healthy boundary between shared product and tenant-specific workflows.',
						notes: [
							{ title: 'Pattern after Wellderly, not copy-paste it', detail: 'Borrow the shell architecture and route discipline, then fill it with TurnKeyOps product-ops content.' },
							{ title: 'Keep BDR visibly separate', detail: 'Link to BDR as a tenant workspace, but never let the TurnKeyOps admin read like the BDR office.' },
							{ title: 'Make the dashboard believable', detail: 'Use launch pressure, implementation blockers, shared reliability, and governance as first-class admin concerns.', tone: 'accent' }
						]
					},
					canvas: {
						label: 'Oversight canvas',
						title: 'Platform rollout and health board',
						summary: 'A working dashboard for the product/platform team to manage launches, upgrades, and risk.',
						actions: [
							{ label: 'View tenants', href: '/turnkeyops/admin/tenants', variant: 'secondary' },
							{ label: 'Open playbooks', href: '/turnkeyops/admin/playbooks' }
						]
					}
				};
		}
	});
</script>

<PlatformAdminShell
	activePath={page.url.pathname}
	activeNav={activeNav}
	title={shellCopy.title}
	description={shellCopy.description}
	context={shellCopy.context}
	focus={shellCopy.focus}
	canvas={shellCopy.canvas}
>
	{@render children()}
</PlatformAdminShell>
