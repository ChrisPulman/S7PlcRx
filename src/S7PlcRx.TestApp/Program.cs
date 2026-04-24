// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Reactive.Linq;
using MockS7Plc;
using S7PlcRx;

using var server = new MockServer();
server.DefaultDb1Size = 10088;
var rc = server.Start();

// ── Connect PLC and register tag ───────────────────────────────────────
using var plc = new RxS7(S7PlcRx.Enums.CpuType.S71500, MockServer.Localhost, 0, 1, null, interval: 100);
plc.AddUpdateTagItem<byte[]>("GlobalVariables", "DB1.DBB0", 10088).SetTagPollIng(false);

// ── Wait for connection and read tag ───────────────────────────────────────
await plc.IsConnected.FirstAsync(x => x).Timeout(System.TimeSpan.FromSeconds(10));

// Seed the tag with some data to read back
var seedData = BuildGlobalVariablesSeedData(server.DefaultDb1Size, plc);
plc.Value("GlobalVariables", seedData);

using var simulationCancellationTokenSource = new CancellationTokenSource();
var simulationTask = SimulateGlobalVariablesAsync(plc, simulationCancellationTokenSource.Token);

Console.WriteLine("Mock PLC DB1 simulation is running. Press any key to stop.");
Console.ReadKey(intercept: true);

simulationCancellationTokenSource.Cancel();

try
{
    await simulationTask;
}
catch (OperationCanceledException)
{
}

////await AdvancedExamples.RunAllExamples();

////Console.WriteLine("Press any key to start the S7PlcRx example...");
////Console.ReadLine();

////var plc = S71500.Create("172.16.13.1", interval: 5);
////plc.LastError.Subscribe(Console.WriteLine);
////plc.Status.Subscribe(Console.WriteLine);
////const string StartLogging = nameof(StartLogging);
////const string PlcData = nameof(PlcData);
////const string TestItems = nameof(TestItems);
////const string TagNames1 = nameof(TagNames1);
////const string TagNames2 = nameof(TagNames2);
////const string TagValues = nameof(TagValues);

////plc.AddUpdateTagItem<byte>(StartLogging, "DB100.DBB3")
////    .AddUpdateTagItem<byte[]>(PlcData, "DB100.DBB0", 64).SetTagPollIng(false)
////    .AddUpdateTagItem<byte[]>(TestItems, "DB101.DBB0", 520).SetTagPollIng(false)
////    ////.AddUpdateTagItem<byte[]>(TagNames1, "DB102.DBB0", 4096).SetTagPollIng(false)
////    .AddUpdateTagItem<float[]>(TagValues, "DB103.DBD4", 98).SetTagPollIng(false);

////plc.IsConnected
////    .Where(x => x)
////    .Take(1)
////    .Subscribe(async _ =>
////    {
////        Console.WriteLine("Connected");
////        var info = await plc.GetCpuInfo();
////        foreach (var item in info)
////        {
////            Console.WriteLine(item);
////        }

////        await Task.Delay(2000);
////        plc.IsPaused.Subscribe(x => Console.WriteLine($"Paused: {x}"));
////        var setupComplete = false;
////        plc.Observe<byte>(StartLogging)
////            .Select(v => v == 2)
////            .Where(log => log && !setupComplete)
////            .Do(v =>
////            {
////                Console.WriteLine($"Start Logging value: {v}");
////                plc?.GetTag(StartLogging).SetTagPollIng(false);
////            })
////            .Subscribe(async _ =>
////            {
////                Console.WriteLine("Setup started");
////                Console.WriteLine("Reading PlcData");
////                var bytesPlcData = await plc?.Value<byte[]>(PlcData)!;
////                Console.WriteLine($"bytesPlcData: {bytesPlcData?.Length}");
////                await Task.Delay(500);
////                Console.WriteLine("Reading TestItems");
////                var bytesTestItems = await plc?.Value<byte[]>(TestItems)!;
////                Console.WriteLine($"bytesTestItems: {bytesTestItems?.Length}");
////                await Task.Delay(500);
////                Console.WriteLine("Reading TagNames");
////                var bytesTagNames1 = await DecodeTagNames(plc); ////plc?.Value<byte[]>(TagNames1)!;
////                Console.WriteLine($"bytesTagNames1: {bytesTagNames1}");
////                await Task.Delay(500);
////                var dummy = await plc?.Value<byte>(StartLogging)!;
////                Console.WriteLine($"dummy: {dummy}");
////                await Task.Delay(500);
////                Console.WriteLine("Setup complete");
////                plc?.GetTag(TagValues).SetTagPollIng(true);
////                setupComplete = true;
////            });
////        plc.Observe<float[]>(TagValues)
////                    .Where(_ => setupComplete)
////                    .Subscribe(values =>
////                    {
////                        try
////                        {
////                            var tagValues = values?.Take(14).ToArray();
////                            Console.WriteLine($"TagValues: {string.Join(", ", tagValues!)}");
////                        }
////                        catch (Exception ex)
////                        {
////                            Console.WriteLine(ex);
////                        }
////                    });
////    });

