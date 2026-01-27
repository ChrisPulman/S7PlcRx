// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Reactive.Linq;
using S7PlcRx.Core;

namespace S7PlcRx.Enterprise;

/// <summary>
/// Provides extension methods for enhanced PLC connectivity, symbolic addressing, high-availability management, and
/// connection pooling in enterprise automation scenarios.
/// </summary>
/// <remarks>The EnterpriseExtensions class offers advanced features for working with PLCs, including loading and
/// caching symbol tables for symbolic access, reading and writing values by symbol name, creating high-availability
/// connections with automatic failover, and managing connection pools for high-throughput applications. These methods
/// are designed to simplify integration with industrial automation systems and improve reliability and scalability in
/// production environments.</remarks>
public static class EnterpriseExtensions
{
    private static readonly ConcurrentDictionary<string, SymbolTable> _symbolTables = new();
    ////private static readonly ConcurrentDictionary<string, SecurityContext> _securityContexts = new();
    private static readonly ConcurrentDictionary<string, ConnectionPool> _connectionPools = new();

    /// <summary>
    /// Loads and caches a symbol table for symbolic addressing support.
    /// Enables reading/writing using symbolic names instead of absolute addresses.
    /// </summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="symbolTableData">Symbol table data (CSV format supported).</param>
    /// <param name="format">The format of the symbol table data.</param>
    /// <returns>The loaded symbol table.</returns>
    public static async Task<SymbolTable> LoadSymbolTable(
        this IRxS7 plc,
        string symbolTableData,
        SymbolTableFormat format = SymbolTableFormat.Csv)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        if (string.IsNullOrWhiteSpace(symbolTableData))
        {
            throw new ArgumentException("Symbol table data cannot be null or empty", nameof(symbolTableData));
        }

        var key = $"{plc.IP}_{plc.PLCType}_{plc.Rack}_{plc.Slot}";

        var symbolTable = format switch
        {
            SymbolTableFormat.Csv => await ParseCsvSymbolTable(symbolTableData),
            SymbolTableFormat.Json => await ParseJsonSymbolTable(symbolTableData),
            SymbolTableFormat.Xml => await ParseXmlSymbolTable(symbolTableData),
            _ => throw new ArgumentException($"Unsupported symbol table format: {format}")
        };

        _symbolTables.AddOrUpdate(key, symbolTable, (_, _) => symbolTable);

        // Automatically create tags for all symbols
        foreach (var symbol in symbolTable.Symbols.Values)
        {
            if (!plc.TagList.ContainsKey(symbol.Name))
            {
                var tagType = symbol.DataType switch
                {
                    "BOOL" => typeof(bool),
                    "BYTE" => typeof(byte),
                    "WORD" => typeof(ushort),
                    "DWORD" => typeof(uint),
                    "INT" => typeof(short),
                    "DINT" => typeof(int),
                    "REAL" => typeof(float),
                    "LREAL" => typeof(double),
                    "STRING" => typeof(string),
                    _ when symbol.DataType.Contains("ARRAY") => typeof(byte[]),
                    _ => typeof(object)
                };

                plc.AddUpdateTagItem(tagType, symbol.Name, symbol.Address, symbol.Length);
            }
        }

