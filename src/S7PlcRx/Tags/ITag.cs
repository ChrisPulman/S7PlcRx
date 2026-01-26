// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx;

/// <summary>
/// Represents a tag that can be configured to control polling behavior.
/// </summary>
public interface ITag
{
    /// <summary>
    /// Sets whether the object should be excluded from polling operations.
    /// </summary>
    /// <param name="value">true to prevent the object from being polled; otherwise, false.</param>
    void SetDoNotPoll(bool value);
}
