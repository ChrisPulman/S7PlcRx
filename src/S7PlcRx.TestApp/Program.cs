// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using System.Text;
using S7PlcRx;

var plc = new RxS7(S7PlcRx.Enums.CpuType.S71500, "172.16.17.1", 0, 1, interval: 5);

const string Tag0_To_99 = nameof(Tag0_To_99);
const string StringArea = nameof(StringArea);
////for (var i = 0; i < 100; i++)
{
    plc.AddUpdateTagItem<double[]>(Tag0_To_99, "DB103.DBD0", 99);
    plc.AddUpdateTagItem<byte[]>(StringArea, "DB101.DBB0", 264).SetTagNoPoll(true);
}

plc.IsConnected.Subscribe(x =>
{
    if (x)
    {
        Console.WriteLine("Connected");
        var v = plc.Value<byte[]>(StringArea);
    }
    else
    {
        Console.WriteLine("Disconnected");
    }
});

////plc.Observe<double>("Tag0").Subscribe(x => Console.WriteLine($"Tag0: {x}"));
////plc.Observe<double>("Tag1").Subscribe(x => Console.WriteLine($"Tag1: {x}"));
var count = 0;
plc.ReadTime
    .Select(x => TimeSpan.FromTicks(x).TotalMilliseconds)
    .TimeInterval()
    .Buffer(200)
    .Select(x => (ExectionTime: x.Select(x => x.Interval.TotalMilliseconds).Average(), Value: x.Select(x => x.Value).Average()))
    .CombineLatest(
plc.Observe<double[]>(Tag0_To_99).ToTagValue(Tag0_To_99))
    .Subscribe(values =>
    {
        count++;
        if (count % 200 == 0)
        {
            count = 0;
            var sb = new StringBuilder();
            sb.Append("Read time: ").Append(values.First).Append(" ms");
            if (values.First.Value > 5)
            {
                Console.ForegroundColor = ConsoleColor.Red;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
            }

            Console.WriteLine(sb.ToString());
            Console.ResetColor();
            sb.Clear().Append("Tag = ").Append(values.Second.Tag);
            for (var i = 0; i < values.Second.Value.Length; i++)
            {
                sb.Append(", Value").Append(i).Append(" = ").Append(values.Second.Value[i]).Append(" / ");
            }

            Console.WriteLine(sb.ToString());
        }
    });

////plc.ReadTime.Select(x => TimeSpan.FromTicks(x).TotalMilliseconds).Buffer(200).Select(x => x.Average()).Subscribe(time => Console.WriteLine($"Read time: {time}"));

Console.WriteLine("Press any key to exit...");
Console.ReadKey();
