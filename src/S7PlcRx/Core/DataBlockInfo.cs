// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Core;

/// <summary>
/// Represents metadata and configuration information for a data block, including its identifier, size, tag details,
/// access frequency, and optimization settings.
/// </summary>
/// <remarks>Use this class to describe the characteristics of a data block, such as its block number, size in
/// bytes, and associated tag names. The properties provide information useful for managing, analyzing, or optimizing
/// data storage and access patterns. Instances of this class are typically used in scenarios where data blocks are
/// processed, monitored, or configured for batch operations.</remarks>
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
    public List<string> TagNames { get; } = [];
}
