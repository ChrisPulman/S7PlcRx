// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive;
#else
namespace S7PlcRx;
#endif

/// <summary>
/// Represents a thread-safe collection of tag objects, providing methods for adding, retrieving, and managing tags by
/// key, name, or tag instance.
/// </summary>
/// <remarks>The Tags class extends Hashtable to store and manage Tag objects and related values. It provides
/// thread-safe operations for adding and retrieving tags. Tags can be accessed by key, by tag name, or by Tag instance.
/// The class supports adding individual tags, collections of tags, and retrieving all tags as a list. When adding or
/// retrieving tags, thread safety is ensured by internal locking. This class is serializable and can be used in
/// scenarios where tag metadata needs to be associated with objects or entities.</remarks>
[Serializable]
public class Tags : Hashtable
{
    /// <summary>Stores the lock used to protect collection mutations.</summary>
    private readonly Lock _lockObject = new();

    /// <summary>Gets or sets the value associated with the specified key.</summary>
    /// <remarks>Access to this indexer is thread-safe for set operations.</remarks>
    /// <param name="key">The key whose value to get or set. Cannot be null.</param>
    /// <returns>The value associated with the specified key, or <see langword="null"/> if the key does not exist.</returns>
    public new object? this[object key]
    {
        get => key is Tag tag ? Get(tag) : base[key];
        set
        {
            lock (_lockObject)
            {
                base[key] = value;
            }
        }
    }

    /// <summary>Gets the tag with the specified name, if it exists.</summary>
    /// <param name="name">The name of the tag to retrieve. The comparison may be case-sensitive depending on the implementation.</param>
    /// <returns>The tag associated with the specified name, or null if no tag with that name exists.</returns>
    public Tag? this[string name] => (Tag?)base[name];

    /// <summary>Gets the tag from the collection that matches the specified tag's name, if present.</summary>
    /// <param name="tag">The tag whose name is used to locate the corresponding tag in the collection. Can be null.</param>
    /// <returns>The tag from the collection that has the same name as the specified tag, or null if no such tag exists.</returns>
    public Tag? Get(Tag? tag) => tag is null ? null : (Tag?)base[tag.Name!];

    /// <summary>Adds an element with the specified key and value to the collection in a thread-safe manner.</summary>
    /// <remarks>This method ensures that the add operation is thread-safe. If an element with the same key
    /// already exists, an exception is thrown.</remarks>
    /// <param name="key">The key of the element to add. Cannot be null.</param>
    /// <param name="value">The value of the element to add. Can be null.</param>
    public new void Add(object key, object value)
    {
        lock (_lockObject)
        {
            base.Add(key, value);
        }
    }

    /// <summary>Adds the specified tag to the collection with the associated key.</summary>
    /// <remarks>If the collection already contains an element with the same key, an exception may be thrown
    /// depending on the underlying implementation. This method is thread-safe.</remarks>
    /// <param name="key">The key with which the specified tag is to be associated. Cannot be null.</param>
    /// <param name="tag">The tag to add to the collection. Cannot be null.</param>
    public void Add(object key, Tag tag)
    {
        lock (_lockObject)
        {
            base.Add(key, tag);
        }
    }

    /// <summary>Adds the specified tag to the collection.</summary>
    /// <param name="tag">The tag to add to the collection. Cannot be null.</param>
    public void Add(Tag tag)
    {
        lock (_lockObject)
        {
            base.Add(tag?.Name!, tag);
        }
    }

    /// <summary>Adds the specified key and associated tags to the collection.</summary>
    /// <param name="key">The key with which the specified tags are to be associated. Cannot be null.</param>
    /// <param name="tags">The tags to associate with the specified key. Cannot be null.</param>
    public void Add(object key, Tags tags)
    {
        lock (_lockObject)
        {
            base.Add(key, tags);
        }
    }

    /// <summary>Adds a collection of tags to the current instance, including only those tags whose values are not null.</summary>
    /// <param name="tags">The collection of <see cref="Tag"/> objects to add. Only tags with non-null values are added.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="tags"/> is null.</exception>
    public void AddRange(IEnumerable<Tag> tags)
    {
        if (tags is null)
        {
            throw new ArgumentNullException(nameof(tags));
        }

        lock (_lockObject)
        {
            foreach (var tag in tags)
            {
                if (tag.Value is not null)
                {
                    base.Add(tag.Name!, tag);
                }
            }
        }
    }

    /// <summary>Retrieves a collection of tags that have non-null values.</summary>
    /// <returns>A <see cref="Tags"/> collection containing all tags with non-null values. The collection will be empty if no
    /// such tags exist.</returns>
    public Tags GetTags()
    {
        var tags = new Tags();
        foreach (var tag in ToList())
        {
            if (tag.Value is not null)
            {
                tags.Add(tag);
            }
        }

        return tags;
    }

    /// <summary>Returns a list containing all tags in the collection.</summary>
    /// <remarks>The returned list is a snapshot of the collection at the time of the call. Subsequent
    /// modifications to the collection are not reflected in the returned list. This method is thread-safe.</remarks>
    /// <returns>A list of <see cref="Tag"/> objects representing the tags in the collection. The list is empty if the collection
    /// contains no tags or if an error occurs while retrieving the tags.</returns>
    public List<Tag> ToList()
    {
        if (Count == 0)
        {
            return [];
        }

        var result = new List<Tag>();
        lock (_lockObject)
        {
            try
            {
                // make a copy of the hashtable to avoid modifying it while iterating
                var hashtableCopy = new Hashtable(this);
                foreach (var value in hashtableCopy.Values)
                {
                    if (value is Tag tag)
                    {
                        result.Add(tag);
                    }
                }
            }
            catch
            {
                return [];
            }
        }

        return result;
    }
}
