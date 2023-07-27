// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using S7PlcRx.Enums;

namespace S7PlcRx.Core
{
    /// <summary>
    /// PLC Request.
    /// </summary>
    internal class PLCRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PLCRequest"/> class.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="tag">The tag.</param>
        public PLCRequest(PLCRequestType request, Tag? tag)
        {
            Request = request;
            Tag = tag;
        }

        /// <summary>
        /// Gets the request.
        /// </summary>
        /// <value>The request.</value>
        public PLCRequestType Request { get; }

        /// <summary>
        /// Gets the tag.
        /// </summary>
        /// <value>The tag.</value>
        public Tag? Tag { get; }
    }
}
