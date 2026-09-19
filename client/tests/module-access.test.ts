import {test} from 'node:test';
import assert from 'node:assert/strict';
import {adminModule,hasModuleAccess,firstAllowedAdminPage} from '../src/lib/module-access.ts';
test('company routes map to the same modules as the API',()=>{
 assert.equal(adminModule('/bdr/admin/users'),'users');
 assert.equal(adminModule('/thinkpink/admin/contact'),'users');
 assert.equal(adminModule('/bdr/admin/customers'),'contacts');
 assert.equal(adminModule('/bdr/admin/website'),'settings');
 assert.equal(adminModule('/turnkeyops/admin/access'),null);
});
test('view does not authorize mutation or aggregate data',()=>{
 assert.equal(hasModuleAccess(['jobs.read'],'jobs'),true);
 assert.equal(hasModuleAccess(['jobs.read'],'jobs',true),false);
 assert.equal(hasModuleAccess(['dashboard.read'],'dashboard'),false);
 assert.equal(hasModuleAccess(['bob.read','bob.write'],'bob'),false);
});

test('restricted users land on an allowed page instead of Bob',()=>{
 assert.equal(firstAllowedAdminPage(['jobs.read']),'jobs');
 assert.equal(firstAllowedAdminPage(['contacts.read']),'customers');
 assert.equal(firstAllowedAdminPage(['bob.read','bob.write','users.read']),'users');
 assert.equal(firstAllowedAdminPage([]),null);
});
