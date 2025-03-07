﻿// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

namespace S7PlcRx;

/// <summary>
/// Tags is a hash table.
/// </summary>
/// <seealso cref="Hashtable"/>
[Serializable]
public class Tags : Hashtable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Tags"/> class.
    /// </summary>
    public Tags()
    {
    }

    /// <summary>
    /// Gets or sets the <see cref="object"/> with the specified key.
    /// </summary>
    /// <value>The <see cref="object"/>.</value>
    /// <param name="key">The key.</param>
    /// <param name="isEnd">if set to <c>true</c> [is end].</param>
    /// <returns>A object.</returns>
#pragma warning disable RCS1163 // Unused parameter.
    public object? this[object key, bool isEnd = false]
#pragma warning restore RCS1163 // Unused parameter.
    {
        get => base[key];
        set => base[key] = value;
    }

    /// <summary>
    /// Gets the <see cref="object"/> with the specified name.
    /// </summary>
    /// <value>The <see cref="object"/>.</value>
    /// <param name="name">The name.</param>
    /// <returns>A tag.</returns>
    public Tag? this[string name] => (Tag?)base[name];

    /// <summary>
    /// Gets the <see cref="object"/> with the specified tag.
    /// </summary>
    /// <value>The <see cref="object"/>.</value>
    /// <param name="tag">The tag.</param>
    /// <returns>A tag.</returns>
    public Tag? this[Tag? tag] => (Tag?)base[tag?.Name!];

    /// <summary>
    /// Adds an element with the specified key and value into the <see cref="Hashtable"/>.
    /// </summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add. The value can be null.</param>
    public new void Add(object key, object value) =>
        base.Add(key, value);

    /// <summary>
    /// Adds the specified key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="tag">The tag.</param>
    public void Add(object key, Tag tag) =>
        base.Add(key, tag);

    /// <summary>
    /// Adds the specified tag.
    /// </summary>
    /// <param name="tag">The tag.</param>
    public void Add(Tag tag) =>
        base.Add(tag?.Name!, tag);

    /// <summary>
    /// Adds the specified key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="tags">The tags.</param>
    public void Add(object key, Tags tags) =>
        base.Add(key, tags);

    /// <summary>
    /// Adds the range.
    /// </summary>
    /// <param name="tags">The tags.</param>
    public void AddRange(IEnumerable<Tag> tags)
    {
        if (tags == null)
        {
            throw new ArgumentNullException(nameof(tags));
        }

        foreach (var tag in tags)
        {
            if (tag.Value != null)
            {
                base.Add(tag.Name!, tag);
            }
        }
    }

    /// <summary>
    /// Gets the tags.
    /// </summary>
    /// <returns>A tags.</returns>
    public Tags GetTags()
    {
        var tags = new Tags();
        tags.AddRange(ToList().Where(x => x.Value != null));

        return tags;
    }

    /// <summary>
    /// Gets the tag list.
    /// </summary>
    /// <returns>An IEnumerable of Tag.</returns>
    public List<Tag> ToList() => Values.OfType<Tag>().ToList();
}
