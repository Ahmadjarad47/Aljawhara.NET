// Run against a disposable Angular/API pair with a verified test customer and a
// local mail sink. Set AUDIT_BROWSER_MODULE to a Playwright package entry point,
// AUDIT_BROWSER_EXECUTABLE to a local Chromium executable, AUDIT_EMAIL to the
// fixture address, AUDIT_MAIL to its mail sink directory, and AUDIT_NEW_PASSWORD
// to a fresh fixture password. The sink writes recipient and body to text files.
// Optionally set AUDIT_PSQL to a local psql binary to test expiry and renewal
// against the dedicated audit_followup database on 127.0.0.1:56918.
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const { chromium } = require(process.env.AUDIT_BROWSER_MODULE || 'playwright');

const port = process.env.AUDIT_FRONTEND_PORT || '54288';
assert.match(port, /^\d{4,5}$/);
const origin = 'http://127.0.0.1:' + port;
const { AUDIT_EMAIL: email, AUDIT_MAIL: mail, AUDIT_NEW_PASSWORD: password } = process.env;

async function readCode(before) {
  for (let retry = 0; retry < 30; retry++) {
    for (const name of await fs.readdir(mail)) {
      if (before.has(name)) continue;
      const message = await fs.readFile(path.join(mail, name), 'utf8');
      const code = message.match(/:\s*([0-9]{6})\b/)?.[1];
      if (code && message.split('\n')[0].includes(email)) return code;
    }
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  throw new Error('The local mail sink did not receive a recovery code.');
}

async function main() {
  assert.ok(email?.endsWith('@audit.test') && mail && password, 'Configure disposable fixtures.');
  const before = new Set(await fs.readdir(mail));
  const browser = await chromium.launch({
    executablePath: process.env.AUDIT_BROWSER_EXECUTABLE,
    headless: true,
    args: ['--disable-background-networking', '--disable-component-update',
      '--no-proxy-server', '--host-resolver-rules=MAP * ~NOTFOUND, EXCLUDE 127.0.0.1']
  });
  try {
    const context = await browser.newContext({ serviceWorkers: 'block' });
    await context.route('**/*', route =>
      new URL(route.request().url()).origin === origin ? route.continue() : route.abort());
    const page = await context.newPage();
    page.setDefaultTimeout(6000);
    const field = name => page.locator(`[formControlName="${name}"]`);
    const submit = async (suffix, status = 200) => {
      const [response] = await Promise.all([
        page.waitForResponse(r => new URL(r.url()).pathname.toLowerCase() === suffix &&
          r.request().method() === 'POST'),
        page.locator('form button[type="submit"]').click()
      ]);
      assert.equal(response.status(), status, 'Unexpected status from local ' + suffix);
    };

    const secrets = new Set([password]);
    const leaked = [];
    page.on('request', r => { if ([...secrets].some(s => r.url().includes(s))) leaked.push('request URL'); });
    page.on('console', m => { if ([...secrets].some(s => m.text().includes(s))) leaked.push('console'); });
    const openReset = async () => {
      const link = page.locator('a[href*="/auth/reset-password"]').first();
      await link.waitFor().catch(() => { throw new Error('The success page has no reset form link.'); });
      await link.click();
      await page.waitForURL('**/auth/reset-password');
      assert.equal(new URL(page.url()).search, '');
      assert.ok(await field('otp').isEditable());
      assert.equal(await field('otp').inputValue(), '');
    };
    const requestCode = async files => {
      await field('email').fill(email);
      await submit('/api/auth/forgot-password');
      const code = await readCode(files);
      secrets.add(code);
      return code;
    };
    const reset = async (code, status) => {
      await field('email').fill(email);
      await field('otp').fill(code);
      await field('newPassword').fill(password);
      await field('confirmNewPassword').fill(password);
      await submit('/api/auth/reset-password', status);
      if (status === 400) {
        await page.getByText(/رمز التحقق غير صالح أو منتهي|This code is invalid or expired/).first().waitFor();
        assert.equal(await field('otp').inputValue(), '');
      }
    };
    await page.goto(origin + '/auth/forgot-password');
    let code = await requestCode(before);
    await openReset();
    await reset(code === '000000' ? '000001' : '000000', 400);
    if (process.env.AUDIT_PSQL) {
      // Age the test challenge past both its expiry and resend cooldown.
      // psql variables quote the fixture email as a SQL literal.
      const sql = `UPDATE "AccountChallenges"
        SET "ExpiresAtUtc" = now() - interval '1 second', "LastIssuedAtUtc" = now() - interval '2 minutes'
        WHERE "Purpose" = 2 AND "UserId" = (SELECT "Id" FROM "AspNetUsers" WHERE "Email" = :'fixture_email');`;
      require('node:child_process').execFileSync(process.env.AUDIT_PSQL,
        ['-X', '-h', '127.0.0.1', '-p', '56918', '-U', 'audit_local', '-d', 'audit_followup',
          '-v', 'ON_ERROR_STOP=1', '-v', 'fixture_email=' + email],
        { input: sql, stdio: ['pipe', 'ignore', 'ignore'] });
      await reset(code, 400);
      await page.locator('a[href="/auth/forgot-password"]').click();
      code = await requestCode(new Set(await fs.readdir(mail)));
      await openReset();
    }
    await reset(code, 200);
    await page.waitForURL('**/auth/login');
    // Open the published reset route via the public recovery flow again.
    await page.locator('a[href="/auth/forgot-password"]').click();
    await field('email').fill(email);
    await submit('/api/auth/forgot-password');
    await openReset();
    await reset(code, 400);
    await page.goto(origin + '/auth/login');
    await field('email').fill(email);
    await field('password').fill(password);
    await submit('/api/auth/login');
    const storage = await page.evaluate(() => JSON.stringify([Object.entries(localStorage), Object.entries(sessionStorage)]));
    assert.ok(![...secrets].some(s => storage.includes(s)), 'Recovery material reached persistent storage.');
    assert.deepEqual(leaked, []);
  } finally {
    await browser.close();
  }
}

main().catch(error => {
  // Avoid logging Playwright action detail containing fixture codes or passwords.
  console.error(error.message.split('\n')[0]);
  process.exitCode = 1;
});
