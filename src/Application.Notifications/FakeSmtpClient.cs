// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Notifications;

using BridgingIT.DevKit.Common;
using MailKit;
using MailKit.Net.Proxy;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A fake implementation of <see cref="ISmtpClient"/> that logs actions
/// instead of performing real SMTP operations. Useful for testing.
/// Includes a LogKey in each log message.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="FakeSmtpClient"/> class.
/// </remarks>
/// <param name="logger">The logger instance to use for logging activities.</param>
#pragma warning disable SYSLIB0058 // Type or member is obsolete
public class FakeSmtpClient(ILogger<FakeSmtpClient> logger, FakeSmtpClientOptions options = null) : ISmtpClient
{
    private readonly ILogger<FakeSmtpClient> logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly FakeSmtpClientOptions options = options ?? new FakeSmtpClientOptions();
    private bool isConnected;
    private bool isAuthenticated;
    private string localDomain;
    private DeliveryStatusNotificationType deliveryStatusNotificationType;
    private TimeSpan timeout = TimeSpan.FromMinutes(2); // Default similar to MailKit.SmtpClient
    private bool checkCertificateRevocation;
    private IPEndPoint localEndPoint;
    private IProxyClient proxyClient;
    private SslProtocols sslProtocols = SslProtocols.None;
    private bool requireTls;
    private SslProtocols? sslProtocol;
    private CipherAlgorithmType? sslCipherAlgorithm;
    private int? sslCipherStrength;
    private HashAlgorithmType? sslHashAlgorithm;
    private int? sslHashStrength;
    private ExchangeAlgorithmType? sslKeyExchangeAlgorithm;
    private int? sslKeyExchangeStrength;
    private CipherSuitesPolicy sslCipherSuitesPolicy;

    // ISmtpClient specific properties
    /// <summary>
    /// Gets or sets the capabilities.
    /// </summary>
    public SmtpCapabilities Capabilities { get; private set; } = SmtpCapabilities.Authentication | SmtpCapabilities.BinaryMime | SmtpCapabilities.UTF8 | SmtpCapabilities.Size;

    /// <summary>
    /// Stores the local domain.
    /// </summary>
    public string LocalDomain
    {
        get => this.localDomain;
        set
        {
            this.logger.LogTrace("[{LogKey}] fakesmtpclient - Setting LocalDomain to: {LocalDomainValue}", Constants.LogKey, value);
            this.localDomain = value;
        }
    }

    /// <summary>
    /// Gets or sets the max size.
    /// </summary>
    public uint MaxSize { get; private set; } = checked((uint)ByteSize.Megabytes(50));

    /// <summary>
    /// Stores the require tls.
    /// </summary>
    public bool RequireTLS
    {
        get => this.requireTls;
        set
        {
            this.logger.LogTrace("[{LogKey}] fakesmtpclient - Setting RequireTLS to: {RequireTLSValue}", Constants.LogKey, value);
            this.requireTls = value;
        }
    }

    /// <summary>
    /// Stores the delivery status notification type.
    /// </summary>
    public DeliveryStatusNotificationType DeliveryStatusNotificationType
    {
        get => this.deliveryStatusNotificationType;
        set
        {
            this.logger.LogTrace("[{LogKey}] fakesmtpclient - Setting DeliveryStatusNotificationType to: {DeliveryStatusNotificationTypeValue}", Constants.LogKey, value);
            this.deliveryStatusNotificationType = value;
        }
    }

    // IMailService properties
    /// <summary>
    /// Gets the authentication mechanisms.
    /// </summary>
    public HashSet<string> AuthenticationMechanisms { get; } = ["PLAIN", "LOGIN", "XOAUTH2"];
    /// <summary>
    /// Gets the is connected.
    /// </summary>
    public bool IsConnected => this.isConnected;
    /// <summary>
    /// Gets the is authenticated.
    /// </summary>
    public bool IsAuthenticated => this.isAuthenticated;
    /// <summary>
    /// Gets or sets the is secure.
    /// </summary>
    public bool IsSecure { get; private set; }
    /// <summary>
    /// Gets or sets the is encrypted.
    /// </summary>
    public bool IsEncrypted { get; private set; }
    /// <summary>
    /// Gets or sets the is signed.
    /// </summary>
    public bool IsSigned { get; private set; }

