// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx
{
    /// <summary>
    /// ITag.
    /// </summary>
    public interface ITag
    {
        /// <summary>
        /// Sets the do not poll.
        /// </summary>
        /// <param name="value">if set to <c>true</c> [value].</param>
        void SetDoNotPoll(bool value);
    }
}
