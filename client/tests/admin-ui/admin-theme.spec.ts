import { test, expect, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
async function axe(page: Page) {
 const results = await new AxeBuilder({page}).withTags(['wcag2a','wcag2aa','wcag21aa']).analyze();
 expect(results.violations, JSON.stringify(results.violations,null,2)).toEqual([]);
}
async function profile(page: Page, mobile: boolean) {
 if(mobile) { await page.getByRole('button',{name:'Open navigation',exact:true}).click(); await page.getByRole('button',{name:'Profile & Bob voice'}).click(); }
 else await page.getByRole('button',{name:'Open profile',exact:true}).click();
 await expect(page.getByRole('dialog',{name:'Operator settings'})).toBeVisible();
}
for(const tenant of ['bdr','thinkpink','carlzipf']) for(const theme of ['light','dark'] as const) {
 test(`${tenant} ${theme}: dashboard, navigation and profile accessibility`,async({page,isMobile})=>{
  const errors:string[]=[];page.on('pageerror',e=>errors.push(e.message));
  await page.emulateMedia({colorScheme:theme});
  await page.goto(`/?tenant=${tenant}`);
  await expect(page.locator('.admin-theme')).toHaveAttribute('data-theme',theme);
  await expect(page.getByRole('main')).toHaveCount(1); await axe(page);
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  if(tenant==='bdr') { await expect(page.getByRole('heading',{name:'Dashboard',exact:true})).toBeVisible();await expect(page.getByText('Collected this month')).toBeVisible();await expect(page.getByText('Test patio').first()).toBeVisible(); }
  await page.screenshot({path:`test-results/admin-ui/${tenant}-${theme}-${isMobile?'mobile':'desktop'}.png`});
  await profile(page,isMobile);await axe(page);
  await expect(page.getByLabel('Bob voice', {exact:true})).toHaveValue('practical');
  await page.getByLabel('Appearance').selectOption(theme==='light'?'dark':'light');
  await expect(page.locator('.admin-theme')).toHaveAttribute('data-theme',theme==='light'?'dark':'light');
  await page.keyboard.press('Escape');await expect(page.getByRole('dialog')).toHaveCount(0);
  if(!isMobile) await expect(page.getByRole('button',{name:'Open profile',exact:true})).toBeFocused();
  await page.reload();await expect(page.locator('.admin-theme')).toHaveAttribute('data-theme',theme==='light'?'dark':'light');
  expect(errors).toEqual([]);
 });
}
for(const theme of ['light','dark'] as const) for(const module of ['controls','customers','users']) {
 test(`${module} ${theme}: controls and keyboard`,async({page})=>{
  await page.emulateMedia({colorScheme:theme});await page.goto(`/?module=${module}&new=1`);
  if(module==='users')await page.getByRole('button',{name:'Add user',exact:true}).click();
  await axe(page);
  if(module==='controls') {
   await page.getByRole('button',{name:'Open details'}).click();const dialog=page.getByRole('dialog');await expect(dialog).toBeVisible();await axe(page);
   await page.getByRole('button',{name:'Save note'}).focus();await page.keyboard.press('Tab');await expect(page.getByRole('button',{name:'Close request details'})).toBeFocused();
   await page.keyboard.press('Escape');await expect(page.getByRole('button',{name:'Open details'})).toBeFocused();
  }
 });
}
test('permission-filtered navigation and collapse preserve destinations',async({page,isMobile})=>{
 await page.goto('/?tenant=bdr&module=leads&restricted=1');
 if(isMobile)await page.getByRole('button',{name:'Open navigation',exact:true}).click();
 const nav=page.getByRole('navigation',{name:isMobile?'Mobile navigation':'Primary navigation',exact:true});
 await expect(nav.getByRole('link',{name:'Leads',exact:true})).toHaveAttribute('href','/bdr/admin/leads');
 await expect(nav.getByRole('link',{name:'Contacts'})).toHaveCount(0);
 await expect(nav.getByRole('link',{name:'Dashboard'})).toHaveCount(0);
 await expect(nav.getByRole('link',{name:'Leads',exact:true})).toHaveAttribute('aria-current','page');
 if(!isMobile) {await page.getByRole('button',{name:'Collapse navigation',exact:true}).click();await expect(nav.getByRole('link',{name:'Leads',exact:true})).toBeVisible();await page.getByRole('button',{name:'Expand navigation',exact:true}).click();}
});

for(const theme of ['light','dark'] as const) for(const module of ['requests','estimates','invoices','jobs','settings','bob']) {
 test(`${module} ${theme}: real module accessibility`,async({page,isMobile})=>{
  const errors:string[]=[]; page.on('pageerror',e=>errors.push(e.message));
  await page.emulateMedia({colorScheme:theme});await page.goto(`/?module=${module}`);
  await expect(page.locator('.admin-theme')).toHaveAttribute('data-theme',theme);
  await expect(page.getByRole('main')).toBeVisible(); await axe(page);
  expect(errors).toEqual([]);
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  if(module==='requests')await expect(page.getByText('7909B5B4').first()).toBeVisible();
  await page.screenshot({path:`test-results/admin-ui/${module}-${theme}-${isMobile?'mobile':'desktop'}.png`});
 });
}

test('system preference follows changes until an explicit choice is saved',async({page,isMobile})=>{
 await page.emulateMedia({colorScheme:'dark',reducedMotion:'reduce'});await page.goto('/?module=controls');
 await expect(page.locator('.admin-theme')).toHaveAttribute('data-theme','dark');
 await page.emulateMedia({colorScheme:'light'});await expect(page.locator('.admin-theme')).toHaveAttribute('data-theme','light');
 await profile(page,isMobile);await page.getByLabel('Appearance').selectOption('dark');await page.emulateMedia({colorScheme:'light'});
 await expect(page.locator('.admin-theme')).toHaveAttribute('data-theme','dark');await page.getByLabel('Appearance').selectOption('system');
 await expect(page.locator('.admin-theme')).toHaveAttribute('data-theme','light');
 expect(await page.evaluate(()=>localStorage.getItem('tko-admin-theme'))).toBeNull();
});
for(const theme of ['light','dark'] as const) test(`${theme}: semantic text, CTA, inputs and focus meet contrast thresholds`,async({page,isMobile})=>{
 await page.emulateMedia({colorScheme:theme});await page.goto('/?module=controls');await expect(page.locator('.admin-theme')).toHaveAttribute('data-theme',theme);
 const ratios = await page.locator('.admin-theme').evaluate(node=>{
  const style=getComputedStyle(node);
  const luminance=(color:string)=>{ const rgb=color.trim().replace('#','').match(/../g)!.map(c=>parseInt(c,16)/255).map(c=>c<=.04045?c/12.92:((c+.055)/1.055)**2.4);return rgb[0]*.2126+rgb[1]*.7152+rgb[2]*.0722; };
  const ratio=(a:string,b:string)=>{const x=luminance(style.getPropertyValue(a));const y=luminance(style.getPropertyValue(b));return (Math.max(x,y)+.05)/(Math.min(x,y)+.05);};
  return {text:ratio('--text-base','--surface'),muted:ratio('--text-muted','--surface'),selected:ratio('--nav-active-text','--nav-active-bg'),cta:ratio('--cta-text','--cta'),hover:ratio('--cta-text','--cta-hover'),focus:ratio('--focus-ring','--surface'),input:ratio('--input-border','--surface')};
 });
 for(const key of ['text','muted','selected','cta','hover'] as const)expect(ratios[key],`${key} contrast`).toBeGreaterThanOrEqual(4.5);
 for(const key of ['focus','input'] as const)expect(ratios[key],`${key} contrast`).toBeGreaterThanOrEqual(3);
 await page.getByRole('button',{name:'Save request'}).focus();await expect(page.getByRole('button',{name:'Save request'})).toHaveCSS('outline-style','solid');
 if(isMobile){const bounds=await page.getByRole('button',{name:'Save request'}).boundingBox();expect(bounds!.height).toBeGreaterThanOrEqual(44);}
});
