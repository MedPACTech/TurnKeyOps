import assert from 'node:assert/strict';
import test from 'node:test';
import { resolveProductionPathname, resolveProductionRedirect } from '../src/lib/config/domains.ts';

test('canonical domains select each tenant and surface', () => {
 const cases = [
  ['turnkeyops.ai', '/turnkeyops/public'],
  ['admin.turnkeyops.ai', '/turnkeyops/admin'],
  ['thinkpinklandclearing.com', '/thinkpink/public'],
  ['admin.thinkpinklc.com', '/thinkpink/admin'],
  ['bdrconcrete.com', '/bdr/public'],
  ['admin.bdrconcrete.com', '/bdr/admin']
 ];
 for (const [host, path] of cases) {
  assert.equal(resolveProductionPathname(host, '/'), path);
  assert.equal(resolveProductionRedirect(new URL(`https://${host}/`)), null);
 }
});

test('aliases redirect once to HTTPS and preserve path and query', () => {
 for (const [host, canonical] of [
  ['thinkpinklc.com', 'thinkpinklandclearing.com'],
  ['www.thinkpinklc.com', 'thinkpinklandclearing.com'],
  ['www.thinkpinklandclearing.com', 'thinkpinklandclearing.com'],
  ['bdr.construction', 'bdrconcrete.com'],
  ['www.bdr.construction', 'bdrconcrete.com'],
  ['www.bdrconcrete.com', 'bdrconcrete.com'],
  ['www.turnkeyops.ai', 'turnkeyops.ai']
 ]) {
  const redirected = resolveProductionRedirect(new URL(`http://${host}:8080/services/land-clearing?source=a%20b`));
  assert.equal(redirected, `https://${canonical}/services/land-clearing?source=a%20b`);
  assert.equal(resolveProductionRedirect(new URL(redirected!)), null);
 }
});

test('unknown hosts, auth and assets retain existing behavior', () => {
 for (const host of ['localhost', 'turnkeyops-web.azurewebsites.net', 'constructor']) {
  assert.equal(resolveProductionRedirect(new URL(`https://${host}/`)), null);
  assert.equal(resolveProductionPathname(host, '/'), '/');
 }
 for (const path of ['/auth/login', '/_app/immutable/file.js', '/robots.txt']) {
  assert.equal(resolveProductionPathname('admin.thinkpinklc.com', path), path);
 }
});
