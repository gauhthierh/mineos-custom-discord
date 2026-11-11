import { crc32 } from 'node:zlib';

// A valid, uncompressed ZIP64 archive of exactly `size` bytes, generated in chunks.
// This permits a real >4 GiB transfer without allocating the archive in memory.
/** @param {number} size @param {(sent: number) => void} [progress] */
export async function* zip64Fixture(size, progress = () => {}) {
	const name = Buffer.from('padding.bin');
	const local = Buffer.alloc(30 + name.length + 20);
	const central = Buffer.alloc(46 + name.length + 20);
	const overhead = local.length + 24 + central.length + 56 + 20 + 22;
	const dataSize = size - overhead;
	if (!Number.isSafeInteger(size) || dataSize < 0) throw new Error('Invalid fixture size');
	const extra = Buffer.alloc(20);
	extra.writeUInt16LE(1, 0); extra.writeUInt16LE(16, 2);
	extra.writeBigUInt64LE(BigInt(dataSize), 4); extra.writeBigUInt64LE(BigInt(dataSize), 12);
	local.writeUInt32LE(0x04034b50, 0);
	local.writeUInt16LE(45, 4); local.writeUInt16LE(8, 6); local.writeUInt16LE(0x21, 12);
	local.writeUInt32LE(0xffffffff, 18); local.writeUInt32LE(0xffffffff, 22);
	local.writeUInt16LE(name.length, 26); local.writeUInt16LE(extra.length, 28);
	name.copy(local, 30); extra.copy(local, 30 + name.length);
	yield local;
	const chunk = Buffer.alloc(256 * 1024, 0x5a);
	let checksum = 0, remaining = dataSize;
	while (remaining > 0) {
		const part = chunk.subarray(0, Math.min(remaining, chunk.length));
		checksum = crc32(part, checksum);
		remaining -= part.length;
		yield part;
		progress(local.length + dataSize - remaining);
	}
	const descriptor = Buffer.alloc(24);
	descriptor.writeUInt32LE(0x08074b50); descriptor.writeUInt32LE(checksum, 4);
	descriptor.writeBigUInt64LE(BigInt(dataSize), 8); descriptor.writeBigUInt64LE(BigInt(dataSize), 16);
	yield descriptor;
	central.writeUInt32LE(0x02014b50); central.writeUInt16LE(45, 4); central.writeUInt16LE(45, 6);
	central.writeUInt16LE(8, 8); central.writeUInt16LE(0x21, 14);
	central.writeUInt32LE(checksum, 16); central.writeUInt32LE(0xffffffff, 20); central.writeUInt32LE(0xffffffff, 24);
	central.writeUInt16LE(name.length, 28); central.writeUInt16LE(extra.length, 30);
	name.copy(central, 46); extra.copy(central, 46 + name.length);
	yield central;
	const centralOffset = BigInt(local.length + dataSize + descriptor.length);
	const end64 = Buffer.alloc(56);
	end64.writeUInt32LE(0x06064b50); end64.writeBigUInt64LE(44n, 4);
	end64.writeUInt16LE(45, 12); end64.writeUInt16LE(45, 14);
	end64.writeBigUInt64LE(1n, 24); end64.writeBigUInt64LE(1n, 32);
	end64.writeBigUInt64LE(BigInt(central.length), 40); end64.writeBigUInt64LE(centralOffset, 48);
	yield end64;
	const locator = Buffer.alloc(20);
	locator.writeUInt32LE(0x07064b50); locator.writeBigUInt64LE(centralOffset + BigInt(central.length), 8);
	locator.writeUInt32LE(1, 16);
	yield locator;
	const end = Buffer.alloc(22);
	end.writeUInt32LE(0x06054b50); end.writeUInt16LE(0xffff, 8); end.writeUInt16LE(0xffff, 10);
	end.writeUInt32LE(0xffffffff, 12); end.writeUInt32LE(0xffffffff, 16);
	yield end;
	progress(size);
}
