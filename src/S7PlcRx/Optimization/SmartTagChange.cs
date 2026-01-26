// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Optimization;

/// <summary>
/// Represents a change to a smart tag, including its name, previous and current values, the time of change, the amount
/// of change for numeric types, and associated metadata.
/// </summary>
/// <remarks>Use this class to track changes to smart tags in applications that require auditing, history, or
/// notification of tag value updates. The <see cref="ChangeAmount"/> property is intended for numeric types; for
/// non-numeric types, its value may be ignored. The <see cref="Metadata"/> dictionary can be used to store additional
/// context or information relevant to the change.</remarks>
/// <typeparam name="T">The type of the value associated with the smart tag. This can be any type representing the tag's value before and
/// after the change.</typeparam>
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
