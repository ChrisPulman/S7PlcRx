// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace MockS7Plc;

/// <summary>Represents a Snap7 read/write area callback.</summary>
/// <param name="usrPtr">The user data pointer supplied during registration.</param>
/// <param name="sender">The sender identifier.</param>
/// <param name="operation">The read/write operation code.</param>
/// <param name="tag">The area tag being accessed.</param>
/// <param name="buffer">The area data buffer.</param>
/// <returns>The Snap7 result code.</returns>
public delegate int SrvRwAreaCallback(nint usrPtr, int sender, int operation, ref S7Tag tag, ref RwBuffer buffer);
