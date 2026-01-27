// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Production;

/// <summary>
/// Represents aggregated metrics related to production tags, including counts and distribution information.
/// </summary>
/// <remarks>Use this class to track and analyze the status and distribution of tags within a production
/// environment. The metrics provided can assist in monitoring tag activity and identifying trends or anomalies in tag
/// usage.</remarks>
public class ProductionTagMetrics
{
    /// <summary>Gets or sets the total number of tags.</summary>
    public int TotalTags { get; set; }

    /// <summary>Gets or sets the number of active tags.</summary>
    public int ActiveTags { get; set; }

    /// <summary>Gets or sets the number of inactive tags.</summary>
    public int InactiveTags { get; set; }

    /// <summary>Gets or sets the distribution of tags by data block.</summary>
    public Dictionary<string, int> DataBlockDistribution { get; set; } = [];
}
