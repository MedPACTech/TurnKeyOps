import { test, expect } from '@playwright/test';
for (const javascript of [true, false]) {
 test.describe(`tenant login URLs: JavaScript ${javascript}`, () => {
  test.use({ javaScriptEnabled: javascript });
  for (const [destination, label] of [['/carlzipf/admin', 'Carl Zipf Admin'], ['/thinkpink/admin/requests', 'Think Pink Admin'], ['/bdr/admin/requests', 'BDR Admin']]) {
   test(`${label} survives submission and refresh`, async ({ page }) => {
    await page.goto(`/auth/login?returnTo=${encodeURIComponent(destination)}`);
    await page.waitForLoadState('networkidle');
    const action = `?/request&returnTo=${encodeURIComponent(destination)}`;
    await expect(page.locator('form')).toHaveAttribute('action', action);
    // Invalid input exercises the real action without sending email/SMS.
    await page.getByLabel('Work email or mobile number').fill('invalid-identifier');
    await page.getByRole('button', { name: 'Send code', exact: true }).click();
    await expect(page.getByText('Enter a valid email address or mobile number.')).toBeVisible();
    expect(new URL(page.url()).searchParams.get('returnTo')).toBe(destination);
    await expect(page.getByText(label, { exact: true })).toBeVisible();
    await page.reload();
    await expect(page.getByText(label, { exact: true })).toBeVisible();
    await expect(page.locator('form')).toHaveAttribute('action', action);
   });
  }
 });
}
test('Carl Zipf verification and resend preserve the destination', async ({ page }) => {
 const destination='/carlzipf/admin';
 await page.goto(`/auth/login?returnTo=${encodeURIComponent(destination)}`);
 await page.waitForLoadState('networkidle');
 await page.route('**/auth/login?*/request*', route => route.fulfill({ status:200, contentType:'application/json', body:JSON.stringify({type:'success',status:200,data:'[{"step":1,"identifier":2,"otpState":3},"verify","test@example.invalid",{"channel":4,"challengeId":5},"email","test-challenge"]'}) }));
 await page.getByLabel('Work email or mobile number').fill('test@example.invalid');
 await page.getByRole('button',{name:'Send code',exact:true}).click();
 await expect(page.getByLabel('Verification code')).toBeVisible();
 await expect(page.locator('form[action^="?/verify"]')).toHaveAttribute('action',`?/verify&returnTo=${encodeURIComponent(destination)}`);
 await expect(page.locator('form[action^="?/request"]')).toHaveAttribute('action',`?/request&returnTo=${encodeURIComponent(destination)}`);
 await expect(page.getByRole('link',{name:'Use a different email or phone number'})).toHaveAttribute('href',`/auth/login?returnTo=${encodeURIComponent(destination)}`);
});

test('scoped login rejects another tenant return destination', async ({ page }) => {
 await page.goto('/carlzipf/auth/login?returnTo=%2Fbdr%2Fadmin%2Frequests');
 await expect(page.getByText('Carl Zipf Admin',{exact:true})).toBeVisible();
 await expect(page.locator('input[name="returnTo"]')).toHaveValue('/carlzipf/admin/bob');
 const response=await page.goto('/unknown/auth/login'); expect(response?.status()).toBe(404);
});
test('generic login has no implicit BDR tenant', async ({page}) => {
 await page.goto('/auth/login');
 await expect(page.getByText('TurnKeyOps',{exact:true})).toBeVisible();
 await expect(page.locator('input[name="returnTo"]')).toHaveValue('/auth/workspaces');
});
test('workspace switch replaces tenant and refresh token after server selection', async ({page,context})=> {
 const claims={sub:'11111111-1111-1111-1111-111111111111',role:['owner'],tid:'88888888-8888-4888-8888-888888888883',exp:Math.floor(Date.now()/1000)+3600};
 const token=`${Buffer.from('{}').toString('base64url')}.${Buffer.from(JSON.stringify(claims)).toString('base64url')}.fixture`;
 await context.addCookies([{name:'tko_auth_token',value:token,url:'http://127.0.0.1:5191'},{name:'tko_refresh_token',value:'old-refresh',url:'http://127.0.0.1:5191'}]);
 await page.goto('/bdr/auth/login?returnTo=%2Fbdr%2Fadmin%2Fusers');
 await expect(page.getByRole('heading',{name:'Choose your workspace'})).toBeVisible();
 await expect(page.getByRole('button',{name:'Think Pink Land Clearing'})).toHaveCount(0);
 await page.getByRole('button',{name:'BDR Construction'}).click();
 await expect(page).toHaveURL(/bdr\/admin\/users/);
 await expect(page.getByRole('heading',{name:'People & access'})).toBeVisible();
 const cookies=await context.cookies();
 const access=cookies.find(c=>c.name==='tko_auth_token')!;
 expect(JSON.parse(Buffer.from(access.value.split('.')[1],'base64url').toString()).tid).toBe('7d40ea6c-313f-4f53-bf7d-5d1ecb9cc50b');
 expect(cookies.find(c=>c.name==='tko_refresh_token')?.value).toBe('new-workspace-refresh');
 expect(access.httpOnly).toBe(true);
});

test('multi-tenant OTP uses a temporary HttpOnly selection cookie', async ({page,context})=> {
 await page.goto('/auth/login');
 await page.getByLabel('Work email or mobile number').fill('multi@example.invalid');
 await page.getByRole('button',{name:'Send code',exact:true}).click();
 await page.getByLabel('Verification code').fill('123456');
 await page.getByRole('button',{name:/Verify/}).click();
 await expect(page.getByRole('heading',{name:'Choose your workspace'})).toBeVisible();
 const cookies=await context.cookies();
 expect(cookies.find(c=>c.name==='tko_workspace_token')?.httpOnly).toBe(true);
 expect(cookies.find(c=>c.name==='tko_auth_token')).toBeUndefined();
});
