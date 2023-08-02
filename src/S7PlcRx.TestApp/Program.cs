// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using S7PlcRx;

var plc = new RxS7(S7PlcRx.Enums.CpuType.S71500, "172.16.17.1", 0, 1, interval: 5);
plc.LastError.Subscribe(ex => Console.WriteLine(ex));
plc.Status.Subscribe(status => Console.WriteLine(status));
const string DB100_DBB3 = nameof(DB100_DBB3);
const string PlcData = nameof(PlcData);
const string TestItems = nameof(TestItems);
const string TagNames1 = nameof(TagNames1);
const string TagNames2 = nameof(TagNames2);
const string TagValues = nameof(TagValues);

plc.AddUpdateTagItem<byte>(DB100_DBB3, "DB100.DBB3");
plc.AddUpdateTagItem<byte[]>(PlcData, "DB100.DBB0", 64).SetTagPollIng(false);
plc.AddUpdateTagItem<byte[]>(TestItems, "DB101.DBB0", 520).SetTagPollIng(false);
plc.AddUpdateTagItem<byte[]>(TagNames1, "DB102.DBB0", 4096).SetTagPollIng(false);
plc.AddUpdateTagItem<double[]>(TagValues, "DB103.DBD0", 99).SetTagPollIng(false);

plc.IsConnected
    .Where(x => x)
    .Take(1)
    .Subscribe(_ =>
    {
        Console.WriteLine("Connected");
        plc.IsPaused.Subscribe(x => Console.WriteLine($"Paused: {x}"));
        var setupComplete = false;
        plc.Observe<byte>(DB100_DBB3)
            .Select(v => v == 1)
            .Where(_ => !setupComplete)
            .Do(v =>
            {
                Console.WriteLine($"DB100 DBB3 value: {v}");
                plc?.GetTag(DB100_DBB3)?.SetTagPollIng(false);
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
                var dummy = await plc?.Value<byte>(DB100_DBB3)!;
                Console.WriteLine($"dummy: {dummy}");
                await Task.Delay(500);
                Console.WriteLine("Setup complete");
                plc?.GetTag(TagValues)?.SetTagPollIng(true);
                setupComplete = true;
            });
        plc.Observe<double[]>(TagValues)
                    .Where(_ => setupComplete)
                    .Subscribe(values =>
                    {
                        try
                        {
                            var tagValues = values?.Take(14).Select(Convert.ToSingle).ToArray();
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
