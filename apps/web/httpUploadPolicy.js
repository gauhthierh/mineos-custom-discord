// Shared by the custom Node server and the streaming upload proxy.
export const DEFAULT_BODY_SIZE_LIMIT = 10 * 1024 ** 3;
export const DEFAULT_UPLOAD_REQUEST_TIMEOUT = 24 * 60 * 60 * 1000;

/** Parse the same K/M/G byte units accepted by adapter-node. @param {string | undefined} value */
export function uploadBodySizeLimit(value = String(DEFAULT_BODY_SIZE_LIMIT)) {
	const multiplier = { K: 1024, M: 1024 ** 2, G: 1024 ** 3 }[(value.at(-1) ?? '').toUpperCase()] ?? 1;
	const limit = Number(multiplier === 1 ? value : value.slice(0, -1)) * multiplier;
	if (Number.isNaN(limit) || limit < 0) throw new Error('Invalid BODY_SIZE_LIMIT.');
	return limit;
}

/** @param {string | undefined} value */
export function uploadRequestTimeout(value) {
	const timeout = value === undefined ? DEFAULT_UPLOAD_REQUEST_TIMEOUT : Number(value);
	if (!Number.isSafeInteger(timeout) || timeout < 60_000 || timeout > 2_147_483_647) {
		throw new Error('WEB_REQUEST_TIMEOUT_MS must be between 60000 and 2147483647.');
	}
	return timeout;
}
