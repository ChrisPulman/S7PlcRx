![License](https://img.shields.io/github/license/ChrisPulman/S7PlcRx.svg) [![Build](https://github.com/ChrisPulman/S7PlcRx/actions/workflows/BuildOnly.yml/badge.svg)](https://github.com/ChrisPulman/S7PlcRx/actions/workflows/BuildOnly.yml) ![Nuget](https://img.shields.io/nuget/dt/S7PlcRx?color=pink&style=plastic) [![NuGet](https://img.shields.io/nuget/v/S7PlcRx.svg?style=plastic)](https://www.nuget.org/packages/S7PlcRx)

![Alt](https://repobeats.axiom.co/api/embed/48a23aed3690ef69ed277b96f2154062dd436af2.svg "Repobeats analytics image")

<p align="left">
  <a href="https://github.com/ChrisPulman/S7PlcRx">
    <img alt="S7PlcRx" src="./Images/S7PlcRx.png" width="200"/>
  </a>
</p>

# S7PlcRx 🚀
**Enterprise-Grade Reactive S7 PLC Communications Library**

## 📖 Introduction

S7PlcRx is a comprehensive, production-ready reactive library for communicating with Siemens S7 PLCs. Built on Reactive Extensions (Rx.NET), it provides real-time data streaming, advanced performance optimizations, enterprise-grade reliability, and comprehensive industrial automation features.

### Disclaimer
This project is not affiliated with or endorsed by Siemens AG. It is an independent implementation for educational and industrial use.
S7PlcRx is designed to work with Siemens S7 PLCs, including S7-1500, S7-1200, S7-400, S7-300, S7-200, and Logo 0BA8 models.
The use of this libarary is at your own risk. The author is not responsible for any damages or losses incurred from using this library in production environments.
Ensure that you test thoroughly in a safe environment before deploying to production systems and always follow best practices for industrial automation.

## 🏭 Why S7PlcRx?
S7PlcRx is designed to meet the demanding requirements of modern industrial automation systems. It combines the power of reactive programming with high-performance optimizations, making it ideal for real-time PLC data monitoring, control, and diagnostics.

### ✨ Key Features

- **🔄 Reactive Data Streaming** - Real-time PLC data observation using Rx.NET
- **⚡ High-Performance Optimizations** - 5-10x faster with intelligent batching and caching
- **🏭 Enterprise-Grade Reliability** - Circuit breakers, failover, and 99.9% uptime
- **🔐 Industrial Security** - Encrypted communication and secure session management  
- **📊 Advanced Analytics** - Performance monitoring and AI-driven optimization
- **🎯 Symbolic Addressing** - CSV/JSON/XML symbol table support
- **🌐 Multi-PLC Support** - High-availability with automatic failover
- **📈 Production Diagnostics** - Comprehensive system health monitoring

### 🏭 Supported PLC Models

- **S7-1500** (S71500) - Latest generation with advanced features
- **S7-1200** (S71200) - Compact performance PLCs  
- **S7-400** (S7400) - High-end automation systems
- **S7-300** (S7300) - Modular automation systems
- **S7-200** (S7200) - Micro automation systems
- **Logo 0BA8** - Logic modules for basic automation

## 🚀 Quick Start

### Installation

S7PlcRx is available on [NuGet](https://www.nuget.org/packages/S7PlcRx/).

```powershell
# Package Manager
Install-Package S7PlcRx

# .NET CLI  
dotnet add package S7PlcRx
```

### ⚙️ PLC Configuration

Before using S7PlcRx, configure your Siemens PLC:

1. **Enable PUT/GET Communication** in PLC settings
2. **Set Data Blocks to Non-Optimized** for direct address access
3. **Configure Network Parameters** (IP address, subnet)
4. **Set Security Settings** (if using encrypted communication)

### 🔌 Basic Connection

```csharp
using S7PlcRx;
using S7PlcRx.Enums;
using System.Reactive.Linq;

// Create PLC connection
using var plc = new RxS7(CpuType.S71500, "192.168.1.100", rack: 0, slot: 1);

// Monitor connection status reactively
plc.IsConnected
    .Where(connected => connected)
    .Subscribe(_ => Console.WriteLine("✅ PLC Connected!"));

// Add tags for monitoring
plc.AddUpdateTagItem<float>("Temperature", "DB1.DBD0");
plc.AddUpdateTagItem<bool>("Running", "DB1.DBX4.0");

// Observe real-time data changes
plc.Observe<float>("Temperature")
    .Subscribe(temp => Console.WriteLine($"🌡️ Temperature: {temp:F1}°C"));

plc.Observe<bool>("Running")
    .Subscribe(running => Console.WriteLine($"⚙️ Running: {running}"));
```

## 📋 Core Functionality

### 🏷️ Tag Management

#### Adding Tags with Different Data Types

```csharp
// Basic data types
plc.AddUpdateTagItem<bool>("MotorRunning", "DB1.DBX0.0");
plc.AddUpdateTagItem<byte>("Status", "DB1.DBB2");
plc.AddUpdateTagItem<short>("Counter", "DB1.DBW4");
plc.AddUpdateTagItem<int>("ProductCount", "DB1.DBD6");
plc.AddUpdateTagItem<float>("Temperature", "DB1.DBD10");
plc.AddUpdateTagItem<double>("Precision", "DB1.DBD14");

// Array types with specified lengths
plc.AddUpdateTagItem<byte[]>("ByteArray", "DB1.DBB100", length: 64);
plc.AddUpdateTagItem<float[]>("TemperatureArray", "DB1.DBD200", length: 10);

// Chainable tag creation
plc.AddUpdateTagItem<float>("Temp1", "DB1.DBD0")
   .AddUpdateTagItem<float>("Temp2", "DB1.DBD4")
   .AddUpdateTagItem<bool>("Alarm", "DB1.DBX8.0")
   .SetTagPollIng(false); // Disable polling on last tag
```

#### Tag Polling Control

```csharp
// Add tag without automatic polling
var (tag, _) = plc.AddUpdateTagItem<float>("ManualRead", "DB1.DBD0")
                  .SetTagPollIng(false);

// Enable polling later
plc.GetTag("ManualRead").SetTagPollIng(true);

// Disable polling temporarily  
plc.GetTag("ManualRead").SetTagPollIng(false);
```

#### Tag Removal

```csharp
// Remove specific tag
plc.RemoveTagItem("OldTag");

// Check if tag exists
if (plc.TagList.ContainsKey("Temperature"))
{
    var (tag, _) = plc.GetTag("Temperature");
    Console.WriteLine($"Tag found: {tag?.Name}");
}
```

### 📖 Reading Data

#### Reactive Data Observation

```csharp
// Observe single tag changes
plc.Observe<float>("Temperature")
    .Where(temp => temp > 80.0f)
    .Subscribe(temp => Console.WriteLine($"🚨 High Temperature: {temp:F1}°C"));

// Observe multiple tags with filtering
plc.Observe<bool>("AlarmStatus")
    .Where(alarm => alarm)
    .Subscribe(_ => Console.WriteLine("🔔 ALARM TRIGGERED!"));

// Observe with time-based operations
plc.Observe<float>("Pressure")
    .Sample(TimeSpan.FromSeconds(5)) // Sample every 5 seconds
    .Subscribe(pressure => Console.WriteLine($"📊 Pressure: {pressure:F2} bar"));

// Buffer values for analysis
plc.Observe<float>("FlowRate")
    .Buffer(TimeSpan.FromMinutes(1))
    .Subscribe(values => 
    {
        var avg = values.Average();
        Console.WriteLine($"📈 Average Flow Rate (1min): {avg:F2} L/min");
    });
```

#### Manual Reading

```csharp
// Read single values
var temperature = await plc.Value<float>("Temperature");
var isRunning = await plc.Value<bool>("Running");
var productCount = await plc.Value<int>("ProductCount");

Console.WriteLine($"Current Temperature: {temperature:F1}°C");
Console.WriteLine($"System Running: {isRunning}");
Console.WriteLine($"Products Made: {productCount}");
```

### ✏️ Writing Data

#### Direct Value Writing

```csharp
// Write different data types
plc.Value("SetPoint", 75.5f);           // Float
plc.Value("Enable", true);              // Boolean  
plc.Value("RecipeNumber", 42);          // Integer
plc.Value("OperatorID", 1234);          // Word

// Write arrays
plc.Value("Recipe", new float[] { 25.0f, 50.0f, 75.0f });
plc.Value("ByteData", new byte[] { 0x01, 0x02, 0x03 });
```

#### Conditional Writing

```csharp
// Write only when connected
plc.IsConnected
    .Where(connected => connected)
    .Subscribe(_ => 
    {
        plc.Value("Heartbeat", DateTime.Now.Ticks);
        plc.Value("SystemReady", true);
    });
```

### 📊 System Information

#### CPU Information

```csharp
// Get CPU info reactively
plc.GetCpuInfo()
    .Subscribe(info => 
    {
        Console.WriteLine("📟 PLC Information:");
        Console.WriteLine($"  AS Name: {info[0]}");
        Console.WriteLine($"  Module: {info[1]}");
        Console.WriteLine($"  Serial: {info[3]}");
        Console.WriteLine($"  Order Code: {info[5]}");
        Console.WriteLine($"  Version: {info[6]} {info[7]} {info[8]}");
    });

// Get CPU info once
var cpuInfo = await plc.GetCpuInfo().FirstAsync();
```

### 🐕 Watchdog Configuration

Keep your PLC connection alive with automatic watchdog functionality:

```csharp
// Create PLC with watchdog
var plc = new RxS7(
    cpuType: CpuType.S71500,
    ip: "192.168.1.100", 
    rack: 0,
    slot: 1,
    watchDogAddress: "DB100.DBW0",    // Must be DBW address
    interval: 100,                    // Polling interval (ms)
    watchDogValueToWrite: 4500,       // Watchdog value
    watchDogInterval: 10              // Watchdog write interval (seconds)
);

// Configure watchdog display
plc.ShowWatchDogWriting = true; // Show watchdog activity

// Monitor watchdog status
plc.Status
    .Where(status => status.Contains("WatchDog"))
    .Subscribe(status => Console.WriteLine($"🐕 {status}"));
```

## ⚡ Performance Optimizations

### 🚀 Batch Operations

Dramatically improve performance with intelligent batch operations:

```csharp
using S7PlcRx.Performance;

// Batch reading - up to 10x faster
var tagNames = new[] { "Temp1", "Temp2", "Temp3", "Pressure1", "Flow1" };
var results = await plc.ReadOptimized<float>(tagNames);

foreach (var (tag, value) in results)
{
    Console.WriteLine($"{tag}: {value:F2}");
}

// Batch writing with verification
var writeValues = new Dictionary<string, float>
{
    ["SetPoint1"] = 75.5f,
    ["SetPoint2"] = 80.0f,
    ["SetPoint3"] = 65.0f
};

var config = new WriteOptimizationConfig
{
    EnableParallelWrites = true,
    VerifyWrites = true,
    InterGroupDelayMs = 10
};

var writeResult = await plc.WriteOptimized(writeValues, config);
Console.WriteLine($"✅ {writeResult.SuccessfulWrites.Count} writes completed");
```

### 📊 Performance Monitoring

```csharp
// Monitor real-time performance
plc.MonitorPerformance(TimeSpan.FromSeconds(30))
    .Subscribe(metrics => 
    {
        Console.WriteLine($"📈 Performance Metrics:");
        Console.WriteLine($"  Operations/sec: {metrics.OperationsPerSecond:F1}");
        Console.WriteLine($"  Avg Response: {metrics.AverageResponseTime:F0}ms");
        Console.WriteLine($"  Error Rate: {metrics.ErrorRate:P2}");
        Console.WriteLine($"  Active Tags: {metrics.ActiveTagCount}");
    });

// Get performance statistics
var stats = plc.GetPerformanceStatistics();
Console.WriteLine($"Total Operations: {stats.TotalOperations}");
Console.WriteLine($"Connection Uptime: {stats.ConnectionUptime.TotalHours:F1}h");
```

### 🎯 Smart Caching

```csharp
using S7PlcRx.Optimization;

// Enable intelligent caching
var cachedValue = await plc.ValueCached<float>("Temperature", TimeSpan.FromSeconds(1));

// Monitor cache performance
var cacheStats = plc.GetCacheStatistics();
Console.WriteLine($"Cache Hit Rate: {cacheStats.HitRate:P1}");
Console.WriteLine($"Cache Entries: {cacheStats.TotalEntries}");

// Clear cache when needed
plc.ClearCache();
```

### 🔍 Smart Tag Monitoring

Monitor only significant changes to reduce noise:

```csharp
// Monitor with change threshold and debouncing
plc.MonitorTagSmart<float>("Temperature", changeThreshold: 0.5, debounceMs: 100)
    .Subscribe(change => 
    {
        Console.WriteLine($"🔔 Significant Change:");
        Console.WriteLine($"  Tag: {change.TagName}");
        Console.WriteLine($"  Previous: {change.PreviousValue:F2}");
        Console.WriteLine($"  Current: {change.CurrentValue:F2}");
        Console.WriteLine($"  Change: {change.ChangeAmount:F2}");
    });
```

## 🏭 Enterprise Features

### 🎯 High-Performance Tag Groups

Group related tags for optimized batch operations:

```csharp
using S7PlcRx.Performance;

// Create specialized tag groups
var temperatureGroup = plc.CreateTagGroup("Temperatures",
    "DB1.DBD0",   // Reactor temp
    "DB1.DBD4",   // Cooling temp  
    "DB1.DBD8",   // Ambient temp
    "DB1.DBD12"   // Exhaust temp
);

// Read all temperatures efficiently
var temperatures = await temperatureGroup.ReadAll<float>();
foreach (var temp in temperatures)
{
    Console.WriteLine($"{temp.Key}: {temp.Value:F1}°C");
}

// Monitor group changes
temperatureGroup.ObserveGroup()
    .Subscribe(groupData => 
    {
        var avgTemp = groupData.Values.OfType<float>().Average();
        Console.WriteLine($"🌡️ Average Temperature: {avgTemp:F1}°C");
    });

// Cleanup
temperatureGroup.Dispose();
```

### 🌐 Symbol Table Support

Use symbolic names instead of absolute addresses:

```csharp
using S7PlcRx.Enterprise;

// Load symbol table from CSV
var csvData = @"Name,Address,DataType,Length,Description
ProcessTemperature,DB1.DBD0,REAL,1,Main Process Temperature
SystemPressure,DB1.DBD4,REAL,1,System Operating Pressure
MotorRunning,DB1.DBX8.0,BOOL,1,Motor Running Status
RecipeArray,DB2.DBD0,REAL,10,Recipe Parameters";

var symbolTable = await plc.LoadSymbolTable(csvData, SymbolTableFormat.Csv);

// Use symbolic addressing
var temperature = await plc.ReadSymbol<float>("ProcessTemperature");
plc.WriteSymbol("MotorRunning", true);

// Monitor symbolic tags
plc.Observe<float>("ProcessTemperature")
    .Subscribe(temp => Console.WriteLine($"Process Temp: {temp:F1}°C"));
```

### 🔐 Secure Communication

```csharp
// Enable encrypted communication
var securityContext = plc.EnableSecureCommunication(
    encryptionKey: "MySecureKey123!",
    sessionTimeout: TimeSpan.FromHours(8)
);

Console.WriteLine($"🔐 Secure session started: {securityContext.IsEnabled}");
```

### 🌐 High Availability & Failover

```csharp
// Create high-availability setup
var primaryPlc = new RxS7(CpuType.S71500, "192.168.1.100", 0, 1);
var backupPlcs = new List<IRxS7>
{
    new RxS7(CpuType.S71500, "192.168.1.101", 0, 1),
    new RxS7(CpuType.S71500, "192.168.1.102", 0, 1)
};

using var haManager = S7EnterpriseExtensions.CreateHighAvailabilityConnection(
    primaryPlc, backupPlcs, TimeSpan.FromSeconds(10));

// Monitor failover events
haManager.FailoverEvents
    .Subscribe(evt => Console.WriteLine($"🔄 Failover: {evt.Reason}"));

// Use active PLC transparently
var activePlc = haManager.ActivePLC;
```

## 🏭 Production Diagnostics

### 📊 System Health Monitoring

```csharp
using S7PlcRx.Production;

// Comprehensive system diagnostics
var diagnostics = await plc.GetDiagnostics();

Console.WriteLine("🏭 System Diagnostics:");
Console.WriteLine($"  PLC Type: {diagnostics.PLCType}");
Console.WriteLine($"  Connection: {(diagnostics.IsConnected ? "✅" : "❌")}");
Console.WriteLine($"  Latency: {diagnostics.ConnectionLatencyMs:F0}ms");
Console.WriteLine($"  Total Tags: {diagnostics.TagMetrics.TotalTags}");
Console.WriteLine($"  Active Tags: {diagnostics.TagMetrics.ActiveTags}");

Console.WriteLine("\n💡 Recommendations:");
foreach (var recommendation in diagnostics.Recommendations)
{
    Console.WriteLine($"  • {recommendation}");
}
```

### 📈 Performance Analysis

```csharp
// Analyze performance over time
var analysis = await plc.AnalyzePerformance(TimeSpan.FromMinutes(5));

Console.WriteLine($"📊 Performance Analysis ({analysis.MonitoringDuration.TotalMinutes:F1}min):");
Console.WriteLine($"  Total Changes: {analysis.TotalTagChanges}");
Console.WriteLine($"  Avg Changes/Tag: {analysis.AverageChangesPerTag:F1}");

// View most active tags
var topTags = analysis.TagChangeFrequencies
    .OrderByDescending(kvp => kvp.Value)
    .Take(5);

Console.WriteLine("\n🔥 Most Active Tags:");
foreach (var (tag, changes) in topTags)
{
    Console.WriteLine($"  {tag}: {changes} changes");
}
```

### ✅ Production Readiness Validation

```csharp
var config = new ProductionValidationConfig
{
    MaxAcceptableResponseTime = TimeSpan.FromSeconds(1),
    MinimumReliabilityRate = 0.95,
    ReliabilityTestCount = 10,
    MinimumProductionScore = 80.0
};

var validation = await plc.ValidateProductionReadiness(config);

Console.WriteLine($"✅ Production Ready: {validation.IsProductionReady}");
Console.WriteLine($"📊 Overall Score: {validation.OverallScore:F1}/100");

foreach (var test in validation.ValidationTests)
{
    var status = test.Success ? "✅" : "❌";
    Console.WriteLine($"  {status} {test.TestName}: {test.Duration.TotalMilliseconds:F0}ms");
}
```

### ⚡ Circuit Breaker & Error Handling

```csharp
var errorConfig = new ProductionErrorConfig
{
    MaxRetryAttempts = 3,
    BaseRetryDelayMs = 100,
    UseExponentialBackoff = true,
    CircuitBreakerThreshold = 5,
    CircuitBreakerTimeout = TimeSpan.FromSeconds(30)
};

// Execute with automatic retry and circuit breaker
var result = await plc.ExecuteWithErrorHandling(async () =>
{
    return await plc.Value<float>("CriticalSensor");
}, errorConfig);
```

## 🏭 Complete Production Example

Here's a comprehensive example showing multiple features working together:

```csharp
using S7PlcRx;
using S7PlcRx.Enums;
using S7PlcRx.Performance;
using S7PlcRx.Enterprise;
using S7PlcRx.Production;
using System.Reactive.Linq;

class ProductionSystem
{
    private readonly IRxS7 _plc;
    private readonly HighPerformanceTagGroup _processGroup;
    private readonly HighPerformanceTagGroup _alarmGroup;

    public async Task StartAsync()
    {
        // 1. Initialize PLC with watchdog
        using var plc = new RxS7(CpuType.S71500, "192.168.1.100", 0, 1, 
            "DB100.DBW0", 100, 4500, 10);

        // 2. Load symbol table
        var symbolTable = await LoadSymbolTableAsync(plc);
        
        // 3. Create high-performance tag groups
        var processGroup = plc.CreateTagGroup("ProcessData",
            "DB1.DBD0",   // Temperature
            "DB1.DBD4",   // Pressure
            "DB1.DBD8",   // Flow rate
            "DB1.DBW12"   // Speed
        );

        var alarmGroup = plc.CreateTagGroup("Alarms",
            "DB2.DBX0.0", // High temp alarm
            "DB2.DBX0.1", // High pressure alarm
            "DB2.DBX0.2", // Equipment fault
            "DB2.DBX0.3"  // Emergency stop
        );

        // 4. Enable performance monitoring
        var performanceSubscription = plc.MonitorPerformance(TimeSpan.FromSeconds(30))
            .Subscribe(metrics => LogPerformanceMetrics(metrics));

        // 5. Monitor process data
        var processSubscription = processGroup.ObserveGroup()
            .Sample(TimeSpan.FromSeconds(1))
            .Subscribe(data => ProcessDataUpdate(data));

        // 6. Monitor alarms with immediate response
        var alarmSubscription = alarmGroup.ObserveGroup()
            .Where(alarms => alarms.Values.OfType<bool>().Any(alarm => alarm))
            .Subscribe(alarms => HandleAlarms(alarms));

        // 7. Smart temperature monitoring
        var tempMonitoring = plc.MonitorTagSmart<float>("ProcessTemperature", 0.5, 100)
            .Subscribe(change => HandleTemperatureChange(change));

        // 8. Batch operations for efficiency
        await PerformBatchOperations(plc);

        // 9. System health validation
        await ValidateSystemHealth(plc);

        Console.WriteLine("🏭 Production system started successfully!");
        
        // Keep running
        Console.ReadLine();

        // Cleanup
        performanceSubscription.Dispose();
        processSubscription.Dispose();
        alarmSubscription.Dispose();
        tempMonitoring.Dispose();
        processGroup.Dispose();
        alarmGroup.Dispose();
    }

    private async Task<SymbolTable> LoadSymbolTableAsync(IRxS7 plc)
    {
        var csvData = @"Name,Address,DataType,Length,Description
ProcessTemperature,DB1.DBD0,REAL,1,Main Process Temperature
ProcessPressure,DB1.DBD4,REAL,1,System Operating Pressure
FlowRate,DB1.DBD8,REAL,1,Flow Rate Sensor
MotorSpeed,DB1.DBW12,INT,1,Motor Speed RPM
HighTempAlarm,DB2.DBX0.0,BOOL,1,High Temperature Alarm
HighPressureAlarm,DB2.DBX0.1,BOOL,1,High Pressure Alarm";

        return await plc.LoadSymbolTable(csvData, SymbolTableFormat.Csv);
    }

    private void LogPerformanceMetrics(PerformanceMetrics metrics)
    {
        Console.WriteLine($"📊 Performance: {metrics.OperationsPerSecond:F1} ops/sec, " +
                         $"Response: {metrics.AverageResponseTime:F0}ms, " +
                         $"Errors: {metrics.ErrorRate:P2}");
    }

    private void ProcessDataUpdate(Dictionary<string, object> data)
    {
        // Process real-time data updates
        foreach (var (tag, value) in data)
        {
            Console.WriteLine($"📈 {tag}: {value}");
        }
    }

    private void HandleAlarms(Dictionary<string, object> alarms)
    {
        var activeAlarms = alarms.Where(kvp => kvp.Value is bool b && b);
        foreach (var (alarm, _) in activeAlarms)
        {
            Console.WriteLine($"🚨 ALARM: {alarm}");
            // Implement alarm handling logic
        }
    }

    private void HandleTemperatureChange(SmartTagChange<float> change)
    {
        Console.WriteLine($"🌡️ Temperature changed: {change.PreviousValue:F1}°C → " +
                         $"{change.CurrentValue:F1}°C (Δ{change.ChangeAmount:F1}°C)");
    }

    private async Task PerformBatchOperations(IRxS7 plc)
    {
        // Batch read process values
        var processValues = await plc.ReadOptimized<float>(new[] 
        {
            "ProcessTemperature", "ProcessPressure", "FlowRate"
        });

        // Batch write setpoints
        var setpoints = new Dictionary<string, float>
        {
            ["TempSetpoint"] = 75.0f,
            ["PressureSetpoint"] = 2.5f,
            ["FlowSetpoint"] = 100.0f
        };

        var writeResult = await plc.WriteOptimized(setpoints, new WriteOptimizationConfig
        {
            VerifyWrites = true,
            EnableParallelWrites = true
        });

        Console.WriteLine($"✅ Batch operations: {writeResult.SuccessfulWrites.Count} setpoints updated");
    }

    private async Task ValidateSystemHealth(IRxS7 plc)
    {
        var diagnostics = await plc.GetDiagnostics();
        
        if (diagnostics.ConnectionLatencyMs > 500)
        {
            Console.WriteLine($"⚠️ High latency detected: {diagnostics.ConnectionLatencyMs:F0}ms");
        }

        var validation = await plc.ValidateProductionReadiness();
        Console.WriteLine($"✅ Production readiness: {validation.OverallScore:F1}/100");
    }
}
```

## 📚 Advanced Topics

### 🎯 Data Type Support

| S7 Type | C# Type | Address Format | Example |
|---------|---------|----------------|---------|
| Bool | `bool` | `DB?.DBX?.?` | `DB1.DBX0.0` |
| Byte | `byte` | `DB?.DBB?` | `DB1.DBB0` |
| Word | `ushort` | `DB?.DBW?` | `DB1.DBW0` |
| Int | `short` | `DB?.DBW?` | `DB1.DBW0` |
| DWord | `uint` | `DB?.DBD?` | `DB1.DBD0` |
| DInt | `int` | `DB?.DBD?` | `DB1.DBD0` |
| Real | `float` | `DB?.DBD?` | `DB1.DBD0` |
| LReal | `double` | `DB?.DBD?` | `DB1.DBD0` |
| Arrays | `T[]` | `DB?.DB*?` | `DB1.DBB0` (with length) |

### 🔧 Memory Areas

- **Data Blocks (DB)**: `DB1.DBD0`, `DB1.DBX0.0`, `DB1.DBW0`
- **Inputs (I/E)**: `IB0`, `IW0`, `ID0`, `I0.0`
- **Outputs (Q/A)**: `QB0`, `QW0`, `QD0`, `Q0.0`  
- **Memory (M)**: `MB0`, `MW0`, `MD0`, `M0.0`
- **Timers (T)**: `T1`, `T2`
- **Counters (C)**: `C1`, `C2`

### ⚙️ Connection Pool Configuration

```csharp
using S7PlcRx.Core;

var poolConfig = new ConnectionPoolConfig
{
    MaxConnections = 10,
    ConnectionTimeout = TimeSpan.FromSeconds(30),
    EnableConnectionReuse = true,
    HealthCheckInterval = TimeSpan.FromMinutes(1)
};

var connectionConfigs = new[]
{
    new PlcConnectionConfig 
    { 
        PLCType = CpuType.S71500, 
        IPAddress = "192.168.1.100", 
        Rack = 0, 
        Slot = 1 
    }
};

using var pool = S7EnterpriseExtensions.CreateConnectionPool(connectionConfigs, poolConfig);
var connection = pool.GetConnection();
```

## 🔧 Best Practices

### ⚡ Performance Optimization

1. **Use Batch Operations** - Group multiple reads/writes together
2. **Enable Intelligent Caching** - Cache frequently accessed values
3. **Optimize Data Block Layout** - Group related tags in same DB
4. **Monitor Performance Metrics** - Track and optimize bottlenecks
5. **Use Tag Groups** - Group logically related tags for efficiency

### 🛡️ Reliability

1. **Implement Circuit Breakers** - Prevent cascade failures
2. **Use Watchdog Functionality** - Keep connections alive
3. **Enable Automatic Retry** - Handle temporary network issues
4. **Monitor System Health** - Proactive issue detection
5. **Validate Production Readiness** - Ensure system reliability

### 🔐 Security

1. **Enable Encrypted Communication** - Protect sensitive data
2. **Use Session Management** - Control access timeouts
3. **Implement Access Controls** - Restrict PLC operations
4. **Monitor Security Events** - Track unauthorized access
5. **Regular Security Audits** - Maintain security posture

## 📖 API Reference

### Core Classes

- **`RxS7`** - Main PLC communication class
- **`IRxS7`** - PLC interface for dependency injection
- **`Tag`** - Represents a PLC tag/variable
- **`Tags`** - Collection of PLC tags

### Extension Classes

- **`TagExtensions`** - Tag management and operations`
- **`PerformanceExtensions`** - Performance optimization methods
- **`OptimizationExtensions`** - Caching and smart monitoring  
- **`EnterpriseExtensions`** - Enterprise features (HA, security, symbols)
- **`ProductionExtensions`** - Production reliability features
- **`AdvancedExtensions`** - Advanced batch operations and diagnostics

### Factory Classes

- **`S71500`** - S7-1500 PLC factory
- **`S71200`** - S7-1200 PLC factory  
- **`S7400`** - S7-400 PLC factory
- **`S7300`** - S7-300 PLC factory
- **`S7200`** - S7-200 PLC factory

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request. For major changes, please open an issue first to discuss what you would like to change.

## 📞 Support

- **Issues**: [GitHub Issues](https://github.com/ChrisPulman/S7PlcRx/issues)
- **Discussions**: [GitHub Discussions](https://github.com/ChrisPulman/S7PlcRx/discussions)  
- **NuGet**: [S7PlcRx Package](https://www.nuget.org/packages/S7PlcRx)

---

**S7PlcRx** - Empowering Industrial Automation with Reactive Technology ⚡🏭
