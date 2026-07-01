// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace MockS7Plc;

/// <summary>Represents a Snap7 server event callback.</summary>
/// <param name="usrPtr">The user data pointer supplied during registration.</param>
/// <param name="event">The event data.</param>
/// <param name="size">The native event size.</param>
public delegate void SrvCallback(nint usrPtr, ref USrvEvent @event, int size);
