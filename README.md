![License](https://img.shields.io/github/license/ChrisPulman/S7PlcRx.svg) [![Build](https://github.com/ChrisPulman/S7PlcRx/actions/workflows/BuildOnly.yml/badge.svg)](https://github.com/ChrisPulman/S7PlcRx/actions/workflows/BuildOnly.yml) ![Nuget](https://img.shields.io/nuget/dt/S7PlcRx?color=pink&style=plastic) [![NuGet](https://img.shields.io/nuget/v/S7PlcRx.svg?style=plastic)](https://www.nuget.org/packages/S7PlcRx)

<p align="left">
  <a href="https://github.com/ChrisPulman/S7PlcRx">
    <img alt="S7PlcRx" src="https://github.com/ChrisPulman/S7PlcRx/blob/main/Images/S7PlcRx.png" width="200"/>
  </a>
</p>

# S7PlcRx
S7 PLC Communications Library

## Introduction
S7PlcRx is a library that provides a simple interface to communicate with Siemens S7 PLCs.

## Features
- Read and Write to PLC
- Read and Write to PLC with Subscription


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

var plc = new RxS7(S7PlcRx.Enums.CpuType.S71500, "", 0, 1);
plc.AddUpdateTagItem<double>("Tag0", "DB500.DBD0");
plc.AddUpdateTagItem<double>("Tag1", "DB500.DBD8");

plc.Observe<double>("Tag0").Subscribe(x => Console.WriteLine($"Tag0: {x}"));
plc.Observe<double>("Tag1").Subscribe(x => Console.WriteLine($"Tag1: {x}"));
```

#### Write to PLC
```csharp
plc.Value<double>("Tag0", 1.0);
```
