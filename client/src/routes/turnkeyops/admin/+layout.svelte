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
						metrics: []
					},
					focus: {
						label: 'Focus',
						title: 'Protect repeatability',
						summary: 'The goal is not just shipping one tenant. It is proving the next three are easier because the product absorbs the lessons.',
						notes: [
							{ title: 'Keep tenant variance explicit', detail: 'Separate product capabilities, vertical defaults, and client-specific exceptions before they sprawl.' },
							{ title: 'Launch reviews need product owners', detail: 'Every blocker logged here should either become a backlog item or a playbook update.' },
							{ title: 'Tenants stay isolated', detail: 'BDR and Think Pink share platform capabilities without sharing records, branding, or trade defaults.', tone: 'accent' }
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
					description: 'Codify what good rollout looks like across discovery, configuration, workflow modeling, migration, training, and launch readiness so new tenants do not restart from scratch.',
					context: {
						label: 'Context',
						title: 'Reusable operating knowledge',
						summary: 'Playbooks convert one-off launch lessons into repeatable implementation assets and product requirements, including the shared workflow data model behind request, visit, and estimate handoffs.',
						metrics: []
					},
					focus: {
						label: 'Focus',
						title: 'Standardize the good parts',
						summary: 'A believable platform console should show how the team captures and reuses operational knowledge, not just status snapshots.',
						notes: [
							{ title: 'Playbooks should map to routes and configuration', detail: 'Every playbook stage should have a matching UI surface, owner, and exit criteria.' },
							{ title: 'Workflow models should stay explicit', detail: 'Document source of truth, editability, and audit posture before request, visit, and estimate logic diverges.' },
							{ title: 'Audit for drift', detail: 'When a tenant invents a workaround, decide whether it belongs in the platform or should stay local.' }
						]
					},
					canvas: {
						label: 'Playbook canvas',
						title: 'Launch standards and templates',
						summary: 'Stage ownership, artifacts, schema expectations, and upgrade pressure across the implementation lifecycle.',
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
						metrics: []
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
						metrics: []
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
						metrics: []
					},
					focus: {
						label: 'Focus',
						title: 'Run the product like a platform',
						summary: 'The admin shell should make platform priorities obvious: repeatability, launch confidence, controls, and a healthy boundary between shared product and tenant-specific workflows.',
						notes: [
							{ title: 'Pattern after Wellderly, not copy-paste it', detail: 'Borrow the shell architecture and route discipline, then fill it with TurnKeyOps product-ops content.' },
							{ title: 'Keep every tenant visibly separate', detail: 'Internal Admin can open BDR or Think Pink without blending their branding, records, or operating assumptions.' },
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
