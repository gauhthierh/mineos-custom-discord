import { afterEach, describe, expect, it, vi } from 'vitest';
import { Readable } from 'node:stream';
import type { RequestEvent } from '@sveltejs/kit';
import type { Dispatcher } from 'undici';

const { upstream, config } = vi.hoisted(() => ({ upstream: vi.fn(), config: {
	PRIVATE_API_BASE_URL: 'http://api.test', PRIVATE_API_KEY: 'must-not-be-forwarded',
	BODY_SIZE_LIMIT: undefined as string | undefined
} }));
vi.mock('$env/dynamic/private', () => ({ env: config }));
vi.mock('undici', async (original) => ({ ...await original<typeof import('undici')>(), request: upstream }));
import { importUploadProxy } from './importUploadProxy';

afterEach(() => { upstream.mockReset(); config.BODY_SIZE_LIMIT = undefined; });

function response(statusCode = 200) {
	return { statusCode, headers: { 'content-type': 'application/json' },
		body: Readable.from([Buffer.from('{"filename":"large.zip"}')]) } as unknown as Dispatcher.ResponseData;
}

function input(): RequestEvent {
	const body = new ReadableStream<Uint8Array>({ start(controller) {
		controller.enqueue(new Uint8Array([80, 75, 3, 4])); controller.close();
	} });
	return {
		cookies: { get: vi.fn(() => 'user-session'), delete: vi.fn() },
		request: new Request('http://localhost/api/host/imports/upload', {
			method: 'POST', body, duplex: 'half', headers: {
				'X-File-Name': 'large.zip', 'Content-Type': 'application/zip', 'Content-Length': String(10 * 1024 ** 3)
			}
		} as RequestInit)
	} as unknown as RequestEvent;
}

describe('Streaming archive upload proxy', () => {
	it('rejects missing sessions before forwarding the body', async () => {
		const event = input();
		vi.mocked(event.cookies.get).mockReturnValue(undefined);
		expect((await importUploadProxy(event)).status).toBe(401);
		expect(upstream).not.toHaveBeenCalled();
		expect(event.request.bodyUsed).toBe(false);
	});
	it('forwards a bounded stream and a 64-bit length with only the user JWT', async () => {
		const event = input();
		upstream.mockResolvedValue(response());
		const result = await importUploadProxy(event);
		const [url, options] = upstream.mock.calls[0];
		expect(url).toBe('http://api.test/api/v1/host/imports/upload');
		expect(event.request.bodyUsed).toBe(false);
		const chunks = [];
		for await (const chunk of options.body) chunks.push(chunk);
		expect(Buffer.concat(chunks)).toEqual(Buffer.from([80, 75, 3, 4]));
		expect(options.headers['content-length']).toBe('10737418240');
		expect(options.headers.Authorization).toBe('Bearer user-session');
		expect(options.headers['X-Api-Key']).toBeUndefined();
		expect(options.signal).toBe(event.request.signal);
		expect(result.headers.get('cache-control')).toBe('no-store');
		expect(await result.json()).toEqual({ filename: 'large.zip' });
	});
	it('rejects archives larger than 10 GiB before contacting the API', async () => {
		const event = input();
		event.request.headers.set('Content-Length', String(10 * 1024 ** 3 + 1));
		expect((await importUploadProxy(event)).status).toBe(413);
		expect(upstream).not.toHaveBeenCalled();
		expect(event.request.bodyUsed).toBe(false);
	});
	it('also enforces a configured limit on chunked uploads without Content-Length', async () => {
		config.BODY_SIZE_LIMIT = '3';
		const event = input(); event.request.headers.delete('Content-Length');
		upstream.mockImplementationOnce(async (_url, options) => {
			for await (const _chunk of options.body) { /* consume the incoming stream */ }
			return response();
		});
		expect((await importUploadProxy(event)).status).toBe(413);
	});
	it.each([401, 403, 302])('preserves HTTP %s without retrying as administrator', async (status) => {
		const event = input(); upstream.mockResolvedValue(response(status));
		expect((await importUploadProxy(event)).status).toBe(status);
		expect(upstream).toHaveBeenCalledOnce();
		if (status === 401) expect(event.cookies.delete).toHaveBeenCalledWith('auth_token', { path: '/' });
	});
	it('keeps an adapter size rejection readable as HTTP 413', async () => {
		upstream.mockRejectedValue({ cause: { status: 413 } });
		expect((await importUploadProxy(input())).status).toBe(413);
	});
	it('does not expose credentials or upstream details on connection errors', async () => {
		upstream.mockRejectedValue(new Error('private-service-key in http://internal-api'));
		const result = await importUploadProxy(input());
		expect(result.status).toBe(502);
		expect(await result.text()).not.toMatch(/private-service-key|internal-api/);
	});
});
