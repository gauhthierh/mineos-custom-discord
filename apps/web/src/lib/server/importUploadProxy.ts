import { env } from '$env/dynamic/private';
import { json, type RequestEvent } from '@sveltejs/kit';
import { Agent, request } from 'undici';
import { Readable } from 'node:stream';
import type { ReadableStream as NodeReadableStream } from 'node:stream/web';
import { uploadBodySizeLimit, uploadRequestTimeout } from '../../../httpUploadPolicy.js';

// A large upload can outlast the usual response-header deadline.
const dispatcher = new Agent({
	headersTimeout: uploadRequestTimeout(env.WEB_REQUEST_TIMEOUT_MS),
	bodyTimeout: 30_000
});

function boundedBody(body: ReadableStream<Uint8Array>, limit: number) {
	// DOM and Node types differ on async iteration, but refer to the same WHATWG stream.
	const source = Readable.fromWeb(body as unknown as NodeReadableStream<Uint8Array>);
	return Readable.from((async function* () {
		let bytes = 0;
		for await (const chunk of source) {
			bytes += chunk.length;
			if (bytes > limit) throw Object.assign(new Error('Archive exceeds upload limit.'), { status: 413 });
			yield chunk;
		}
	})(), { objectMode: false });
}

export async function importUploadProxy(event: RequestEvent) {
	const token = event.cookies.get('auth_token');
	if (!token) return json({ error: 'Connexion requise pour envoyer une archive.' }, { status: 401 });
	if (!event.request.headers.get('x-file-name')) {
		return json({ error: 'Le nom du fichier est manquant.' }, { status: 400 });
	}
	const contentLength = event.request.headers.get('content-length');
	const limit = uploadBodySizeLimit(env.BODY_SIZE_LIMIT);
	if (contentLength && Number(contentLength) > limit) {
		return json({ error: 'Le fichier dépasse la taille maximale configurée pour les envois.' }, { status: 413 });
	}
	const headers: Record<string, string> = { Authorization: `Bearer ${token}` };
	for (const name of ['content-type', 'content-length', 'x-file-name', 'x-file-size']) {
		const value = event.request.headers.get(name);
		if (value) headers[name] = value;
	}
	const base = env.PRIVATE_API_BASE_URL ?? env.INTERNAL_API_URL ?? 'http://api:5078';
	try {
		// Forward the stream directly; never call arrayBuffer(), text() or formData().
		// The API checks the user's admin role. Do not elevate with the service key.
		const response = await request(`${base}/api/v1/host/imports/upload`, {
			method: 'POST', headers, dispatcher, signal: event.request.signal,
			body: event.request.body ? boundedBody(event.request.body, limit) : undefined
		});
		if (response.statusCode === 401) event.cookies.delete('auth_token', { path: '/' });
		return new Response(Readable.toWeb(response.body) as unknown as ReadableStream<Uint8Array>, { status: response.statusCode, headers: {
			'Content-Type': String(response.headers['content-type'] ?? 'application/json'),
			'Cache-Control': 'no-store'
		} });
	} catch (error) {
		// The HTTP dispatcher may wrap a body-size rejection as its cause.
		let cause: unknown = error;
		for (let depth = 0; depth < 5 && cause && typeof cause === 'object'; depth++) {
			if ('status' in cause && cause.status === 413) {
				return json({ error: 'Le fichier dépasse la taille maximale configurée pour les envois.' }, { status: 413 });
			}
			cause = 'cause' in cause ? cause.cause : undefined;
		}
		return json({ error: 'Le transfert vers le serveur a été interrompu. Vérifie la connexion et réessaie.' }, { status: 502 });
	}
}
