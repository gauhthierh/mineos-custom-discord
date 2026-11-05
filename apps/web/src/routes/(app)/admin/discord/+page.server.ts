import { error } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import type { DiscordConfiguration, DiscordEventDefinition, DiscordJournal } from '$lib/discord';

export const load: PageServerLoad = async ({ fetch, parent }) => {
	const { user } = await parent();
	if (user?.role !== 'admin') error(403, 'Ces réglages sont réservés aux administrateurs.');
	const responses = await Promise.all(['configuration', 'events', 'journal'].map((path) => fetch(`/api/discord/${path}`)));
	if (responses.some((response) => !response.ok)) error(502, 'Impossible de charger les réglages Discord. Réessaie dans un instant.');
	const [configuration, events, journal] = await Promise.all(responses.map((response) => response.json()));
	return {
		configuration: configuration as DiscordConfiguration,
		events: events as DiscordEventDefinition[],
		journal: journal as DiscordJournal
	};
};
