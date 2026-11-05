import { afterEach, describe, expect, it, vi } from 'vitest';
import { webcrypto } from 'node:crypto';
import { newDiscordId, newDiscordRule } from './discord';

const uuidV4 = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;
afterEach(() => vi.unstubAllGlobals());

describe('Discord identifiers on HTTPS and HTTP LAN addresses', () => {
	it('uses the native UUID function when available', () => {
		const randomUUID = vi.fn(() => 'a6824284-7e11-4071-9ff2-f13931424ff0');
		vi.stubGlobal('crypto', { randomUUID });
		expect(newDiscordId()).toBe('a6824284-7e11-4071-9ff2-f13931424ff0');
		expect(randomUUID).toHaveBeenCalledOnce();
	});

	it('generates distinct valid UUIDs when HTTP hides randomUUID', () => {
		vi.stubGlobal('crypto', { getRandomValues: webcrypto.getRandomValues.bind(webcrypto) });
		const ids = Array.from({ length: 100 }, newDiscordId);
		for (const id of ids) expect(id).toMatch(uuidV4);
		expect(new Set(ids).size).toBe(ids.length);
	});

	it('creates status and backup rules without randomUUID', () => {
		vi.stubGlobal('crypto', { getRandomValues: webcrypto.getRandomValues.bind(webcrypto) });
		const destination = newDiscordId();
		const status = newDiscordRule('Statut', ['server.started'], [destination]);
		const backup = newDiscordRule('Sauvegardes', ['backup.completed'], [destination]);
		expect(status.id).toMatch(uuidV4);
		expect(backup.id).toMatch(uuidV4);
		expect(status.id).not.toBe(backup.id);
		expect(status.destinationIds).toEqual([destination]);
		expect(backup.eventTypes).toEqual(['backup.completed']);
	});
});
