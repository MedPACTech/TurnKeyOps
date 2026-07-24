<script lang="ts">
	import { onMount } from 'svelte';
	import { serviceCenter, serviceRadiusMeters } from '$lib/tenants/thinkpink/content';

	let el: HTMLDivElement;

	onMount(() => {
		let map: import('leaflet').Map | undefined;

		// Leaflet touches `window` at import time, so it can only load in the browser.
		(async () => {
			const L = (await import('leaflet')).default;
			await import('leaflet/dist/leaflet.css');

			map = L.map(el, { scrollWheelZoom: false, zoomControl: true });
			L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
				attribution: '&copy; OpenStreetMap contributors'
			}).addTo(map);

			const circle = L.circle(serviceCenter, {
				radius: serviceRadiusMeters,
				color: '#E5148C',
				weight: 3,
				fillColor: '#E5148C',
				fillOpacity: 0.12
			}).addTo(map);

			L.circleMarker(serviceCenter, {
				radius: 6,
				color: '#E5148C',
				weight: 3,
				fillColor: '#fff',
				fillOpacity: 1
			})
				.addTo(map)
				.bindTooltip('Columbus, OH');

			map.fitBounds(circle.getBounds(), { padding: [12, 12] });
		})();

		return () => map?.remove();
	});
</script>

<div
	bind:this={el}
	class="border-line-2 h-[340px] w-full overflow-hidden rounded-[10px] border bg-[#f4f1f2] shadow-[0_12px_40px_rgba(28,20,24,0.12)] sm:h-[460px]"
	role="img"
	aria-label="Map of the Think Pink Land Clearing service area — roughly 50 miles around Columbus, Ohio"
></div>

