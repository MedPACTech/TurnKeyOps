/* Field PWA: cache public recovery assets only. Never cache sessions, HTML, APIs, or customer data. */
const cacheName = 'carlzipf-field-public-v1';
const offlinePath = '/carlzipf/tech/pwa/offline.html';
const publicAssets = [offlinePath, '/carlzipf/tech/pwa/icon-192.png', '/carlzipf/tech/pwa/icon-512.png'];

self.addEventListener('install', (event) => {
	event.waitUntil(caches.open(cacheName).then((cache) => cache.addAll(publicAssets)).then(() => self.skipWaiting()));
});
self.addEventListener('activate', (event) => {
	event.waitUntil(caches.keys().then((keys) => Promise.all(keys.filter((key) => key.startsWith('carlzipf-field-public-') && key !== cacheName).map((key) => caches.delete(key)))).then(() => self.clients.claim()));
});
self.addEventListener('fetch', (event) => {
	const url = new URL(event.request.url);
	if (url.origin !== self.location.origin || event.request.method !== 'GET') return;
	if (event.request.mode === 'navigate' && (url.pathname === '/carlzipf/tech' || url.pathname === '/carlzipf/tech/')) {
		event.respondWith(fetch(event.request).catch(async () => (await caches.match(offlinePath)) || new Response('Reconnect to verify your account and restore this device’s saved field draft.', { status: 503, headers: { 'Content-Type': 'text/plain' } })));
	}
});
