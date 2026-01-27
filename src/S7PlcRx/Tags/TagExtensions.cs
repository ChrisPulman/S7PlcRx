// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace S7PlcRx;

/// <summary>
/// Provides extension methods for managing and interacting with tag items in IRxS7-based PLC systems. These methods
/// enable adding, updating, retrieving, and removing tags, as well as converting tag streams to dictionary
/// representations.
/// </summary>
/// <remarks>The TagExtensions class offers a fluent API for working with tags in Siemens S7 PLC communication
/// scenarios. It includes methods for adding tags with type and array support, controlling polling behavior, and
/// transforming tag data streams. All methods are implemented as extension methods to enhance usability and integration
/// with IRxS7 and related types. Thread safety and error handling depend on the underlying IRxS7 and Tag
/// implementations.</remarks>
public static class TagExtensions
{
    /// <summary>
    /// Adds or updates a tag item of the specified type in the PLC and returns the created tag and the PLC instance.
    /// </summary>
    /// <remarks>If the specified type parameter is a string or an array type and an array length is provided,
    /// the tag will be created as an array with the given length. Otherwise, the tag will be created as a single value.
    /// This method is an extension method for IRxS7 implementations that support tag management.</remarks>
    /// <typeparam name="T">The data type of the tag to add or update. This determines the type of value the tag will hold.</typeparam>
    /// <param name="this">The PLC instance to which the tag item will be added or updated.</param>
    /// <param name="tagName">The name to assign to the tag item.</param>
    /// <param name="address">The address in the PLC where the tag item is located.</param>
    /// <param name="arrayLength">The length of the array for the tag item, if the tag represents an array type. Specify null for non-array types.</param>
    /// <returns>A tuple containing the created or updated tag as the first element and the PLC instance as the second element.
    /// The tag will be null if the PLC instance does not support tag operations.</returns>
    public static (ITag? tag, IRxS7? plc) AddUpdateTagItem<T>(this IRxS7 @this, string tagName, string address, int? arrayLength = null)
    {
        var tag = default(Tag);
        if (@this is RxS7 plc)
        {
            if ((typeof(T) == typeof(string) || typeof(T).IsArray) && arrayLength.HasValue)
            {
                tag = new(tagName, address, typeof(T), arrayLength.Value);
            }
            else
            {
                tag = new(tagName, address, typeof(T));
            }

            plc.AddUpdateTagItem(tag);
        }

        return (tag, @this);
    }

    /// <summary>
    /// Adds or updates a tag item in the specified PLC instance and returns the created tag and the PLC reference.
    /// </summary>
    /// <remarks>If the specified type is an array or a string, the array length must be provided. If the PLC
    /// instance does not support adding or updating tags, the returned tag will be null.</remarks>
    /// <param name="this">The PLC instance to which the tag item will be added or updated.</param>
    /// <param name="type">The data type of the tag to add or update. Must not be null.</param>
    /// <param name="tagName">The name of the tag to add or update.</param>
    /// <param name="address">The address in the PLC where the tag is located.</param>
    /// <param name="arrayLength">The length of the array if the tag type is an array or a string; otherwise, null.</param>
    /// <returns>A tuple containing the created tag (or null if the operation was not successful) and the PLC reference.</returns>
    public static (ITag? tag, IRxS7? plc) AddUpdateTagItem(this IRxS7 @this, Type type, string tagName, string address, int? arrayLength = null)
    {
        var tag = default(Tag);
        if (@this is RxS7 plc && type != null)
        {
            if ((type == typeof(string) || type.IsArray) && arrayLength.HasValue)
            {
                tag = new(tagName, address, type, arrayLength.Value);
            }
            else
            {
                tag = new(tagName, address, type);
            }

            plc.AddUpdateTagItem(tag);
        }

        return (tag, @this);
    }

    /// <summary>
    /// Adds or updates a tag item of the specified type in the PLC and returns the created tag along with the PLC
    /// instance.
    /// </summary>
    /// <remarks>If the PLC instance is not of type <see cref="RxS7"/>, no tag is added or updated and the
    /// returned tag will be null. When adding a string or array tag, <paramref name="arrayLength"/> must be specified
    /// to define the size of the tag.</remarks>
    /// <typeparam name="T">The data type of the tag to add or update. This can be a primitive type, string, or array type.</typeparam>
    /// <param name="this">A tuple containing the current tag (which may be null) and the PLC instance in which to add or update the tag.</param>
    /// <param name="tagName">The name to assign to the tag item.</param>
    /// <param name="address">The address in the PLC where the tag is located.</param>
    /// <param name="arrayLength">The length of the array if the tag represents an array type. This parameter is required when <typeparamref
    /// name="T"/> is a string or an array type; otherwise, it is ignored.</param>
    /// <returns>A tuple containing the created or updated tag as <see cref="ITag"/> and the PLC instance as <see cref="IRxS7"/>.
    /// If the PLC instance is null, the tag will also be null.</returns>
    public static (ITag? tag, IRxS7? plc) AddUpdateTagItem<T>(this (ITag? _, IRxS7? plc) @this, string tagName, string address, int? arrayLength = null)
    {
        var tag = default(Tag);
        if (@this.plc is RxS7 plc)
        {
            if ((typeof(T) == typeof(string) || typeof(T).IsArray) && arrayLength.HasValue)
            {
                tag = new(tagName, address, typeof(T), arrayLength.Value);
            }
            else
            {
                tag = new(tagName, address, typeof(T));
            }

            plc.AddUpdateTagItem(tag);
        }

        return (tag, @this.plc);
    }

