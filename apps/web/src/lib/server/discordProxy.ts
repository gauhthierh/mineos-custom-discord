import { env } from '$env/dynamic/private';
import { json, type RequestEvent } from '@sveltejs/kit';

/** This admin proxy uses only the user's session, never the privileged service key. */
export async function discordProxy(event: RequestEvent, path: string) {
	const token = event.cookies.get('auth_token');
	if (!token) return json({ error: 'Connexion requise.' }, { status: 401 });
	const mutating = event.request.method !== 'GET';
	if (mutating && !event.request.headers.get('content-type')?.startsWith('application/json')) {
		return json({ error: 'Un contenu JSON est requis.' }, { status: 415 });
	}
	const body = mutating ? await event.request.text() : undefined;
	if (body && new TextEncoder().encode(body).length > 131072) {
		return json({ error: 'Configuration trop volumineuse.' }, { status: 413 });
	}
	const base = env.PRIVATE_API_BASE_URL ?? env.INTERNAL_API_URL ?? 'http://api:5078';
	const response = await event.fetch(`${base}/api/v1/discord/${path}`, {
		method: event.request.method,
		headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
		body
	});
	return new Response(response.body, {
		status: response.status,
		headers: { 'Content-Type': response.headers.get('content-type') ?? 'application/json', 'Cache-Control': 'no-store' }
	});
}
