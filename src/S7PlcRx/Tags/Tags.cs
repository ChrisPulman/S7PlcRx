// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

namespace S7PlcRx;

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
    private readonly object _lockObject = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="Tags"/> class.
    /// </summary>
    public Tags()
    {
    }

    /// <summary>
    /// Gets or sets the value associated with the specified key. Supports an optional flag to indicate whether the key
    /// represents the end of a range or sequence.
    /// </summary>
    /// <remarks>Access to this indexer is thread-safe for set operations. The behavior of the indexer may
    /// depend on the value of <paramref name="isEnd"/> if the underlying implementation distinguishes between start and
    /// end keys.</remarks>
    /// <param name="key">The key whose value to get or set. Cannot be null.</param>
    /// <param name="isEnd">A value indicating whether the key represents the end of a range or sequence. The default is <see
    /// langword="false"/>.</param>
    /// <returns>The value associated with the specified key, or <see langword="null"/> if the key does not exist.</returns>
#pragma warning disable RCS1163 // Unused parameter.
    public object? this[object key, bool isEnd = false]
#pragma warning restore RCS1163 // Unused parameter.
    {
        get => base[key];
        set
        {
            lock (_lockObject)
            {
                base[key] = value;
            }
        }
    }

    /// <summary>
    /// Gets the tag with the specified name, if it exists.
    /// </summary>
    /// <param name="name">The name of the tag to retrieve. The comparison may be case-sensitive depending on the implementation.</param>
    /// <returns>The tag associated with the specified name, or null if no tag with that name exists.</returns>
    public Tag? this[string name] => (Tag?)base[name];

    /// <summary>
    /// Gets the tag from the collection that matches the specified tag's name, if present.
    /// </summary>
    /// <remarks>This indexer performs a lookup based on the name of the provided tag. If the specified tag is
    /// null, the result is null.</remarks>
    /// <param name="tag">The tag whose name is used to locate the corresponding tag in the collection. Can be null.</param>
    /// <returns>The tag from the collection that has the same name as the specified tag, or null if no such tag exists.</returns>
    public Tag? this[Tag? tag] => (Tag?)base[tag?.Name!];

    /// <summary>
    /// Adds an element with the specified key and value to the collection in a thread-safe manner.
    /// </summary>
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

    /// <summary>
    /// Adds the specified tag to the collection with the associated key.
    /// </summary>
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

    /// <summary>
    /// Adds the specified tag to the collection.
    /// </summary>
    /// <param name="tag">The tag to add to the collection. Cannot be null.</param>
    public void Add(Tag tag)
    {
        lock (_lockObject)
        {
            base.Add(tag?.Name!, tag);
        }
    }

    /// <summary>
    /// Adds the specified key and associated tags to the collection.
    /// </summary>
    /// <param name="key">The key with which the specified tags are to be associated. Cannot be null.</param>
    /// <param name="tags">The tags to associate with the specified key. Cannot be null.</param>
    public void Add(object key, Tags tags)
    {
        lock (_lockObject)
        {
            base.Add(key, tags);
        }
    }

    /// <summary>
    /// Adds a collection of tags to the current instance, including only those tags whose values are not null.
    /// </summary>
    /// <param name="tags">The collection of <see cref="Tag"/> objects to add. Only tags with non-null values are added.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="tags"/> is null.</exception>
    public void AddRange(IEnumerable<Tag> tags)
    {
        if (tags == null)
        {
            throw new ArgumentNullException(nameof(tags));
        }

        lock (_lockObject)
        {
            foreach (var tag in tags)
            {
                if (tag.Value != null)
                {
                    base.Add(tag.Name!, tag);
                }
            }
        }
    }

    /// <summary>
    /// Retrieves a collection of tags that have non-null values.
    /// </summary>
    /// <returns>A <see cref="Tags"/> collection containing all tags with non-null values. The collection will be empty if no
    /// such tags exist.</returns>
    public Tags GetTags()
    {
        var tags = new Tags();
        tags.AddRange(ToList().Where(x => x.Value != null));

        return tags;
    }

    /// <summary>
    /// Returns a list containing all tags in the collection.
    /// </summary>
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
                result = [.. hashtableCopy.Values.OfType<Tag>()];
            }
            catch
            {
                return [];
            }
        }

        return result;
    }
}
