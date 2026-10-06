import {test} from 'node:test';
import assert from 'node:assert/strict';
import {queueFor,stageLabel,leadStages} from '../src/lib/leads.ts';
import {previewLeadImport} from '../src/lib/lead-import.ts';
import {getAdminSessionFromToken} from '../src/lib/server/session-policy.ts';
test('canonical states map to actionable queues regardless of display label',()=>{
 assert.equal(queueFor('DISCOVERY'),'Discovery / site visit');
 assert.equal(stageLabel('DISCOVERY',{stageLabels:{DISCOVERY:'Site walkthrough'}} as any),'Site walkthrough');
 assert.ok(leadStages.includes('DISCOVERY'));assert.equal(queueFor('LOST'),'Lost');
});
test('import adapters map without persisting, merging or discarding ambiguous rows',()=>{
 const preview=previewLeadImport([{Project:'Repair',Email:'same@example.invalid',ID:'A'},{Project:'Other work',Email:'same@example.invalid',ID:'B'},{}],{title:'Project',email:'Email',externalId:'ID'},'hubspot');
 assert.equal(preview.length,3);assert.equal(preview[0].lead.attribution.importProvider,'hubspot');assert.equal(preview[1].lead.attribution.externalId,'B');assert.ok(preview[2].problems.length);assert.ok(preview.every(row=>row.requiresReview));
});
test('staff module route admission does not open other admin modules or portal contacts',()=>{
 const token=(role:string)=>'header.'+Buffer.from(JSON.stringify({role,tenant_id:'test',exp:4102444800})).toString('base64url')+'.signature';
 assert.ok(getAdminSessionFromToken(token('salesperson'),'/bdr/admin/leads'));
 assert.equal(getAdminSessionFromToken(token('salesperson'),'/bdr/admin/users'),null);
 assert.equal(getAdminSessionFromToken(token('contact'),'/bdr/admin/leads'),null);
});
