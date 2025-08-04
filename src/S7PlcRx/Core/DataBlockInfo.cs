// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Core;

/// <summary>
/// Data block optimization information.
/// </summary>
public class DataBlockInfo
{
    /// <summary>Gets or sets the data block number.</summary>
    public int BlockNumber { get; set; }

    /// <summary>Gets or sets the total size in bytes.</summary>
    public int SizeBytes { get; set; }

    /// <summary>Gets or sets the number of tags in this block.</summary>
    public int TagCount { get; set; }

    /// <summary>Gets or sets the access frequency.</summary>
    public double AccessFrequency { get; set; }

    /// <summary>Gets or sets a value indicating whether gets or sets whether the block is optimized for batch operations.</summary>
    public bool IsBatchOptimized { get; set; }

    /// <summary>Gets the tags in this data block.</summary>
    public List<string> TagNames { get; } = new();
}
