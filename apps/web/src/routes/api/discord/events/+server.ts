import { discordProxy } from '$lib/server/discordProxy';
import type { RequestHandler } from './$types';
export const GET: RequestHandler = (event) => discordProxy(event, 'events');
