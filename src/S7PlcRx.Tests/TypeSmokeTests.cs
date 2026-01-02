// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Tests;

/// <summary>
/// Smoke tests to ensure NUnit discovery is working for newly added test files.
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
