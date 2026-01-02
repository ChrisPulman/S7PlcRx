// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using S7PlcRx.Enums;

namespace S7PlcRx.Tests;

/// <summary>
/// Tests for internal S7MultiVar request building.
/// </summary>
public class S7MultiVarRequestBuilderTests
{
    /// <summary>
    /// Ensures a read-var request is built with a valid TPKT header and correct item count.
    /// </summary>
    [Test]
    public void BuildReadVarRequest_WithSingleItem_ShouldBuildPacket()
    {
        var s7MultiVar = GetS7MultiVarType();
        var readItemType = GetNestedType(s7MultiVar, "ReadItem");

        var items = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(readItemType))!;
        items.Add(Activator.CreateInstance(readItemType, DataType.DataBlock, 1, 0, 1, "T0")!);

        var method = s7MultiVar.GetMethod("BuildReadVarRequest", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);

        var bytes = (byte[])method!.Invoke(null, new object[] { items })!;

        Assert.That(bytes, Is.Not.Null);
        Assert.That(bytes.Length, Is.GreaterThanOrEqualTo(31));

        // TPKT
        Assert.That(bytes[0], Is.EqualTo(0x03));
        Assert.That(bytes[1], Is.EqualTo(0x00));
        Assert.That(bytes[2], Is.EqualTo(0x00));

        // function = Read Var (0x04) and item count
        Assert.That(bytes[17], Is.EqualTo(0x04));
        Assert.That(bytes[18], Is.EqualTo(0x01));
    }

    /// <summary>
    /// Ensures a write-var request includes a non-zero data section and correct item count.
    /// </summary>
    [Test]
    public void BuildWriteVarRequest_WithSingleItem_ShouldBuildPacket()
    {
        var s7MultiVar = GetS7MultiVarType();
        var writeItemType = GetNestedType(s7MultiVar, "WriteItem");

        var items = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(writeItemType))!;
        items.Add(Activator.CreateInstance(writeItemType, DataType.DataBlock, 1, 0, 1, (byte)0x02, new byte[] { 0x12, 0x34 }, "W0")!);

        var method = s7MultiVar.GetMethod("BuildWriteVarRequest", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);

        var bytes = (byte[])method!.Invoke(null, new object[] { items })!;

        Assert.That(bytes, Is.Not.Null);
        Assert.That(bytes.Length, Is.GreaterThanOrEqualTo(35));

        // TPKT
        Assert.That(bytes[0], Is.EqualTo(0x03));
        Assert.That(bytes[1], Is.EqualTo(0x00));
        Assert.That(bytes[2], Is.EqualTo(0x00));

        // function = Write Var (0x05) and item count
        Assert.That(bytes[17], Is.EqualTo(0x05));
        Assert.That(bytes[18], Is.EqualTo(0x01));

        // data length should be non-zero for write
        var dataLen = (bytes[15] << 8) | bytes[16];
        Assert.That(dataLen, Is.GreaterThan(0));
    }

    /// <summary>
    /// Ensures item count constraint is enforced for read requests.
    /// </summary>
    [Test]
    public void BuildReadVarRequest_WhenMoreThan255Items_ShouldThrow()
    {
        var s7MultiVar = GetS7MultiVarType();
        var readItemType = GetNestedType(s7MultiVar, "ReadItem");

        var listType = typeof(List<>).MakeGenericType(readItemType);
        var items = (System.Collections.IList)Activator.CreateInstance(listType)!;
        for (var i = 0; i < 256; i++)
        {
            items.Add(Activator.CreateInstance(readItemType, DataType.DataBlock, 1, i * 2, 1, $"T{i}")!);
        }

        var method = s7MultiVar.GetMethod("BuildReadVarRequest", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);

        var ex = Assert.Throws<TargetInvocationException>(() => method!.Invoke(null, new object[] { items }));
        Assert.That(ex!.InnerException, Is.TypeOf<ArgumentOutOfRangeException>());
    }

    private static Type GetS7MultiVarType()
    {
        var asm = typeof(RxS7).Assembly;
        var t = asm.GetType("S7PlcRx.Core.S7MultiVar", throwOnError: false);
        Assert.That(t, Is.Not.Null);
        return t!;
    }

    private static Type GetNestedType(Type parent, string name)
    {
        var t = parent.GetNestedType(name, BindingFlags.NonPublic);
        Assert.That(t, Is.Not.Null);
        return t!;
    }
}
