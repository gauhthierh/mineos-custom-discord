import { describe, it, expect, vi } from 'vitest';
import type { RequestEvent } from '@sveltejs/kit';

vi.mock('$env/dynamic/private', () => ({ env: { PRIVATE_API_BASE_URL: 'http://api.test', PRIVATE_API_KEY: 'privileged-key' } }));
import { discordProxy } from './discordProxy';

function event(token: string | undefined, method = 'GET', contentType?: string): RequestEvent {
	return {
		cookies: { get: () => token },
		request: new Request('http://localhost/api/discord/configuration', { method, headers: contentType ? { 'content-type': contentType } : {}, ...(method !== 'GET' ? { body: '{}' } : {}) }),
		fetch: vi.fn().mockResolvedValue(new Response('{}', { headers: { 'content-type': 'application/json' } }))
	} as unknown as RequestEvent;
}

describe('Discord admin proxy', () => {
	it('rejects anonymous requests without contacting the API', async () => {
		const input = event(undefined);
		expect((await discordProxy(input, 'configuration')).status).toBe(401);
		expect(input.fetch).not.toHaveBeenCalled();
	});
	it('forwards only the user session, never the privileged service key', async () => {
		const input = event('user-session');
		const response = await discordProxy(input, 'configuration');
		expect(input.fetch).toHaveBeenCalledWith('http://api.test/api/v1/discord/configuration', expect.objectContaining({ headers: { Authorization: 'Bearer user-session', 'Content-Type': 'application/json' } }));
		expect(response.headers.get('cache-control')).toBe('no-store');
	});
	it('rejects form submissions', async () => {
		const input = event('user-session', 'PUT', 'text/plain');
		expect((await discordProxy(input, 'configuration')).status).toBe(415);
		expect(input.fetch).not.toHaveBeenCalled();
	});
	it('propagates expired sessions instead of retrying as an administrator', async () => {
		const input = event('expired');
		vi.mocked(input.fetch).mockResolvedValueOnce(new Response('Unauthorized', { status: 401 }));
		expect((await discordProxy(input, 'configuration')).status).toBe(401);
		expect(input.fetch).toHaveBeenCalledTimes(1);
	});
});
