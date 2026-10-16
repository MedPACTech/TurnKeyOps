import { leadAssignees } from '../src/lib/leads.ts';
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {queueFor,stageLabel,leadStages} from '../src/lib/leads.ts';
import {previewLeadImport} from '../src/lib/lead-import.ts';
import {hasModuleAccess} from '../src/lib/module-access.ts';
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
test('employee session admission still requires module grants and excludes contact or arbitrary roles',()=>{
 const token=(role:string)=>'header.'+Buffer.from(JSON.stringify({role,tenant_id:'test',exp:4102444800})).toString('base64url')+'.signature';
 assert.ok(getAdminSessionFromToken(token('staff'),'/bdr/admin/leads'));
 assert.equal(hasModuleAccess(['leads.read'],'users'),false);
 assert.equal(getAdminSessionFromToken(token('salesperson'),'/bdr/admin/leads'),null);
 assert.equal(getAdminSessionFromToken(token('salesperson'),'/bdr/admin/users'),null);
 assert.equal(getAdminSessionFromToken(token('contact'),'/bdr/admin/leads'),null);
});

test('assignment choices avoid duplicate employees and preserve an existing membership assignment',()=>{
 const profiles=[{id:'person-1',name:'Sales employee'}];const members=[{id:'member-1',profileId:'person-1',name:'Sales employee'},{id:'member-2',name:'Company owner'}];
 assert.deepEqual(leadAssignees(profiles,members),[{value:'profile:person-1',name:'Sales employee'},{value:'member:member-2',name:'Company owner'}]);
 assert.deepEqual(leadAssignees(profiles,members,'member-1'),[{value:'member:member-1',name:'Sales employee'},{value:'member:member-2',name:'Company owner'}]);
});
