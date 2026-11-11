// Standalone production-server test. Mock API by default; real API mode requires
// explicit disposable credentials and an opt-in. Never aim this at a live server.
import assert from 'node:assert/strict';
import { once } from 'node:events';
import { createServer, request } from 'node:http';
import { Readable } from 'node:stream';
import { open, readFile, readdir, stat, unlink } from 'node:fs/promises';
import { basename, dirname } from 'node:path';
import { zip64Fixture } from './zip64-fixture.mjs';

const size = Number(process.env.MINEOS_UPLOAD_TEST_BYTES ?? 10 * 1024 ** 3);
assert(Number.isSafeInteger(size) && size >= 1024 && size <= 10 * 1024 ** 3);
const realApi = process.env.MINEOS_UPLOAD_TEST_API;
let api, uploaded = 0, calls = 0, token = 'isolated-upload-session';
let web;
let ownsTestFile = false;
let peakRss = process.memoryUsage().rss;
const started = performance.now();
const transferAbort = new AbortController();
const timer = setInterval(() => {
	peakRss = Math.max(peakRss, process.memoryUsage().rss);
	if (peakRss >= 1024 ** 3) transferAbort.abort(new Error('Upload memory exceeded 1 GiB.'));
}, 100).unref();

function send(base, length, body, cookie = token) {
	return new Promise((resolve, reject) => {
		const headers = { 'Content-Type': 'application/zip', 'X-File-Name': 'large-upload-test.zip', 'Content-Length': String(length) };
		if (cookie) headers.Cookie = `auth_token=${cookie}`;
		const req = request(`${base}/api/host/imports/upload`, { method: 'POST', headers, signal: transferAbort.signal }, (res) => {
			body?.destroy();
			let text = '';
			res.setEncoding('utf8');
			res.on('data', (part) => { text += part; assert(text.length < 65536); });
			res.on('end', () => { resolve({ status: res.statusCode, text }); req.destroy(); });
			res.on('error', reject);
		});
		req.on('error', (error) => { body?.destroy(); reject(error); });
		if (body) { body.on('error', (error) => req.destroy(error)); body.pipe(req); }
		else req.flushHeaders();
	});
}

