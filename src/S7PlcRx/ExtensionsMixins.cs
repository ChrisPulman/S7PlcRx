// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        public static void AddUpdateTagItem<T>(this IRxS7 @this, string tagName, string address)
        {
            if (@this is RxS7 plc)
            {
                plc.AddUpdateTagItem(new(tagName, address, typeof(T)));
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
    }
}
