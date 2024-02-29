// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using S7PlcRx;

var plc = S71500.Create("172.16.17.1", interval: 5);
plc.LastError.Subscribe(Console.WriteLine);
plc.Status.Subscribe(Console.WriteLine);
const string StartLogging = nameof(StartLogging);
const string PlcData = nameof(PlcData);
const string TestItems = nameof(TestItems);
const string TagNames1 = nameof(TagNames1);
const string TagNames2 = nameof(TagNames2);
const string TagValues = nameof(TagValues);

plc.AddUpdateTagItem<byte>(StartLogging, "DB100.DBB3")
    .AddUpdateTagItem<byte[]>(PlcData, "DB100.DBB0", 64).SetTagPollIng(false)
    .AddUpdateTagItem<byte[]>(TestItems, "DB101.DBB0", 520).SetTagPollIng(false)
    .AddUpdateTagItem<byte[]>(TagNames1, "DB102.DBB0", 4096).SetTagPollIng(false)
    .AddUpdateTagItem<float[]>(TagValues, "DB103.DBD4", 98).SetTagPollIng(false);

plc.IsConnected
    .Where(x => x)
    .Take(1)
    .Subscribe(async _ =>
    {
        Console.WriteLine("Connected");
        var info = await plc.GetCpuInfo();
        foreach (var item in info)
        {
            Console.WriteLine(item);
        }

        await Task.Delay(2000);
        plc.IsPaused.Subscribe(x => Console.WriteLine($"Paused: {x}"));
        var setupComplete = false;
        plc.Observe<byte>(StartLogging)
            .Select(v => v == 2)
            .Where(log => log && !setupComplete)
            .Do(v =>
            {
                Console.WriteLine($"Start Logging value: {v}");
                plc?.GetTag(StartLogging).SetTagPollIng(false);
            })
            .Subscribe(async _ =>
            {
                Console.WriteLine("Setup started");
                Console.WriteLine("Reading PlcData");
                var bytesPlcData = await plc?.Value<byte[]>(PlcData)!;
                Console.WriteLine($"bytesPlcData: {bytesPlcData?.Length}");
                await Task.Delay(500);
                Console.WriteLine("Reading TestItems");
                var bytesTestItems = await plc?.Value<byte[]>(TestItems)!;
                Console.WriteLine($"bytesTestItems: {bytesTestItems?.Length}");
                await Task.Delay(500);
                Console.WriteLine("Reading TagNames");
                var bytesTagNames1 = await plc?.Value<byte[]>(TagNames1)!;
                Console.WriteLine($"bytesTagNames1: {bytesTagNames1?.Length}");
                await Task.Delay(500);
                var dummy = await plc?.Value<byte>(StartLogging)!;
                Console.WriteLine($"dummy: {dummy}");
                await Task.Delay(500);
                Console.WriteLine("Setup complete");
                plc?.GetTag(TagValues).SetTagPollIng(true);
                setupComplete = true;
            });
        plc.Observe<float[]>(TagValues)
                    .Where(_ => setupComplete)
                    .Subscribe(values =>
                    {
                        try
                        {
                            var tagValues = values?.Take(14).ToArray();
                            Console.WriteLine($"TagValues: {string.Join(", ", tagValues!)}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                        }
                    });
    });

Console.WriteLine("Press any key to exit...");
Console.ReadKey();
