<script lang="ts">
	import { formatCurrency, formatDate } from '$lib/utils/format';
	import type { PageProps } from './$types';

	type EstimateLocation = {
		id: string;
		name: string;
		lengthFeet: number;
		widthFeet: number;
		depthInches: number;
		wastePercent: number;
		numberOfPours: number;
	};

	type CalculatedLocation = EstimateLocation & {
		squareFeet: number;
		cubicYards: number;
		formLinearFeet: number;
		rebarLinearFeet: number;
		materialCost: number;
		laborCost: number;
		estimatedTotal: number;
	};

	let { data, form }: PageProps = $props();

	const concreteCostPerYard = $derived(data.estimateDefaults?.concreteCostPerYard ?? 165);
	const laborRatePerSquareFoot = $derived(data.estimateDefaults?.laborRatePerSquareFoot ?? 4);
	const rebarUnitCost = $derived(data.estimateDefaults?.rebarUnitCost ?? 1.5);

	const calculateLocation = (location: EstimateLocation): CalculatedLocation => {
		const squareFeet = Math.max(0, location.lengthFeet) * Math.max(0, location.widthFeet);
		const cubicYardsBase = (squareFeet * Math.max(0, location.depthInches)) / 12 / 27;
		const cubicYards = Math.ceil(cubicYardsBase * (1 + Math.max(0, location.wastePercent)) * 10) / 10;
		const formLinearFeet = Math.ceil(4 * Math.sqrt(squareFeet) * 1.1);
		const rebarLinearFeet = Math.ceil(Math.ceil(Math.sqrt(squareFeet)) * Math.sqrt(squareFeet) * 2 * 1.1);
		const materialCost = cubicYards * concreteCostPerYard + rebarLinearFeet * rebarUnitCost;
		const laborCost = squareFeet * laborRatePerSquareFoot;
		return {
			...location,
			squareFeet,
			cubicYards,
			formLinearFeet,
			rebarLinearFeet,
			materialCost,
			laborCost,
			estimatedTotal: materialCost + laborCost
		};
	};

	const calculatedLocations = $derived(data.draft.locations.map(calculateLocation));
	const totalSquareFeet = $derived(calculatedLocations.reduce((sum, location) => sum + location.squareFeet, 0));
	const totalCubicYards = $derived(calculatedLocations.reduce((sum, location) => sum + location.cubicYards, 0));
	const totalMaterials = $derived(calculatedLocations.reduce((sum, location) => sum + location.materialCost, 0));
	const totalLabor = $derived(calculatedLocations.reduce((sum, location) => sum + location.laborCost, 0));
	const estimatedTotal = $derived(calculatedLocations.reduce((sum, location) => sum + location.estimatedTotal, 0));
	const deliveryStatus = $derived(data.draft.delivery?.status ?? 'sent');
	const isApproved = $derived(deliveryStatus === 'approved' || form?.approved);
	const hasRequestedChanges = $derived(deliveryStatus === 'changes-requested' || form?.changesRequested);
</script>

<svelte:head>
	<title>Estimate Review · BDR Construction</title>
</svelte:head>

