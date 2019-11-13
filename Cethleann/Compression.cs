using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Cethleann.Structure;
using DragonLib;

namespace Cethleann
{
    /// <summary>
    ///     Compression helper class
    /// </summary>
    public static class Compression
    {
        /// <summary>
        ///     Compresses a stream into a .gz stream.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="blockSize"></param>
        /// <returns></returns>
        public static Span<byte> Compress(Span<byte> data, int blockSize = (int) DataType.Compressed)
        {
            var compInfo = new CompressionInfo
            {
                ChunkSize = blockSize,
                ChunkCount = (int) Math.Ceiling((double) data.Length / blockSize),
                Size = data.Length
            };
            var buffer = new Span<byte>(new byte[data.Length]);
            MemoryMarshal.Write(buffer, ref compInfo);
            var headerCursor = SizeHelper.SizeOf<CompressionInfo>();
            var cursor = (headerCursor + 4 * compInfo.ChunkCount).Align(0x80);
            for (int i = 0; i < data.Length; i += blockSize)
            {
                using var ms = new MemoryStream(blockSize);
                using var deflateStream = new DeflateStream(ms, CompressionLevel.Optimal);

                var block = data.Slice(i, Math.Min(blockSize, data.Length - i));
                deflateStream.Write(block);
                deflateStream.Flush();
                var write = block.Length;
                if (ms.Length < block.Length) // special case where the last block is too small to compress properly.
                {
                    write = (int) ms.Position + 2;
                    block = new Span<byte>(new byte[ms.Length]);
                    ms.Position = 0;
                    ms.Read(block);
                }

                var absWrite = write + 4;
                MemoryMarshal.Write(buffer.Slice(headerCursor), ref absWrite);
                headerCursor += 4;
                MemoryMarshal.Write(buffer.Slice(cursor), ref write);
                buffer[cursor + 4] = 0x78;
                buffer[cursor + 5] = 0xDA;

                block.CopyTo(buffer.Slice(cursor + 6));
                cursor = (cursor + write + 6).Align(0x80);
            }

            return buffer.Slice(0, cursor);
        }

        /// <summary>
        ///     Decompresses a .gz stream.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static unsafe Memory<byte> Decompress(Span<byte> data)
        {
            var cursor = 0;
            var compInfo = MemoryMarshal.Read<CompressionInfo>(data);
            var buffer = new Memory<byte>(new byte[compInfo.Size]);
            cursor += SizeHelper.SizeOf<CompressionInfo>();
            var chunkSizes = MemoryMarshal.Cast<byte, int>(data.Slice(cursor, 4 * compInfo.ChunkCount));
            cursor = (cursor + 4 * compInfo.ChunkCount).Align(0x80);
            var bufferCursor = 0;
            for (var i = 0; i < compInfo.ChunkCount; ++i)
            {
                var chunkSize = chunkSizes[i];
                try
                {
                    if (chunkSize + bufferCursor == buffer.Length)
                    {
                        data.Slice(cursor, chunkSize).CopyTo(buffer.Span.Slice(bufferCursor));
                        bufferCursor += chunkSize;
                        continue;
                    }

                    fixed (byte* pinData = &data.Slice(cursor)[6])
                    {
                        using var stream = new UnmanagedMemoryStream(pinData, chunkSize - 6);
                        using var inflateStream = new DeflateStream(stream, CompressionMode.Decompress);
                        var block = new Span<byte>(new byte[0x0001_0000]);
                        var read = inflateStream.Read(block);
                        block.Slice(0, read).CopyTo(buffer.Span.Slice(bufferCursor));
                        bufferCursor = (bufferCursor + read).Align(0x80);
                    }
                }
                finally
                {
                    cursor = (cursor + chunkSize).Align(0x80);
                }
            }

            return buffer;
        }

        /// <summary>
        ///     Wrapper for Stream based buffers
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="compressedSize"></param>
        /// <returns></returns>
        public static Memory<byte> Decompress(Stream stream, long compressedSize)
        {
            var compressedBuffer = new Span<byte>(new byte[compressedSize + SizeHelper.SizeOf<CompressionInfo>()]);
            stream.Read(compressedBuffer);
            return Decompress(compressedBuffer);
        }
    }
}