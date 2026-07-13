// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using TUnitAssert = TUnit.Assertions.Assert;

namespace S7PlcRx.Tests;

/// <summary>
/// Live-PLC integration tests that validate the DecodeTagNames logic against a real
/// Siemens S7-1500 PLC at 172.16.13.1.
/// These tests require a physical (or network-accessible) PLC and are marked [Explicit]
/// so they are excluded from automated CI runs.
/// </summary>
[Explicit]
[Category("LivePLC")]
[NotInParallel]
public class S7PlcRxDecodeTagNamesLivePlcTests
{
    private const string PlcIp = "172.16.13.1";
    private const string TagNames1 = nameof(TagNames1);
    private const int HeaderByteCount = 4;
    private const byte FirstValidIsLoggingState = 1;
    private const byte LastValidIsLoggingState = 3;
    private const int BytesPerTagName = 32;
    private const int TagNameReservedLength = BytesPerTagName - 2;
    private const int TotalTagCount = 64;
    private const int TotalTagNameBytes = HeaderByteCount + (BytesPerTagName * TotalTagCount);

    /// <summary>
    /// The 64 tag names expected to be stored in DB102, as defined by the ST assignment
    /// block in the PLC program (indices 0–63).
    /// </summary>
    private static readonly string[] ExpectedTagNames =
    [
        "PRESSURE_SUPPLY_BARG",           // 0
        "PRESSURE_CONDITIONED_BARG",      // 1
        "PRESSURE_CASING_HIGH_BARG",      // 2
        "PRESSURE_CASING_LOW_BARG",       // 3
        "PRESSURE_CASING_BARG",           // 4
        "PRESSURE_INTERSPACE_HIGH_BARG",  // 5
        "PRESSURE_INTERSPACE_LOW_BARG",   // 6
        "PRESSURE_INTERSPACE_DE_BARG",    // 7
        "PRESSURE_INTERSPACE_NDE_BARG",   // 8
        "PRESSURE_BACKPRESSURE_DE_BARG",  // 9
        "PRESSURE_BACKPRESSURE_NDE_BARG", // 10
        "PRESSURE_OUTBOARD_DE_BARG",      // 11
        "PRESSURE_OUTBOARD_NDE_BARG",     // 12
        "PRESSURE_SEPARATIONSUPPLY_BARG", // 13
        "PRESSURE_SEPARATION_DE_BARG",    // 14
        "PRESSURE_SEPARATION_NDE_BARG",   // 15
        "PRESSURE_CASING_BP_BARG",        // 16
        "PRESSURE_SHUNTVESSEL_BARG",      // 17
        "PRESSURE_CONTROL_HIGH_BARG",     // 18
        "PRESSURE_CONTROL_LOW_BARG",      // 19
        "PRESSURE_GEARBOX_INLET_BARG",    // 20
        "LEAKAGE_OUTBOARD_DE_SLPM",       // 21
        "LEAKAGE_OUTBOARD_NDE_SLPM",      // 22
        "LEAKAGE_INBOARD_DE_SLPM",        // 23
        "LEAKAGE_INBOARD_NDE_SLPM",       // 24
        "LEAKAGE_SEPARATION_DE_SLPM",     // 25
        "LEAKAGE_SEPARATION_NDE_SLPM",    // 26
        "LEAKAGE_COMBINED_SLPM",          // 27
        "TEMPERATURE_BEARING_DE_C",       // 28
        "TEMPERATURE_OUTBOARD_DE_C",      // 29
        "TEMPERATURE_INBOARD_DE_C",       // 30
        "TEMPERATURE_CASING_C",           // 31
        "TEMPERATURE_INBOARD_NDE_C",      // 32
        "TEMPERATURE_OUTBOARD_NDE_C",     // 33
        "TEMPERATURE_BEARING_NDE_C",      // 34
        "TEMPERATURE_AUXILIARY_1_C",      // 35
        "TEMPERATURE_AUXILIARY_2_C",      // 36
        "TEMPERATURE_AUXILIARY_3_C",      // 37
        "TEMPERATURE_AUXILIARY_4_C",      // 38
        "TEMPERATURE_PEDBRG_INBOARD_C",   // 39
        "TEMPERATURE_PEDBRG_OUTBOARD_C",  // 40
        "TEMPERATURE_GEARBOX_INBOARD_C",  // 41
        "TEMPERATURE_GEARBOX_OUTBOARD_C", // 42
        "TEMPERATURE_GEARBOX_INLET_C",    // 43
        "TEMPERATURE_AMBIENT_C",          // 44
        "TEMPERATURE_COOLING_INLET_C",    // 45
        "TEMPERATURE_COOLING_OUTLET_C",   // 46
        "TEMPERATURE_OILTANK_C",          // 47
        "DRIVE_SPEED_RPM",                // 48
        "DRIVE_TORQUE_NM",                // 49
        "ACOUSTIC_DE_V",                  // 50
        "ACOUSTIC_NDE_V",                 // 51
        "VIBRATION_MOTOR_MMS",            // 52
        "VIBRATION_GEARBOX_X_MMS",        // 53
        "VIBRATION_GEARBOX_Y_MMS",        // 54
        "VIBRATION_TORQUE_X_MMS",         // 55
        "VIBRATION_TORQUE_Y_MMS",         // 56
        "VIBRATION_SEAL_DEAXIAL_MMS",     // 57
        "VIBRATION_SEAL_DERADIAL_MMS",    // 58
        "VIBRATION_SEAL_NDE_MMS",         // 59
        "ACTUATOR_POSITION_DE_MM",        // 60
        "ACTUATOR_POSITION_NDE_MM",       // 61
        "ACTUATOR_SPEED_DE_MMS",          // 62
        "ACTUATOR_SPEED_NDE_MMS",         // 63
    ];