        return symbolTable;
    }

    /// <summary>
    /// Asynchronously reads the value of the specified symbol from the PLC and returns it as the specified type.
    /// </summary>
    /// <typeparam name="T">The type to which the symbol's value is converted and returned.</typeparam>
    /// <param name="plc">The PLC connection used to access the symbol table and read the symbol value. Cannot be null.</param>
    /// <param name="symbolName">The name of the symbol to read from the PLC. Must correspond to a symbol present in the PLC's symbol table.</param>
    /// <returns>A task that represents the asynchronous read operation. The task result contains the value of the symbol as type
    /// <typeparamref name="T"/>, or <see langword="null"/> if the symbol's value is null.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="plc"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if a symbol with the specified <paramref name="symbolName"/> does not exist in the PLC's symbol table.</exception>
    public static async Task<T?> ReadSymbol<T>(this IRxS7 plc, string symbolName)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        var symbolTable = GetSymbolTable(plc);
        if (symbolTable?.Symbols.TryGetValue(symbolName, out var symbol) == true)
        {
            return await plc.Value<T>(symbol.Name);
        }

        throw new ArgumentException($"Symbol '{symbolName}' not found in symbol table");
    }

    /// <summary>
    /// Writes a value to the specified PLC symbol by name.
    /// </summary>
    /// <typeparam name="T">The type of the value to write to the symbol.</typeparam>
    /// <param name="plc">The PLC instance to which the symbol value will be written. Cannot be null.</param>
    /// <param name="symbolName">The name of the symbol in the PLC to write the value to. Must correspond to a valid symbol in the PLC's symbol
    /// table.</param>
    /// <param name="value">The value to write to the specified symbol.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="plc"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="symbolName"/> does not correspond to a symbol in the PLC's symbol table.</exception>
    public static void WriteSymbol<T>(this IRxS7 plc, string symbolName, T value)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        var symbolTable = GetSymbolTable(plc);
        if (symbolTable?.Symbols.TryGetValue(symbolName, out var symbol) == true)
        {
            plc.Value(symbol.Name, value);
            return;
        }

        throw new ArgumentException($"Symbol '{symbolName}' not found in symbol table");
    }

    /////// <summary>
    /////// Enables secure communication with encrypted credentials and session management.
    /////// </summary>
    /////// <param name="plc">The PLC instance.</param>
    /////// <param name="encryptionKey">Encryption key for secure communication.</param>
    /////// <param name="sessionTimeout">Session timeout duration.</param>
    /////// <returns>Security context for the connection.</returns>
    ////public static SecurityContext EnableSecureCommunication(
    ////    this IRxS7 plc,
    ////    string encryptionKey,
    ////    TimeSpan? sessionTimeout = null)
    ////{
    ////    if (plc == null)
    ////    {
    ////        throw new ArgumentNullException(nameof(plc));
    ////    }

    ////    if (string.IsNullOrWhiteSpace(encryptionKey))
    ////    {
    ////        throw new ArgumentException("Encryption key cannot be null or empty", nameof(encryptionKey));
    ////    }

    ////    var key = $"{plc.IP}_{plc.PLCType}_{plc.Rack}_{plc.Slot}";
    ////    var timeout = sessionTimeout ?? TimeSpan.FromHours(8);

    ////    var securityContext = new SecurityContext
    ////    {
    ////        PLCKey = key,
    ////        EncryptionKey = encryptionKey,
    ////        SessionStartTime = DateTime.UtcNow,
    ////        SessionTimeout = timeout,
    ////        IsEnabled = true
    ////    };

    ////    _securityContexts.AddOrUpdate(key, securityContext, (_, _) => securityContext);
    ////    return securityContext;
    ////}

    /// <summary>
    /// Creates a high-availability connection manager that coordinates failover between a primary PLC and one or more
    /// backup PLCs.
    /// </summary>
    /// <remarks>The returned manager automatically monitors the health of the primary and backup PLCs and
    /// handles failover as needed. The order of backupPlcs determines the failover priority.</remarks>
    /// <param name="primaryPlc">The primary PLC instance to be used for initial communication and operations. Cannot be null.</param>
    /// <param name="backupPlcs">A list of backup PLC instances to be used for failover if the primary PLC becomes unavailable. Cannot be null or
    /// empty.</param>
    /// <param name="healthCheckInterval">The interval at which the health of the PLCs is checked. If null, a default interval is used.</param>
    /// <returns>A HighAvailabilityPlcManager instance that manages high-availability communication across the specified PLCs.</returns>
    /// <exception cref="ArgumentNullException">Thrown if primaryPlc or backupPlcs is null.</exception>
    public static HighAvailabilityPlcManager CreateHighAvailabilityConnection(
        IRxS7 primaryPlc,
        IList<IRxS7> backupPlcs,
        TimeSpan? healthCheckInterval = null)
    {
        if (primaryPlc == null)
        {
            throw new ArgumentNullException(nameof(primaryPlc));
        }

        if (backupPlcs == null)
        {
            throw new ArgumentNullException(nameof(backupPlcs));
        }

        return new HighAvailabilityPlcManager(primaryPlc, backupPlcs, healthCheckInterval);
    }

    /// <summary>
    /// Creates a new connection pool using the specified PLC connection configurations and pool settings.
    /// </summary>
    /// <param name="connectionConfigs">A collection of PLC connection configurations to include in the pool. Must contain at least one configuration.</param>
    /// <param name="poolConfig">The configuration settings to apply to the connection pool. Cannot be null.</param>
    /// <returns>A new instance of <see cref="ConnectionPool"/> initialized with the provided connection configurations and pool
    /// settings.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="connectionConfigs"/> or <paramref name="poolConfig"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="connectionConfigs"/> does not contain at least one configuration.</exception>
    public static ConnectionPool CreateConnectionPool(
        IEnumerable<PlcConnectionConfig> connectionConfigs,
        ConnectionPoolConfig poolConfig)
    {
        if (connectionConfigs == null)
        {
            throw new ArgumentNullException(nameof(connectionConfigs));
        }

        if (poolConfig == null)
        {
            throw new ArgumentNullException(nameof(poolConfig));
        }

        var configs = connectionConfigs.ToList();
        if (configs.Count == 0)
        {
            throw new ArgumentException("At least one connection configuration is required", nameof(connectionConfigs));
        }

        var poolKey = $"Pool_{DateTime.UtcNow.Ticks}";
        var pool = new ConnectionPool(configs, poolConfig);
        _connectionPools.AddOrUpdate(poolKey, pool, (_, _) => pool);

        return pool;
    }

    /// <summary>
    /// Retrieves the symbol table associated with the specified PLC connection, if available.
    /// </summary>
    /// <param name="plc">The PLC connection for which to retrieve the symbol table. Cannot be null.</param>
    /// <returns>The symbol table associated with the specified PLC connection, or null if no symbol table is found.</returns>
    private static SymbolTable? GetSymbolTable(IRxS7 plc)
    {
        var key = $"{plc.IP}_{plc.PLCType}_{plc.Rack}_{plc.Slot}";
        return _symbolTables.TryGetValue(key, out var symbolTable) ? symbolTable : null;
    }

    /// <summary>
    /// Parses a CSV-formatted string to create a symbol table containing symbol definitions.
    /// </summary>
    /// <remarks>Each symbol is expected to have at least three columns: Name, Address, and DataType.
    /// Additional columns for Length and Description are optional. If a header row is present, it will be skipped
    /// automatically.</remarks>
    /// <param name="csvData">The CSV data as a string, where each line represents a symbol and columns are separated by commas. The first
    /// line may optionally contain a header.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a SymbolTable populated with symbols
    /// parsed from the CSV data. If the input is empty or contains no valid symbols, the returned table will be empty.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the CSV data cannot be parsed into a symbol table due to invalid format or other errors.</exception>
    private static async Task<SymbolTable> ParseCsvSymbolTable(string csvData)
    {
        var symbolTable = new SymbolTable();

        try
        {
            var lines = csvData.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
            {
                return symbolTable;
            }

            // Skip header if present
            var startIndex = lines[0].Contains("Name") || lines[0].Contains("Address") ? 1 : 0;

            for (var i = startIndex; i < lines.Length; i++)
            {
                var values = lines[i].Split(',');
                if (values.Length >= 3)
                {
                    var symbol = new Symbol
                    {
                        Name = values[0].Trim('"'),
                        Address = values[1].Trim('"'),
                        DataType = values[2].Trim('"'),
                        Length = values.Length > 3 && int.TryParse(values[3], out var len) ? len : 1,
                        Description = values.Length > 4 ? values[4].Trim('"') : string.Empty
                    };

                    symbolTable.Symbols[symbol.Name] = symbol;
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse CSV symbol table: {ex.Message}", ex);
        }

        await Task.Delay(1); // Make it async
        return symbolTable;
    }

    /// <summary>
    /// Parses a JSON string containing symbol definitions and returns a corresponding symbol table.
    /// </summary>
    /// <param name="jsonData">A JSON-formatted string representing a collection of symbols to be loaded into the symbol table. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="SymbolTable"/>
    /// populated with symbols parsed from the provided JSON data.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the JSON data cannot be parsed into a valid collection of symbols.</exception>
    private static async Task<SymbolTable> ParseJsonSymbolTable(string jsonData)
    {
        var symbolTable = new SymbolTable();

        // Assuming a simple JSON structure for symbols
        try
        {
            var symbols = System.Text.Json.JsonSerializer.Deserialize<List<Symbol>>(jsonData);
            if (symbols != null)
            {
                foreach (var symbol in symbols)
                {
                    symbolTable.Symbols[symbol.Name] = symbol;
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse JSON symbol table: {ex.Message}", ex);
        }

        await Task.Delay(1); // Placeholder implementation
        return symbolTable;
    }

    /// <summary>
    /// Parses an XML string containing symbol definitions and returns a populated symbol table.
    /// </summary>
    /// <param name="xmlData">The XML data representing the symbol table. Must be a well-formed XML string containing symbol entries.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="SymbolTable"/>
    /// populated with symbols parsed from the XML data.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the XML data cannot be parsed or does not conform to the expected structure.</exception>
    private static async Task<SymbolTable> ParseXmlSymbolTable(string xmlData)
    {
        var symbolTable = new SymbolTable();

        // Assuming a simple XML structure for symbols
        try
        {
            var doc = new System.Xml.Linq.XDocument();
            doc = System.Xml.Linq.XDocument.Parse(xmlData);
            var symbols = doc.Descendants("Symbol")
                .Select(x => new Symbol
                {
                    Name = x.Element("Name")?.Value ?? string.Empty,
                    Address = x.Element("Address")?.Value ?? string.Empty,
                    DataType = x.Element("DataType")?.Value ?? string.Empty,
                    Length = int.TryParse(x.Element("Length")?.Value, out var len) ? len : 1,
                    Description = x.Element("Description")?.Value ?? string.Empty
                })
                .Where(s => !string.IsNullOrEmpty(s.Name));
            foreach (var symbol in symbols)
            {
                symbolTable.Symbols[symbol.Name] = symbol;
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse XML symbol table: {ex.Message}", ex);
        }

        await Task.Delay(1); // Placeholder implementation
        return symbolTable;
    }
}
