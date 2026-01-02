// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NUnit.Framework;

namespace S7PlcRx.Tests;

/// <summary>
/// Tests for `Tags` collection helpers.
/// </summary>
public class TagsTests
{
    /// <summary>
    /// Ensures `AddRange` validates input.
    /// </summary>
    [Test]
    public void AddRange_WhenNull_ShouldThrow()
    {
        var tags = new Tags();
        Assert.Throws<ArgumentNullException>(() => tags.AddRange(null!));
    }

    /// <summary>
    /// Ensures `AddRange` skips tags with null values.
    /// </summary>
    [Test]
    public void AddRange_WhenTagValueNull_ShouldSkip()
    {
        var tags = new Tags();
        tags.AddRange([
            new Tag("T0", "DB1.DBX0.0", typeof(bool)),
            new Tag("T1", "DB1.DBX0.1", typeof(bool)) { Value = null },
        ]);

        Assert.That(tags["T0"], Is.Not.Null);
        Assert.That(tags["T1"], Is.Null);
    }

    /// <summary>
    /// Ensures indexer by `Tag` resolves by name.
    /// </summary>
    [Test]
    public void Indexer_ByTag_ShouldReturnByName()
    {
        var tags = new Tags();
        var tag = new Tag("T0", "DB1.DBX0.0", typeof(bool));
        tags.Add(tag);

        Assert.That(tags[tag], Is.SameAs(tag));
    }

    /// <summary>
    /// Ensures `GetTags` returns only tags with non-null values.
    /// </summary>
    [Test]
    public void GetTags_ShouldReturnOnlyNonNullValues()
    {
        var tags = new Tags();
        tags.Add(new Tag("T0", "DB1.DBX0.0", typeof(bool)));
        tags.Add(new Tag("T1", "DB1.DBX0.1", typeof(bool)) { Value = null });

        var filtered = tags.GetTags();
        Assert.That(filtered["T0"], Is.Not.Null);
        Assert.That(filtered["T1"], Is.Null);
    }

    /// <summary>
    /// Ensures `ToList` returns empty when collection is empty.
    /// </summary>
    [Test]
    public void ToList_WhenEmpty_ShouldReturnEmpty()
    {
        var tags = new Tags();
        Assert.That(tags.ToList(), Is.Empty);
    }
}
