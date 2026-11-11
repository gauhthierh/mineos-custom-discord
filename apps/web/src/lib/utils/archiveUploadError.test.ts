import { describe, expect, it } from 'vitest';
import { archiveUploadError } from './archiveUploadError';

describe('Archive upload errors', () => {
	it('explains a size rejection instead of showing an HTML proxy page', () => {
		expect(archiveUploadError(413, '<html>Too large</html>')).toContain('10 Go');
	});
	it.each([408, 504])('explains a transfer timeout (%s)', (status) => {
		expect(archiveUploadError(status)).toContain('délai');
	});
	it('explains network interruption and expired sessions', () => {
		expect(archiveUploadError(0)).toContain('connexion');
		expect(archiveUploadError(401)).toContain('session');
	});
	it('uses safe structured API errors and keeps unknown errors readable', () => {
		expect(archiveUploadError(400, '{"error":"Archive invalide"}')).toBe('Archive invalide');
		expect(archiveUploadError(502, '<html>internal proxy details</html>')).toBe('Échec de l’envoi (HTTP 502). Vérifie les journaux du serveur.');
	});
});
