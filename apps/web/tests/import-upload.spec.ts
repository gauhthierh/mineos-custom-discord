import { test, expect } from '@playwright/test';
import { zip64Fixture } from './zip64-fixture.mjs';

// Opt in only on a disposable installation, as for the Discord browser tests.
test.skip(!process.env.MINEOS_E2E_USERNAME || !process.env.MINEOS_E2E_PASSWORD,
	'Requires credentials for an isolated MineOS test instance.');

test('Import archive: choose a ZIP, upload it and list only the completed file', async ({ page }) => {
	const login = await page.request.post('/api/auth/login', { data: {
		username: process.env.MINEOS_E2E_USERNAME, password: process.env.MINEOS_E2E_PASSWORD
	} });
	expect(login.ok()).toBeTruthy();
	const { accessToken } = await login.json();
	await page.context().addCookies([{ name: 'auth_token', value: accessToken,
		url: test.info().project.use.baseURL!, httpOnly: true, sameSite: 'Lax' }]);
	const filename = `codex-upload-${Date.now()}.zip`;
	const chunks: Buffer[] = [];
	for await (const chunk of zip64Fixture(16 * 1024 ** 2)) chunks.push(chunk);
	try {
		await page.goto('/servers#import');
		await expect(page.getByRole('heading', { name: 'Import Servers', exact: true })).toBeVisible();
		const uploaded = page.waitForResponse((response) =>
			new URL(response.url()).pathname === '/api/host/imports/upload' && response.request().method() === 'POST');
		await page.locator('#import input[type="file"]').setInputFiles({
			name: filename, mimeType: 'application/zip', buffer: Buffer.concat(chunks)
		});
		expect((await uploaded).status()).toBe(200);
		await expect(page.getByRole('dialog')).toContainText('Upload complete');
		await page.getByRole('dialog').getByRole('button', { name: 'OK', exact: true }).click();
		await expect(page.locator('#import .imports-card')).toContainText(filename);
		const response = await page.request.get('/api/host/imports');
		const imports = await response.json();
		expect(imports.find((entry: { filename: string }) => entry.filename === filename)?.size).toBe(16 * 1024 ** 2);
		expect(imports.some((entry: { filename: string }) => entry.filename.startsWith('.upload-'))).toBe(false);
	} finally {
		await page.request.delete(`/api/host/imports/${encodeURIComponent(filename)}`);
	}
});
