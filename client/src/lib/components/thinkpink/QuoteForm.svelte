<script lang="ts">
	import { enhance } from '$app/forms';
	import { acreageOptions, serviceOptions, timelineOptions } from '$lib/tenants/thinkpink/content';
	import type { QuoteFormResult } from '$lib/tenants/thinkpink/types';

	let { form, submissionId }: { form: QuoteFormResult; submissionId: string } = $props();

	let submitting = $state(false);
	let fileInput = $state<HTMLInputElement | null>(null);
	let fileNames = $state<string[]>([]);
	let dragging = $state(false);

	const field =
		'border-line-3 bg-bone text-ink focus:border-pink w-full rounded-md border px-3.5 py-3 text-[15px] outline-none transition-colors';
	const label =
		'text-muted flex flex-col gap-1.5 text-[13px] font-bold tracking-[0.04em] uppercase';

	function syncFiles() {
		fileNames = Array.from(fileInput?.files ?? []).map((f) => f.name);
	}

	function onDrop(e: DragEvent) {
		e.preventDefault();
		dragging = false;
		if (fileInput && e.dataTransfer?.files.length) {
			fileInput.files = e.dataTransfer.files;
			syncFiles();
		}
	}
</script>

{#if form?.success}
	<div
		class="bg-ink text-bone flex flex-col items-start gap-4 rounded-xl px-8 py-12 sm:px-12 sm:py-14"
	>
		<span class="display text-pink-bright text-[44px] font-black">Got it!</span>
		<p class="text-dark-body-2 text-[17px] leading-relaxed">
			Thanks — your request was saved{form.reference ? ` as ${form.reference}` : ''}. We'll call you within one business day to schedule your free site visit.
		</p>
		<a
			href="#quote"
			onclick={() => window.location.reload()}
			class="hover:border-pink-bright hover:text-pink-bright rounded-md border border-[#4A3A42] px-5 py-[11px] text-sm font-bold transition-colors"
		>
			Send another request
		</a>
	</div>
{:else}
	<form
		method="POST"
		action="?/quote"
		enctype="multipart/form-data"
		use:enhance={() => {
			submitting = true;
			return async ({ update }) => {
				await update();
				submitting = false;
			};
		}}
		class="border-line flex flex-col gap-5 rounded-xl border bg-white p-6 shadow-[0_12px_40px_rgba(28,20,24,0.08)] sm:p-10"
		aria-busy={submitting}
	>
		<input type="hidden" name="submissionId" value={submissionId} />
		<label class="sr-only" aria-hidden="true">Website<input name="website" tabindex="-1" autocomplete="off" /></label>
		{#if form?.error}
			<p
				class="border-pink/30 bg-pink/5 text-pink-dark rounded-md border px-4 py-3 text-sm font-semibold"
				role="alert"
			>
				{form.error}
			</p>
		{/if}

		<div class="grid gap-4 sm:grid-cols-2">
			<label class={label}>
				Name
				<input
					required
					type="text"
					name="name"
					autocomplete="name"
					placeholder="Full name"
					value={form?.values?.name ?? ''}
					class={field}
				/>
			</label>
			<label class={label}>
				Phone
				<input
					required
					type="tel"
					name="phone"
					autocomplete="tel"
					placeholder="(614) 555-0100"
					value={form?.values?.phone ?? ''}
					class={field}
				/>
			</label>
		</div>

		<label class={label}>
			Email
			<input
				type="email"
				name="email"
				autocomplete="email"
				placeholder="you@example.com"
				value={form?.values?.email ?? ''}
				class={field}
			/>
		</label>

		<label class={label}>
			Property address
			<input
				required
				type="text"
				name="address"
				placeholder="Street address, city, or nearest crossroads"
				value={form?.values?.address ?? ''}
				class={field}
			/>
		</label>

		<div class="grid gap-4 sm:grid-cols-2">
			<label class={label}>
				Approx. acreage
				<select name="acreage" class={field}>
					<option value="">Select…</option>
					{#each acreageOptions as opt (opt)}
						<option>{opt}</option>
					{/each}
				</select>
			</label>
			<label class={label}>
				Service needed
				<select name="service" class={field}>
					<option value="">Select…</option>
					{#each serviceOptions as opt (opt)}
						<option>{opt}</option>
					{/each}
				</select>
			</label>
		</div>

		<label class={label}>
			Timeline
			<select name="timeline" class={field}>
				<option value="">Select…</option>
				{#each timelineOptions as opt (opt)}
					<option>{opt}</option>
				{/each}
			</select>
		</label>

		<div class={label}>
			<span>
				Property photos
				<span class="text-faint font-medium tracking-normal normal-case">
					(optional, but speeds things up)
				</span>
			</span>
			<button
				type="button"
				onclick={() => fileInput?.click()}
				ondragover={(e) => {
					e.preventDefault();
					dragging = true;
				}}
				ondragleave={() => (dragging = false)}
				ondrop={onDrop}
				class="bg-bone hover:border-pink flex w-full flex-col items-center gap-1.5 rounded-lg border-2 border-dashed p-6 transition-colors {dragging
					? 'border-pink'
					: 'border-line-3'}"
			>
				<span class="text-ink text-[15px] font-semibold tracking-normal normal-case">
					{fileNames.length
						? `${fileNames.length} photo${fileNames.length === 1 ? '' : 's'} selected`
						: 'Drop photos here or click to browse'}
				</span>
				<span class="text-faint text-[13px] font-medium tracking-normal normal-case">
					{fileNames.length ? fileNames.join(', ') : 'JPG or PNG, up to 10 files'}
				</span>
			</button>
			<input
				bind:this={fileInput}
				onchange={syncFiles}
				type="file"
				name="photos"
				accept="image/*"
				multiple
				class="hidden"
			/>
		</div>

		<button
			type="submit"
			disabled={submitting}
			class="bg-pink hover:bg-pink-dark cursor-pointer rounded-md px-7 py-4 text-[17px] font-bold tracking-[0.02em] text-white transition-colors disabled:cursor-not-allowed disabled:opacity-60"
		>
			{submitting ? 'Sending…' : 'Request My Free Site Visit'}
		</button>
		<p class="text-faint m-0 text-center text-[13px]">
			By submitting, you consent to Think Pink using these details to respond to this quote request. Photos are stored privately with the request; do not upload sensitive identity or financial documents.
		</p>
	</form>
{/if}
