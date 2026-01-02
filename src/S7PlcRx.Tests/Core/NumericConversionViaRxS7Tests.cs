// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using S7PlcRx.Enums;

namespace S7PlcRx.Tests.Core;

/// <summary>
/// Verifies numeric decoding performed by RxS7 internal VarType parsing.
/// </summary>
public class NumericConversionViaRxS7Tests
{
    /// <summary>
    /// Ensures internal VarType-to-bytes parsing uses the expected big-endian conventions.
    /// </summary>
    [Test]
    public void ParseBytes_ShouldDecodeWordDWordDInt()
    {
        using var plc = new RxS7(CpuType.S7200, "127.0.0.1", rack: 0, slot: 0);

        var parseBytes = typeof(RxS7).GetMethod("ParseBytes", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(parseBytes, Is.Not.Null);

        // Word (ushort): 0x1234
        var word = (ushort)parseBytes!.Invoke(plc, new object[] { VarType.Word, new byte[] { 0x12, 0x34 }, 1 })!;
        Assert.That(word, Is.EqualTo(0x1234));

        // DWord (uint): 0x01020304
        var dword = (uint)parseBytes.Invoke(plc, new object[] { VarType.DWord, new byte[] { 0x01, 0x02, 0x03, 0x04 }, 1 })!;
        Assert.That(dword, Is.EqualTo(0x01020304u));

        // DInt (int): -1 => 0xFFFFFFFF
        var dint = (int)parseBytes.Invoke(plc, new object[] { VarType.DInt, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, 1 })!;
        Assert.That(dint, Is.EqualTo(-1));
    }

    /// <summary>
    /// Ensures internal floating parsing roundtrips using S7 big-endian format.
    /// </summary>
    [Test]
    public void ParseBytes_ShouldDecodeRealAndLReal()
    {
        using var plc = new RxS7(CpuType.S7200, "127.0.0.1", rack: 0, slot: 0);

        var parseBytes = typeof(RxS7).GetMethod("ParseBytes", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(parseBytes, Is.Not.Null);

        // float 1.0f => 0x3F800000 (big-endian bytes)
        var real = (float)parseBytes!.Invoke(plc, new object[] { VarType.Real, new byte[] { 0x3F, 0x80, 0x00, 0x00 }, 1 })!;
        Assert.That(real, Is.EqualTo(1.0f));

        // double 1.0 => 0x3FF0000000000000 (big-endian bytes)
        var lreal = (double)parseBytes.Invoke(plc, new object[] { VarType.LReal, new byte[] { 0x3F, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 1 })!;
        Assert.That(lreal, Is.EqualTo(1.0d));
    }
}
