// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MockS7Plc;

public delegate void SrvCallback(nint usrPtr, ref USrvEvent @event, int size);
