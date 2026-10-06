import { test, expect } from '@playwright/test';
for (const javascript of [true, false]) {
 test.describe(`tenant login URLs: JavaScript ${javascript}`, () => {
  test.use({ javaScriptEnabled: javascript });
  for (const [destination, label] of [['/carlzipf/admin', 'Carl Zipf Admin'], ['/thinkpink/admin/requests', 'Think Pink Admin'], ['/bdr/admin/requests', 'BDR Admin']]) {
   test(`${label} survives submission and refresh`, async ({ page }) => {
    await page.goto(`/auth/login?returnTo=${encodeURIComponent(destination)}`);
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
