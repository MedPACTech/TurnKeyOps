import {test} from 'node:test';
import assert from 'node:assert/strict';
import {adminModule,hasModuleAccess,firstAllowedAdminPage} from '../src/lib/module-access.ts';
test('company routes map to the same modules as the API',()=>{
 assert.equal(adminModule('/bdr/admin/users'),'users');
 assert.equal(adminModule('/thinkpink/admin/contact'),'contacts');
 assert.equal(adminModule('/bdr/admin/customers'),'contacts');
 assert.equal(adminModule('/bdr/admin/website'),'settings');
 assert.equal(adminModule('/carlzipf/admin/estimates'),'estimates');
 assert.equal(adminModule('/carlzipf/admin/invoices'),'invoices');
 assert.equal(adminModule('/carlzipf/admin/users'),'users');
 assert.equal(adminModule('/turnkeyops/admin/access'),null);
});
test('view does not authorize mutation or aggregate data',()=>{
 assert.equal(hasModuleAccess(['jobs.read'],'jobs'),true);
 assert.equal(hasModuleAccess(['jobs.read'],'jobs',true),false);
 assert.equal(hasModuleAccess(['dashboard.read'],'dashboard'),false);
 assert.equal(hasModuleAccess(['bob.read','bob.write'],'bob'),true);
});

test('restricted users land on an allowed page instead of Bob',()=>{
 assert.equal(firstAllowedAdminPage(['jobs.read']),'jobs');
 assert.equal(firstAllowedAdminPage(['leads.read']),'leads');
 assert.equal(hasModuleAccess(['leads.read'],'leads',true),false);
 assert.equal(firstAllowedAdminPage(['contacts.read']),'customers');
 assert.equal(firstAllowedAdminPage(['bob.read','bob.write','users.read']),'bob');
 assert.equal(firstAllowedAdminPage([]),null);
});

test('technician navigation exposes Bob and Today without opening restricted modules',()=>{
 const grants=['bob.read','bob.write','calendar.read','jobs.read','inventory.read','estimates.read','contacts.read'];
 for(const module of ['bob','intelligence','calendar','jobs','inventory','estimates','contacts'])assert.equal(hasModuleAccess(grants,module),true);
 for(const module of ['purchasing','leads','invoices','finance','payroll','users','settings','billing'])assert.equal(hasModuleAccess(grants,module),false);
});
