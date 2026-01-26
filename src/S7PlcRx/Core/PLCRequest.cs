// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using S7PlcRx.Enums;

namespace S7PlcRx.Core;

/// <summary>
/// Represents a request to a programmable logic controller (PLC), including the request type and an optional tag.
/// </summary>
/// <param name="request">The type of PLC request to perform.</param>
/// <param name="tag">The tag associated with the request, or null if the request does not require a tag.</param>
internal class PLCRequest(PLCRequestType request, Tag? tag)
{
    /// <summary>
    /// Gets the PLC request associated with this instance.
    /// </summary>
    public PLCRequestType Request { get; } = request;

    /// <summary>
    /// Gets the tag associated with this instance, if any.
    /// </summary>
    public Tag? Tag { get; } = tag;
}
