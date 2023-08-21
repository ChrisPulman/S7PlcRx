// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using S7PlcRx.Enums;

namespace S7PlcRx.Core;

/// <summary>
/// PLC Request.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PLCRequest"/> class.
/// </remarks>
/// <param name="request">The request.</param>
/// <param name="tag">The tag.</param>
internal class PLCRequest(PLCRequestType request, Tag? tag)
{
    /// <summary>
    /// Gets the request.
    /// </summary>
    /// <value>The request.</value>
    public PLCRequestType Request { get; } = request;

    /// <summary>
    /// Gets the tag.
    /// </summary>
    /// <value>The tag.</value>
    public Tag? Tag { get; } = tag;
}
