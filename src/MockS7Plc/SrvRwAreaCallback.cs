// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MockS7Plc;

public delegate int SrvRwAreaCallback(nint usrPtr, int sender, int operation, ref S7Tag tag, ref RwBuffer buffer);
