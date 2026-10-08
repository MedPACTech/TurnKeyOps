import http from 'node:http';
import assert from 'node:assert/strict';
const port = Number(process.env.CARLZIPF_BUILT_PORT ?? 5209);
const get = (path, host = 'carlzipflockshop.com') => new Promise((resolve, reject) => {
 http.get({ hostname: '127.0.0.1', port, path, headers: { Host: host } }, response => {
  let body = ''; response.on('data', chunk => body += chunk);
  response.on('end', () => resolve({ status: response.statusCode, headers: response.headers, body }));
 }).on('error', reject);
});
let response = await get('/');
assert.equal(response.status, 200); assert.match(response.body, /More Than Locks/); assert.match(response.body, /href="\/residential"/);
response = await get('/residential'); assert.equal(response.status, 200); assert.match(response.body, /Good doors/);
response = await get('/sitemap.xml'); assert.equal(response.status, 200); assert.match(response.body, /https:\/\/carlzipflockshop.com\/residential/);
response = await get('/robots.txt'); assert.match(response.body, /Sitemap: https:\/\/carlzipflockshop.com\/sitemap.xml/);
response = await get('/carlzipf/public/residential?source=test'); assert.equal(response.status, 308); assert.equal(response.headers.location, 'https://carlzipflockshop.com/residential?source=test');
response = await get('/', 'bdrconcrete.com'); assert.equal(response.status, 200); assert.doesNotMatch(response.body, /Complete Opening Solutions/);
console.log('Built app: Carl Zipf root/residential, sitemap, robots, canonical alias redirect and separate BDR root passed.');
