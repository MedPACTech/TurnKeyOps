import {test,expect} from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
const token=[{alg:'fixture'},{role:'owner',tenant_id:'7d40ea6c-313f-4f53-bf7d-5d1ecb9cc50b',email:'fixture@example.invalid',exp:4102444800}].map(x=>Buffer.from(JSON.stringify(x)).toString('base64url')).join('.')+'.fixture';
test.beforeEach(async({page,request})=>{
 await request.post('http://127.0.0.1:5298/reset');
 await page.context().addCookies([{name:'tko_auth_token',value:token,domain:'127.0.0.1',path:'/'}]);
});
for(const theme of ['light','dark'])test(`Leads ${theme}: queue, detail and keyboard remain accessible`,async({page})=>{
 await page.addInitScript(theme=>document.documentElement.setAttribute('data-theme',theme),theme);
 await page.goto('/bdr/admin/leads');
 await expect(page.getByRole('heading',{name:'What needs to happen next?'})).toBeVisible();
 await expect(page.getByRole('button',{name:'Queue',exact:true})).toHaveAttribute('aria-pressed','true');
 await page.getByRole('button',{name:'Pipeline',exact:true}).click();await expect(page.getByRole('button',{name:'Pipeline',exact:true})).toHaveAttribute('aria-pressed','true');
 await page.getByRole('button',{name:'Queue',exact:true}).click();
 await page.getByRole('link',{name:'Respond to the customer',exact:true}).focus();await page.keyboard.press('Enter');
 await expect(page.getByRole('heading',{name:'Browser fixture opportunity',exact:true})).toBeVisible();
 await expect(page.getByRole('heading',{name:'What’s missing'})).toBeVisible();
 let results=await new AxeBuilder({page}).include('.leads').withTags(['wcag2a','wcag2aa','wcag21aa']).analyze();expect(results.violations).toEqual([]);
 await page.getByLabel('Note',{exact:true}).fill('Fixture field note');await page.getByRole('button',{name:'Save note',exact:true}).click();await expect(page.getByText('Fixture field note',{exact:true})).toBeVisible();
 expect(await page.locator('.leads').evaluate(el=>el.scrollWidth<=el.clientWidth)).toBeTruthy();
 await page.screenshot({path:`test-results/leads-${theme}-${test.info().project.name}.png`,fullPage:true});
});
test('Manual capture creates a distinct opportunity and stage changes preserve it',async({page})=>{
 await page.goto('/bdr/admin/leads');await page.getByRole('button',{name:'New lead',exact:true}).click();
 await page.getByLabel('Opportunity name',{exact:true}).fill('Second fixture opportunity');await page.getByLabel('Customer / contact name').fill('Fixture contact');await page.getByRole('button',{name:'Create lead',exact:true}).click();
 await expect(page.getByRole('heading',{name:'Second fixture opportunity',exact:true})).toBeVisible();
 await page.getByRole('combobox',{name:'Next stage',exact:true}).selectOption('LOST');await page.getByRole('combobox',{name:'Outcome reason',exact:true}).selectOption('Price');await page.getByRole('button',{name:'Move forward',exact:true}).click();
 await expect(page.getByRole('button',{name:'Reopen lead',exact:true})).toBeVisible();await page.getByRole('button',{name:'Reopen lead',exact:true}).click();
 await expect(page.getByRole('button',{name:'Move forward',exact:true})).toBeVisible();
});

test('PWA recovers offline without caching customer records',async({page,context})=>{
 await page.goto('/bdr/admin/leads');
 await expect(page.getByRole('button',{name:'New lead',exact:true})).toBeEnabled();
 await page.evaluate(async()=>{await navigator.serviceWorker.ready; if(!navigator.serviceWorker.controller) await new Promise<void>(resolve=>navigator.serviceWorker.addEventListener('controllerchange',()=>resolve(),{once:true}));});
 const cached=await page.evaluate(async()=>{const names=await caches.keys();return (await Promise.all(names.map(async name=>(await (await caches.open(name)).keys()).map(r=>new URL(r.url).pathname)))).flat();});
 expect(cached).toContain('/leads/offline.html');expect(cached.every(path=>path==='/leads/offline.html')).toBeTruthy();
 await context.setOffline(true);await page.reload();
 await expect(page.getByRole('heading')).toContainText('offline');
 await context.setOffline(false);
});

test('Assign, reassign and unassign an associate with matching card avatars',async({page})=>{
 await page.goto('/bdr/admin/leads/11111111-2222-4333-8444-555555555555');
 const assignment=page.locator('form').filter({has:page.getByRole('button',{name:'Save assignment',exact:true})});
 for(const [id,name,initials] of [['aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa','Jordan Ellis','JE'],['bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb','Morgan Chen','MC'],['','Unassigned','—']]){
  await assignment.getByLabel('Assigned associate').selectOption(id ? `profile:${id}` : '');
  await assignment.getByRole('button',{name:'Save assignment',exact:true}).click();
  await expect(page.locator('.leads header')).toContainText(name);
  await page.getByRole('link',{name:'All leads',exact:true}).click();
  const card=page.locator('.leads article').first();
  await expect(card).toContainText(name);await expect(card.locator('.avatar')).toHaveText(initials);
  await card.getByRole('link',{name:'Respond to the customer',exact:true}).click();
 }
});
