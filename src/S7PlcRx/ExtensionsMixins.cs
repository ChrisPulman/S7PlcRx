// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace S7PlcRx
{
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
        public static void AddUpdateTagItem<T>(this IRxS7 @this, string tagName, string address, int? arrayLength = null)
        {
            if (@this is RxS7 plc)
            {
                if (typeof(T).IsArray && arrayLength.HasValue)
                {
                    plc.AddUpdateTagItem(new(tagName, address, typeof(T), arrayLength.Value));
                }
                else
                {
                    plc.AddUpdateTagItem(new(tagName, address, typeof(T)));
                }
            }
        }

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
                .Select(t => (Name: t!.Name!, Value: (TValue)t!.Value))
                .Where(t => t.Value != null)
                .Select(t =>
                {
                    tagValues[t.Name] = t.Value;
                    return tagValues;
                });
        }
    }
}
