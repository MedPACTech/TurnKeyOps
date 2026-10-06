import {test,expect} from '@playwright/test';import AxeBuilder from '@axe-core/playwright';
const id='11111111-2222-4333-8444-555555555555';
const token=[{alg:'fixture'},{role:'owner',tenant_id:'7d40ea6c-313f-4f53-bf7d-5d1ecb9cc50b',exp:4102444800,email:'fixture@example.invalid'}].map(x=>Buffer.from(JSON.stringify(x)).toString('base64url')).join('.')+'.fixture';
test.beforeEach(async({page,request})=>{await request.post('http://127.0.0.1:5398/reset');await page.context().addCookies([{name:'tko_auth_token',value:token,domain:'127.0.0.1',path:'/'}]);});
for(const theme of ['light','dark'])test(`Estimate ${theme}: confirm, price, preview and issue`,async({page})=>{
 await page.addInitScript(theme=>localStorage.setItem('tko-admin-theme',theme),theme);
 await page.goto('/bdr/admin/estimates');await page.getByRole('link',{name:/Fixture project/}).focus();await page.keyboard.press('Enter');
 await expect(page.getByRole('heading',{name:'Fixture project',exact:true})).toBeVisible();await expect(page.locator('.admin-theme')).toHaveAttribute('data-theme',theme);
 await page.getByLabel('Quantity confirmed',{exact:true}).check();await page.getByRole('button',{name:'Save & calculate authoritative price'}).click();await expect(page.getByRole('heading',{name:'Preview and issue proposal'})).toBeVisible();
 await page.getByRole('button',{name:'Preview proposal',exact:true}).click();await expect(page.getByRole('article')).toContainText('Fixture contractor');
 const axe=await new AxeBuilder({page}).include('.estimates').withTags(['wcag2a','wcag2aa','wcag21aa']).analyze();expect(axe.violations).toEqual([]);
 expect(await page.locator('.estimates').evaluate(e=>e.scrollWidth<=e.clientWidth)).toBeTruthy();await page.screenshot({path:`test-results/estimate-${theme}-${test.info().project.name}.png`,fullPage:true});
 await page.getByRole('button',{name:'Issue customer proposal',exact:true}).click();await expect(page.getByRole('link',{name:'Open exact issued revision'})).toBeVisible();
 await page.getByRole('button',{name:'Create new revision'}).click();await expect(page.getByText(/draft · Revision 2/i)).toBeVisible();
});
test('Customer reviews, selects scope and signs revision',async({page})=>{
 await page.goto(`/bdr/estimate/${id}?token=fixture`);await expect(page.getByRole('heading',{name:'Choose your scope & approve'})).toBeVisible();
 const axe=await new AxeBuilder({page}).include('.customer-proposal').withTags(['wcag2a','wcag2aa','wcag21aa']).analyze();expect(axe.violations).toEqual([]);
 await page.getByLabel('Your full name').fill('Fixture signer');await page.getByRole('checkbox',{name:/By typing my name/}).check();await page.getByRole('button',{name:'Sign & accept selected scope'}).click();await expect(page.getByText('Your signed acceptance is recorded.')).toBeVisible();
});
test('Pricing settings are usable on touch and keyboard',async({page})=>{
 await page.goto('/bdr/admin/estimates/settings');await expect(page.getByLabel('Company display name')).toBeVisible();
 const axe=await new AxeBuilder({page}).include('.settings').withTags(['wcag2a','wcag2aa','wcag21aa']).analyze();expect(axe.violations).toEqual([]);
});

test('Read-only estimating keeps preview available and hides mutations',async({page,request})=>{
 await request.post('http://127.0.0.1:5398/scenario',{data:{readOnly:true}});await page.goto(`/bdr/admin/estimates/${id}`);
 await expect(page.getByRole('button',{name:'Save & calculate authoritative price'})).toHaveCount(0);await expect(page.getByRole('button',{name:'Ask Bob to structure notes'})).toHaveCount(0);
 await page.getByRole('button',{name:'Preview proposal',exact:true}).click();await expect(page.getByRole('article')).toBeVisible();
});
test('Customer can switch exclusive alternatives with keyboard',async({page,request})=>{
 await request.post('http://127.0.0.1:5398/scenario',{data:{alternatives:true}});await page.goto(`/bdr/estimate/${id}?token=fixture`);
 const first=page.getByRole('checkbox',{name:/Base scope/}),second=page.getByRole('checkbox',{name:/Replace full opening/});
 await expect(first).toBeEnabled();await first.focus();await page.keyboard.press('Space');await expect(first).toBeChecked();
 await second.focus();await page.keyboard.press('Space');await expect(second).toBeChecked();await expect(first).not.toBeChecked();
});
test('PWA recovery caches no estimate or customer records',async({page,context})=>{
 await page.goto('/bdr/admin/estimates');await page.evaluate(async()=>{await navigator.serviceWorker.ready;if(!navigator.serviceWorker.controller)await new Promise<void>(resolve=>navigator.serviceWorker.addEventListener('controllerchange',()=>resolve(),{once:true}));});
 const cached=await page.evaluate(async()=>{const names=await caches.keys();return(await Promise.all(names.map(async n=>(await(await caches.open(n)).keys()).map(r=>new URL(r.url).pathname)))).flat();});
 expect(cached).toEqual(['/estimates/offline.html']);await context.setOffline(true);await page.reload();await expect(page.getByRole('heading')).toContainText('offline');await expect(page.getByText(/load your Estimates/)).toBeVisible();await context.setOffline(false);
});

test('Manifest metadata is available without cookies and protected pages remain private',async({request})=>{
 for(const tenant of ['bdr','carlzipf','thinkpink'])for(const module of ['leads','estimates']){
  const response=await request.get(`/${tenant}/admin/${module}/manifest.webmanifest`);
  expect(response.status()).toBe(200);expect(response.headers()['content-type']).toContain('application/manifest+json');expect(response.headers()['set-cookie']).toBeUndefined();
  const manifest=await response.json();expect(manifest.start_url).toBe(`/${tenant}/admin/${module}`);expect((await request.get(manifest.icons[0].src)).status()).toBe(200);
 }
 expect((await request.get('/bdr/admin/estimates',{headers:{Accept:'application/json'}})).status()).toBe(401);
});
