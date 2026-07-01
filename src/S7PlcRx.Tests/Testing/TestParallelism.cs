// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using TUnit.Core.Interfaces;

[assembly: ParallelLimiter<S7PlcRx.Tests.Testing.SerialTestParallelLimit>]

namespace S7PlcRx.Tests.Testing;

/// <summary>Limits the test assembly to one concurrently executing test.</summary>
public sealed class SerialTestParallelLimit : IParallelLimit
{
    /// <inheritdoc />
    public int Limit => 1;
}
