// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Enterprise;

/// <summary>
/// Represents the security context for a session, including encryption settings, session timing, and certificate
/// information.
/// </summary>
/// <remarks>The SecurityContext class encapsulates all security-related parameters required to manage and
/// validate a secure session. It provides properties for encryption keys, session validity, and certificate details,
/// allowing consumers to configure and query the security state of a session. This class is sealed and cannot be
/// inherited.</remarks>
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

    /// <summary>
    /// Gets a value indicating whether [enable encryption].
    /// </summary>
    /// <value>
    ///   <c>true</c> if [enable encryption]; otherwise, <c>false</c>.
    /// </value>
    public bool EnableEncryption { get; internal set; }

    /// <summary>
    /// Gets the certificate path.
    /// </summary>
    /// <value>
    /// The certificate path.
    /// </value>
    public string? CertificatePath { get; internal set; }

    /// <summary>
    /// Gets the certificate password.
    /// </summary>
    /// <value>
    /// The certificate password.
    /// </value>
    public string? CertificatePassword { get; internal set; }
}