////Console.WriteLine("Press any key to exit...");
////Console.ReadKey();

////static bool IsSTX(byte[]? bytes) => bytes?.Length > 3 && bytes[0] == 'S' && bytes[1] == 'T' && bytes[2] == 'X';

////static async Task<IEnumerable<string>> DecodeTagNames(IRxS7 plc)
////{
////    try
////    {
////        const int bytesPerTagName = 32;
////        var noOfBytes = Convert.ToInt32(bytesPerTagName * 64);

////        plc?.AddUpdateTagItem<byte[]>(TagNames1, "DB102.DBB0", 2048 + 4).SetTagPollIng(false);
////        var bytes = default(byte[]);
////        var count = 0;
////        while (!IsSTX(bytes) || bytes?[5] == 0)
////        {
////            if (count++ > 10)
////            {
////                return [];
////            }

////            bytes = await plc!.Value<byte[]>(TagNames1);
////            await Task.Delay(50);
////        }

////        if (noOfBytes > bytes?.Length)
////        {
////            return [];
////        }

////        var tagNames = GetTagNames(bytes, bytesPerTagName, noOfBytes);

////        await Task.Delay(500);

////        return tagNames;
////    }
////    catch
////    {
////    }

////    return [];
////}

////static IEnumerable<string> GetTagNames(byte[]? bytes, int bytesPerTagName, int? noOfBytes)
////{
////    if (!(bytes?.Length != 0 && noOfBytes.HasValue && IsSTX(bytes)))
////    {
////        yield break;
////    }

////    for (var i = 4; i < noOfBytes; i += bytesPerTagName)
////    {
////        var itemLen = bytes![i + 1];
////        yield return GetItemBytesToString(bytes, i + 2, itemLen);
////    }
////}