    /// <summary>
    /// Stores the ssl protocols.
    /// </summary>
    public SslProtocols SslProtocols
    {
        get => this.sslProtocols;
        set
        {
            this.logger.LogTrace("[{LogKey}] fakesmtpclient - Setting SslProtocols to: {SslProtocolsValue}", Constants.LogKey, value);
            this.sslProtocols = value;
        }
    }
    /// <summary>
    /// Gets or sets the cipher algorithm.
    /// </summary>
    public CipherAlgorithmType? CipherAlgorithm { get; private set; }
    /// <summary>
    /// Gets or sets the cipher strength.
    /// </summary>
    public int? CipherStrength { get; private set; }
    /// <summary>
    /// Gets or sets the tls cipher suite.
    /// </summary>
    public TlsCipherSuite? TlsCipherSuite { get; private set; }
    /// <summary>
    /// Gets the service name.
    /// </summary>
    public string ServiceName => "smtp";

    /// <summary>
    /// Stores the timeout.
    /// </summary>
    public TimeSpan Timeout
    {
        get => this.timeout;
        set
        {
            this.logger.LogTrace("[{LogKey}] fakesmtpclient - Setting Timeout to: {TimeoutValue}", Constants.LogKey, value);
            this.timeout = value;
        }
    }

    /// <summary>
    /// Gets or sets the client certificates.
    /// </summary>
    public X509CertificateCollection ClientCertificates { get; set; } = [];

    /// <summary>
    /// Gets or sets the server certificate validation callback.
    /// </summary>
    public RemoteCertificateValidationCallback ServerCertificateValidationCallback { get; set; }

    /// <summary>
    /// Gets the sync root.
    /// </summary>
    public object SyncRoot { get; } = new object();

    /// <summary>
    /// Stores the check certificate revocation.
    /// </summary>
    public bool CheckCertificateRevocation
    {
        get => this.checkCertificateRevocation;
        set
        {
            this.logger.LogTrace("[{LogKey}] fakesmtpclient - Setting CheckCertificateRevocation to: {CheckCertificateRevocationValue}", Constants.LogKey, value);
            this.checkCertificateRevocation = value;
        }
    }

    /// <summary>
    /// Stores the local end point.
    /// </summary>
    public IPEndPoint LocalEndPoint
    {
        get => this.localEndPoint;
        set
        {
            this.logger.LogTrace("[{LogKey}] fakesmtpclient - Setting LocalEndPoint to: {LocalEndPointValue}", Constants.LogKey, value?.ToString() ?? "null");
            this.localEndPoint = value;
        }
    }

    /// <summary>
    /// Stores the proxy client.
    /// </summary>
    public IProxyClient ProxyClient
    {
        get => this.proxyClient;
        set
        {
            this.logger.LogTrace("[{LogKey}] fakesmtpclient - Setting ProxyClient to: {ProxyClientType}", Constants.LogKey, value?.GetType().Name ?? "null");
            this.proxyClient = value;
        }
    }

    /// <summary>
    /// Stores the ssl cipher suites policy.
    /// </summary>
    public CipherSuitesPolicy SslCipherSuitesPolicy
    {
        get => this.sslCipherSuitesPolicy;
        set
        {
            this.logger.LogTrace("[{LogKey}] fakesmtpclient - Setting SslCipherSuitesPolicy.", Constants.LogKey);
            this.sslCipherSuitesPolicy = value;
        }
    }

    /// <summary>
    /// Stores the ssl cipher suite.
    /// </summary>
    public TlsCipherSuite? SslCipherSuite
    {
        get => this.TlsCipherSuite;
        // Not settable publicly; set internally in SimulateConnectionSecurityDetails or via test setup if you wish.
    }

    /// <summary>
    /// Stores the ssl protocol.
    /// </summary>
    public SslProtocols SslProtocol
    {
        get => this.sslProtocol ?? this.sslProtocols;
        // Not settable publicly; set internally in SimulateConnectionSecurityDetails or via test setup if you wish.
    }

    /// <summary>
    /// Stores the ssl cipher algorithm.
    /// </summary>
    public CipherAlgorithmType? SslCipherAlgorithm
    {
        get => this.sslCipherAlgorithm ?? this.CipherAlgorithm;
        // Not settable publicly; set internally in SimulateConnectionSecurityDetails or via test setup if you wish.
    }

