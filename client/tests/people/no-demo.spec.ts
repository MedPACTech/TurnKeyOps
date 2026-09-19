import {test,expect,type BrowserContext} from '@playwright/test';
async function signIn(context:BrowserContext,role:string) {
 const claims={sub:'11111111-1111-1111-1111-111111111111',role:[role],tid:'7d40ea6c-313f-4f53-bf7d-5d1ecb9cc50b',exp:Math.floor(Date.now()/1000)+3600};
 const token=`${Buffer.from('{}').toString('base64url')}.${Buffer.from(JSON.stringify(claims)).toString('base64url')}.ui-fixture`;
 await context.addCookies([{name:'tko_auth_token',value:token,url:'http://127.0.0.1:5191'}]);
}
test('platform overview and health show configuration and unavailable telemetry instead of invented metrics',async({page,context})=>{
 await signIn(context,'internal_admin');
 await page.goto('/turnkeyops/admin/dashboard');
 await expect(page.getByRole('heading',{name:'Platform overview',exact:true})).toBeVisible();
 await expect(page.getByText('Configured companies',{exact:true})).toBeVisible();
 await expect(page.getByText('Field Services Pilot',{exact:true})).toHaveCount(0);
 await expect(page.getByText('84%',{exact:true})).toHaveCount(0);
 await page.goto('/turnkeyops/admin/health');
 await expect(page.getByText('Health status is unavailable here.',{exact:false})).toBeVisible();
 await expect(page.getByText('One degraded sync',{exact:false})).toHaveCount(0);
});
test('an empty estimate API stays empty instead of supplying sample estimates',async({page,context})=>{
 await signIn(context,'owner');
 await page.goto('/bdr/admin/estimates');
 await expect(page.getByText('Sent estimates',{exact:true})).toBeVisible();
 await expect(page.getByText('Ridgeway Residence',{exact:false})).toHaveCount(0);
 await expect(page.getByText('EST-24031',{exact:false})).toHaveCount(0);
 await expect(page.getByText('Existing estimates',{exact:true})).toHaveCount(0);
});
test('public defaults do not publish fictitious phone numbers or project records',async({page})=>{
 await page.goto('/bdr/public');
 await expect(page.locator('a[href^="tel:"]')).toHaveCount(0);
 await expect(page.getByText('100+ happy customers',{exact:false})).toHaveCount(0);
 await page.goto('/thinkpink/public');
 await expect(page.locator('a[href^="tel:"]')).toHaveCount(0);
 await expect(page.getByText('12-acre pasture reclaim',{exact:false})).toHaveCount(0);
 await expect(page.getByText('Project photos will be added when available.',{exact:true})).toBeVisible();
});
