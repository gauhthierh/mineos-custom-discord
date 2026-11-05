export type DiscordDestination = {
	id: string;
	name: string;
	enabled: boolean;
	webhookUrl: string | null;
	hasWebhook: boolean;
};
export type DiscordRule = {
	id: string;
	name: string;
	enabled: boolean;
	eventTypes: string[];
	serverNames: string[];
	destinationIds: string[];
	titleTemplate: string;
	messageTemplate: string;
	format: 'embed' | 'text';
	color: string | null;
	showTimestamp: boolean;
	showServer: boolean;
};
export type DiscordConfiguration = {
	enabled: boolean;
	username: string;
	destinations: DiscordDestination[];
	rules: DiscordRule[];
};
export type DiscordEventDefinition = {
	id: string;
	category: string;
	label: string;
	sampleTitle: string;
	sampleMessage: string;
};
export type DiscordJournal = {
	entries: {
		timestamp: string;
		destinationId: string;
		destinationName: string;
		eventType: string;
		serverName: string | null;
		success: boolean;
		detail: string;
	}[];
	droppedEvents: number;
};
export type DiscordPreview = {
	username: string;
	content?: string;
	embeds?: { title: string; description: string; color: number; timestamp?: string; fields?: { name: string; value: string }[] }[];
};

export function newDiscordRule(name: string, eventTypes: string[], destinationIds: string[]): DiscordRule {
	return {
		id: crypto.randomUUID(), name, enabled: true, eventTypes, serverNames: [], destinationIds,
		titleTemplate: '{title}', messageTemplate: '{message}', format: 'embed', color: null,
		showTimestamp: true, showServer: true
	};
}
