// Match the existing field PWA policy: cache recovery assets only, never customer HTML, API responses or sessions.
const cacheName = 'turnkey-supply-recovery-v1';
const offlinePath = '/supply/offline.html';
self.addEventListener('install', event => event.waitUntil(caches.open(cacheName).then(cache => cache.add(offlinePath)).then(() => self.skipWaiting())));
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));
self.addEventListener('fetch', event => {
 if (event.request.method !== 'GET' || event.request.mode !== 'navigate') return;
 const url = new URL(event.request.url);
 if (url.origin !== self.location.origin || !url.href.startsWith(self.registration.scope)) return;
 event.respondWith(fetch(event.request).catch(async () => await caches.match(offlinePath) || new Response('Reconnect to load Inventory and Purchasing.', {status:503})));
});
