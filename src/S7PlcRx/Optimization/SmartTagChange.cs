// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Optimization;

/// <summary>
/// Smart tag change information with enhanced metadata.
/// </summary>
/// <typeparam name="T">The type of the tag value.</typeparam>
public sealed class SmartTagChange<T>
{
    /// <summary>Gets or sets the tag name.</summary>
    public string TagName { get; set; } = string.Empty;

    /// <summary>Gets or sets the previous value.</summary>
    public T? PreviousValue { get; set; }

    /// <summary>Gets or sets the current value.</summary>
    public T? CurrentValue { get; set; }

    /// <summary>Gets or sets the change timestamp.</summary>
    public DateTimeOffset ChangeTime { get; set; }

    /// <summary>Gets or sets the amount of change for numeric types.</summary>
    public double ChangeAmount { get; set; }

    /// <summary>Gets or sets additional metadata about the change.</summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