static byte[] BuildGlobalVariablesSeedData(int size, RxS7 plc)
{
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

    var data = new byte[size];
    double offset = 0;

    Write("Rig.Casing.Pressure.Low.PID.P", 3000.0f);
    Write("Rig.Casing.Pressure.Low.PID.I", 85.0f);
    Write("Rig.Casing.Pressure.Low.PID.D", 10.0f);
    Write("Rig.Casing.Pressure.Low.PV.EngHi", 16.0f);
    Write("Rig.Casing.Pressure.Low.PV.RawHi", (short)27610);
    Write("Rig.Casing.Pressure.Low.PV.RawLo", (short)-4);
    Write("Rig.Casing.Pressure.Medium.PID.P", 3000.0f);
    Write("Rig.Casing.Pressure.Medium.PID.I", 100.0f);
    Write("Rig.Casing.Pressure.Medium.PID.D", 20.0f);
    Write("Rig.Casing.Pressure.Medium.PID.Suppression", 0.05f);
    Write("Rig.Casing.Pressure.Medium.PV.EngHi", 100.0f);
    Write("Rig.Casing.Pressure.Medium.PV.RawHi", (short)27590);
    Write("Rig.Casing.Pressure.Medium.PV.RawLo", (short)-12);
    Write("Rig.Casing.Pressure.High.PID.P", 1000.0f);
    Write("Rig.Casing.Pressure.High.PID.I", 100.0f);
    Write("Rig.Casing.Pressure.High.PID.D", 50.0f);
    Write("Rig.Casing.Pressure.High.PID.Suppression", 0.5f);
    Write("Rig.Casing.Pressure.High.PV.EngHi", 450.0f);
    Write("Rig.Casing.Pressure.High.PV.RawHi", (short)27588);
    Write("Rig.Casing.Pressure.High.PV.RawLo", (short)-18);
    Write("Rig.Casing.Pressure.PV.EngHi", 450.0f);
    Write("Rig.Casing.Pressure.PV.RawHi", (short)27666);
    Write("Rig.Casing.Pressure.PV.RawLo", (short)8);
    Write("Rig.Casing.Pressure.PV.SimulationVal", 180.0f);
    Write("Rig.Casing.Pressure.ChangeOverLowToMid", 5.0f);
    Write("Rig.Casing.Pressure.ChangeOverMidToHigh", 65.0f);
    Write("Rig.Casing.Temperature.PV.Offset", 0.6f);
    Write("Rig.Casing.Temperature.PID.P", 2000.0f);
    Write("Rig.Casing.Temperature.PID.I", 10.0f);
    Write("Rig.Casing.Temperature.PID.D", 100.0f);
    Write("Rig.Interspace.Pressure.Low.PID.P", 3000.0f);
    Write("Rig.Interspace.Pressure.Low.PID.I", 85.0f);
    Write("Rig.Interspace.Pressure.Low.PID.D", 20.0f);
    Write("Rig.Interspace.Pressure.Low.PV.EngHi", 16.0f);
    Write("Rig.Interspace.Pressure.Low.PV.RawHi", (short)27610);
    Write("Rig.Interspace.Pressure.Low.PV.RawLo", (short)-4);
    Write("Rig.Interspace.Pressure.Medium.PID.P", 3000.0f);
    Write("Rig.Interspace.Pressure.Medium.PID.I", 80.0f);
    Write("Rig.Interspace.Pressure.Medium.PID.D", 10.0f);
    Write("Rig.Interspace.Pressure.Medium.PID.Suppression", 0.1f);
    Write("Rig.Interspace.Pressure.Medium.PV.EngHi", 100.0f);
    Write("Rig.Interspace.Pressure.Medium.PV.RawHi", (short)27585);
    Write("Rig.Interspace.Pressure.Medium.PV.RawLo", (short)-10);
    Write("Rig.Interspace.Pressure.High.PID.P", 2000.0f);
    Write("Rig.Interspace.Pressure.High.PID.I", 100.0f);
    Write("Rig.Interspace.Pressure.High.PID.D", 50.0f);
    Write("Rig.Interspace.Pressure.High.PV.EngHi", 450.0f);
    Write("Rig.Interspace.Pressure.High.PV.RawHi", (short)27583);
    Write("Rig.Interspace.Pressure.High.PV.RawLo", (short)-11);
    Write("Rig.Interspace.Pressure.DE.PV.EngHi", 450.0f);
    Write("Rig.Interspace.Pressure.DE.PV.RawHi", (short)27672);
    Write("Rig.Interspace.Pressure.DE.PV.RawLo", (short)8);
    Write("Rig.Interspace.Pressure.DE.PV.SimulationVal", 150.0f);
    Write("Rig.Interspace.Pressure.NDE.PV.EngHi", 450.0f);
    Write("Rig.Interspace.Pressure.NDE.PV.RawHi", (short)27668);
    Write("Rig.Interspace.Pressure.NDE.PV.RawLo", (short)9);
    Write("Rig.Interspace.Pressure.NDE.PV.SimulationVal", 145.0f);
    Write("Rig.Interspace.Pressure.DEBP.PID.P", 2000.0f);
    Write("Rig.Interspace.Pressure.DEBP.PID.I", 200.0f);
    Write("Rig.Interspace.Pressure.DEBP.PID.D", 50.0f);
    Write("Rig.Interspace.Pressure.DEBP.PV.EngHi", 21.0f);
    Write("Rig.Interspace.Pressure.DEBP.PV.RawHi", (short)27598);
    Write("Rig.Interspace.Pressure.DEBP.PV.RawLo", (short)-8);
    Write("Rig.Interspace.Pressure.NDEBP.PID.P", 2000.0f);
    Write("Rig.Interspace.Pressure.NDEBP.PID.I", 200.0f);
    Write("Rig.Interspace.Pressure.NDEBP.PID.D", 50.0f);
    Write("Rig.Interspace.Pressure.NDEBP.PV.EngHi", 21.0f);
    Write("Rig.Interspace.Pressure.NDEBP.PV.RawHi", (short)27571);
    Write("Rig.Interspace.Pressure.NDEBP.PV.RawLo", (short)-10);
    Write("Rig.Interspace.Pressure.ChangeOverLowToMid", 5.0f);
    Write("Rig.Interspace.Pressure.ChangeOverMidToHigh", 65.0f);
    Write("Rig.Interspace.Temperature.DE.PV.Offset", 0.6f);
    Write("Rig.Interspace.Temperature.DE.PV.SimulationVal", 60.0f);
    Write("Rig.Interspace.Temperature.DE.PID.P", 2000.0f);
    Write("Rig.Interspace.Temperature.DE.PID.I", 20.0f);
    Write("Rig.Interspace.Temperature.DE.PID.D", 100.0f);
    Write("Rig.Interspace.Temperature.NDE.PV.Offset", 0.3f);
    Write("Rig.Interspace.Temperature.NDE.PV.SimulationVal", 53.0f);
    Write("Rig.Interspace.Temperature.NDE.PID.P", 2000.0f);
    Write("Rig.Interspace.Temperature.NDE.PID.I", 10.0f);
    Write("Rig.Interspace.Temperature.NDE.PID.D", 100.0f);
    Write("Rig.Interspace.Flow.DE.connectParamClient.InterfaceId", (ushort)64);
    Write("Rig.Interspace.Flow.DE.connectParamClient.ID", (ushort)1);
    Write("Rig.Interspace.Flow.DE.connectParamClient.ConnectionType", (ushort)11);
    Write("Rig.Interspace.Flow.DE.connectParamClient.ActiveEstablished", (ushort)1);
    Write("Rig.Interspace.Flow.DE.connectParamClient.RemotePort", (ushort)502);
    Write("Rig.Outboard.Pressure.DE.PV.EngHi", 20.0f);
    Write("Rig.Outboard.Pressure.DE.PV.RawHi", (short)27648);
    Write("Rig.Outboard.Pressure.DE.PV.RawLo", (short)10);
    Write("Rig.Outboard.Pressure.DE.PV.SimulationVal", 5.0f);
    Write("Rig.Outboard.Pressure.NDE.PV.EngHi", 20.0f);
    Write("Rig.Outboard.Pressure.NDE.PV.RawHi", (short)27648);
    Write("Rig.Outboard.Pressure.NDE.PV.RawLo", (short)8);
    Write("Rig.Outboard.Pressure.NDE.PV.SimulationVal", 4.0f);
    Write("Rig.Outboard.Temperature.DE.PV.Offset", 0.1f);
    Write("Rig.Outboard.Temperature.DE.PV.SimulationVal", 30.0f);
    Write("Rig.Outboard.Temperature.NDE.PV.Offset", 0.4f);
    Write("Rig.Outboard.Temperature.NDE.PV.SimulationVal", 28.0f);
    Write("Rig.Bearing.Temperature.DE.PV.Offset", 0.0f);
    Write("Rig.Bearing.Temperature.DE.PV.SimulationVal", 40.0f);
    Write("Rig.Bearing.Temperature.DE.PID.P", 2000.0f);
    Write("Rig.Bearing.Temperature.DE.PID.I", 10.0f);
    Write("Rig.Bearing.Temperature.DE.PID.D", 100.0f);
    Write("Rig.Bearing.Temperature.NDE.PV.Offset", 0.6f);
    Write("Rig.Bearing.Temperature.NDE.PV.SimulationVal", 36.0f);
    Write("Rig.Bearing.Temperature.NDE.PID.P", 2000.0f);
    Write("Rig.Bearing.Temperature.NDE.PID.I", 10.0f);
    Write("Rig.Bearing.Temperature.NDE.PID.D", 100.0f);
    Write("Rig.Drive.Gearbox.Temperature.OutboardDE.PV.Offset", 0.1f);
    Write("Rig.Drive.Gearbox.Temperature.OutboardDE.PV.SimulationVal", 33.0f);
    Write("Rig.Drive.Gearbox.Temperature.InboardDE.PV.Offset", -0.2f);
    Write("Rig.Drive.Gearbox.Temperature.InboardDE.PV.SimulationVal", 33.0f);
    Write("Rig.Drive.Gearbox.Temperature.OutboardNDE.PV.Offset", -0.2f);
    Write("Rig.Drive.Gearbox.InletTemperature.PV.Offset", 0.3f);
    Write("Rig.Drive.Gearbox.InletTemperature.PV.SimulationVal", 33.0f);
    Write("Rig.Drive.Gearbox.InletPressure.PV.EngHi", 10.0f);
    Write("Rig.Drive.Gearbox.InletPressure.PV.RawHi", (short)27648);
    Write("Rig.Drive.Gearbox.InletPressure.PV.RawLo", (short)26);
    Write("Rig.Drive.Gearbox.Vibration.X.PV.EngHi", 25.0f);
    Write("Rig.Drive.Gearbox.Vibration.X.PV.RawHi", (short)27648);
    Write("Rig.Drive.Gearbox.Vibration.X.PV.RawLo", (short)-71);
    Write("Rig.Drive.Gearbox.Vibration.X.PV.SimulationVal", 0.4f);
    Write("Rig.Drive.Gearbox.Vibration.Y.PV.EngHi", 25.0f);
    Write("Rig.Drive.Gearbox.Vibration.Y.PV.RawHi", (short)27648);
    Write("Rig.Drive.Gearbox.Vibration.Y.PV.SimulationVal", 0.7f);
    Write("Rig.Drive.Gearbox.Vibration.Z.PV.EngHi", 25.0f);
    Write("Rig.Drive.Gearbox.Vibration.Z.PV.RawHi", (short)27648);
    Write("Rig.Drive.Gearbox.Vibration.Z.PV.RawLo", (short)-63);
    Write("Rig.Drive.PedestalBearing.Temperature.PV.Offset", 0.2f);
    Write("Rig.Drive.HydraulicPack.OilTankTemperature.PV.SimulationVal", 33.0f);
    Write("Rig.Drive.HydraulicPack.OilFlow.PV.EngHi", 22.7f);
    Write("Rig.Drive.HydraulicPack.OilFlow.PV.RawHi", (short)27648);
    Write("Rig.Drive.HydraulicPack.OilFlow.PV.RawLo", (short)3);
    Write("Rig.Drive.Torque.PV.EngHi", 200.0f);
    Write("Rig.Drive.Torque.PV.EngLo", 0.0f);
    Write("Rig.Drive.Torque.PV.Offset", 27200.0f);
    Write("Rig.Drive.Torque.PV.RawHi", (short)27800);
    Write("Rig.Drive.Torque.PV.RawLo", (short)-27);
    Write("Rig.Drive.Torque.PV.AllowNegValues", true);
    Write("Rig.Drive.Torque.PV.SimulationVal", 14.0f);
    Write("Rig.Drive.Torque.SpeedSetpoint", 0.0f);
    Write("Rig.Drive.Torque.RotationDetectionLevel", 30.0f);
    Write("Rig.Drive.Torque.Temperature.Inboard.PV.SimulationVal", 33.0f);
    Write("Rig.Drive.Torque.Temperature.Outboard.PV.SimulationVal", 33.0f);
    Write("Rig.Drive.Torque.Vibration.X.PV.EngHi", 25.0f);
    Write("Rig.Drive.Torque.Vibration.X.PV.RawHi", (short)27648);
    Write("Rig.Drive.Torque.Vibration.X.PV.RawLo", (short)-10);
    Write("Rig.Drive.Torque.Vibration.X.PV.SimulationVal", 0.6f);
    Write("Rig.Drive.Torque.Vibration.Y.PV.EngHi", 25.0f);
    Write("Rig.Drive.Torque.Vibration.Y.PV.RawHi", (short)27648);
    Write("Rig.Drive.Torque.Vibration.Y.PV.SimulationVal", 0.65f);
    Write("Rig.Drive.Torque.Vibration.Z.PV.EngHi", 25.0f);
    Write("Rig.Drive.Torque.Vibration.Z.PV.RawHi", (short)27648);
    Write("Rig.Drive.Torque.Vibration.Z.PV.RawLo", (short)-45);
    Write("Rig.Drive.Speed.PV.SimulationVal", 14560.0f);
    Write("Rig.Drive.PID.P", 0.16f);
    Write("Rig.Drive.PID.I", 0.16f);
    Write("Rig.Drive.PID.D", 0.0001f);
    Write("Rig.Drive.RunClockwise", true);
    Write("Rig.Cooling.Inlet.Temperature.PV.Offset", -0.6f);
    Write("Rig.Cooling.Inlet.Pressure.PV.EngHi", 400.0f);
    Write("Rig.Cooling.Inlet.Pressure.PV.RawHi", (short)27648);
    Write("Rig.Cooling.Outlet.PV.Offset", 0.6f);
    Write("Rig.Seal.DeAxial.PV.EngHi", 25.0f);
    Write("Rig.Seal.DeAxial.PV.RawHi", (short)27648);
    Write("Rig.Seal.DeAxial.PV.RawLo", (short)-83);
    Write("Rig.Seal.DeAxial.PV.SimulationVal", 0.5f);
    Write("Rig.Seal.DeRadial.PV.EngHi", 25.0f);
    Write("Rig.Seal.DeRadial.PV.RawHi", (short)27648);
    Write("Rig.Seal.DeRadial.PV.RawLo", (short)32);
    Write("Rig.Seal.DeRadial.PV.SimulationVal", 0.6f);
    Write("Rig.Seal.NDE.PV.EngHi", 25.0f);
    Write("Rig.Seal.NDE.PV.RawHi", (short)27648);
    Write("Rig.Seal.NDE.PV.RawLo", (short)-60);
    Write("Rig.Seal.NDE.PV.SimulationVal", 0.4f);
    Write("Rig.ControlLowPressure.PV.EngHi", 25.0f);
    Write("Rig.ControlLowPressure.PV.RawHi", (short)27648);
    Write("Rig.ControlLowPressure.PV.RawLo", (short)0);
    Write("Rig.ControlHighPressure.PV.EngHi", 25.0f);
    Write("Rig.ControlHighPressure.PV.RawHi", (short)27648);
    Write("Rig.SupplyPressure.PV.EngHi", 600.0f);
    Write("Rig.SupplyPressure.PV.RawHi", (short)27688);
    Write("Rig.SupplyPressure.PV.RawLo", (short)9);
    Write("Rig.SupplyPressure.PV.SimulationVal", 400.0f);
    Write("Rig.ConditionedPressure.PV.EngHi", 600.0f);
    Write("Rig.ConditionedPressure.PV.RawHi", (short)27666);
    Write("Rig.ConditionedPressure.PV.RawLo", (short)5);
    Write("Rig.ConditionedPressure.PV.SimulationVal", 350.0f);
    Write("Rig.Auxiliary1Temperature.PV.Offset", 0.0f);
    Write("Rig.Auxiliary1Temperature.PV.SimulationVal", 16.0f);
    Write("Rig.Auxiliary2Temperature.PV.SimulationVal", 22.0f);
    Write("Rig.Limits.Control.MinSupplyPressure", 200.0f);
    Write("Rig.Limits.Control.MinLowPressure", 4.0f);
    Write("Rig.Limits.Control.MinHighPressure", 5.0f);
    Write("Rig.MaxRateOfIncrease", 1.0f);
    Write("CasingHighPressureCommsPRV2.Node", (ushort)2);
    Write("InterspaceHighPressureCommsPRV3.Node", (ushort)3);
    Write("CasingMediumPressureCommsPRV4.Node", (ushort)4);
    Write("InterspaceMediumPressureCommsPRV5.Node", (ushort)5);
    Write("DEBackPressureCommsBPV2.Node", (ushort)12);
    Write("NDEBackPressureCommsBPV3.Node", (ushort)13);
    Write("CasingLowPressureCommsPRV20.Node", (ushort)21);
    Write("InterspaceLowPressureCommsPRV21.Node", (ushort)22);

    return data;

    void Write<T>(string path, T value)
    {
        switch (value)
        {
            case bool boolValue:
                WriteBoolean(path, boolValue);
                break;
            case byte byteValue:
                WriteByte(path, byteValue);
                break;
            case sbyte sbyteValue:
                WriteByte(path, unchecked((byte)sbyteValue));
                break;
            case short shortValue:
                WriteInt16(path, shortValue);
                break;
            case ushort ushortValue:
                WriteUInt16(path, ushortValue);
                break;
            case int intValue:
                WriteInt32(path, intValue);
                break;
            case uint uintValue:
                WriteUInt32(path, uintValue);
                break;
            case float floatValue:
                WriteSingle(path, floatValue);
                break;
            default:
                throw new NotSupportedException($"Seed data type {typeof(T).Name} is not supported for {path}.");
        }
    }

    void WriteBoolean(string path, bool value)
    {
        var byteOffset = (int)Math.Floor(offset);
        var bitOffset = (int)((offset - byteOffset) / 0.125);
        EnsureCapacity(path, byteOffset, 1);
        RegisterTag(path, typeof(bool), $"DB1.DBX{byteOffset}.{bitOffset}");

        if (value)
        {
            data[byteOffset] |= (byte)(1 << bitOffset);
        }
        else
        {
            data[byteOffset] &= (byte)~(1 << bitOffset);
        }

        offset += 0.125;
    }

    void WriteByte(string path, byte value)
    {
        AlignByte();
        RegisterTag(path, typeof(byte), $"DB1.DBB{(int)offset}");
        WriteBytes(path, [value]);
    }

    void WriteInt16(string path, short value)
    {
        AlignWord();
        RegisterTag(path, typeof(short), $"DB1.DBW{(int)offset}");
        Span<byte> bytes = stackalloc byte[sizeof(short)];
        BinaryPrimitives.WriteInt16BigEndian(bytes, value);
        WriteBytes(path, bytes);
    }

    void WriteUInt16(string path, ushort value)
    {
        AlignWord();
        RegisterTag(path, typeof(ushort), $"DB1.DBW{(int)offset}");
        Span<byte> bytes = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        WriteBytes(path, bytes);
    }

    void WriteInt32(string path, int value)
    {
        AlignWord();
        RegisterTag(path, typeof(int), $"DB1.DBD{(int)offset}");
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        WriteBytes(path, bytes);
    }

    void WriteUInt32(string path, uint value)
    {
        AlignWord();
        RegisterTag(path, typeof(uint), $"DB1.DBD{(int)offset}");
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        WriteBytes(path, bytes);
    }

    void WriteSingle(string path, float value)
    {
        AlignWord();
        RegisterTag(path, typeof(float), $"DB1.DBD{(int)offset}");
        var rawValue = BitConverter.SingleToUInt32Bits(value);
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, rawValue);
        WriteBytes(path, bytes);
    }

    void RegisterTag(string path, Type type, string address)
        => plc.AddUpdateTagItem(type, path, address).SetTagPollIng(false);

    void AlignByte() => offset = Math.Ceiling(offset);

    void AlignWord()
    {
        offset = Math.Ceiling(offset);
        if ((offset / 2) > Math.Floor(offset / 2.0))
        {
            offset++;
        }
    }

    void EnsureCapacity(string path, int startIndex, int length)
    {
        if (startIndex < 0 || startIndex + length > data.Length)
        {
            throw new InvalidOperationException($"Seed data buffer is too small for {path} at offset {startIndex}.");
        }
    }

    void WriteBytes(string path, ReadOnlySpan<byte> value)
    {
        var startIndex = (int)offset;
        EnsureCapacity(path, startIndex, value.Length);
        value.CopyTo(data.AsSpan(startIndex, value.Length));
        offset += value.Length;
    }
}

