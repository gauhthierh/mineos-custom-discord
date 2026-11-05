import { test, expect } from '@playwright/test';

// Opt in only against an isolated instance with a disposable database.
test.skip(!process.env.MINEOS_E2E_USERNAME || !process.env.MINEOS_E2E_PASSWORD,
	'Requires credentials for an isolated MineOS test instance.');

test('Discord: route status and backups separately, preview, save and reload', async ({ page }) => {
	const errors: string[] = [];
	page.on('pageerror', (error) => errors.push(error.message));
	const login = await page.request.post('/api/auth/login', { data: {
		username: process.env.MINEOS_E2E_USERNAME, password: process.env.MINEOS_E2E_PASSWORD
	} });
	expect(login.ok()).toBeTruthy();
	const { accessToken } = await login.json();
	await page.context().addCookies([{ name: 'auth_token', value: accessToken,
		url: test.info().project.use.baseURL!, httpOnly: true, sameSite: 'Lax' }]);
	const reset = await page.request.put('/api/discord/configuration', { data: {
		enabled: false, username: 'Noryandor', destinations: [], rules: []
	} });
	expect(reset.ok()).toBeTruthy();
	await page.goto('/admin/discord');
	await expect(page.getByRole('heading', { name: 'Discord', exact: true })).toBeVisible();
	await page.getByRole('button', { name: '+ Ajouter un salon' }).click();
	let destination = page.locator('.destination').nth(0);
	await destination.getByLabel('Nom du salon').fill('statut');
	// Deliberately fictitious webhooks. Automatic delivery stays paused throughout this test.
	await destination.getByLabel('URL du webhook').fill('https://discord.com/api/webhooks/111/fictitious-status-token');
	await page.getByRole('button', { name: '+ Ajouter un salon' }).click();
	destination = page.locator('.destination').nth(1);
	await destination.getByLabel('Nom du salon').fill('sauvegardes');
	await destination.getByLabel('URL du webhook').fill('https://discord.com/api/webhooks/222/fictitious-backup-token');
	await page.getByRole('button', { name: 'Statut en ligne / hors ligne', exact: true }).click();
	await page.getByRole('button', { name: 'Sauvegardes', exact: true }).click();
	const backupRule = page.locator('.rule').nth(2);
	await backupRule.getByLabel('#statut', { exact: true }).uncheck();
	await backupRule.getByLabel('#sauvegardes', { exact: true }).check();
	const onlineRule = page.locator('.rule').nth(0);
	await onlineRule.getByRole('button', { name: 'Voir l’aperçu' }).click();
	await expect(onlineRule.locator('.embed')).toContainText('serveur-exemple est maintenant en ligne.');
	await page.getByRole('button', { name: 'Enregistrer', exact: true }).click();
	await expect(page.getByRole('status')).toContainText('Réglages enregistrés.');
	const response = await page.request.get('/api/discord/configuration');
	const saved = await response.json();
	expect(saved.enabled).toBe(false);
	expect(saved.rules).toHaveLength(3);
	expect(saved.rules[0].destinationIds).toEqual([saved.destinations[0].id]);
	expect(saved.rules[2].destinationIds).toEqual([saved.destinations[1].id]);
	expect(saved.rules[2].eventTypes).toEqual(['backup.completed', 'backup.failed', 'backup.cancelled']);
	expect(JSON.stringify(saved)).not.toContain('fictitious-');
	await page.reload();
	await expect(page.locator('.rule')).toHaveCount(3);
	await expect(page.locator('.destination').nth(0).getByLabel('URL du webhook')).toHaveValue('');
	await expect(page.locator('.rule').nth(2).getByLabel('#sauvegardes', { exact: true })).toBeChecked();
	await page.screenshot({ path: 'test-results/discord-desktop.png', fullPage: true });
	await page.setViewportSize({ width: 390, height: 844 });
	await expect(page.locator('body')).toHaveJSProperty('scrollWidth', 390);
	await page.screenshot({ path: 'test-results/discord-mobile.png', fullPage: true });
	expect(errors).toEqual([]);
});
