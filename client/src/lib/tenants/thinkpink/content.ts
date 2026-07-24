export const site = {
	name: 'Think Pink Land Clearing',
	legalName: 'Think Pink Land Clearing LLC',
	phone: '(614) 555-0199',
	phoneHref: 'tel:+16145550199',
	region: 'Central Ohio',
	tagline: 'Central Ohio · Licensed & Insured',
	/** Flip to true to show the storm-response banner above the header. */
	stormBanner: false
};

export type Service = {
	num: string;
	name: string;
	desc: string;
};

export const services: Service[] = [
	{
		num: '01',
		name: 'Land Clearing',
		desc: 'Full-lot clearing for new builds, barns, driveways, and pasture conversion. Trees, brush, and debris — gone in one pass.'
	},
	{
		num: '02',
		name: 'Forestry Mulching',
		desc: 'Brush and undergrowth ground into clean mulch on the spot. No burn piles, no hauling, no mess left behind.'
	},
	{
		num: '03',
		name: 'Stump Grinding',
		desc: 'Stumps ground below grade so you can mow, plant, or build right over them. Any size, any count.'
	},
	{
		num: '04',
		name: 'Right-of-Way & Trails',
		desc: 'Fence lines, easements, access roads, and recreation trails cut clean and kept passable.'
	},
	{
		num: '05',
		name: 'Storm Cleanup',
		desc: 'Downed trees and storm debris cleared fast. Priority scheduling when weather does its worst.'
	}
];

export type Project = {
	title: string;
	meta: string;
	/** Optional real image pair — drop files in /static and reference them here. */
	before?: string;
	after?: string;
	placeholder: string;
};

export const projects: Project[] = [
	{
		title: '12-acre pasture reclaim',
		meta: 'Delaware County · Forestry mulching',
		placeholder: 'before/after pair'
	},
	{
		title: 'Building site prep',
		meta: 'Licking County · Land clearing',
		placeholder: 'before/after pair'
	},
	{
		title: 'Fence line clearing',
		meta: 'Madison County · Right-of-way',
		placeholder: 'before/after pair'
	}
];

export const counties = [
	'Franklin County',
	'Delaware County',
	'Licking County',
	'Fairfield County',
	'Union County',
	'Madison County',
	'Pickaway County',
	'Knox County'
];

export const navLinks = [
	{ href: '#services', label: 'Services' },
	{ href: '#work', label: 'Our Work' },
	{ href: '#area', label: 'Service Area' }
];

export const acreageOptions = [
	'Under 1 acre',
	'1–5 acres',
	'5–20 acres',
	'20–100 acres',
	'100+ acres'
];

export const serviceOptions = [
	'Land clearing',
	'Brush removal / forestry mulching',
	'Stump grinding / removal',
	'Right-of-way / trail clearing',
	'Storm cleanup',
	'Not sure — advise me'
];

export const timelineOptions = [
	"ASAP — it's urgent",
	'Within a month',
	'1–3 months',
	'Just planning ahead'
];

/** Columbus, OH — centre of the ~50 mile service radius. */
export const serviceCenter: [number, number] = [39.9612, -82.9988];
export const serviceRadiusMeters = 80467;