static async Task SimulateGlobalVariablesAsync(RxS7 plc, CancellationToken cancellationToken)
{
    ArgumentNullException.ThrowIfNull(plc);

    var startTime = System.DateTime.UtcNow;

    while (!cancellationToken.IsCancellationRequested)
    {
        var elapsedSeconds = (System.DateTime.UtcNow - startTime).TotalSeconds;
        var slowWave = MathF.Sin((float)(elapsedSeconds / 6.0));
        var fastWave = MathF.Sin((float)(elapsedSeconds / 2.5));
        var sawWave = (float)((elapsedSeconds % 10.0) / 10.0);

        plc.Value("Rig.Casing.Pressure.PV.SimulationVal", 180.0f + (slowWave * 12.0f));
        plc.Value("Rig.Interspace.Pressure.DE.PV.SimulationVal", 150.0f + (fastWave * 10.0f));
        plc.Value("Rig.Interspace.Pressure.NDE.PV.SimulationVal", 145.0f + (slowWave * 8.0f));
        plc.Value("Rig.Outboard.Pressure.DE.PV.SimulationVal", 5.0f + (fastWave * 0.8f));
        plc.Value("Rig.Outboard.Pressure.NDE.PV.SimulationVal", 4.0f + (slowWave * 0.6f));

        plc.Value("Rig.Interspace.Temperature.DE.PV.SimulationVal", 60.0f + (slowWave * 4.0f));
        plc.Value("Rig.Interspace.Temperature.NDE.PV.SimulationVal", 53.0f + (fastWave * 4.0f));
        plc.Value("Rig.Bearing.Temperature.DE.PV.SimulationVal", 40.0f + (slowWave * 2.0f));
        plc.Value("Rig.Bearing.Temperature.NDE.PV.SimulationVal", 36.0f + (fastWave * 2.0f));
        plc.Value("Rig.Drive.Gearbox.InletTemperature.PV.SimulationVal", 33.0f + (slowWave * 1.5f));
        plc.Value("Rig.Drive.HydraulicPack.OilTankTemperature.PV.SimulationVal", 33.0f + (fastWave * 1.5f));
        plc.Value("Rig.Auxiliary1Temperature.PV.SimulationVal", 16.0f + (sawWave * 3.0f));
        plc.Value("Rig.Auxiliary2Temperature.PV.SimulationVal", 22.0f + ((1.0f - sawWave) * 2.0f));

        plc.Value("Rig.Drive.Gearbox.Vibration.X.PV.SimulationVal", 0.4f + ((fastWave + 1.0f) * 0.15f));
        plc.Value("Rig.Drive.Gearbox.Vibration.Y.PV.SimulationVal", 0.7f + ((slowWave + 1.0f) * 0.12f));
        plc.Value("Rig.Drive.Torque.PV.SimulationVal", 14.0f + (slowWave * 3.0f));
        plc.Value("Rig.Drive.Torque.Temperature.Inboard.PV.SimulationVal", 33.0f + (fastWave * 1.2f));
        plc.Value("Rig.Drive.Torque.Temperature.Outboard.PV.SimulationVal", 33.0f + (slowWave * 1.2f));
        plc.Value("Rig.Drive.Torque.Vibration.X.PV.SimulationVal", 0.6f + ((fastWave + 1.0f) * 0.1f));
        plc.Value("Rig.Drive.Torque.Vibration.Y.PV.SimulationVal", 0.65f + ((slowWave + 1.0f) * 0.1f));
        plc.Value("Rig.Drive.Speed.PV.SimulationVal", 14560.0f + (slowWave * 850.0f));

        plc.Value("Rig.SupplyPressure.PV.SimulationVal", 400.0f + (slowWave * 25.0f));
        plc.Value("Rig.ConditionedPressure.PV.SimulationVal", 350.0f + (fastWave * 20.0f));
        plc.Value("Rig.Seal.DeAxial.PV.SimulationVal", 0.5f + ((fastWave + 1.0f) * 0.08f));
        plc.Value("Rig.Seal.DeRadial.PV.SimulationVal", 0.6f + ((slowWave + 1.0f) * 0.08f));
        plc.Value("Rig.Seal.NDE.PV.SimulationVal", 0.4f + ((fastWave + 1.0f) * 0.08f));

        plc.Value("Rig.Drive.RunClockwise", fastWave >= 0.0f);
        plc.Value("Rig.Drive.Torque.PV.AllowNegValues", true);

        await Task.Delay(System.TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
    }
}

////static string GetItemBytesToString(byte[]? bytes, int sourceIndex, int length)
////{
////    if (bytes?.Length == 0)
////    {
////        return string.Empty;
////    }

////    try
////    {
////        var itemBytes = new byte[length];
////        Array.Copy(bytes!, sourceIndex, itemBytes, 0, length);
////        return Encoding.ASCII.GetString(itemBytes.TakeWhile(x => x != 0).ToArray());
////    }
////    catch (Exception)
////    {
////        return string.Empty;
////    }
////}