try {
	if (realApi) {
		assert.equal(process.env.MINEOS_UPLOAD_TEST_ALLOW_MUTATION, '1', 'Real API mode requires a disposable instance');
		assert.equal(basename(process.env.MINEOS_UPLOAD_TEST_FILE ?? ''), 'large-upload-test.zip');
		await assert.rejects(stat(process.env.MINEOS_UPLOAD_TEST_FILE), { code: 'ENOENT' });
		ownsTestFile = true;
		const credentials = JSON.parse(await readFile(process.env.MINEOS_UPLOAD_TEST_CREDENTIALS, 'utf8'));
		const login = await fetch(`${realApi}/api/v1/auth/login`, { method: 'POST', headers: { 'Content-Type': 'application/json' },
			body: JSON.stringify({ username: credentials.Username, password: credentials.Password }) });
		assert.equal(login.status, 200, 'Disposable API login failed');
		token = (await login.json()).accessToken;
		process.env.PRIVATE_API_BASE_URL = realApi;
	} else {
		api = createServer(async (req, res) => {
			try {
				calls++;
				assert.equal(req.url, '/api/v1/host/imports/upload');
				assert.equal(req.headers['x-api-key'], undefined);
				assert.equal(req.headers.authorization, `Bearer ${token}`);
				for await (const chunk of req) uploaded += chunk.length;
				res.writeHead(200, { 'Content-Type': 'application/json' });
				res.end('{"filename":"large-upload-test.zip"}');
			} catch { res.writeHead(500); res.end(); }
		});
		api.listen(0, '127.0.0.1'); await once(api, 'listening');
		process.env.PRIVATE_API_BASE_URL = `http://127.0.0.1:${api.address().port}`;
	}
	process.env.PRIVATE_API_KEY = 'must-not-elevate-upload';
	process.env.HOST = '127.0.0.1'; process.env.PORT = '0';
	process.env.ORIGIN = 'http://localhost'; process.env.NODE_ENV = 'production';
	delete process.env.BODY_SIZE_LIMIT;
	({ server: web } = await import('../server.js'));
	if (!web.listening) await once(web, 'listening');
	assert.equal(web.requestTimeout, 86_400_000);
	assert.equal(web.headersTimeout, 60_000);
	const base = `http://127.0.0.1:${web.address().port}`;
	const anonymous = await send(base, 4, Readable.from([Buffer.from('test')]), '');
	assert.equal(anonymous.status, 401);
	const oversized = await send(base, 10 * 1024 ** 3 + 1, undefined);
	assert.equal(oversized.status, 413, `Size rejection returned ${oversized.status}: ${oversized.text}`);
	if (!realApi) assert.equal(calls, 0, 'Rejected requests must not reach the API');
	let lastGiB = 0;
	const source = Readable.from(zip64Fixture(size, (sent) => {
		const gib = Math.floor(sent / 1024 ** 3);
		if (gib > lastGiB) { lastGiB = gib; console.log(`Upload progress: ${gib} GiB`); }
	}), { objectMode: false, highWaterMark: 256 * 1024 });
	const result = await send(base, size, source);
	assert.equal(result.status, 200, `Upload failed: HTTP ${result.status}`);
	assert.equal(JSON.parse(result.text).filename, 'large-upload-test.zip');
	if (realApi) {
		assert(process.env.MINEOS_UPLOAD_TEST_FILE, 'Real mode must specify its disposable output file');
		assert.equal((await stat(process.env.MINEOS_UPLOAD_TEST_FILE)).size, size);
		const stored = await open(process.env.MINEOS_UPLOAD_TEST_FILE, 'r');
		try {
			const end = Buffer.alloc(98);
			await stored.read(end, 0, end.length, size - end.length);
			assert.equal(end.readUInt32LE(0), 0x06064b50, 'Stored archive must retain its ZIP64 directory');
			const central = Buffer.alloc(77);
			await stored.read(central, 0, central.length, Number(end.readBigUInt64LE(48)));
			assert.equal(central.readUInt32LE(0), 0x02014b50);
			assert.equal(central.readBigUInt64LE(61), BigInt(size - 260));
		} finally { await stored.close(); }
		// Cancel a second upload with the same name after the API starts writing.
		// The complete archive must survive, with no abandoned temporary file.
		const importDir = dirname(process.env.MINEOS_UPLOAD_TEST_FILE);
		const partial = request(`${base}/api/host/imports/upload`, { method: 'POST', headers: {
			'Content-Length': String(512 * 1024 ** 2), 'Content-Type': 'application/zip',
			'X-File-Name': 'large-upload-test.zip', Cookie: `auth_token=${token}`
		} });
		partial.on('error', () => {});
		partial.write(Buffer.alloc(1024 ** 2));
		async function waitForTemporary(present) {
			for (let attempt = 0; attempt < 150; attempt++) {
				const files = await readdir(importDir);
				if (files.some((name) => name.startsWith('.upload-') && name.endsWith('.tmp')) === present) return;
				await new Promise((resolve) => setTimeout(resolve, 100));
			}
			throw new Error('Partial-upload cleanup did not finish');
		}
		try { await waitForTemporary(true); } finally { partial.destroy(); }
		await waitForTemporary(false);
		assert.equal((await stat(process.env.MINEOS_UPLOAD_TEST_FILE)).size, size);
		console.log('Cancellation: complete archive preserved and temporary upload removed.');
	} else {
		assert.equal(uploaded, size); assert.equal(calls, 1);
	}
	assert(peakRss < 1024 ** 3, `Upload process exceeded 1 GiB RAM: ${peakRss}`);
	console.log(JSON.stringify({ bytes: size, peakWebMiB: Math.round(peakRss / 1024 ** 2), seconds: Math.round((performance.now() - started) / 1000), mode: realApi ? 'real-api' : 'counting-api' }));
} finally {
	clearInterval(timer);
	if (web) { web.closeAllConnections(); await new Promise((resolve) => web.close(resolve)); }
	if (api) { api.closeAllConnections(); await new Promise((resolve) => api.close(resolve)); }
	if (ownsTestFile) {
		await unlink(process.env.MINEOS_UPLOAD_TEST_FILE).catch(() => {});
	}
}