    /// <summary>
    /// Connects to the live PLC, reads DB102 via the DecodeTagNames logic, and asserts
    /// that all 64 tag names match the expected values exactly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Test]
    public async Task DecodeTagNames_FromLivePlc_ShouldReturnAllExpectedTagNames()
    {
        using var plc = S71500.Create(PlcIp, interval: 5);

        var connected = await plc.IsConnected
            .Where(x => x)
            .Take(1)
            .Timeout(TimeSpan.FromSeconds(30))
            .FirstAsync();

        await TUnitAssert.That(connected).IsTrue();

        // Allow a brief settle time before reading.
        await Task.Delay(500);

        var bytes = await ReadTagNameBytesAsync(plc);
        await TUnitAssert.That(bytes).IsNotNull();
        await TUnitAssert.That(bytes!.Length).IsGreaterThanOrEqualTo(TotalTagNameBytes);

        await TUnitAssert.That(bytes[0]).IsEqualTo((byte)'S');
        await TUnitAssert.That(bytes[1]).IsEqualTo((byte)'T');
        await TUnitAssert.That(bytes[2]).IsEqualTo((byte)'X');
        // Byte 3 is the PLC's IsLogging runtime state, not a fixed protocol marker.
        var hasValidIsLoggingState = bytes[3] is >= FirstValidIsLoggingState and <= LastValidIsLoggingState;
        await TUnitAssert.That(hasValidIsLoggingState).IsTrue();

        await AssertTagNameBytesAsync(bytes);

        var tagNames = GetTagNames(bytes, BytesPerTagName, TotalTagNameBytes).ToList();

        await TUnitAssert.That(tagNames.Count).IsEqualTo(ExpectedTagNames.Length);

        for (var i = 0; i < ExpectedTagNames.Length; i++)
        {
            await TUnitAssert.That(tagNames[i]).IsEqualTo(ExpectedTagNames[i]);
        }
    }

    // ── Helper functions ported from S7PlcRx.TestApp/Program.cs ───────────────
    private static bool IsSTX(byte[]? bytes) => bytes?.Length > 3 && bytes[0] == 'S' && bytes[1] == 'T' && bytes[2] == 'X';

    private static async Task<byte[]?> ReadTagNameBytesAsync(IRxS7 plc)
    {
        try
        {
            plc.AddUpdateTagItem<byte[]>(TagNames1, "DB102.DBB0", TotalTagNameBytes).SetTagPollIng(false);

            var bytes = default(byte[]);
            var count = 0;
            while (!IsSTX(bytes) || bytes?[5] == 0)
            {
                if (count++ > 10)
                {
                    return default;
                }

                bytes = await plc.Value<byte[]>(TagNames1);
                await Task.Delay(50);
            }

            if (bytes == null || bytes.Length < TotalTagNameBytes)
            {
                return default;
            }

            await Task.Delay(500);

            return bytes;
        }
        catch
        {
        }

        return default;
    }

    private static async Task AssertTagNameBytesAsync(byte[] bytes)
    {
        for (var i = 0; i < ExpectedTagNames.Length; i++)
        {
            var expectedBytes = Encoding.ASCII.GetBytes(ExpectedTagNames[i]);
            var slotStart = HeaderByteCount + (i * BytesPerTagName);
            var actualBytes = new byte[expectedBytes.Length];

            Array.Copy(bytes, slotStart + 2, actualBytes, 0, expectedBytes.Length);

            await TUnitAssert.That(bytes[slotStart]).IsEqualTo((byte)TagNameReservedLength);
            await TUnitAssert.That(bytes[slotStart + 1]).IsEqualTo((byte)expectedBytes.Length);
            await TUnitAssert.That(actualBytes.SequenceEqual(expectedBytes)).IsTrue();

            for (var j = slotStart + 2 + expectedBytes.Length; j < slotStart + BytesPerTagName; j++)
            {
                await TUnitAssert.That(bytes[j]).IsEqualTo((byte)0);
            }
        }
    }

    private static IEnumerable<string> GetTagNames(byte[]? bytes, int bytesPerTagName, int? noOfBytes)
    {
        if (!(bytes?.Length != 0 && noOfBytes.HasValue && IsSTX(bytes)))
        {
            yield break;
        }

        for (var i = 4; i < noOfBytes; i += bytesPerTagName)
        {
            var itemLen = bytes![i + 1];
            yield return GetItemBytesToString(bytes, i + 2, itemLen);
        }
    }

    private static string GetItemBytesToString(byte[]? bytes, int sourceIndex, int length)
    {
        if (bytes?.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            var itemBytes = new byte[length];
            Array.Copy(bytes!, sourceIndex, itemBytes, 0, length);
            return Encoding.ASCII.GetString(itemBytes.TakeWhile(x => x != 0).ToArray());
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
