// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using MockS7Plc;
using S7PlcRx.PlcTypes;

namespace S7PlcRx.Tests;

/// <summary>
/// Tests that large byte[] reads and writes work correctly across all payload sizes,
/// including payloads that require multiple PDU-sized chunks (multi-chunk path).
/// Each test case writes packed S7-String data via the PLC protocol, reads it back as
/// byte[], converts to a list of strings and compares, then writes modified data back and
/// reads once more to confirm the round-trip is correct.
/// </summary>
[NonParallelizable]
public class S7PlcRxLargeDataTests
{
    // Each S7 string slot = 2 (header) + reservedLength bytes.
    private const int StringReservedLength = 20;
    private const int StringSlotSize = 2 + StringReservedLength; // 22 bytes

    // Sizes exercised: sub-PDU, near-PDU, multi-chunk × 2.
    private static readonly int[] DataSizes = [64, 960, 2000, 4000];

    /// <summary>
    /// Verifies that a byte[] payload of the given total size can be written to the MockServer,
    /// read back, converted to a list of strings, compared, then written back and read again.
    /// </summary>
    /// <param name="totalBytes">Total byte footprint to test.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [TestCaseSource(nameof(DataSizes))]
    public async Task LargeStringBlock_SeedReadWriteRoundTrip_ShouldMatchAtAllSizes(int totalBytes)
    {
        // ── Build the seed payload ──────────────────────────────────────────────
        var stringCount = totalBytes / StringSlotSize;
        if (stringCount == 0)
        {
            stringCount = 1;
        }

        var actualTotalBytes = stringCount * StringSlotSize;
        var seedStrings = BuildStringList(stringCount);
        var seedBytes = StringListToBytes(seedStrings);
        Assert.That(seedBytes.Length, Is.EqualTo(actualTotalBytes), "Seed byte count should match string packing.");

        // ── Start server with DB1 large enough for the payload ─────────────────
        // DB1 is auto-registered by MockServer.Start(). Size must cover the payload.
        using var server = new MockServer();
        server.DefaultDb1Size = Math.Max(4096, actualTotalBytes + 64);

        var rc = server.Start();
        Assert.That(rc, Is.EqualTo(0), "Server Start should succeed.");

        // ── Connect PLC and register tag ───────────────────────────────────────
        using var plc = new RxS7(S7PlcRx.Enums.CpuType.S71500, MockServer.Localhost, 0, 1, null, interval: 100);
        plc.AddUpdateTagItem<byte[]>("LargeBlock", "DB1.DBB0", actualTotalBytes).SetTagPollIng(false);

        await plc.IsConnected.FirstAsync(x => x).Timeout(System.TimeSpan.FromSeconds(10));

        // ── Seed via write-first (the only reliable way with MockServer) ────────
        plc.Value("LargeBlock", seedBytes);

        // ── Read back and compare ───────────────────────────────────────────────
        var readBytes = await WaitForExpectedBytesAsync(plc, "LargeBlock", seedBytes, System.TimeSpan.FromSeconds(10));
        Assert.That(readBytes, Is.Not.Null, $"Read of {actualTotalBytes} bytes should return non-null (size={totalBytes}).");
        Assert.That(readBytes!.Length, Is.EqualTo(actualTotalBytes), $"Read byte count should equal seeded count (size={totalBytes}).");

        var readStrings = BytesToStringList(readBytes, stringCount);
        Assert.That(readStrings, Is.EqualTo(seedStrings), $"Strings read from PLC should match seeded strings (size={totalBytes}).");

        // ── Write back modified data and read again ────────────────────────────
        var altStrings = seedStrings.ConvertAll(ModifyString);
        var altBytes = StringListToBytes(altStrings);

        plc.Value("LargeBlock", altBytes);

        var readBytes2 = await WaitForExpectedBytesAsync(plc, "LargeBlock", altBytes, System.TimeSpan.FromSeconds(10));
        Assert.That(readBytes2, Is.Not.Null, $"Second read after write should return non-null (size={totalBytes}).");
        Assert.That(readBytes2!.Length, Is.EqualTo(actualTotalBytes), $"Second read byte count should equal written count (size={totalBytes}).");

        var readStrings2 = BytesToStringList(readBytes2, stringCount);
        Assert.That(readStrings2, Is.EqualTo(altStrings), $"Strings after write should match modified strings (size={totalBytes}).");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>Builds a list of distinct, deterministic strings of varying lengths up to <see cref="StringReservedLength"/>.</summary>
    private static List<string> BuildStringList(int count)
    {
        var list = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            // Create strings of varying lengths that fit within the reserved area.
            var len = (i % StringReservedLength) + 1;
            list.Add(new string((char)('A' + (i % 26)), len) + i.ToString(System.Globalization.CultureInfo.InvariantCulture));

            // Truncate to reserved length if the i.ToString() made it too long.
            if (list[i].Length > StringReservedLength)
            {
                list[i] = list[i][..StringReservedLength];
            }
        }

        return list;
    }

    /// <summary>Returns a modified version of the string by rotating the first character.</summary>
    private static string ModifyString(string s)
    {
        if (s.Length == 0)
        {
            return "X";
        }

        var rotated = (char)(((s[0] - 'A' + 1) % 26) + 'A');
        return rotated + s[1..];
    }

    private static async Task<byte[]?> WaitForExpectedBytesAsync(RxS7 plc, string tagName, byte[] expected, System.TimeSpan timeout)
    {
        var deadline = System.DateTime.UtcNow + timeout;
        byte[]? latest = null;

        while (System.DateTime.UtcNow < deadline)
        {
            latest = await plc.ValueAsync<byte[]>(tagName, CancellationToken.None);
            if (latest is { Length: > 0 } && latest.Length == expected.Length && latest.AsSpan().SequenceEqual(expected))
            {
                return latest;
            }

            await Task.Delay(100);
        }

        return latest;
    }

    /// <summary>Encodes a list of strings as back-to-back S7 string slots, each <see cref="StringSlotSize"/> bytes.</summary>
    private static byte[] StringListToBytes(IList<string> strings)
    {
        var buf = new byte[strings.Count * StringSlotSize];
        var offset = 0;
        foreach (var s in strings)
        {
            S7String.ToSpan(s, StringReservedLength, buf.AsSpan(offset, StringSlotSize));
            offset += StringSlotSize;
        }

        return buf;
    }

    /// <summary>Decodes a byte[] containing back-to-back S7 string slots back into a list of strings.</summary>
    private static List<string> BytesToStringList(byte[] bytes, int count)
    {
        var list = new List<string>(count);
        var offset = 0;
        for (var i = 0; i < count; i++)
        {
            if (offset + StringSlotSize > bytes.Length)
            {
                break;
            }

            list.Add(S7String.FromSpan(bytes.AsSpan(offset, StringSlotSize)));
            offset += StringSlotSize;
        }

        return list;
    }
}