    /// <summary>
    /// Stores the ssl cipher strength.
    /// </summary>
    public int? SslCipherStrength
    {
        get => this.sslCipherStrength ?? this.CipherStrength;
        // Not settable publicly; set internally in SimulateConnectionSecurityDetails or via test setup if you wish.
    }

    /// <summary>
    /// Stores the ssl hash algorithm.
    /// </summary>
    public HashAlgorithmType? SslHashAlgorithm
    {
        get => this.sslHashAlgorithm;
        // Not settable publicly; set internally in SimulateConnectionSecurityDetails or via test setup if you wish.
    }

    /// <summary>
    /// Stores the ssl hash strength.
    /// </summary>
    public int? SslHashStrength
    {
        get => this.sslHashStrength;
        // Not settable publicly; set internally in SimulateConnectionSecurityDetails or via test setup if you wish.
    }

    /// <summary>
    /// Stores the ssl key exchange algorithm.
    /// </summary>
    public ExchangeAlgorithmType? SslKeyExchangeAlgorithm
    {
        get => this.sslKeyExchangeAlgorithm;
        // Not settable publicly; set internally in SimulateConnectionSecurityDetails or via test setup if you wish.
    }

    /// <summary>
    /// Stores the ssl key exchange strength.
    /// </summary>
    public int? SslKeyExchangeStrength
    {
        get => this.sslKeyExchangeStrength;
        // Not settable publicly; set internally in SimulateConnectionSecurityDetails or via test setup if you wish.
    }

    int IMailService.Timeout
    {
        get => (int)this.Timeout.TotalMilliseconds;
        set => this.Timeout = TimeSpan.FromMilliseconds(value);
    }

    // Events
    /// <summary>
    /// Stores the connected.
    /// </summary>
    public event EventHandler<ConnectedEventArgs> Connected;
    /// <summary>
    /// Stores the disconnected.
    /// </summary>
    public event EventHandler<DisconnectedEventArgs> Disconnected;
    /// <summary>
    /// Stores the authenticated.
    /// </summary>
    public event EventHandler<AuthenticatedEventArgs> Authenticated;
    /// <summary>
    /// Stores the message sent.
    /// </summary>
    public event EventHandler<MessageSentEventArgs> MessageSent;

    // ISmtpClient specific methods
    /// <summary>
    /// Executes the expand operation.
    /// </summary>
    /// <param name="alias">The alias used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The result of the operation.</returns>
    public InternetAddressList Expand(string alias, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("[{LogKey}] fakesmtpclient - Expand called for alias: {Alias}", Constants.LogKey, alias);
        return [];
    }

