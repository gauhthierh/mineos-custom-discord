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

/** randomUUID is unavailable on HTTP LAN addresses; getRandomValues still works there. */
export function newDiscordId(): string {
	if (typeof crypto.randomUUID === 'function') return crypto.randomUUID();
	const bytes = crypto.getRandomValues(new Uint8Array(16));
	bytes[6] = (bytes[6] & 0x0f) | 0x40;
	bytes[8] = (bytes[8] & 0x3f) | 0x80;
	const hex = Array.from(bytes, (byte) => byte.toString(16).padStart(2, '0')).join('');
	return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

export function newDiscordRule(name: string, eventTypes: string[], destinationIds: string[]): DiscordRule {
	return {
		id: newDiscordId(), name, enabled: true, eventTypes, serverNames: [], destinationIds,
		titleTemplate: '{title}', messageTemplate: '{message}', format: 'embed', color: null,
		showTimestamp: true, showServer: true
	};
}
