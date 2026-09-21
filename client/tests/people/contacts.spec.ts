import {test,expect,type BrowserContext} from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
async function signIn(context:BrowserContext, contactsOnly=false, tenant='bdr') {
 const claims={sub:'11111111-1111-1111-1111-111111111111',role:[contactsOnly?'admin':'owner'],fixtureContacts:contactsOnly,tid:tenant==='bdr'?'7d40ea6c-313f-4f53-bf7d-5d1ecb9cc50b':'88888888-8888-8888-8888-888888888882',exp:Math.floor(Date.now()/1000)+3600};
 const token=`${Buffer.from('{}').toString('base64url')}.${Buffer.from(JSON.stringify(claims)).toString('base64url')}.ui-fixture`;
 await context.addCookies([{name:'tko_auth_token',value:token,url:'http://127.0.0.1:5191'}]);
}
for(const tenant of ['bdr','thinkpink']) {
 test(`${tenant} contacts persist business details and share the person with People & Access`,async({page,context,request})=>{
  await request.get('http://127.0.0.1:5190/reset');await signIn(context,false,tenant);
  await page.goto(`/${tenant}/admin/customers`);
  await expect(page.getByRole('heading',{name:'Contacts',exact:true})).toBeVisible();
  await page.getByRole('link',{name:'Add contact',exact:true}).click();
  await page.waitForLoadState('networkidle');
  await page.getByLabel('First name',{exact:true}).fill('Casey');await page.getByLabel('Last name',{exact:true}).fill('Jordan');
  await page.getByLabel('Email',{exact:true}).fill('casey@example.invalid');
  await page.getByRole('checkbox',{name:'Vendor',exact:true}).check();
  await page.getByLabel('Street address',{exact:true}).fill('1 Test Street');await page.getByLabel('Notes',{exact:true}).fill('Use the west entrance.');
  await page.getByLabel('Linked customer record').selectOption('22222222-2222-2222-2222-222222222222');
  await page.getByRole('button',{name:'Save contact',exact:true}).click();
  await expect(page.getByRole('status')).toHaveText('Contact saved.');await page.reload();
  await expect(page.getByLabel('Notes',{exact:true})).toHaveValue('Use the west entrance.');
  await expect(page.getByText('No linked jobs yet.',{exact:true})).toBeVisible();
  await expect(page.getByLabel('Permission policy')).toHaveCount(0);
  expect((await new AxeBuilder({page}).analyze()).violations.filter(v=>['critical','serious'].includes(v.impact??''))).toEqual([]);
  await page.screenshot({path:test.info().outputPath('contacts-desktop.png'),fullPage:true});
  await page.setViewportSize({width:390,height:844});
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth)).toBe(true);
  await page.screenshot({path:test.info().outputPath('contacts-mobile.png'),fullPage:true});
  await page.getByRole('link',{name:'Manage this person’s app access'}).click();
  await expect(page.getByLabel('First name',{exact:true})).toHaveValue('Casey');
  await expect(page.getByRole('checkbox',{name:'vendor',exact:true})).toBeChecked();
  await page.getByRole('button',{name:/Existing Owner/}).click();
  await page.screenshot({path:test.info().outputPath('people-corner.png'),fullPage:true});
 });
}
test('Contacts-only administrators can work without user-management permission',async({page,context,request})=>{
 await request.get('http://127.0.0.1:5190/reset');await signIn(context,true);
 await page.goto('/bdr/admin');await expect(page).toHaveURL(/\/bdr\/admin\/customers$/);
 await expect(page.getByRole('heading',{name:'Contacts',exact:true})).toBeVisible();
 await expect(page.getByRole('link',{name:'Add contact',exact:true})).toBeVisible();
 await expect(page.getByRole('link',{name:'People & access',exact:false})).toHaveCount(0);
 expect((await page.goto('/bdr/admin/users'))?.status()).toBe(403);
});

test('owner deletion confirms removal of another owner and can be cancelled',async({page,context,request})=>{
 await request.get('http://127.0.0.1:5190/reset');await signIn(context);
 await page.goto('/bdr/admin/users');await page.waitForLoadState('networkidle');
 await page.getByRole('button',{name:/Second Owner/}).click();
 await page.getByRole('button',{name:'Delete user',exact:true}).click();
 await page.getByRole('button',{name:'Cancel',exact:true}).click();
 await expect(page.getByRole('button',{name:/Second Owner/})).toBeVisible();
 await page.getByRole('button',{name:'Delete user',exact:true}).click();
 await page.getByRole('button',{name:'Confirm delete',exact:true}).click();
 await expect(page.getByRole('status')).toContainText('deleted from this company');
 await page.reload();await expect(page.getByRole('button',{name:/Second Owner/})).toHaveCount(0);
 await expect(page.getByRole('button',{name:/Existing Owner/})).toBeVisible();
});
