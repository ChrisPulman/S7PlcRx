![License](https://img.shields.io/github/license/ChrisPulman/S7PlcRx.svg) [![Build](https://github.com/ChrisPulman/S7PlcRx/actions/workflows/BuildOnly.yml/badge.svg)](https://github.com/ChrisPulman/S7PlcRx/actions/workflows/BuildOnly.yml) ![Nuget](https://img.shields.io/nuget/dt/S7PlcRx?color=pink&style=plastic) [![NuGet](https://img.shields.io/nuget/v/S7PlcRx.svg?style=plastic)](https://www.nuget.org/packages/S7PlcRx)

<p align="left">
  <a href="https://github.com/ChrisPulman/S7PlcRx">
    <img alt="S7PlcRx" src="https://github.com/ChrisPulman/S7PlcRx/blob/main/Images/S7PlcRx.png" width="200"/>
  </a>
</p>

# S7PlcRx
Reactive S7 PLC Communications Library

## Introduction
S7PlcRx is a library that provides a simple interface to communicate with Siemens S7 PLCs.

## Features
- Read and Write to PLC
- Read from PLC with Reactive Subscription


## Getting Started
### Installation
S7PlcRx is available on [NuGet](https://www.nuget.org/packages/S7PlcRx/).

#### Package Manager
```powershell
Install-Package S7PlcRx
```

#### .NET CLI
```powershell  
dotnet add package S7PlcRx
```

### Usage
#### Setup Tags and Observe values in PLC
```csharp
using S7PlcRx;

var plc = new RxS7(S7PlcRx.Enums.CpuType.S71500, "PLC_IP_ADDRESS", 0, 5);
// Add Tag without Polling
plc.AddUpdateTagItem<double>("Tag0", "DB500.DBD0").SetTagPollIng(false);
// Add Tag with Polling
plc.AddUpdateTagItem<double>("Tag1", "DB500.DBD8");

plc.IsConnected
    .Where(x => x)
    .Take(1)
    .Subscribe(async _ =>
    {
        Console.WriteLine("Connected");

        // Read Tag Value manually
        var tag0 = await plc.Value<double>("Tag0");
    });

// Subscribe to Tag Values
plc.Observe<double>("Tag0").Subscribe(x => Console.WriteLine($"Tag0: {x}"));
plc.Observe<double>("Tag1").Subscribe(x => Console.WriteLine($"Tag1: {x}"));
// Start Polling on previously disabled Tag
plc?.GetTag("Tag0")?.SetTagPollIng(true);
```

#### Write to PLC
```csharp
plc.Value<double>("Tag0", 1.0);
```