    /// <summary>
    /// Adds or updates a tag item in the specified PLC instance using the provided type, tag name, and address.
    /// </summary>
    /// <remarks>If the tag type is a string or an array, the array length must be specified. The method does
    /// not modify the PLC instance if it is null or if the type is null.</remarks>
    /// <param name="this">A tuple containing the current tag (ignored) and the PLC instance in which to add or update the tag item.</param>
    /// <param name="type">The type of the tag to add or update. Must not be null.</param>
    /// <param name="tagName">The name of the tag to add or update in the PLC.</param>
    /// <param name="address">The address in the PLC where the tag is located.</param>
    /// <param name="arrayLength">The length of the array if the tag type is an array or a string. Optional.</param>
    /// <returns>A tuple containing the created or updated tag and the PLC instance. The tag is null if the PLC instance or type
    /// is null.</returns>
    public static (ITag? tag, IRxS7? plc) AddUpdateTagItem(this (ITag? _, IRxS7? plc) @this, Type type, string tagName, string address, int? arrayLength = null)
    {
        var tag = default(Tag);
        if (@this.plc is RxS7 plc && type != null)
        {
            if ((type == typeof(string) || type.IsArray) && arrayLength.HasValue)
            {
                tag = new(tagName, address, type, arrayLength.Value);
            }
            else
            {
                tag = new(tagName, address, type);
            }

            plc.AddUpdateTagItem(tag);
        }

        return (tag, @this.plc);
    }

    /// <summary>
    /// Enables or disables polling for the specified tag by setting its polling state.
    /// </summary>
    /// <remarks>If the tag is null, this method has no effect. This method does not modify the PLC
    /// instance.</remarks>
    /// <param name="this">A tuple containing the tag and PLC instance to update.</param>
    /// <param name="polling">true to enable polling for the tag; false to disable polling. The default is true.</param>
    /// <returns>A tuple containing the original tag and PLC instance.</returns>
    public static (ITag? tag, IRxS7? plc) SetTagPollIng(this (ITag? tag, IRxS7? plc) @this, bool polling = true)
    {
        @this.tag?.SetDoNotPoll(!polling);
        return @this;
    }

    /// <summary>
    /// Retrieves the tag with the specified name from the PLC's tag list, along with the associated PLC instance.
    /// </summary>
    /// <remarks>If the specified tag name does not exist in the PLC's tag list, the method returns a tuple
    /// with a null tag and the original PLC instance. This method is an extension method for the IRxS7
    /// interface.</remarks>
    /// <param name="this">The PLC instance from which to retrieve the tag.</param>
    /// <param name="tagName">The name of the tag to retrieve. Cannot be null.</param>
    /// <returns>A tuple containing the tag that matches the specified name and the PLC instance. If the tag is not found, the
    /// tag element of the tuple is null.</returns>
    public static (ITag? tag, IRxS7? plc) GetTag(this IRxS7 @this, string tagName) =>
        @this?.TagList[tagName!] switch
        {
            Tag tag => (tag, @this),
            _ => (default, @this)
        };

    /// <summary>
    /// Removes the tag item with the specified name from the underlying RxS7 instance, if applicable.
    /// </summary>
    /// <remarks>If the underlying object is not an RxS7 instance, this method has no effect.</remarks>
    /// <param name="this">The IRxS7 instance from which to remove the tag item.</param>
    /// <param name="tagName">The name of the tag item to remove. Cannot be null.</param>
    public static void RemoveTagItem(this IRxS7 @this, string tagName)
    {
        if (@this is RxS7 plc)
        {
            plc.RemoveTagItem(tagName);
        }
    }

    /// <summary>
    /// Projects a sequence of nullable Tag objects into a stream of dictionaries containing the most recent values for
    /// each tag name, filtered by the specified value type.
    /// </summary>
    /// <remarks>Each emitted dictionary contains all tag names encountered so far whose values are of type
    /// TValue and are not null. The dictionary is updated with each new matching tag in the source sequence. The same
    /// dictionary instance is reused and updated for each emission; callers should not modify the returned
    /// dictionary.</remarks>
    /// <typeparam name="TValue">The type to which tag values are filtered and cast. Only tags whose values are of this type are included in the
    /// resulting dictionaries.</typeparam>
    /// <param name="source">The observable sequence of nullable Tag objects to process.</param>
    /// <returns>An observable sequence of dictionaries mapping tag names to their most recent non-null values of type TValue.
    /// Each dictionary reflects the accumulated state up to that point in the source sequence.</returns>
    public static IObservable<IDictionary<string, TValue>> TagToDictionary<TValue>(this IObservable<Tag?> source)
    {
        var tagValues = new Dictionary<string, TValue>();
        return source
            .Where(t => t != null && t?.Value is TValue)
            .Select(t => (Name: t!.Name!, Value: (TValue)t!.Value!))
            .Where(t => t.Value != null)
            .Select(t =>
            {
                tagValues[t.Name] = t.Value;
                return tagValues;
            });
    }

    /// <summary>
    /// Projects each non-null value in the source sequence into a tuple containing the specified tag and the value.
    /// </summary>
    /// <remarks>Null values in the source sequence are filtered out. The resulting sequence will not contain
    /// any tuples with a null value.</remarks>
    /// <typeparam name="TValue">The type of the values in the source sequence.</typeparam>
    /// <param name="source">The observable sequence of nullable values to process. Only non-null values are included in the result.</param>
    /// <param name="tag">The tag to associate with each value in the resulting sequence. This value is included in each emitted tuple.</param>
    /// <returns>An observable sequence of tuples, where each tuple contains the specified tag and a non-null value from the
    /// source sequence.</returns>
    public static IObservable<(string Tag, TValue Value)> ToTagValue<TValue>(this IObservable<TValue?> source, string tag) =>
        source
            .Where(t => t != null)
            .Select(t => (Tag: tag, Value: t!))
            .Where(t => t.Value != null);
}
