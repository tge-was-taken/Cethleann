﻿using Cethleann.Compression;
using Cethleann.Structure.Archive;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Cethleann.Archive
{
    /// <summary>
    ///     DATA0 is a list of information for which to read DATA1 with.
    /// </summary>
    [PublicAPI]
    public class DATA0
    {
        public DATA0() => Entries = new List<DATA0Entry>();

        /// <summary>
        ///     Reads a DATA0 file list from a path
        /// </summary>
        /// <param name="path">File path to read</param>
#pragma warning disable IDE0068 // Use recommended dispose pattern, reason: disposed in sub-method DATA0(Stream, bool) when bool leaveOpen is false.
        public DATA0(string path) : this(File.OpenRead(path)) { }
#pragma warning restore IDE0068 // Use recommended dispose pattern

        /// <summary>
        ///     Reads a DATA0 file list from a stream
        /// </summary>
        /// <param name="stream">Binary Read-capable Stream of DATA0</param>
        /// <param name="leaveOpen">If true, won't dispose <paramref name="stream" /></param>
        public DATA0(Stream stream, bool leaveOpen = false)
        {
            try
            {
                if (!stream.CanRead) throw new InvalidOperationException("Cannot read from stream!");

                var buffer = new Span<byte>(new byte[stream.Length]);
                stream.Read(buffer);
                Entries = MemoryMarshal.Cast<byte, DATA0Entry>(buffer).ToArray().ToList();
            }
            finally
            {
                if (!leaveOpen)
                {
                    stream.Close();
                    stream.Dispose();
                }
            }
        }

        /// <summary>
        ///     lsof <seealso cref="DATA0Entry" />
        /// </summary>
        public List<DATA0Entry> Entries { get; protected set; }

        /// <summary>
        ///     Reads a file index from DATA1
        /// </summary>
        /// <param name="data1">Binary Read-capable Stream of DATA1</param>
        /// <param name="index">Entry Index to read</param>
        /// <returns>memory stream of uncompressed bytes</returns>
        public Memory<byte> ReadEntry(Stream data1, int index)
        {
            if (index == Entries.Count) return Memory<byte>.Empty;
            if (index < 0 || index >= Entries.Count) throw new IndexOutOfRangeException($"Index {index} does not exist!");

            return ReadEntry(data1, Entries[index]);
        }

        /// <summary>
        ///     Reads a file entry from DATA1
        /// </summary>
        /// <param name="data1">Binary Read-capable Stream of DATA1</param>
        /// <param name="entry">Entry to read</param>
        /// <returns>memory stream of uncompressed bytes</returns>
        public static Memory<byte> ReadEntry(Stream data1, DATA0Entry entry)
        {
            if (!data1.CanRead) throw new InvalidOperationException("Cannot read from stream!");
            if (entry.UncompressedSize == 0) return Memory<byte>.Empty;
            if (entry.UncompressedSize >= 0x7FFFFFFF) return Memory<byte>.Empty;

            data1.Position = entry.Offset;

            if (entry.IsCompressed)
                return TableCompression.Decompress(data1, new CompressionOptions
                {
                    Length = entry.CompressedSize
                });

            var buffer = new Memory<byte>(new byte[entry.UncompressedSize]);
            data1.Read(buffer.Span);
            return buffer;
        }
    }
}
