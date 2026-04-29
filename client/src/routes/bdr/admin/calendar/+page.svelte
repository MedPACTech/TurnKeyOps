<script lang="ts">
	import type { PageProps } from './$types';
	import {
		CalendarDays,
		ChevronLeft,
		ChevronRight,
		ClipboardList,
		Droplets,
		Hammer,
		HardHat,
		Snowflake,
		Sun,
		Wrench,
		CloudRain,
		CloudSun,
		CalendarRange,
		Building2,
		UserRound,
		MapPin,
		X
	} from 'lucide-svelte';

	type CalendarView = 'day' | 'week' | 'month';
	type EventType = 'Estimate' | 'Install Forms' | 'Pour Concrete' | 'Repair' | 'Site Visit';
	type WeatherKind = 'sunny' | 'partly-cloudy' | 'rain' | 'snow';

type SiteVisit = {
	id: string;
	date: string;
	customer: string;
	status: string;
};

	type CalendarEvent = {
		id: string;
		quoteRequestId?: string;
		type: EventType;
		name: string;
		customerType: 'customer' | 'company';
		date: string;
		startTime: string;
		endTime: string;
		address: string;
		zipCode: string;
		customerContact: string;
		phone: string;
		projectSummary: string;
		status: string;
		crew: string;
		estimatedValue: string;
		notes: string;
		source?: 'seed' | 'quote-request';
	};

	type WeatherSummary = {
		kind: WeatherKind;
		label: string;
		high: number;
		low: number;
	};

	let { data }: { data: PageProps['data'] } = $props();

	const today = new Date();
	const todayKey = formatDateKey(today);

	const eventTypeMeta: Record<
		EventType,
		{
			colorClass: string;
			softClass: string;
			icon: typeof ClipboardList;
		}
	> = {
		Estimate: {
			colorClass: 'border-sky-200 bg-sky-50 text-sky-700',
			softClass: 'bg-sky-100/80 text-sky-700',
			icon: ClipboardList
		},
		'Install Forms': {
			colorClass: 'border-violet-200 bg-violet-50 text-violet-700',
			softClass: 'bg-violet-100/80 text-violet-700',
			icon: HardHat
		},
		'Pour Concrete': {
			colorClass: 'border-emerald-200 bg-emerald-50 text-emerald-700',
			softClass: 'bg-emerald-100/80 text-emerald-700',
			icon: Droplets
		},
		Repair: {
			colorClass: 'border-amber-200 bg-amber-50 text-amber-700',
			softClass: 'bg-amber-100/80 text-amber-700',
			icon: Wrench
		},
		'Site Visit': {
			colorClass: 'border-slate-300 bg-slate-100 text-slate-700',
			softClass: 'bg-slate-200/80 text-slate-700',
			icon: CalendarDays
		}
	};



	const mockedSiteVisits: SiteVisit[] = [
		{
			id: 'sv-1',
			date: todayKey,
			customer: 'Parker, James',
			status: 'Scheduled'
		}
	];

	const mockedEvents: CalendarEvent[] = [
		{
			id: 'evt-1',
			type: 'Estimate',
			name: 'Doe, John',
			customerType: 'customer',
			date: offsetDateKey(-1),
			startTime: '9:00 AM',
			endTime: '10:00 AM',
			address: '14 Mill Run Dr, Sunbury, OH',
			zipCode: '43074',
			customerContact: 'John Doe',
			phone: '(614) 555-0144',
			projectSummary: 'Driveway replacement estimate with drainage review and curb transition.',
			status: 'Ready to quote',
			crew: 'Estimator - Lane',
			estimatedValue: '$18,400',
			notes: 'Customer wants quote before HOA meeting on Friday.'
		},
		{
			id: mockedSiteVisits[0].id,
			type: 'Site Visit',
			name: mockedSiteVisits[0].customer,
			customerType: 'customer',
			date: mockedSiteVisits[0].date,
			startTime: '11:00 AM',
			endTime: '12:00 PM',
			address: '42 Cherry Fork Rd, Sunbury, OH',
			zipCode: '43074',
			customerContact: 'James Parker',
			phone: '(614) 555-0139',
			projectSummary: 'Initial site visit for driveway, front porch, and sidewalk replacement quote.',
			status: mockedSiteVisits[0].status,
			crew: 'Estimator - Maya',
			estimatedValue: '$0',
			notes: 'Capture measurements and photos for all three work areas.'
		},
		{
			id: 'evt-2',
			type: 'Install Forms',
			name: 'Acme Corp',
			customerType: 'company',
			date: todayKey,
			startTime: '7:30 AM',
			endTime: '11:30 AM',
			address: '220 Commerce Park, Columbus, OH',
			zipCode: '43074',
			customerContact: 'Sarah Benton',
			phone: '(614) 555-0181',
			projectSummary: 'Commercial slab prep and form layout for warehouse loading pad.',
			status: 'Crew confirmed',
			crew: 'Crew 2',
			estimatedValue: '$42,000',
			notes: 'Need forklift lane kept clear by 10 AM.'
		},
		{
			id: 'evt-3',
			type: 'Pour Concrete',
			name: 'Global Solutions',
			customerType: 'company',
			date: offsetDateKey(1),
			startTime: '8:00 AM',
			endTime: '2:00 PM',
			address: '9 Progress Way, Delaware, OH',
			zipCode: '43074',
			customerContact: 'Marc Ellis',
			phone: '(740) 555-0198',
			projectSummary: 'Foundation pour for small industrial expansion with pump coordination.',
			status: 'Batch plant confirmed',
			crew: 'Crew 1',
			estimatedValue: '$61,500',
			notes: 'Weather cutoff decision by 6 AM if rain holds.'
		},
		{
			id: 'evt-4',
			type: 'Repair',
			name: 'White, Emily',
			customerType: 'customer',
			date: offsetDateKey(3),
			startTime: '1:00 PM',
			endTime: '3:00 PM',
			address: '118 Oak Bend Ct, Sunbury, OH',
			zipCode: '43074',
			customerContact: 'Emily White',
			phone: '(614) 555-0122',
			projectSummary: 'Front walk crack repair and apron resurfacing.',
			status: 'Material staged',
			crew: 'Repair team',
			estimatedValue: '$6,900',
			notes: 'Customer needs vehicle access restored by evening.'
		},
		{
			id: 'evt-5',
			type: 'Estimate',
			name: 'Midwest Storage',
			customerType: 'company',
			date: offsetDateKey(6),
			startTime: '10:30 AM',
			endTime: '11:30 AM',
			address: '77 Industrial Loop, Columbus, OH',
			zipCode: '43074',
			customerContact: 'Dana Webb',
			phone: '(614) 555-0175',
			projectSummary: 'Parking lot patching and dock apron estimate.',
			status: 'Site walk booked',
			crew: 'Estimator - Chris',
			estimatedValue: '$28,600',
			notes: 'Need line-striping option separated on quote.'
		},
		{
			id: 'evt-6',
			type: 'Pour Concrete',
			name: 'Riverstone HOA',
			customerType: 'company',
			date: offsetDateKey(8),
			startTime: '9:00 AM',
			endTime: '1:00 PM',
			address: '300 Riverstone Dr, Lewis Center, OH',
			zipCode: '43074',
			customerContact: 'Amy Flores',
			phone: '(740) 555-0118',
			projectSummary: 'Pool deck pour with decorative edge finish.',
			status: 'Awaiting weather go/no-go',
			crew: 'Crew 3',
			estimatedValue: '$33,200',
			notes: 'Protect adjacent landscaping and pool coping.'
		},
		{
			id: 'evt-7',
			type: 'Install Forms',
			name: 'Baker, Helen',
			customerType: 'customer',
			date: offsetDateKey(10),
			startTime: '8:30 AM',
			endTime: '11:00 AM',
			address: '65 Birch Hollow Ln, Sunbury, OH',
			zipCode: '43074',
			customerContact: 'Helen Baker',
			phone: '(614) 555-0106',
			projectSummary: 'Patio extension form set with step detail.',
			status: 'Ready to mobilize',
			crew: 'Crew 4',
			estimatedValue: '$12,800',
			notes: 'Confirm final layout with homeowner before stakes go in.'
		},
		{
			id: 'evt-8',
			type: 'Repair',
			name: 'Northgate Retail',
			customerType: 'company',
			date: offsetDateKey(13),
			startTime: '12:00 PM',
			endTime: '4:00 PM',
			address: '145 Northgate Ave, Columbus, OH',
			zipCode: '43074',
			customerContact: 'Luis Carter',
			phone: '(614) 555-0169',
			projectSummary: 'Trip-hazard repair at storefront access and ADA route.',
			status: 'Permit not needed',
			crew: 'Repair team',
			estimatedValue: '$9,400',
			notes: 'Retail foot traffic requires afternoon barricade plan.'
		}
	];

	const mockedWeather = new Map<string, WeatherSummary>();
	for (let offset = -7; offset <= 40; offset++) {
		const key = offsetDateKey(offset);
		mockedWeather.set(key, createWeather(offset));
	}

	let currentView = $state<CalendarView>('month');
	let selectedDate = $state(new Date(today.getFullYear(), today.getMonth(), today.getDate()));
	let pickerValue = $state(formatDateKey(today));
	let enabledTypes = $state<Record<EventType, boolean>>({
		Estimate: true,
		'Install Forms': true,
		'Pour Concrete': true,
		Repair: true,
		'Site Visit': true
	});
	let selectedEventId = $state<string | null>(null);
	let focusedScheduleRequestId = $state<string | null>(null);

	const selectedDateKey = $derived(formatDateKey(selectedDate));
	const scheduledRequest = $derived(data.scheduledRequest);
	const scheduledRequestQualification = $derived(data.scheduledRequestQualification);
	const scheduledRequestDateLabel = $derived(
		scheduledRequest
			? new Date(scheduledRequest.submittedAtUtc).toLocaleString('en-US', {
				month: 'short',
				day: 'numeric',
				hour: 'numeric',
				minute: '2-digit'
			})
			: ''
	);
	const scheduledVisitEvents = $derived((data.scheduledVisitRequests ?? []).map(toScheduledVisitEvent));
	const calendarEvents = $derived([...mockedEvents, ...scheduledVisitEvents]);
	const selectedEvent = $derived(calendarEvents.find((event) => event.id === selectedEventId) ?? null);
	const filteredEvents = $derived(calendarEvents.filter((event) => enabledTypes[event.type]));
	const monthLabel = $derived(
		selectedDate.toLocaleDateString('en-US', { month: 'long', year: 'numeric' })
	);
	const weekLabel = $derived(getWeekLabel(selectedDate));
	const dayLabel = $derived(
		selectedDate.toLocaleDateString('en-US', { weekday: 'long', month: 'long', day: 'numeric', year: 'numeric' })
	);

	const monthGrid = $derived(buildMonthGrid(selectedDate, filteredEvents, mockedWeather));
	const weekDays = $derived(buildWeekDays(selectedDate, filteredEvents, mockedWeather));
	const dayEvents = $derived(
		filteredEvents.filter((event) => event.date === selectedDateKey).sort(compareEvents)
	);
	const selectedDayWeather = $derived(mockedWeather.get(selectedDateKey) ?? createWeather(0));

	$effect(() => {
		pickerValue = formatDateKey(selectedDate);
	});

	$effect(() => {
		const requestId = data.scheduleRequestId;
		if (!requestId || focusedScheduleRequestId === requestId) return;

		const event = scheduledVisitEvents.find((item) => item.quoteRequestId === requestId);
		if (!event) return;

		selectedDate = parseDateKey(event.date) ?? selectedDate;
		selectedEventId = event.id;
		focusedScheduleRequestId = requestId;
	});

	function setView(view: CalendarView) {
		currentView = view;
	}

	function handleDateInput(value: string) {
		pickerValue = value;
		const parsed = parseDateKey(value);
		if (parsed) selectedDate = parsed;
	}

	function shiftRange(direction: -1 | 1) {
		const next = new Date(selectedDate);
		if (currentView === 'month') next.setMonth(next.getMonth() + direction);
		else if (currentView === 'week') next.setDate(next.getDate() + direction * 7);
		else next.setDate(next.getDate() + direction);
		selectedDate = next;
	}

	function toggleType(type: EventType) {
		enabledTypes = { ...enabledTypes, [type]: !enabledTypes[type] };
	}

	function selectDate(date: Date) {
		selectedDate = new Date(date.getFullYear(), date.getMonth(), date.getDate());
	}

	function openEvent(event: CalendarEvent) {
		selectedEventId = event.id;
		selectedDate = parseDateKey(event.date) ?? selectedDate;
	}

	function closeDrawer() {
		selectedEventId = null;
	}

	function weatherIcon(kind: WeatherKind) {
		if (kind === 'sunny') return Sun;
		if (kind === 'partly-cloudy') return CloudSun;
		if (kind === 'rain') return CloudRain;
		return Snowflake;
	}

	function formatRangeTitle() {
		if (currentView === 'month') return monthLabel;
		if (currentView === 'week') return weekLabel;
		return dayLabel;
	}

	function visibleEventsSummary() {
		if (currentView === 'day') return `${dayEvents.length} event(s)`;
		if (currentView === 'week') return `${weekDays.reduce((sum, day) => sum + day.events.length, 0)} event(s)`;
		return `${monthGrid.reduce((sum, week) => sum + week.reduce((inner, day) => inner + day.events.length, 0), 0)} event(s)`;
	}

	function toScheduledVisitEvent(request: PageProps['data']['scheduledVisitRequests'][number]): CalendarEvent {
		const schedule = request.siteVisitSchedule;
		return {
			id: `quote-request-${request.id}`,
			quoteRequestId: request.id,
			type: 'Site Visit',
			name: request.companyName || request.customerName,
			customerType: request.companyName && request.companyName !== request.customerName ? 'company' : 'customer',
			date: schedule?.visitDate ?? todayKey,
			startTime: formatScheduleTime(schedule?.windowStart ?? '09:00'),
			endTime: formatScheduleTime(schedule?.windowEnd ?? '10:00'),
			address: request.serviceAddress,
			zipCode: extractZipCode(request.serviceAddress),
			customerContact: schedule?.siteContact || request.contactName,
			phone: schedule?.siteContactPhone || request.phone,
			projectSummary: request.need || request.intakeSummary,
			status: 'Site Visit Scheduled',
			crew: schedule?.assignedFieldResource || request.assignedTo,
			estimatedValue: 'Pending estimate',
			notes: schedule?.notes || request.nextAction,
			source: 'quote-request'
		};
	}

	function formatScheduleTime(value: string) {
		const [hoursText = '', minutesText = ''] = value.split(':');
		const hours = Number(hoursText);
		const minutes = Number(minutesText);
		if (Number.isNaN(hours) || Number.isNaN(minutes)) return value;

		return new Date(2026, 0, 1, hours, minutes).toLocaleTimeString('en-US', {
			hour: 'numeric',
			minute: '2-digit'
		});
	}

	function extractZipCode(address: string) {
		return address.match(/\b\d{5}(?:-\d{4})?\b/)?.[0] ?? '—';
	}

	function formatDateKey(date: Date) {
		const year = date.getFullYear();
		const month = String(date.getMonth() + 1).padStart(2, '0');
		const day = String(date.getDate()).padStart(2, '0');
		return `${year}-${month}-${day}`;
	}

	function parseDateKey(value: string) {
		const [year, month, day] = value.split('-').map(Number);
		if (!year || !month || !day) return null;
		return new Date(year, month - 1, day);
	}

	function offsetDateKey(days: number) {
		const date = new Date(today.getFullYear(), today.getMonth(), today.getDate());
		date.setDate(date.getDate() + days);
		return formatDateKey(date);
	}

	function createWeather(offset: number): WeatherSummary {
		const patterns: WeatherSummary[] = [
			{ kind: 'partly-cloudy', label: 'Partly cloudy', high: 63, low: 45 },
			{ kind: 'sunny', label: 'Sunny', high: 68, low: 46 },
			{ kind: 'rain', label: 'Showers', high: 58, low: 42 },
			{ kind: 'partly-cloudy', label: 'Dry window', high: 66, low: 48 },
			{ kind: 'sunny', label: 'Clear', high: 71, low: 50 },
			{ kind: 'rain', label: 'Rain risk', high: 55, low: 41 },
			{ kind: 'snow', label: 'Cold snap', high: 39, low: 28 }
		];
		return patterns[Math.abs(offset) % patterns.length];
	}

	function startOfWeek(date: Date) {
		const clone = new Date(date.getFullYear(), date.getMonth(), date.getDate());
		const day = clone.getDay();
		clone.setDate(clone.getDate() - day);
		return clone;
	}

	function buildMonthGrid(date: Date, events: CalendarEvent[], weatherMap: Map<string, WeatherSummary>) {
		const firstOfMonth = new Date(date.getFullYear(), date.getMonth(), 1);
		const gridStart = startOfWeek(firstOfMonth);
		const weeks: Array<
			Array<{
				date: Date;
				key: string;
				dayNumber: number;
				inCurrentMonth: boolean;
				isToday: boolean;
				isSelected: boolean;
				events: CalendarEvent[];
				weather: WeatherSummary;
			}>
		> = [];

		for (let weekIndex = 0; weekIndex < 6; weekIndex++) {
			const week = [];
			for (let dayIndex = 0; dayIndex < 7; dayIndex++) {
				const cellDate = new Date(gridStart.getFullYear(), gridStart.getMonth(), gridStart.getDate() + weekIndex * 7 + dayIndex);
				const key = formatDateKey(cellDate);
				week.push({
					date: cellDate,
					key,
					dayNumber: cellDate.getDate(),
					inCurrentMonth: cellDate.getMonth() === date.getMonth(),
					isToday: key === todayKey,
					isSelected: key === selectedDateKey,
					events: events.filter((event) => event.date === key).sort(compareEvents),
					weather: weatherMap.get(key) ?? createWeather(0)
				});
			}
			weeks.push(week);
		}

		return weeks;
	}

	function buildWeekDays(date: Date, events: CalendarEvent[], weatherMap: Map<string, WeatherSummary>) {
		const start = startOfWeek(date);
		return Array.from({ length: 7 }, (_, index) => {
			const current = new Date(start.getFullYear(), start.getMonth(), start.getDate() + index);
			const key = formatDateKey(current);
			return {
				date: current,
				key,
				label: current.toLocaleDateString('en-US', { weekday: 'short' }),
				dayNumber: current.getDate(),
				isToday: key === todayKey,
				isSelected: key === selectedDateKey,
				events: events.filter((event) => event.date === key).sort(compareEvents),
				weather: weatherMap.get(key) ?? createWeather(0)
			};
		});
	}

	function compareEvents(a: CalendarEvent, b: CalendarEvent) {
		return toMinutes(a.startTime) - toMinutes(b.startTime);
	}

	function toMinutes(value: string) {
		const [time, suffix] = value.split(' ');
		const [hoursRaw, minutesRaw] = time.split(':').map(Number);
		let hours = hoursRaw % 12;
		if (suffix === 'PM') hours += 12;
		return hours * 60 + minutesRaw;
	}

	function getWeekLabel(date: Date) {
		const start = startOfWeek(date);
		const end = new Date(start.getFullYear(), start.getMonth(), start.getDate() + 6);
		const startLabel = start.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
		const endLabel = end.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
		return `${startLabel} – ${endLabel}`;
	}