    /// <summary>
    /// Executes the expand operation.
    /// </summary>
    /// <param name="alias">The alias used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task<InternetAddressList> ExpandAsync(string alias, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("[{LogKey}] fakesmtpclient - ExpandAsync called for alias: {Alias}", Constants.LogKey, alias);
        return Task.FromResult(new InternetAddressList());
    }

    /// <summary>
    /// Executes the verify operation.
    /// </summary>
    /// <param name="address">The address used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The result of the operation.</returns>
    public MailboxAddress Verify(string address, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("[{LogKey}] fakesmtpclient - Verify called for address: {Address}", Constants.LogKey, address);
        return null;
    }

    /// <summary>
    /// Executes the verify operation.
    /// </summary>
    /// <param name="address">The address used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task<MailboxAddress> VerifyAsync(string address, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("[{LogKey}] fakesmtpclient - VerifyAsync called for address: {Address}", Constants.LogKey, address);
        return Task.FromResult<MailboxAddress>(null);
    }

    // IMailService methods
    private void SimulateConnectionSecurityDetails(SecureSocketOptions options)
    {
        this.IsSecure = options != SecureSocketOptions.None;
        if (this.IsSecure)
        {
            this.IsEncrypted = true;
            if (this.SslProtocols == SslProtocols.None && options != SecureSocketOptions.StartTlsWhenAvailable && options != SecureSocketOptions.StartTls)
            {
                this.SslProtocols = SslProtocols.Tls12;
            }

            this.CipherAlgorithm = CipherAlgorithmType.Aes256;
            this.CipherStrength = 256;
            this.TlsCipherSuite = System.Net.Security.TlsCipherSuite.TLS_AES_256_GCM_SHA384;
            this.sslProtocol = this.SslProtocols;
            this.sslCipherAlgorithm = CipherAlgorithmType.Aes256;
            this.sslCipherStrength = 256;
            this.sslHashAlgorithm = HashAlgorithmType.Sha384;
            this.sslHashStrength = 384;
            this.sslKeyExchangeAlgorithm = ExchangeAlgorithmType.DiffieHellman;
            this.sslKeyExchangeStrength = 2048;
        }
        else
        {
            this.IsEncrypted = false;
            this.CipherAlgorithm = null;
            this.CipherStrength = null;
            this.TlsCipherSuite = null;
            this.sslProtocol = null;
            this.sslCipherAlgorithm = null;
            this.sslCipherStrength = null;
            this.sslHashAlgorithm = null;
            this.sslHashStrength = null;
            this.sslKeyExchangeAlgorithm = null;
            this.sslKeyExchangeStrength = null;
        }
    }

    /// <summary>
    /// Executes the connect operation.
    /// </summary>
    /// <param name="host">The host used by the operation.</param>
    /// <param name="port">The port used by the operation.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    public void Connect(string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("[{LogKey}] fakesmtpclient - Connecting to {Host}:{Port} with options {Options}.", Constants.LogKey, host, port, options);
        this.isConnected = true;
        this.SimulateConnectionSecurityDetails(options);
        this.Connected?.Invoke(this, new ConnectedEventArgs(host, port, options));
    }

    /// <summary>
    /// Executes the connect operation.
    /// </summary>
    /// <param name="host">The host used by the operation.</param>
    /// <param name="port">The port used by the operation.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task ConnectAsync(string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("[{LogKey}] fakesmtpclient - Connecting to {Host}:{Port} with options {Options}.", Constants.LogKey, host, port, options);
        this.isConnected = true;
        this.SimulateConnectionSecurityDetails(options);
        this.Connected?.Invoke(this, new ConnectedEventArgs(host, port, options));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes the connect operation.
    /// </summary>
    /// <param name="host">The host used by the operation.</param>
    /// <param name="port">The port used by the operation.</param>
    /// <param name="useSsl">The use ssl used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    public void Connect(string host, int port, bool useSsl, CancellationToken cancellationToken = default)
    {
        var options = useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.None;
        if (port == 0) port = useSsl ? 465 : 25;
        this.logger.LogDebug("[{LogKey}] fakesmtpclient - Connecting to {Host}:{Port} with useSsl: {UseSsl} (mapped to options: {Options}).", Constants.LogKey, host, port, useSsl, options);
        this.Connect(host, port, options, cancellationToken);
    }

    /// <summary>
    /// Executes the connect operation.
    /// </summary>
    /// <param name="host">The host used by the operation.</param>
    /// <param name="port">The port used by the operation.</param>
    /// <param name="useSsl">The use ssl used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task ConnectAsync(string host, int port, bool useSsl, CancellationToken cancellationToken = default)
    {
        var options = useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.None;
        if (port == 0) port = useSsl ? 465 : 25;
        this.logger.LogDebug("[{LogKey}] fakesmtpclient - Connecting to {Host}:{Port} with useSsl: {UseSsl} (mapped to options: {Options}).", Constants.LogKey, host, port, useSsl, options);
        return this.ConnectAsync(host, port, options, cancellationToken);
    }

    /// <summary>
    /// Executes the connect operation.
    /// </summary>
    /// <param name="socket">The socket used by the operation.</param>
    /// <param name="host">The host used by the operation.</param>
    /// <param name="port">The port used by the operation.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    public void Connect(Socket socket, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("[{LogKey}] fakesmtpclient - Connecting via socket to {Host}:{Port} with options {Options}. Socket connected: {IsSocketConnected}", Constants.LogKey, host, port, options, socket?.Connected);
        this.isConnected = true;
        this.SimulateConnectionSecurityDetails(options);
        this.Connected?.Invoke(this, new ConnectedEventArgs(host, port, options));
    }

    /// <summary>
    /// Executes the connect operation.
    /// </summary>
    /// <param name="socket">The socket used by the operation.</param>
    /// <param name="host">The host used by the operation.</param>
    /// <param name="port">The port used by the operation.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task ConnectAsync(Socket socket, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("[{LogKey}] fakesmtpclient - Connecting via socket to {Host}:{Port} with options {Options}. Socket connected: {IsSocketConnected}", Constants.LogKey, host, port, options, socket?.Connected);
        this.isConnected = true;
        this.SimulateConnectionSecurityDetails(options);
        this.Connected?.Invoke(this, new ConnectedEventArgs(host, port, options));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes the connect operation.
    /// </summary>
    /// <param name="stream">The stream used by the operation.</param>
    /// <param name="host">The host used by the operation.</param>
    /// <param name="port">The port used by the operation.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    public void Connect(Stream stream, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("[{LogKey}] fakesmtpclient - Connecting via stream to {Host}:{Port} with options {Options}. Stream type: {StreamType}", Constants.LogKey, host, port, options, stream?.GetType().Name);
        this.isConnected = true;
        this.SimulateConnectionSecurityDetails(options);
        this.Connected?.Invoke(this, new ConnectedEventArgs(host, port, options));
    }

    /// <summary>
    /// Executes the connect operation.
    /// </summary>
    /// <param name="stream">The stream used by the operation.</param>
    /// <param name="host">The host used by the operation.</param>
    /// <param name="port">The port used by the operation.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task ConnectAsync(Stream stream, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("[{LogKey}] fakesmtpclient - ConnectAsync via stream to {Host}:{Port} with options {Options}. Stream type: {StreamType}", Constants.LogKey, host, port, options, stream?.GetType().Name);
        this.isConnected = true;
        this.SimulateConnectionSecurityDetails(options);
        this.Connected?.Invoke(this, new ConnectedEventArgs(host, port, options));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes the authenticate operation.
    /// </summary>
    /// <param name="encoding">The encoding used by the operation.</param>
    /// <param name="userName">The user name used by the operation.</param>
    /// <param name="password">The password used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    public void Authenticate(Encoding encoding, string userName, string password, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("[{LogKey}] fakesmtpclient - Authenticating with credentials. Username: {Username}", Constants.LogKey, userName ?? "N/A");
        this.isAuthenticated = true;
        this.Authenticated?.Invoke(this, new AuthenticatedEventArgs("LOGIN"));
    }

    /// <summary>
    /// Executes the authenticate operation.
    /// </summary>
    /// <param name="encoding">The encoding used by the operation.</param>
    /// <param name="userName">The user name used by the operation.</param>
    /// <param name="password">The password used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task AuthenticateAsync(Encoding encoding, string userName, string password, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("[{LogKey}] fakesmtpclient - Authenticating with credentials. Username: {Username}", Constants.LogKey, userName ?? "N/A");
        this.isAuthenticated = true;
        this.Authenticated?.Invoke(this, new AuthenticatedEventArgs("LOGIN"));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes the authenticate operation.
    /// </summary>
    /// <param name="userName">The user name used by the operation.</param>
    /// <param name="password">The password used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    public void Authenticate(string userName, string password, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("[{LogKey}] fakesmtpclient - Authenticating with credentials. Username: {Username}", Constants.LogKey, userName ?? "N/A");
        this.isAuthenticated = true;
        this.Authenticated?.Invoke(this, new AuthenticatedEventArgs("LOGIN"));
    }

    /// <summary>
    /// Executes the authenticate operation.
    /// </summary>
    /// <param name="userName">The user name used by the operation.</param>
    /// <param name="password">The password used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task AuthenticateAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("[{LogKey}] fakesmtpclient - Authenticating with credentials. Username: {Username}", Constants.LogKey, userName ?? "N/A");
        this.isAuthenticated = true;
        this.Authenticated?.Invoke(this, new AuthenticatedEventArgs("LOGIN"));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes the authenticate operation.
    /// </summary>
    /// <param name="credentials">The credentials used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    public void Authenticate(ICredentials credentials, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("[{LogKey}] fakesmtpclient - Authenticating with credentials.", Constants.LogKey);
        this.isAuthenticated = true;
        this.Authenticated?.Invoke(this, new AuthenticatedEventArgs("LOGIN"));
    }
    /// <summary>
    /// Executes the authenticate operation.
    /// </summary>
    /// <param name="credentials">The credentials used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task AuthenticateAsync(ICredentials credentials, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("[{LogKey}] fakesmtpclient - Authenticating with credentials.", Constants.LogKey);
        this.isAuthenticated = true;
        this.Authenticated?.Invoke(this, new AuthenticatedEventArgs("LOGIN"));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes the authenticate operation.
    /// </summary>
    /// <param name="encoding">The encoding used by the operation.</param>
    /// <param name="credentials">The credentials used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    public void Authenticate(Encoding encoding, ICredentials credentials, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("[{LogKey}] fakesmtpclient - Authenticating with credentials.", Constants.LogKey);
        this.isAuthenticated = true;
        this.Authenticated?.Invoke(this, new AuthenticatedEventArgs("LOGIN"));
    }

    /// <summary>
    /// Executes the authenticate operation.
    /// </summary>
    /// <param name="encoding">The encoding used by the operation.</param>
    /// <param name="credentials">The credentials used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task AuthenticateAsync(Encoding encoding, ICredentials credentials, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("[{LogKey}] fakesmtpclient - Authenticating with credentials.", Constants.LogKey);
        this.isAuthenticated = true;
        this.Authenticated?.Invoke(this, new AuthenticatedEventArgs("LOGIN"));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes the authenticate operation.
    /// </summary>
    /// <param name="mechanism">The mechanism used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    public void Authenticate(SaslMechanism mechanism, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("[{LogKey}] fakesmtpclient - Authenticating with SASL mechanism: {MechanismName}", Constants.LogKey, mechanism?.MechanismName);
        this.isAuthenticated = true;
        this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(mechanism?.MechanismName ?? "SASL_UNKNOWN"));
    }

    /// <summary>
    /// Executes the authenticate operation.
    /// </summary>
    /// <param name="mechanism">The mechanism used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task AuthenticateAsync(SaslMechanism mechanism, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("[{LogKey}] fakesmtpclient - Authenticating with SASL mechanism: {MechanismName}", Constants.LogKey, mechanism?.MechanismName);
        this.isAuthenticated = true;
        this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(mechanism?.MechanismName ?? "SASL_UNKNOWN"));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes the disconnect operation.
    /// </summary>
    /// <param name="quit">The quit used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    public void Disconnect(bool quit, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("[{LogKey}] fakesmtpclient - Disconnecting (quit: {Quit}). WasConnected: {WasConnected}", Constants.LogKey, quit, this.isConnected);
        const string host = "mockhost";
        const int port = 25;
        const SecureSocketOptions options = SecureSocketOptions.None;
        this.isConnected = false;
        this.isAuthenticated = false;
        this.Disconnected?.Invoke(this, new DisconnectedEventArgs(host, port, options, quit));
    }

    /// <summary>
    /// Executes the disconnect operation.
    /// </summary>
    /// <param name="quit">The quit used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task DisconnectAsync(bool quit, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("[{LogKey}] fakesmtpclient - Disconnecting (quit: {Quit}). WasConnected: {WasConnected}", Constants.LogKey, quit, this.isConnected);
        const string host = "mockhost";
        const int port = 25;
        const SecureSocketOptions options = SecureSocketOptions.None;
        this.isConnected = false;
        this.isAuthenticated = false;
        this.Disconnected?.Invoke(this, new DisconnectedEventArgs(host, port, options, quit));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes the no op operation.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    public void NoOp(CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("[{LogKey}] fakesmtpclient - NoOp called.", Constants.LogKey);
    }

    /// <summary>
    /// Executes the no op operation.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task NoOpAsync(CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("[{LogKey}] fakesmtpclient - NoOpAsync called.", Constants.LogKey);
        return Task.CompletedTask;
    }

    // IMailTransport methods
    private void LogMessageDetails(MimeMessage message, MailboxAddress sender = null, IEnumerable<MailboxAddress> recipients = null)
    {
        var from = sender ?? message.From.Mailboxes.FirstOrDefault();
        var to = recipients ?? message.To.Mailboxes;
        var cc = message.Cc.Mailboxes;
        var bcc = message.Bcc.Mailboxes;

        var sb = new StringBuilder();
        sb.AppendLine("--- email message ---");
        sb.AppendLine($"  Message-ID: {message.MessageId}");
        sb.AppendLine($"  From: {from}");
        sb.AppendLine($"  To: {string.Join("; ", to.Select(r => r.ToString()))}");
        if (cc.Any()) sb.AppendLine($"  Cc: {string.Join("; ", cc.Select(r => r.ToString()))}");
        if (bcc.Any()) sb.AppendLine($"  Bcc: {string.Join("; ", bcc.Select(r => r.ToString()))}");
        sb.AppendLine($"  Subject: {message.Subject}");
        sb.AppendLine($"  Date: {message.Date}");
        sb.AppendLine($"  IsHTML: {!string.IsNullOrEmpty(message.HtmlBody)}");
        if (this.options.LogMessageBody)
        {
            sb.AppendLine($"  TextBody: {(message.TextBody?.Length > this.options.LogMessageBodyLength ? message.TextBody.Substring(0, this.options.LogMessageBodyLength) + "..." : message.TextBody)}");
            sb.AppendLine($"  HtmlBody: {(message.HtmlBody?.Length > this.options.LogMessageBodyLength ? message.HtmlBody.Substring(0, this.options.LogMessageBodyLength) + "..." : message.HtmlBody)}");
        }

        if (message.Attachments.SafeAny())
        {
            sb.AppendLine($"  Attachments ({message.Attachments.Count()}):");
            foreach (var attachment in message.Attachments.OfType<MimePart>())
            {
                sb.AppendLine($"    - {attachment.FileName ?? "N/A"} ({attachment.ContentType}, {attachment.Content?.Stream?.Length ?? 0} bytes)");
            }
        }
        else
        {
            sb.AppendLine("  Attachments: none");
        }

        this.logger.LogInformation(sb.ToString());
    }

    /// <summary>
    /// Executes the send operation.
    /// </summary>
    /// <param name="message">The message associated with the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <param name="progress">The progress used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public string Send(MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        this.logger.LogInformation("[{LogKey}] fakesmtpclient - Send mail message. Subject: {Subject}", Constants.LogKey, message.Subject);
        if (!this.isConnected) this.logger.LogWarning("[{LogKey}] fakesmtpclient - Attempted to send email while not connected.", Constants.LogKey);
        if (!this.isAuthenticated && this.AuthenticationMechanisms.Count != 0) this.logger.LogWarning("[{LogKey}] fakesmtpclient - Attempted to send email while not authenticated (and auth is available).", Constants.LogKey);

        this.LogMessageDetails(message);
        this.MessageSent?.Invoke(this, new MessageSentEventArgs(message, "250 2.0.0 OK: Logged (sync)"));
        return "250 2.0.0 OK: Logged (sync)";
    }

    /// <summary>
    /// Executes the send operation.
    /// </summary>
    /// <param name="message">The message associated with the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <param name="progress">The progress used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task<string> SendAsync(MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        this.logger.LogInformation("[{LogKey}] fakesmtpclient - SendAsync mail message. Subject: {Subject}", Constants.LogKey, message.Subject);
        if (!this.isConnected) this.logger.LogWarning("[{LogKey}] fakesmtpclient - Attempted to send email while not connected.", Constants.LogKey);

        this.LogMessageDetails(message);
        this.MessageSent?.Invoke(this, new MessageSentEventArgs(message, "250 2.0.0 OK: Logged (async)"));
        return Task.FromResult("250 2.0.0 OK: Logged (async)");
    }

    /// <summary>
    /// Executes the send operation.
    /// </summary>
    /// <param name="message">The message associated with the operation.</param>
    /// <param name="sender">The sender used by the operation.</param>
    /// <param name="recipients">The recipients used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <param name="progress">The progress used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public string Send(MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        this.logger.LogInformation("[{LogKey}] fakesmtpclient - Send mail message. Sender: {Sender}, Recipients: {Recipients}. Subject: {Subject}", Constants.LogKey, sender, string.Join(";", recipients), message.Subject);
        this.LogMessageDetails(message, sender, recipients);
        this.MessageSent?.Invoke(this, new MessageSentEventArgs(message, "250 2.0.0 OK: Logged (sync, with sender/recipients)"));
        return "250 2.0.0 OK: Logged (sync, with sender/recipients)";
    }

    /// <summary>
    /// Executes the send operation.
    /// </summary>
    /// <param name="message">The message associated with the operation.</param>
    /// <param name="sender">The sender used by the operation.</param>
    /// <param name="recipients">The recipients used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <param name="progress">The progress used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task<string> SendAsync(MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        this.logger.LogInformation("[{LogKey}] fakesmtpclient - SendAsync mail message. Sender: {Sender}, Recipients: {Recipients}. Subject: {Subject}", Constants.LogKey, sender, string.Join(";", recipients), message.Subject);
        this.LogMessageDetails(message, sender, recipients);
        this.MessageSent?.Invoke(this, new MessageSentEventArgs(message, "250 2.0.0 OK: Logged (async, with sender/recipients)"));
        return Task.FromResult("250 2.0.0 OK: Logged (async, with sender/recipients)");
    }

    /// <summary>
    /// Executes the send operation.
    /// </summary>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="message">The message associated with the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <param name="progress">The progress used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public string Send(FormatOptions options, MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        this.logger.LogInformation("[{LogKey}] fakesmtpclient - Send mail message. Options International: {International}. Subject: {Subject}", Constants.LogKey, options.International, message.Subject);
        this.LogMessageDetails(message);
        this.MessageSent?.Invoke(this, new MessageSentEventArgs(message, "250 2.0.0 OK: Logged (sync, with FormatOptions)"));
        return "250 2.0.0 OK: Logged (sync, with FormatOptions)";
    }

    /// <summary>
    /// Executes the send operation.
    /// </summary>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="message">The message associated with the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <param name="progress">The progress used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task<string> SendAsync(FormatOptions options, MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        this.logger.LogInformation("[{LogKey}] fakesmtpclient - SendAsync mail message. Options International: {International}. Subject: {Subject}", Constants.LogKey, options.International, message.Subject);
        this.LogMessageDetails(message);
        this.MessageSent?.Invoke(this, new MessageSentEventArgs(message, "250 2.0.0 OK: Logged (async, with FormatOptions)"));
        return Task.FromResult("250 2.0.0 OK: Logged (async, with FormatOptions)");
    }

    /// <summary>
    /// Executes the send operation.
    /// </summary>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="message">The message associated with the operation.</param>
    /// <param name="sender">The sender used by the operation.</param>
    /// <param name="recipients">The recipients used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <param name="progress">The progress used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public string Send(FormatOptions options, MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        this.logger.LogInformation("[{LogKey}] fakesmtpclient - Send mail message. Options International: {International}. Sender: {Sender}, Recipients: {Recipients}. Subject: {Subject}", Constants.LogKey, options.International, sender, string.Join(";", recipients), message.Subject);
        this.LogMessageDetails(message, sender, recipients);
        this.MessageSent?.Invoke(this, new MessageSentEventArgs(message, "250 2.0.0 OK: Logged (sync, with FormatOptions, sender/recipients)"));
        return "250 2.0.0 OK: Logged (sync, with FormatOptions, sender/recipients)";
    }

    /// <summary>
    /// Executes the send operation.
    /// </summary>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="message">The message associated with the operation.</param>
    /// <param name="sender">The sender used by the operation.</param>
    /// <param name="recipients">The recipients used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <param name="progress">The progress used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task<string> SendAsync(FormatOptions options, MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        this.logger.LogInformation("[{LogKey}] fakesmtpclient - SendAsync mail message. Options International: {International}. Sender: {Sender}, Recipients: {Recipients}. Subject: {Subject}", Constants.LogKey, options.International, sender, string.Join(";", recipients), message.Subject);
        this.LogMessageDetails(message, sender, recipients);
        this.MessageSent?.Invoke(this, new MessageSentEventArgs(message, "250 2.0.0 OK: Logged (async, with FormatOptions, sender/recipients)"));
        return Task.FromResult("250 2.0.0 OK: Logged (async, with FormatOptions, sender/recipients)");
    }

    /// <summary>
    /// Executes the dispose operation.
    /// </summary>
    public void Dispose()
    {
    }
}
#pragma warning restore SYSLIB0058 // Type or member is obsolete
