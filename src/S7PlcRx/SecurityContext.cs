// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx;

/// <summary>
/// Security context for encrypted communication.
/// </summary>
public sealed class SecurityContext
{
    /// <summary>Gets or sets the PLC key identifier.</summary>
    public string PLCKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the encryption key.</summary>
    public string EncryptionKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the session start time.</summary>
    public DateTime SessionStartTime { get; set; }

    /// <summary>Gets or sets the session timeout.</summary>
    public TimeSpan SessionTimeout { get; set; }

    /// <summary>Gets or sets a value indicating whether security is enabled.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Gets a value indicating whether the session is still valid.</summary>
    public bool IsSessionValid => IsEnabled && DateTime.UtcNow - SessionStartTime < SessionTimeout;
}
