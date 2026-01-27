// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Enums;

/// <summary>
/// Specifies the supported variable types for data representation and communication with Siemens S7 PLCs.
/// </summary>
/// <remarks>This enumeration defines the various data types that can be used when reading from or writing to a
/// Siemens S7 programmable logic controller (PLC). The types correspond to standard S7 and IEC data formats, including
/// bit, byte, word, double word, integer, floating-point, string, timer, counter, and date/time representations. Some
/// types, such as S7String and S7WString, represent variable-length string formats specific to the S7 protocol. The
/// Time member is serialized as an S7 DInt and deserialized as a C# TimeSpan.</remarks>
internal enum VarType
{
    /// <summary>
    /// Represents a single binary digit, which can have a value of 0 or 1.
    /// </summary>
    Bit,

    /// <summary>
    /// Represents an 8-bit unsigned integer.
    /// </summary>
    /// <remarks>The Byte type provides methods to compare, convert, and format 8-bit unsigned integer values.
    /// It is not CLS-compliant. The range of a Byte is 0 to 255. Use Byte when you need to store small, non-negative
    /// integer values.</remarks>
    Byte,

    /// <summary>
    /// Represents a single word in a text or document.
    /// </summary>
    Word,

    /// <summary>
    /// Represents a 32-bit unsigned integer value, commonly referred to as a double word (DWORD).
    /// </summary>
    /// <remarks>This type is typically used for interoperability with native APIs or file formats that
    /// require 32-bit unsigned values. Use this type when working with data structures or protocols that specifically
    /// require a DWORD representation.</remarks>
    DWord,

    /// <summary>
    /// Represents a 32-bit signed integer.
    /// </summary>
    Int,

    /// <summary>
    /// Represents a double-width integer value for high-precision arithmetic operations.
    /// </summary>
    /// <remarks>Use this type when calculations require greater range or precision than standard integer
    /// types provide. The specific behavior and supported operations depend on the implementation of this
    /// type.</remarks>
    DInt,

    /// <summary>
    /// Represents a real number value.
    /// </summary>
    Real,

    /// <summary>
    /// Represents a double-precision floating-point value, typically used for high-precision numerical calculations.
    /// </summary>
    LReal,

    /// <summary>
    /// Represents text as a sequence of Unicode characters.
    /// </summary>
    /// <remarks>The String class provides methods for comparing, searching, and manipulating strings. String
    /// objects are immutable; any operation that appears to modify a string actually returns a new string instance.
    /// This class is commonly used for storing and processing textual data in .NET applications.</remarks>
    String,

    /// <summary>
    /// Represents a string encoded in the Siemens S7 PLC string format.
    /// </summary>
    /// <remarks>The S7 string format is commonly used for communication with Siemens S7 programmable logic
    /// controllers (PLCs). This type provides functionality for working with S7-formatted strings, which may include
    /// length prefixes and specific encoding requirements. Use this type when reading from or writing to S7 PLC data
    /// blocks that store string values.</remarks>
    S7String,

    /// <summary>
    /// Represents a Siemens S7 wide (Unicode) string value used for communication with S7 PLCs.
    /// </summary>
    /// <remarks>S7WString is typically used to encode and decode wide-character strings when interacting with
    /// Siemens S7 programmable logic controllers (PLCs). The format supports Unicode characters and is commonly used
    /// for internationalization or when non-ASCII characters are required in PLC data exchange.</remarks>
    S7WString,

    /// <summary>
    /// Represents a timer that executes a callback method at specified intervals.
    /// </summary>
    /// <remarks>The Timer class is typically used to perform recurring operations at regular time intervals.
    /// It can be used for scheduling tasks, periodic polling, or implementing timeouts. Timers are not guaranteed to
    /// execute callbacks precisely on schedule, especially under heavy system load or if the callback takes a long time
    /// to execute. Thread safety and callback execution context may vary depending on the specific Timer
    /// implementation.</remarks>
    Timer,

    /// <summary>
    /// Represents a simple counter that tracks a numeric value, typically used for counting occurrences or events.
    /// </summary>
    Counter,

    /// <summary>
    /// Represents an instant in time, typically expressed as a date and time of day.
    /// </summary>
    /// <remarks>A DateTime value is measured in 100-nanosecond units called ticks, and can represent dates
    /// and times ranging from 12:00:00 midnight, January 1, 0001 to 11:59:59 PM, December 31, 9999. DateTime supports
    /// both date and time arithmetic, and can represent values in either the local time zone, Coordinated Universal
    /// Time (UTC), or as unspecified. Use the Kind property to determine or specify the time zone context of a DateTime
    /// value.</remarks>
    DateTime,

    /// <summary>
    /// IEC date (legacy) variable type.
    /// </summary>
    Date,

    /// <summary>
    /// Represents a date and time value with extended range or precision compared to the standard <see
    /// cref="DateTime"/> structure.
    /// </summary>
    /// <remarks>Use this type when the standard <see cref="DateTime"/> does not provide sufficient range or
    /// precision for your application's requirements. The specific behavior and capabilities may vary depending on the
    /// implementation.</remarks>
    DateTimeLong,

    /// <summary>
    /// S7 TIME variable type - serialized as S7 DInt and deserialized as C# TimeSpan.
    /// </summary>
    Time
}
