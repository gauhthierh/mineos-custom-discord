import { discordProxy } from '$lib/server/discordProxy';
import type { RequestHandler } from './$types';
export const POST: RequestHandler = (event) => discordProxy(event, 'test');
