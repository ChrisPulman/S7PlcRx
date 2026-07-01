// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace S7PlcRx.Tests;

/// <summary>
/// Smoke tests to ensure TUnit discovery is working for newly added test files.
/// </summary>
public class TypeSmokeTests
{
    /// <summary>
    /// Ensures a newly-added test file is discoverable and executable.
    /// </summary>
    [Test]
    public void Smoke_ShouldRun()
    {
        Assert.That(true, Is.True);
    }
}
