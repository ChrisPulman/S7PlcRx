// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive;
#else
namespace S7PlcRx;
#endif

/// <summary>Provides extension methods for managing and interacting with tag items in IRxS7-based PLC systems.</summary>
public static class TagExtensions
{
    /// <summary>Provides fluent tag-management extensions for tag/PLC tuples.</summary>
    /// <param name="current">The current tag and PLC tuple.</param>
    extension((ITag? tag, IRxS7? plc) current)
    {
        /// <summary>Adds or updates a tag item of the specified type in the PLC.</summary>
        /// <typeparam name="T">The data type of the tag to add or update.</typeparam>
        /// <param name="tagName">The name to assign to the tag item.</param>
        /// <param name="address">The address in the PLC where the tag is located.</param>
        /// <param name="arrayLength">The length of the array if the tag represents an array type.</param>
        /// <returns>A tuple containing the created or updated tag and the PLC instance.</returns>
        public (ITag? tag, IRxS7? plc) AddUpdateTagItem<T>(string tagName, string address, int? arrayLength = null)
        {
            var tag = default(Tag);
            if (current.plc is RxS7 plc)
            {
                tag = CreateTag(typeof(T), tagName, address, arrayLength);
                plc.AddUpdateTagItemInternal(tag);
            }

            return (tag, current.plc);
        }

        /// <summary>Adds or updates a tag item in the specified PLC instance.</summary>
        /// <param name="type">The type of the tag to add or update.</param>
        /// <param name="tagName">The name of the tag to add or update in the PLC.</param>
        /// <param name="address">The address in the PLC where the tag is located.</param>
        /// <param name="arrayLength">The length of the array if the tag type is an array or a string.</param>
        /// <returns>A tuple containing the created or updated tag and the PLC instance.</returns>
        public (ITag? tag, IRxS7? plc) AddUpdateTagItem(Type type, string tagName, string address, int? arrayLength = null)
        {
            var tag = default(Tag);
            if (current.plc is RxS7 plc && type is not null)
            {
                tag = CreateTag(type, tagName, address, arrayLength);
                plc.AddUpdateTagItemInternal(tag);
            }

            return (tag, current.plc);
        }

        /// <summary>Enables or disables polling for the specified tag by setting its polling state.</summary>
        /// <param name="polling">true to enable polling for the tag; false to disable polling.</param>
        /// <returns>A tuple containing the original tag and PLC instance.</returns>
        public (ITag? tag, IRxS7? plc) SetTagPollIng(bool polling = true)
        {
            current.tag?.SetDoNotPoll(!polling);
            return current;
        }
    }

    /// <summary>Provides projection extensions for nullable observable values.</summary>
    /// <typeparam name="TValue">The type of the values in the source sequence.</typeparam>
    /// <param name="source">The observable sequence of nullable values to process.</param>
    extension<TValue>(IObservable<TValue?> source)
    {
        /// <summary>Projects each non-null value in the source sequence into a tuple containing the specified tag and the value.</summary>
        /// <param name="tag">The tag to associate with each value in the resulting sequence.</param>
        /// <returns>An observable sequence of tuples containing the specified tag and a non-null value.</returns>
        public IObservable<(string Tag, TValue Value)> ToTagValue(string tag) =>
            source
                .Where(t => t is not null)
                .Select(t => (Tag: tag, Value: t!))
                .Where(t => t.Value is not null);
    }

    /// <summary>Provides projection extensions for tag observables.</summary>
    /// <param name="source">The observable sequence of nullable tag objects.</param>
    extension(IObservable<Tag?> source)
    {
        /// <summary>Projects tag values into a stream of dictionaries containing the most recent values for each tag name.</summary>
        /// <typeparam name="TValue">The value type to include in the resulting dictionaries.</typeparam>
        /// <returns>An observable sequence of dictionaries mapping tag names to their most recent non-null values.</returns>
        public IObservable<IDictionary<string, TValue>> TagToDictionary<TValue>()
        {
            var tagValues = new Dictionary<string, TValue>();
            return source
                .Where(t => t is not null && t?.Value is TValue)
                .Select(t => (Name: t!.Name!, Value: (TValue)t!.Value!))
                .Where(t => t.Value is not null)
                .Select(t =>
                {
                    tagValues[t.Name] = t.Value;
                    return tagValues;
                });
        }
    }

    /// <summary>Provides tag-management extensions for PLC instances.</summary>
    /// <param name="plcSource">The PLC instance.</param>
    extension(IRxS7 plcSource)
    {
        /// <summary>Adds or updates a tag item of the specified type in the PLC.</summary>
        /// <typeparam name="T">The data type of the tag to add or update.</typeparam>
        /// <param name="tagName">The name to assign to the tag item.</param>
        /// <param name="address">The address in the PLC where the tag item is located.</param>
        /// <param name="arrayLength">The length of the array for the tag item, if the tag represents an array type.</param>
        /// <returns>A tuple containing the created or updated tag and the PLC instance.</returns>
        public (ITag? tag, IRxS7? plc) AddUpdateTagItem<T>(string tagName, string address, int? arrayLength = null)
        {
            var tag = default(Tag);
            if (plcSource is RxS7 plc)
            {
                tag = CreateTag(typeof(T), tagName, address, arrayLength);
                plc.AddUpdateTagItemInternal(tag);
            }

            return (tag, plcSource);
        }

        /// <summary>Adds or updates a tag item in the specified PLC instance.</summary>
        /// <param name="type">The data type of the tag to add or update.</param>
        /// <param name="tagName">The name of the tag to add or update.</param>
        /// <param name="address">The address in the PLC where the tag is located.</param>
        /// <param name="arrayLength">The length of the array if the tag type is an array or a string.</param>
        /// <returns>A tuple containing the created tag and the PLC reference.</returns>
        public (ITag? tag, IRxS7? plc) AddUpdateTagItem(Type type, string tagName, string address, int? arrayLength = null)
        {
            var tag = default(Tag);
            if (plcSource is RxS7 plc && type is not null)
            {
                tag = CreateTag(type, tagName, address, arrayLength);
                plc.AddUpdateTagItemInternal(tag);
            }

            return (tag, plcSource);
        }

        /// <summary>Retrieves the tag with the specified name from the PLC's tag list.</summary>
        /// <param name="tagName">The name of the tag to retrieve.</param>
        /// <returns>A tuple containing the tag that matches the specified name and the PLC instance.</returns>
        public (ITag? tag, IRxS7? plc) GetTag(string tagName) =>
            plcSource?.TagList[tagName!] switch
            {
                Tag tag => (tag, plcSource),
                _ => (default, plcSource)
            };

        /// <summary>Removes the tag item with the specified name from the underlying RxS7 instance, if applicable.</summary>
        /// <param name="tagName">The name of the tag item to remove.</param>
        public void RemoveTagItem(string tagName)
        {
            if (plcSource is not RxS7 plc)
            {
                return;
            }

            plc.RemoveTagItemInternal(tagName);
        }
    }

    /// <summary>Creates a tag using the optional fixed array length when required.</summary>
    /// <param name="type">The tag value type.</param>
    /// <param name="tagName">The tag name.</param>
    /// <param name="address">The tag address.</param>
    /// <param name="arrayLength">The optional fixed array length.</param>
    /// <returns>The created tag.</returns>
    private static Tag CreateTag(Type type, string tagName, string address, int? arrayLength) =>
        (type == typeof(string) || type.IsArray) && arrayLength.HasValue
            ? new(tagName, address, type, arrayLength.Value)
            : new(tagName, address, type);
}