<main class="min-h-screen bg-[radial-gradient(circle_at_top_left,rgba(255,116,23,0.12),transparent_28%),radial-gradient(circle_at_top_right,rgba(59,130,246,0.10),transparent_30%),linear-gradient(180deg,#fffaf4_0%,#f8fafc_58%,#f4f7fb_100%)] px-4 py-5 text-slate-950 sm:px-6 lg:px-8">
	<div class="mx-auto max-w-4xl space-y-5">
		<header class="flex flex-col gap-4 rounded-lg bg-white/90 p-5 shadow-[0_18px_48px_rgba(15,23,42,0.08)] md:flex-row md:items-center md:justify-between">
			<div class="flex items-center gap-4">
				<img
					src="/clientFiles/BDRLogo.png"
					alt="BDR Construction"
					class="flex-none rounded-md object-contain"
					style="width: 5rem; height: 5rem;"
				/>
				<div>
					<p class="text-xs font-semibold uppercase tracking-[0.18em] text-orange-600">Estimate Review</p>
					<h1 class="mt-1 text-2xl font-semibold">BDR Construction</h1>
				</div>
			</div>
			<div class="flex flex-col items-start gap-2 md:items-end">
				{#if data.returnTo}
					<a href={data.returnTo} class="inline-flex w-fit justify-center rounded-md bg-white px-4 py-2 text-sm font-semibold text-slate-900 shadow-sm ring-1 ring-slate-200 transition hover:bg-slate-50">
						Return to invoices
					</a>
				{/if}
				<span class="w-fit rounded-full bg-orange-50 px-3 py-1 text-xs font-semibold uppercase tracking-[0.14em] text-orange-700">
					{deliveryStatus.replace('-', ' ')}
				</span>
			</div>
		</header>

		{#if isApproved}
			<section class="rounded-lg bg-emerald-50 p-5 shadow-[0_18px_48px_rgba(15,23,42,0.08)] ring-1 ring-emerald-200">
				<h2 class="text-xl font-semibold text-emerald-900">Estimate approved</h2>
				<p class="mt-2 text-sm leading-6 text-emerald-800">Thanks. BDR has this approval and will move the work into invoice and scheduling handoff.</p>
			</section>
		{:else if hasRequestedChanges}
			<section class="rounded-lg bg-amber-50 p-5 shadow-[0_18px_48px_rgba(15,23,42,0.08)] ring-1 ring-amber-200">
				<h2 class="text-xl font-semibold text-amber-900">Changes requested</h2>
				<p class="mt-2 text-sm leading-6 text-amber-800">BDR has your note and will revise the estimate.</p>
			</section>
		{/if}

		<section class="rounded-lg bg-white/90 p-5 shadow-[0_18px_48px_rgba(15,23,42,0.08)]">
			<div class="grid gap-4 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-start">
				<div>
					<p class="text-sm font-semibold text-slate-600">{data.draft.customerName}</p>
					<h2 class="mt-1 text-3xl font-semibold">{data.draft.siteName}</h2>
					<p class="mt-2 text-sm leading-6 text-slate-600">{data.draft.serviceSummary}</p>
				</div>
				<div class="rounded-lg bg-orange-50 px-4 py-3 text-left sm:text-right">
					<p class="text-xs font-semibold uppercase tracking-[0.16em] text-orange-700">Estimate Total</p>
					<p class="mt-1 text-3xl font-semibold text-slate-950">{formatCurrency(estimatedTotal)}</p>
				</div>
			</div>
			<div class="mt-5 grid gap-2 text-sm sm:grid-cols-3">
				<div class="rounded-md bg-slate-50 px-3 py-2">
					<p class="text-slate-500">Area</p>
					<p class="font-semibold">{totalSquareFeet.toFixed(0)} sqft</p>
				</div>
				<div class="rounded-md bg-slate-50 px-3 py-2">
					<p class="text-slate-500">Concrete</p>
					<p class="font-semibold">{totalCubicYards.toFixed(1)} CY</p>
				</div>
				<div class="rounded-md bg-slate-50 px-3 py-2">
					<p class="text-slate-500">Prepared</p>
					<p class="font-semibold">{data.draft.sentAtUtc ? formatDate(data.draft.sentAtUtc) : formatDate(data.draft.savedAtUtc)}</p>
				</div>
			</div>
		</section>

		<section class="rounded-lg bg-white/90 p-5 shadow-[0_18px_48px_rgba(15,23,42,0.08)]">
			<h2 class="text-xl font-semibold">Project locations</h2>
			<div class="mt-4 grid gap-3">
				{#each calculatedLocations as location}
					<article class="rounded-lg bg-slate-50 p-4">
						<div class="grid gap-3 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-start">
							<div>
								<h3 class="text-lg font-semibold">{location.name}</h3>
								<p class="mt-1 text-sm text-slate-600">
									{location.lengthFeet} ft x {location.widthFeet} ft x {location.depthInches} in · {location.numberOfPours} pour{location.numberOfPours === 1 ? '' : 's'}
								</p>
							</div>
							<p class="text-xl font-semibold">{formatCurrency(location.estimatedTotal)}</p>
						</div>
						<div class="mt-3 grid gap-2 text-sm sm:grid-cols-2">
							<div class="flex justify-between gap-3 rounded-md bg-white px-3 py-2"><span class="text-slate-500">Area</span><strong>{location.squareFeet.toFixed(0)} sqft</strong></div>
							<div class="flex justify-between gap-3 rounded-md bg-white px-3 py-2"><span class="text-slate-500">Concrete</span><strong>{location.cubicYards.toFixed(1)} CY</strong></div>
						</div>
					</article>
				{/each}
			</div>
		</section>

		<section class="grid gap-5 lg:grid-cols-2">
			<div class="rounded-lg bg-white/90 p-5 shadow-[0_18px_48px_rgba(15,23,42,0.08)]">
				<h2 class="text-xl font-semibold">Included scope</h2>
				<ul class="mt-4 space-y-2 text-sm leading-6 text-slate-700">
					{#each data.draft.scopeLineItems as item}
						<li class="rounded-md bg-slate-50 px-3 py-2">{item}</li>
					{/each}
				</ul>
			</div>

			<div class="rounded-lg bg-white/90 p-5 shadow-[0_18px_48px_rgba(15,23,42,0.08)]">
				<h2 class="text-xl font-semibold">Assumptions</h2>
				<ul class="mt-4 space-y-2 text-sm leading-6 text-slate-700">
					{#each data.draft.assumptions as assumption}
						<li class="rounded-md bg-slate-50 px-3 py-2">{assumption}</li>
					{/each}
				</ul>
			</div>
		</section>

		{#if !isApproved && !hasRequestedChanges}
			<section class="rounded-lg bg-white/90 p-5 shadow-[0_18px_48px_rgba(15,23,42,0.08)]">
				<h2 class="text-xl font-semibold">Approve or request changes</h2>
				<p class="mt-2 text-sm leading-6 text-slate-600">Approving tells BDR to move this estimate into invoice and job handoff. If something is off, leave a note and the office will revise it.</p>
				<div class="mt-5 grid gap-3 sm:grid-cols-[auto_minmax(0,1fr)]">
					<form method="POST" action="?/approve">
						<button type="submit" class="w-full rounded-md bg-orange-500 px-5 py-3 text-sm font-semibold text-white shadow-sm transition hover:bg-orange-600 sm:w-auto">Approve estimate</button>
					</form>
					<form method="POST" action="?/requestChanges" class="grid gap-3">
						<textarea name="responseNote" rows="3" class="w-full rounded-lg border border-slate-200 bg-white px-3 py-3 text-sm outline-none focus:border-orange-300" placeholder="What should BDR adjust?"></textarea>
						<button type="submit" class="rounded-md bg-white px-5 py-3 text-sm font-semibold text-slate-950 shadow-sm ring-1 ring-slate-200 transition hover:bg-slate-50">Request changes</button>
						{#if form?.changeMessage}
							<p class="text-sm font-semibold text-amber-700">{form.changeMessage}</p>
						{/if}
					</form>
				</div>
			</section>
		{/if}

		<footer class="pb-8 text-center text-xs text-slate-500">
			BDR Construction · {data.quoteRequest.email} · {data.quoteRequest.phone}
		</footer>
	</div>
</main>
