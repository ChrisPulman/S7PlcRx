// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive;
#else
namespace S7PlcRx;
#endif

/// <summary>Represents a disposable object that exposes whether disposal has already occurred.</summary>
public interface ICancelable : IDisposable
{
    /// <summary>Gets a value indicating whether the object has been disposed.</summary>
    bool IsDisposed { get; }
}
