// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using System.Text;
using S7PlcRx;

var plc = new RxS7(S7PlcRx.Enums.CpuType.S71500, "172.16.17.1", 0, 1, interval: 5);
////for (var i = 0; i < 60; i++)
{
    plc.AddUpdateTagItem<double[]>("Tag0_To_59", "DB103.DBD0", 59);
}

////plc.Observe<double>("Tag0").Subscribe(x => Console.WriteLine($"Tag0: {x}"));
////plc.Observe<double>("Tag1").Subscribe(x => Console.WriteLine($"Tag1: {x}"));

plc.ReadTime.Select(x => TimeSpan.FromTicks(x).TotalMilliseconds).Buffer(200).Select(x => x.Average()).CombineLatest(
plc.ObserveAll.TagToDictionary<double[]>())
    .Subscribe(values =>
    {
        var sb = new StringBuilder();
        sb.Append("Read time: ").Append(values.First).Append(" / ");
        foreach (var kvp in values.Second)
        {
            sb.Append("Key = ").Append(kvp.Key);
            for (var i = 0; i < kvp.Value.Length; i++)
            {
                sb.Append(", Value").Append(i).Append(" = ").Append(kvp.Value[i]).Append(" / ");
            }
        }

        Console.WriteLine(sb.ToString());
    });

////plc.ReadTime.Select(x => TimeSpan.FromTicks(x).TotalMilliseconds).Buffer(200).Select(x => x.Average()).Subscribe(time => Console.WriteLine($"Read time: {time}"));

Console.WriteLine("Press any key to exit...");
Console.ReadKey();
