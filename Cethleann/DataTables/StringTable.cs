﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Cethleann.Structure;
using DragonLib;

namespace Cethleann.DataTables
{
    /// <summary>
    ///     Main string bundle of the game.
    /// </summary>
    public class StringTable
    {
        /// <summary>
        ///     Initialize with a span.
        /// </summary>
        /// <param name="buffer"></param>
        public StringTable(Span<byte> buffer)
        {
            var count = MemoryMarshal.Read<int>(buffer);
            var tableInfo = MemoryMarshal.Cast<byte, DataTableRecord>(buffer.Slice(sizeof(int), SizeHelper.SizeOf<DataTableRecord>() * count));
            var entries = new List<string>();
            foreach (var info in tableInfo)
            {
                var block = buffer.Slice(info.Offset);
                entries.Add(block.ReadString());
            }

            Entries = entries;
        }

        /// <summary>
        ///     Lsof Entries found in the table
        /// </summary>
        public List<string> Entries { get; }
    }
}
