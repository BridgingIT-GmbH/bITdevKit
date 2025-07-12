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
    public SmtpCapabilities Capabilities { get; private set; } = SmtpCapabilities.Authentication | SmtpCapabilities.BinaryMime | SmtpCapabilities.UTF8 | SmtpCapabilities.Size;

    public string LocalDomain
    {
        get => this.localDomain;
        set
        {
            this.logger.LogTrace("{LogKey} fakesmtpclient - Setting LocalDomain to: {LocalDomainValue}", Constants.LogKey, value);
            this.localDomain = value;
        }
    }

    public uint MaxSize { get; private set; } = 50 * 1024 * 1024;

    public bool RequireTLS
    {
        get => this.requireTls;
        set
        {
            this.logger.LogTrace("{LogKey} fakesmtpclient - Setting RequireTLS to: {RequireTLSValue}", Constants.LogKey, value);
            this.requireTls = value;
        }
    }

    public DeliveryStatusNotificationType DeliveryStatusNotificationType
    {
        get => this.deliveryStatusNotificationType;
        set
        {
            this.logger.LogTrace("{LogKey} fakesmtpclient - Setting DeliveryStatusNotificationType to: {DeliveryStatusNotificationTypeValue}", Constants.LogKey, value);
            this.deliveryStatusNotificationType = value;
        }
    }

    // IMailService properties
    public HashSet<string> AuthenticationMechanisms { get; } = ["PLAIN", "LOGIN", "XOAUTH2"];
    public bool IsConnected => this.isConnected;
    public bool IsAuthenticated => this.isAuthenticated;
    public bool IsSecure { get; private set; }
    public bool IsEncrypted { get; private set; }
    public bool IsSigned { get; private set; }

    public SslProtocols SslProtocols
    {
        get => this.sslProtocols;
        set
        {
            this.logger.LogTrace("{LogKey} fakesmtpclient - Setting SslProtocols to: {SslProtocolsValue}", Constants.LogKey, value);
            this.sslProtocols = value;
        }
    }
    public CipherAlgorithmType? CipherAlgorithm { get; private set; }
    public int? CipherStrength { get; private set; }
    public TlsCipherSuite? TlsCipherSuite { get; private set; }
    public string ServiceName => "smtp";

    public TimeSpan Timeout
    {
        get => this.timeout;
        set
        {
            this.logger.LogTrace("{LogKey} fakesmtpclient - Setting Timeout to: {TimeoutValue}", Constants.LogKey, value);
            this.timeout = value;
        }
    }

    public X509CertificateCollection ClientCertificates { get; set; } = [];

    public RemoteCertificateValidationCallback ServerCertificateValidationCallback { get; set; }

    public object SyncRoot { get; } = new object();

    public bool CheckCertificateRevocation
    {
        get => this.checkCertificateRevocation;
        set
        {
            this.logger.LogTrace("{LogKey} fakesmtpclient - Setting CheckCertificateRevocation to: {CheckCertificateRevocationValue}", Constants.LogKey, value);
            this.checkCertificateRevocation = value;
        }
    }

    public IPEndPoint LocalEndPoint
    {
        get => this.localEndPoint;
        set
        {
            this.logger.LogTrace("{LogKey} fakesmtpclient - Setting LocalEndPoint to: {LocalEndPointValue}", Constants.LogKey, value?.ToString() ?? "null");
            this.localEndPoint = value;
        }
    }

    public IProxyClient ProxyClient
    {
        get => this.proxyClient;
        set
        {
            this.logger.LogTrace("{LogKey} fakesmtpclient - Setting ProxyClient to: {ProxyClientType}", Constants.LogKey, value?.GetType().Name ?? "null");
            this.proxyClient = value;
        }
    }

    public CipherSuitesPolicy SslCipherSuitesPolicy
    {
        get => this.sslCipherSuitesPolicy;
        set
        {
            this.logger.LogTrace("{LogKey} fakesmtpclient - Setting SslCipherSuitesPolicy.", Constants.LogKey);
            this.sslCipherSuitesPolicy = value;
        }
    }

    public TlsCipherSuite? SslCipherSuite
    {
        get => this.TlsCipherSuite;
        // Not settable publicly; set internally in SimulateConnectionSecurityDetails or via test setup if you wish.
    }

    public SslProtocols SslProtocol
    {
        get => this.sslProtocol ?? this.sslProtocols;
        // Not settable publicly; set internally in SimulateConnectionSecurityDetails or via test setup if you wish.
    }

    public CipherAlgorithmType? SslCipherAlgorithm
    {
        get => this.sslCipherAlgorithm ?? this.CipherAlgorithm;
        // Not settable publicly; set internally in SimulateConnectionSecurityDetails or via test setup if you wish.
    }

    public int? SslCipherStrength
    {
        get => this.sslCipherStrength ?? this.CipherStrength;
        // Not settable publicly; set internally in SimulateConnectionSecurityDetails or via test setup if you wish.
    }

    public HashAlgorithmType? SslHashAlgorithm
    {
        get => this.sslHashAlgorithm;
        // Not settable publicly; set internally in SimulateConnectionSecurityDetails or via test setup if you wish.
    }

    public int? SslHashStrength
    {
        get => this.sslHashStrength;
        // Not settable publicly; set internally in SimulateConnectionSecurityDetails or via test setup if you wish.
    }

    public ExchangeAlgorithmType? SslKeyExchangeAlgorithm
    {
        get => this.sslKeyExchangeAlgorithm;
        // Not settable publicly; set internally in SimulateConnectionSecurityDetails or via test setup if you wish.
    }

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
    public event EventHandler<ConnectedEventArgs> Connected;
    public event EventHandler<DisconnectedEventArgs> Disconnected;
    public event EventHandler<AuthenticatedEventArgs> Authenticated;
    public event EventHandler<MessageSentEventArgs> MessageSent;

    // ISmtpClient specific methods
    public InternetAddressList Expand(string alias, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("{LogKey} fakesmtpclient - Expand called for alias: {Alias}", Constants.LogKey, alias);
        return [];
    }

    public Task<InternetAddressList> ExpandAsync(string alias, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("{LogKey} fakesmtpclient - ExpandAsync called for alias: {Alias}", Constants.LogKey, alias);
        return Task.FromResult(new InternetAddressList());
    }

    public MailboxAddress Verify(string address, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("{LogKey} fakesmtpclient - Verify called for address: {Address}", Constants.LogKey, address);
        return null;
    }

    public Task<MailboxAddress> VerifyAsync(string address, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("{LogKey} fakesmtpclient - VerifyAsync called for address: {Address}", Constants.LogKey, address);
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

    public void Connect(string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("{LogKey} fakesmtpclient - Connecting to {Host}:{Port} with options {Options}.", Constants.LogKey, host, port, options);
        this.isConnected = true;
        this.SimulateConnectionSecurityDetails(options);
        this.Connected?.Invoke(this, new ConnectedEventArgs(host, port, options));
    }

    public Task ConnectAsync(string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("{LogKey} fakesmtpclient - Connecting to {Host}:{Port} with options {Options}.", Constants.LogKey, host, port, options);
        this.isConnected = true;
        this.SimulateConnectionSecurityDetails(options);
        this.Connected?.Invoke(this, new ConnectedEventArgs(host, port, options));
        return Task.CompletedTask;
    }

    public void Connect(string host, int port, bool useSsl, CancellationToken cancellationToken = default)
    {
        var options = useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.None;
        if (port == 0) port = useSsl ? 465 : 25;
        this.logger.LogDebug("{LogKey} fakesmtpclient - Connecting to {Host}:{Port} with useSsl: {UseSsl} (mapped to options: {Options}).", Constants.LogKey, host, port, useSsl, options);
        this.Connect(host, port, options, cancellationToken);
    }

    public Task ConnectAsync(string host, int port, bool useSsl, CancellationToken cancellationToken = default)
    {
        var options = useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.None;
        if (port == 0) port = useSsl ? 465 : 25;
        this.logger.LogDebug("{LogKey} fakesmtpclient - Connecting to {Host}:{Port} with useSsl: {UseSsl} (mapped to options: {Options}).", Constants.LogKey, host, port, useSsl, options);
        return this.ConnectAsync(host, port, options, cancellationToken);
    }

    public void Connect(Socket socket, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("{LogKey} fakesmtpclient - Connecting via socket to {Host}:{Port} with options {Options}. Socket connected: {IsSocketConnected}", Constants.LogKey, host, port, options, socket?.Connected);
        this.isConnected = true;
        this.SimulateConnectionSecurityDetails(options);
        this.Connected?.Invoke(this, new ConnectedEventArgs(host, port, options));
    }

    public Task ConnectAsync(Socket socket, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("{LogKey} fakesmtpclient - Connecting via socket to {Host}:{Port} with options {Options}. Socket connected: {IsSocketConnected}", Constants.LogKey, host, port, options, socket?.Connected);
        this.isConnected = true;
        this.SimulateConnectionSecurityDetails(options);
        this.Connected?.Invoke(this, new ConnectedEventArgs(host, port, options));
        return Task.CompletedTask;
    }

    public void Connect(Stream stream, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("{LogKey} fakesmtpclient - Connecting via stream to {Host}:{Port} with options {Options}. Stream type: {StreamType}", Constants.LogKey, host, port, options, stream?.GetType().Name);
        this.isConnected = true;
        this.SimulateConnectionSecurityDetails(options);
        this.Connected?.Invoke(this, new ConnectedEventArgs(host, port, options));
    }

    public Task ConnectAsync(Stream stream, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("{LogKey} fakesmtpclient - ConnectAsync via stream to {Host}:{Port} with options {Options}. Stream type: {StreamType}", Constants.LogKey, host, port, options, stream?.GetType().Name);
        this.isConnected = true;
        this.SimulateConnectionSecurityDetails(options);
        this.Connected?.Invoke(this, new ConnectedEventArgs(host, port, options));
        return Task.CompletedTask;
    }

    public void Authenticate(Encoding encoding, string userName, string password, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("{LogKey} fakesmtpclient - Authenticating with credentials. Username: {Username}", Constants.LogKey, userName ?? "N/A");
        this.isAuthenticated = true;
        this.Authenticated?.Invoke(this, new AuthenticatedEventArgs("LOGIN"));
    }

    public Task AuthenticateAsync(Encoding encoding, string userName, string password, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("{LogKey} fakesmtpclient - Authenticating with credentials. Username: {Username}", Constants.LogKey, userName ?? "N/A");
        this.isAuthenticated = true;
        this.Authenticated?.Invoke(this, new AuthenticatedEventArgs("LOGIN"));
        return Task.CompletedTask;
    }

    public void Authenticate(string userName, string password, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("{LogKey} fakesmtpclient - Authenticating with credentials. Username: {Username}", Constants.LogKey, userName ?? "N/A");
        this.isAuthenticated = true;
        this.Authenticated?.Invoke(this, new AuthenticatedEventArgs("LOGIN"));
    }

    public Task AuthenticateAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("{LogKey} fakesmtpclient - Authenticating with credentials. Username: {Username}", Constants.LogKey, userName ?? "N/A");
        this.isAuthenticated = true;
        this.Authenticated?.Invoke(this, new AuthenticatedEventArgs("LOGIN"));
        return Task.CompletedTask;
    }

    public void Authenticate(ICredentials credentials, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("{LogKey} fakesmtpclient - Authenticating with credentials.", Constants.LogKey);
        this.isAuthenticated = true;
        this.Authenticated?.Invoke(this, new AuthenticatedEventArgs("LOGIN"));
    }
    public Task AuthenticateAsync(ICredentials credentials, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("{LogKey} fakesmtpclient - Authenticating with credentials.", Constants.LogKey);
        this.isAuthenticated = true;
        this.Authenticated?.Invoke(this, new AuthenticatedEventArgs("LOGIN"));
        return Task.CompletedTask;
    }

    public void Authenticate(Encoding encoding, ICredentials credentials, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("{LogKey} fakesmtpclient - Authenticating with credentials.", Constants.LogKey);
        this.isAuthenticated = true;
        this.Authenticated?.Invoke(this, new AuthenticatedEventArgs("LOGIN"));
    }

    public Task AuthenticateAsync(Encoding encoding, ICredentials credentials, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("{LogKey} fakesmtpclient - Authenticating with credentials.", Constants.LogKey);
        this.isAuthenticated = true;
        this.Authenticated?.Invoke(this, new AuthenticatedEventArgs("LOGIN"));
        return Task.CompletedTask;
    }

    public void Authenticate(SaslMechanism mechanism, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("{LogKey} fakesmtpclient - Authenticating with SASL mechanism: {MechanismName}", Constants.LogKey, mechanism?.MechanismName);
        this.isAuthenticated = true;
        this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(mechanism?.MechanismName ?? "SASL_UNKNOWN"));
    }

    public Task AuthenticateAsync(SaslMechanism mechanism, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("{LogKey} fakesmtpclient - Authenticating with SASL mechanism: {MechanismName}", Constants.LogKey, mechanism?.MechanismName);
        this.isAuthenticated = true;
        this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(mechanism?.MechanismName ?? "SASL_UNKNOWN"));
        return Task.CompletedTask;
    }

    public void Disconnect(bool quit, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("{LogKey} fakesmtpclient - Disconnecting (quit: {Quit}). WasConnected: {WasConnected}", Constants.LogKey, quit, this.isConnected);
        const string host = "mockhost";
        const int port = 25;
        const SecureSocketOptions options = SecureSocketOptions.None;
        this.isConnected = false;
        this.isAuthenticated = false;
        this.Disconnected?.Invoke(this, new DisconnectedEventArgs(host, port, options, quit));
    }

    public Task DisconnectAsync(bool quit, CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("{LogKey} fakesmtpclient - Disconnecting (quit: {Quit}). WasConnected: {WasConnected}", Constants.LogKey, quit, this.isConnected);
        const string host = "mockhost";
        const int port = 25;
        const SecureSocketOptions options = SecureSocketOptions.None;
        this.isConnected = false;
        this.isAuthenticated = false;
        this.Disconnected?.Invoke(this, new DisconnectedEventArgs(host, port, options, quit));
        return Task.CompletedTask;
    }

    public void NoOp(CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("{LogKey} fakesmtpclient - NoOp called.", Constants.LogKey);
    }

    public Task NoOpAsync(CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("{LogKey} fakesmtpclient - NoOpAsync called.", Constants.LogKey);
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

    public string Send(MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        this.logger.LogInformation("{LogKey} fakesmtpclient - Send mail message. Subject: {Subject}", Constants.LogKey, message.Subject);
        if (!this.isConnected) this.logger.LogWarning("{LogKey} fakesmtpclient - Attempted to send email while not connected.", Constants.LogKey);
        if (!this.isAuthenticated && this.AuthenticationMechanisms.Count != 0) this.logger.LogWarning("{LogKey} fakesmtpclient - Attempted to send email while not authenticated (and auth is available).", Constants.LogKey);

        this.LogMessageDetails(message);
        this.MessageSent?.Invoke(this, new MessageSentEventArgs(message, "250 2.0.0 OK: Logged (sync)"));
        return "250 2.0.0 OK: Logged (sync)";
    }

    public Task<string> SendAsync(MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        this.logger.LogInformation("{LogKey} fakesmtpclient - SendAsync mail message. Subject: {Subject}", Constants.LogKey, message.Subject);
        if (!this.isConnected) this.logger.LogWarning("{LogKey} fakesmtpclient - Attempted to send email while not connected.", Constants.LogKey);

        this.LogMessageDetails(message);
        this.MessageSent?.Invoke(this, new MessageSentEventArgs(message, "250 2.0.0 OK: Logged (async)"));
        return Task.FromResult("250 2.0.0 OK: Logged (async)");
    }

    public string Send(MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        this.logger.LogInformation("{LogKey} fakesmtpclient - Send mail message. Sender: {Sender}, Recipients: {Recipients}. Subject: {Subject}", Constants.LogKey, sender, string.Join(";", recipients), message.Subject);
        this.LogMessageDetails(message, sender, recipients);
        this.MessageSent?.Invoke(this, new MessageSentEventArgs(message, "250 2.0.0 OK: Logged (sync, with sender/recipients)"));
        return "250 2.0.0 OK: Logged (sync, with sender/recipients)";
    }

    public Task<string> SendAsync(MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        this.logger.LogInformation("{LogKey} fakesmtpclient - SendAsync mail message. Sender: {Sender}, Recipients: {Recipients}. Subject: {Subject}", Constants.LogKey, sender, string.Join(";", recipients), message.Subject);
        this.LogMessageDetails(message, sender, recipients);
        this.MessageSent?.Invoke(this, new MessageSentEventArgs(message, "250 2.0.0 OK: Logged (async, with sender/recipients)"));
        return Task.FromResult("250 2.0.0 OK: Logged (async, with sender/recipients)");
    }

    public string Send(FormatOptions options, MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        this.logger.LogInformation("{LogKey} fakesmtpclient - Send mail message. Options International: {International}. Subject: {Subject}", Constants.LogKey, options.International, message.Subject);
        this.LogMessageDetails(message);
        this.MessageSent?.Invoke(this, new MessageSentEventArgs(message, "250 2.0.0 OK: Logged (sync, with FormatOptions)"));
        return "250 2.0.0 OK: Logged (sync, with FormatOptions)";
    }

    public Task<string> SendAsync(FormatOptions options, MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        this.logger.LogInformation("{LogKey} fakesmtpclient - SendAsync mail message. Options International: {International}. Subject: {Subject}", Constants.LogKey, options.International, message.Subject);
        this.LogMessageDetails(message);
        this.MessageSent?.Invoke(this, new MessageSentEventArgs(message, "250 2.0.0 OK: Logged (async, with FormatOptions)"));
        return Task.FromResult("250 2.0.0 OK: Logged (async, with FormatOptions)");
    }

    public string Send(FormatOptions options, MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        this.logger.LogInformation("{LogKey} fakesmtpclient - Send mail message. Options International: {International}. Sender: {Sender}, Recipients: {Recipients}. Subject: {Subject}", Constants.LogKey, options.International, sender, string.Join(";", recipients), message.Subject);
        this.LogMessageDetails(message, sender, recipients);
        this.MessageSent?.Invoke(this, new MessageSentEventArgs(message, "250 2.0.0 OK: Logged (sync, with FormatOptions, sender/recipients)"));
        return "250 2.0.0 OK: Logged (sync, with FormatOptions, sender/recipients)";
    }

    public Task<string> SendAsync(FormatOptions options, MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        this.logger.LogInformation("{LogKey} fakesmtpclient - SendAsync mail message. Options International: {International}. Sender: {Sender}, Recipients: {Recipients}. Subject: {Subject}", Constants.LogKey, options.International, sender, string.Join(";", recipients), message.Subject);
        this.LogMessageDetails(message, sender, recipients);
        this.MessageSent?.Invoke(this, new MessageSentEventArgs(message, "250 2.0.0 OK: Logged (async, with FormatOptions, sender/recipients)"));
        return Task.FromResult("250 2.0.0 OK: Logged (async, with FormatOptions, sender/recipients)");
    }

    public void Dispose()
    {
    }
}