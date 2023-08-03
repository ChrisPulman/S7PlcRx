// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace S7PlcRx;

/// <summary>
/// Extensions Class.
/// </summary>
public static class ExtensionsMixins
{
    /// <summary>
    /// Adds the update tag item.
    /// </summary>
    /// <typeparam name="T">The Type.</typeparam>
    /// <param name="this">The this.</param>
    /// <param name="tagName">The tag name.</param>
    /// <param name="address">The address.</param>
    /// <param name="arrayLength">Length of the array.</param>
    /// <returns>A Tag.</returns>
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
    /// Adds the update tag item.
    /// </summary>
    /// <typeparam name="T">The Type.</typeparam>
    /// <param name="this">The this.</param>
    /// <param name="tagName">The tag name.</param>
    /// <param name="address">The address.</param>
    /// <param name="arrayLength">Length of the array.</param>
    /// <returns>A Tag.</returns>
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
    /// Sets the tag to poll for values.
    /// </summary>
    /// <param name="this">The instance of tag.</param>
    /// <param name="polling">if set to <c>true</c> [poll].</param>
    /// <returns>The instance.</returns>
    public static (ITag? tag, IRxS7? plc) SetTagPollIng(this (ITag? tag, IRxS7? plc) @this, bool polling = true)
    {
        @this.tag?.SetDoNotPoll(!polling);
        return @this;
    }

    /// <summary>
    /// Gets the tag.
    /// </summary>
    /// <param name="this">The rx s7 plc instance.</param>
    /// <param name="tagName">Name of the tag.</param>
    /// <returns>The instance of tag.</returns>
    public static (ITag? tag, IRxS7? plc) GetTag(this IRxS7 @this, string tagName) =>
        @this?.TagList[tagName!] switch
        {
            Tag tag => (tag, @this),
            _ => (default, @this)
        };

    /// <summary>
    /// Removes the tag item.
    /// </summary>
    /// <param name="this">The this.</param>
    /// <param name="tagName">The tag name.</param>
    public static void RemoveTagItem(this IRxS7 @this, string tagName)
    {
        if (@this is RxS7 plc)
        {
            plc.RemoveTagItem(tagName);
        }
    }

    /// <summary>
    /// Tags to dictionary.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>
    /// A Dictionary.
    /// </returns>
    public static IObservable<IDictionary<string, TValue>> TagToDictionary<TValue>(this IObservable<Tag?> source)
    {
        IDictionary<string, TValue> tagValues = new Dictionary<string, TValue>();
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
    /// Tags to dictionary.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="tag">The tag.</param>
    /// <returns>
    /// A Dictionary.
    /// </returns>
    public static IObservable<(string Tag, TValue Value)> ToTagValue<TValue>(this IObservable<TValue?> source, string tag) =>
        source
            .Where(t => t != null)
            .Select(t => (Tag: tag, Value: t!))
            .Where(t => t.Value != null);
}