</script>

<svelte:head>
	<title>BDR Admin · Calendar</title>
</svelte:head>

<div class="relative min-h-[calc(100vh-10rem)]">
	<div class="grid gap-4 xl:grid-cols-[280px_minmax(0,1fr)]">
		<aside class="space-y-4 rounded-lg border border-[var(--shell-border)] bg-[var(--module-bg)] p-4 shadow-[var(--shell-shadow)]">
			<section class="space-y-3 rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
				<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">View</p>
				<div class="grid grid-cols-3 gap-2">
					{#each ['day', 'week', 'month'] as view}
						<button
							type="button"
							class={`rounded-full px-3 py-2 text-sm font-semibold transition ${currentView === view ? 'bg-[var(--accent-solid)] text-white' : 'bg-[var(--shell-panel-strong)] text-[var(--text-muted)] hover:text-[var(--text-strong)]'}`}
							onclick={() => setView(view as CalendarView)}
						>
							{view[0].toUpperCase() + view.slice(1)}
						</button>
					{/each}
				</div>
			</section>

			<section class="space-y-3 rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
				<label class="grid gap-2">
					<span class="text-xs font-semibold uppercase tracking-[0.16em] text-[var(--muted)]">Date picker</span>
					<input
						type="date"
						value={pickerValue}
						onchange={(event) => handleDateInput((event.currentTarget as HTMLInputElement).value)}
						class="rounded-lg border border-[var(--shell-border)] bg-[var(--module-bg)] px-3 py-2.5 text-sm text-[var(--text-strong)] outline-none"
					/>
				</label>
				<div class="grid grid-cols-2 gap-2">
					<button type="button" class="inline-flex items-center justify-center gap-2 rounded-lg border border-[var(--shell-border)] bg-[var(--module-bg)] px-3 py-2.5 text-sm font-medium text-[var(--text-strong)]" onclick={() => shiftRange(-1)}>
						<ChevronLeft size={16} /> Prev
					</button>
					<button type="button" class="inline-flex items-center justify-center gap-2 rounded-lg border border-[var(--shell-border)] bg-[var(--module-bg)] px-3 py-2.5 text-sm font-medium text-[var(--text-strong)]" onclick={() => shiftRange(1)}>
						Next <ChevronRight size={16} />
					</button>
				</div>
			</section>

			<section class="space-y-3 rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
				<div class="flex items-center justify-between gap-3">
					<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Event filters</p>
				</div>
				<div class="space-y-2">
					{#each Object.entries(eventTypeMeta) as [type, meta]}
						{@const Icon = meta.icon}
						<button
							type="button"
							class={`flex w-full items-center justify-between rounded-lg border px-3 py-3 text-left transition ${enabledTypes[type as EventType] ? 'border-[var(--accent-border)] bg-[var(--accent-soft)]' : 'border-[var(--shell-border)] bg-[var(--module-bg)]'}`}
							onclick={() => toggleType(type as EventType)}
						>
							<div class="flex items-center gap-3">
								<div class={`rounded-lg border px-2.5 py-2 ${meta.colorClass}`}>
									<Icon size={16} />
								</div>
								<div>
									<p class="text-sm font-semibold text-[var(--text-strong)]">{type}</p>
									<p class="text-xs text-[var(--text-muted)]">{calendarEvents.filter((event) => event.type === type).length} event(s)</p>
								</div>
							</div>
							<div class={`h-2.5 w-2.5 rounded-full ${enabledTypes[type as EventType] ? 'bg-[var(--accent-solid)]' : 'bg-slate-300'}`}></div>
						</button>
					{/each}
				</div>
			</section>
		</aside>

		<section class="rounded-lg border border-[var(--shell-border)] bg-[var(--module-bg)] p-4 shadow-[var(--shell-shadow)] lg:p-5">
			{#if scheduledRequest}
				<div class="mb-4 rounded-lg border border-[var(--accent-border)] bg-[var(--accent-soft)] p-4">
					<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--accent-text)]">Scheduling from quote request</p>
					<div class="mt-2 flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
						<div class="max-w-3xl">
							<h3 class="text-lg font-semibold text-[var(--text-strong)]">{scheduledRequest.customerName} · {scheduledRequest.projectType}</h3>
							<p class="mt-1 text-sm leading-6 text-[var(--text-muted)]">Submitted {scheduledRequestDateLabel} · {scheduledRequest.serviceAddress}</p>
							<p class="mt-3 text-sm leading-6 text-[var(--text-base)]">{scheduledRequest.intakeSummary}</p>
						</div>
						<a href="/bdr/admin/requests?role=office-admin" class="inline-flex rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel)] px-4 py-2.5 text-sm font-semibold text-[var(--text-strong)] transition hover:bg-[var(--shell-panel-strong)]">Back to quote requests</a>
					</div>
					<div class="mt-4 grid gap-3 lg:grid-cols-3">
						<div class="rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel)] p-3">
							<p class="text-[0.58rem] font-semibold uppercase tracking-[0.16em] text-[var(--muted)]">{scheduledRequest.siteVisitSchedule ? 'Scheduled visit' : 'Scheduling eligibility'}</p>
							{#if scheduledRequest.siteVisitSchedule}
								<p class="mt-2 text-sm leading-6 text-[var(--text-strong)]">
									{formatScheduleTime(scheduledRequest.siteVisitSchedule.windowStart)} – {formatScheduleTime(scheduledRequest.siteVisitSchedule.windowEnd)} on {scheduledRequest.siteVisitSchedule.visitDate}.
								</p>
								<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">Assigned to {scheduledRequest.siteVisitSchedule.assignedFieldResource}</p>
							{:else if scheduledRequestQualification?.isQualified}
								<p class="mt-2 text-sm leading-6 text-[var(--text-strong)]">Qualified for site visit scheduling. Use the request workspace to book the visit, then it will appear on this calendar.</p>
							{:else}
								<p class="mt-2 text-sm leading-6 text-amber-700">Qualification must be cleared before this request can move into site visit scheduling.</p>
								{#if scheduledRequestQualification?.blockerLabels.length}
									<p class="mt-2 text-xs leading-5 text-[var(--text-muted)]">{scheduledRequestQualification.blockerLabels.join(' · ')}</p>
								{/if}
							{/if}
						</div>
						<div class="rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel)] p-3">
							<p class="text-[0.58rem] font-semibold uppercase tracking-[0.16em] text-[var(--muted)]">Customer contact</p>
							<p class="mt-2 text-sm text-[var(--text-strong)]">{scheduledRequest.phone}</p>
							<p class="mt-1 text-sm text-[var(--text-muted)] break-all">{scheduledRequest.email}</p>
						</div>
						<div class="rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel)] p-3">
							<p class="text-[0.58rem] font-semibold uppercase tracking-[0.16em] text-[var(--muted)]">Current office step</p>
							<p class="mt-2 text-sm leading-6 text-[var(--text-strong)]">{scheduledRequest.nextAction}</p>
						</div>
					</div>
				</div>
			{/if}
			<div class="flex flex-col gap-4 border-b border-[var(--shell-border)] pb-4 sm:flex-row sm:items-end sm:justify-between">
				<div>
					<h2 class="mt-2 text-2xl font-semibold tracking-tight text-[var(--text-strong)]">{formatRangeTitle()}</h2>
				</div>
				<div class="inline-flex items-center gap-2 rounded-full border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-2 text-sm text-[var(--text-muted)]">
					<CalendarRange size={16} />
					<span>Today: {today.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })}</span>
				</div>
			</div>

			{#if currentView === 'month'}
				<div class="mt-4 overflow-hidden rounded-lg border border-[var(--shell-border)]">
					<div class="grid grid-cols-7 border-b border-[var(--shell-border)] bg-[var(--shell-panel)]">
						{#each ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'] as weekday}
							<div class="px-3 py-3 text-xs font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">{weekday}</div>
						{/each}
					</div>
					<div class="grid grid-cols-7">
						{#each monthGrid as week}
							{#each week as day}
								<div class={`min-h-40 border-b border-r border-[var(--shell-border)] p-2 ${day.inCurrentMonth ? 'bg-[var(--module-bg)]' : 'bg-[var(--shell-panel)]/50'}`}>
									<div class="flex items-start justify-between gap-2">
										<button
											type="button"
											class={`inline-flex h-8 w-8 items-center justify-center rounded-full text-sm font-semibold transition ${day.isSelected ? 'bg-[var(--accent-solid)] text-white' : day.isToday ? 'border border-[var(--accent-border)] text-[var(--accent-text)]' : 'text-[var(--text-strong)] hover:bg-[var(--shell-panel)]'}`}
											onclick={() => selectDate(day.date)}
										>
											{day.dayNumber}
										</button>
										<div class="inline-flex items-center gap-1 rounded-full bg-[var(--shell-panel)] px-2 py-1 text-[0.68rem] text-[var(--text-muted)]">
											☀
										</div>
									</div>

									<div class="mt-2 max-h-24 space-y-2 overflow-y-auto pr-1">
										{#each day.events as event}
											<button
												type="button"
												class={`w-full rounded-lg border px-2 py-2 text-left text-[0.72rem] transition ${eventTypeMeta[event.type].colorClass}`}
												onclick={() => openEvent(event)}
											>
												<div class="flex items-center gap-1.5">
													•
													<span class="truncate font-semibold">{event.name}</span>
												</div>
												<p class="mt-1 truncate text-[0.65rem] font-medium uppercase tracking-[0.14em] opacity-80">STATUS: {event.status}</p>
											</button>
										{/each}
									</div>
								</div>
							{/each}
						{/each}
					</div>
				</div>
			{:else if currentView === 'week'}
				<div class="mt-4 grid gap-3 xl:grid-cols-7">
					{#each weekDays as day}
						<div class="rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel)] p-3">
							<div class="flex items-start justify-between gap-2">
								<button type="button" class={`text-left ${day.isSelected ? 'text-[var(--accent-text)]' : 'text-[var(--text-strong)]'}`} onclick={() => selectDate(day.date)}>
									<p class="text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">{day.label}</p>
									<p class="mt-1 text-lg font-semibold">{day.dayNumber}</p>
								</button>
								<div class="inline-flex items-center gap-1 rounded-full bg-[var(--module-bg)] px-2 py-1 text-[0.68rem] text-[var(--text-muted)]">☀</div>
							</div>
							<div class="mt-3 space-y-2">
								{#if day.events.length}
									{#each day.events as event}
										{@const EventIcon = eventTypeMeta[event.type].icon}
										<button type="button" class={`w-full rounded-lg border px-3 py-3 text-left transition ${eventTypeMeta[event.type].colorClass}`} onclick={() => openEvent(event)}>
											<div class="flex items-center gap-2">
												<EventIcon size={14} />
												<span class="truncate font-semibold">{event.name}</span>
											</div>
											<p class="mt-2 text-[0.72rem]">{event.startTime} · {event.type}</p>
										</button>
									{/each}
								{:else}
									<div class="rounded-lg border border-dashed border-[var(--shell-border)] px-3 py-6 text-center text-sm text-[var(--text-muted)]">No events</div>
								{/if}
							</div>
						</div>
					{/each}
				</div>
			{:else}
				<div class="mt-4 rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
					<div class="flex items-start justify-between gap-3 border-b border-[var(--shell-border)] pb-4">
						<div>
							<p class="text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Day view</p>
							<h3 class="mt-2 text-xl font-semibold text-[var(--text-strong)]">{dayLabel}</h3>
						</div>
						<div class="inline-flex items-center gap-2 rounded-full bg-[var(--module-bg)] px-3 py-2 text-sm text-[var(--text-muted)]">
							☀
							<span>{selectedDayWeather.label} · {selectedDayWeather.high}° / {selectedDayWeather.low}°</span>
						</div>
					</div>
					<div class="mt-4 space-y-3">
						{#if dayEvents.length}
							{#each dayEvents as event}
								<button type="button" class="w-full rounded-lg border border-[var(--shell-border)] bg-[var(--module-bg)] p-4 text-left transition hover:bg-[var(--shell-panel-strong)]" onclick={() => openEvent(event)}>
									<div class="flex flex-wrap items-start justify-between gap-3">
										<div>
											<div class="flex items-center gap-2">
												<span class={`rounded-full border px-2.5 py-1 text-[0.68rem] font-semibold uppercase tracking-[0.16em] ${eventTypeMeta[event.type].colorClass}`}>
													• {event.type}
												</span>
												<span class="text-xs text-[var(--text-muted)]">{event.startTime} – {event.endTime}</span>
											</div>
											<p class="mt-3 text-lg font-semibold text-[var(--text-strong)]">{event.name}</p>
											<p class="mt-1 text-sm text-[var(--text-muted)]">{event.projectSummary}</p>
										</div>
										<span class="text-sm font-medium text-[var(--accent-text)]">Open details</span>
									</div>
								</button>
							{/each}
						{:else}
							<div class="rounded-lg border border-dashed border-[var(--shell-border)] px-4 py-12 text-center text-sm text-[var(--text-muted)]">No events for the selected day.</div>
						{/if}
					</div>
				</div>
			{/if}
		</section>
	</div>

	{#if selectedEvent}
		<button type="button" class="fixed inset-0 z-40 bg-slate-950/20" aria-label="Close calendar details" onclick={closeDrawer}></button>
		<aside class="fixed inset-y-16 right-4 z-50 flex w-full max-w-md flex-col overflow-hidden rounded-lg border border-[var(--shell-border)] bg-[var(--drawer-bg)] shadow-[0_30px_80px_rgba(15,23,42,0.28)]">
			<div class="flex items-start justify-between gap-3 border-b border-[var(--shell-border)] px-5 py-4">
				<div>
					<p class="text-[0.66rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Event details</p>
					<h3 class="mt-2 text-xl font-semibold text-[var(--text-strong)]">{selectedEvent.name}</h3>
					<p class="mt-1 text-sm text-[var(--text-muted)]">{selectedEvent.date} · {selectedEvent.startTime} – {selectedEvent.endTime}</p>
				</div>
				<button type="button" class="rounded-full border border-[var(--shell-border)] bg-[var(--shell-panel)] p-2 text-[var(--text-muted)] hover:text-[var(--text-strong)]" onclick={closeDrawer}>
					<X size={16} />
				</button>
			</div>

			<div class="flex-1 space-y-4 overflow-y-auto px-5 py-5">
				<div class="flex items-center gap-2">
					<span class={`inline-flex items-center gap-2 rounded-full border px-3 py-1.5 text-xs font-semibold uppercase tracking-[0.16em] ${eventTypeMeta[selectedEvent.type].colorClass}`}>
						•
						{selectedEvent.type}
					</span>
					<span class="rounded-full bg-[var(--shell-panel)] px-3 py-1.5 text-xs font-semibold text-[var(--text-muted)]">{selectedEvent.status}</span>
				</div>

				<section class="rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
					<div class="flex items-center justify-between gap-3">
						<div>
							<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Weather · zip {selectedEvent.zipCode}</p>
							<p class="mt-2 text-sm text-[var(--text-base)]">{(mockedWeather.get(selectedEvent.date) ?? selectedDayWeather).label}</p>
						</div>
						<div class="inline-flex items-center gap-2 rounded-full bg-[var(--module-bg)] px-3 py-2 text-sm text-[var(--text-muted)]">
							☀
							<span>{(mockedWeather.get(selectedEvent.date) ?? selectedDayWeather).high}° / {(mockedWeather.get(selectedEvent.date) ?? selectedDayWeather).low}°</span>
						</div>
					</div>
				</section>

				<section class="rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
					<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Customer details</p>
					<div class="mt-3 space-y-3 text-sm text-[var(--text-base)]">
						<div class="flex items-start gap-3"><UserRound size={16} class="mt-0.5 text-[var(--muted)]" /><div><p class="font-semibold text-[var(--text-strong)]">{selectedEvent.customerContact}</p><p class="text-[var(--text-muted)]">{selectedEvent.phone}</p></div></div>
						<div class="flex items-start gap-3"><MapPin size={16} class="mt-0.5 text-[var(--muted)]" /><div><p class="font-semibold text-[var(--text-strong)]">{selectedEvent.address}</p><p class="text-[var(--text-muted)]">Zip {selectedEvent.zipCode}</p></div></div>
						<div class="flex items-start gap-3"><Building2 size={16} class="mt-0.5 text-[var(--muted)]" /><div><p class="font-semibold text-[var(--text-strong)]">{selectedEvent.customerType === 'company' ? 'Company account' : 'Residential customer'}</p><p class="text-[var(--text-muted)]">{selectedEvent.name}</p></div></div>
					</div>
				</section>

				<section class="rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
					<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Project details</p>
					<div class="mt-3 space-y-3 text-sm text-[var(--text-base)]">
						<div>
							<p class="font-semibold text-[var(--text-strong)]">Summary</p>
							<p class="mt-1 leading-6 text-[var(--text-muted)]">{selectedEvent.projectSummary}</p>
						</div>
						<div class="grid gap-3 sm:grid-cols-2">
							<div class="rounded-lg border border-[var(--shell-border)] bg-[var(--module-bg)] p-3">
								<p class="text-[0.62rem] uppercase tracking-[0.16em] text-[var(--muted)]">Crew</p>
								<p class="mt-1 font-semibold text-[var(--text-strong)]">{selectedEvent.crew}</p>
							</div>
							<div class="rounded-lg border border-[var(--shell-border)] bg-[var(--module-bg)] p-3">
								<p class="text-[0.62rem] uppercase tracking-[0.16em] text-[var(--muted)]">Estimated value</p>
								<p class="mt-1 font-semibold text-[var(--text-strong)]">{selectedEvent.estimatedValue}</p>
							</div>
						</div>
						<div>
							<p class="font-semibold text-[var(--text-strong)]">Operational note</p>
							<p class="mt-1 leading-6 text-[var(--text-muted)]">{selectedEvent.notes}</p>
						</div>
						{#if selectedEvent.quoteRequestId}
							<a href="/bdr/admin/requests?role=office-admin" class="inline-flex rounded-lg border border-[var(--shell-border)] bg-[var(--module-bg)] px-4 py-2.5 text-sm font-semibold text-[var(--text-strong)] transition hover:bg-[var(--shell-panel-strong)]">
								Open quote request queue
							</a>
						{/if}
					</div>
				</section>
			</div>
		</aside>
	{/if}
</div>
