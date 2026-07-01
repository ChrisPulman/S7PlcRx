// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive;
#else
namespace S7PlcRx;
#endif

/// <summary>Represents a tag that can be configured to control polling behavior.</summary>
public interface ITag
{
    /// <summary>Sets whether the object should be excluded from polling operations.</summary>
    /// <param name="value">true to prevent the object from being polled; otherwise, false.</param>
    void SetDoNotPoll(bool value);
}
