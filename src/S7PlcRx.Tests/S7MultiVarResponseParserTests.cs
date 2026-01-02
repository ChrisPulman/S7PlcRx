// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using S7PlcRx.Core;
using S7PlcRx.Enums;

namespace S7PlcRx.Tests;

/// <summary>
/// Tests for S7MultiVar response parsing.
/// </summary>
public class S7MultiVarResponseParserTests
{
    /// <summary>
    /// Ensures read-var response parsing returns an empty list for short frames.
    /// </summary>
    [Test]
    public void ParseReadVarResponse_WhenTooShort_ShouldReturnEmpty()
    {
        var items = new[] { new S7MultiVar.ReadItem(DataType.DataBlock, 1, 0, 1, "T0") };

        var result = S7MultiVar.ParseReadVarResponse(ReadOnlySpan<byte>.Empty, items, ArrayPool<byte>.Shared);

        Assert.That(result, Is.Empty);
    }

    /// <summary>
    /// Ensures read-var response parsing respects item padding to even byte length.
    /// </summary>
    [Test]
    public void ParseReadVarResponse_WithOddLengthData_ShouldSkipPadByte()
    {
        var items = new[]
        {
            new S7MultiVar.ReadItem(DataType.DataBlock, 1, 0, 1, "T0"),
            new S7MultiVar.ReadItem(DataType.DataBlock, 1, 1, 1, "T1"),
        };

        // Build minimal frame with:
        // - paramLength = 2 (so dataStart = 19)
        // - data section has 2 items:
        //   item0: rc=0xFF, ts=0x04, bitLen=8 => 1 byte data + 1 pad byte
        //   item1: rc=0xFF, ts=0x04, bitLen=8 => 1 byte data (no need to include final pad)
        var response = new byte[19 + 6 + 5];
        response[13] = 0x00;
        response[14] = 0x02;

        var o = 19;

        // item 0 header
        response[o + 0] = 0xFF;
        response[o + 1] = 0x04;
        response[o + 2] = 0x00;
        response[o + 3] = 0x08;
        response[o + 4] = 0xAA;
        response[o + 5] = 0x00; // pad
        o += 6;

        // item 1 header
        response[o + 0] = 0xFF;
        response[o + 1] = 0x04;
        response[o + 2] = 0x00;
        response[o + 3] = 0x08;
        response[o + 4] = 0xBB;

        var pool = ArrayPool<byte>.Shared;
        var result = S7MultiVar.ParseReadVarResponse(response, items, pool);
        try
        {
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result[0].Data.ToArray(), Is.EqualTo(new byte[] { 0xAA }));
            Assert.That(result[1].Data.ToArray(), Is.EqualTo(new byte[] { 0xBB }));
        }
        finally
        {
            foreach (var r in result)
            {
                if (r.RentedBuffer != null)
                {
                    pool.Return(r.RentedBuffer);
                }
            }
        }
    }

    /// <summary>
    /// Ensures write-var response parsing reads per-item return codes.
    /// </summary>
    [Test]
    public void ParseWriteVarResponse_ShouldReturnPerItemCodes()
    {
        var response = new byte[19 + 3];
        response[13] = 0x00;
        response[14] = 0x02;

        response[19] = 0xFF;
        response[20] = 0x0A;
        response[21] = 0xFF;

        var result = S7MultiVar.ParseWriteVarResponse(response, 3);

        Assert.That(result.Count, Is.EqualTo(3));
        Assert.That(result[0].ReturnCode, Is.EqualTo(0xFF));
        Assert.That(result[1].ReturnCode, Is.EqualTo(0x0A));
        Assert.That(result[2].ReturnCode, Is.EqualTo(0xFF));
    }
}
