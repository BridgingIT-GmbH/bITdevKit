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
public class FakeSmtpClient : ISmtpClient
{
    private readonly ILogger<FakeSmtpClient> logger;
    private readonly FakeSmtpClientOptions options;
    private bool isConnected;
    private bool isAuthenticated;
    private string localDomain;
    private DeliveryStatusNotificationType _deliveryStatusNotificationType;
    private TimeSpan timeout = TimeSpan.FromMinutes(2); // Default similar to MailKit.SmtpClient
    private bool _checkCertificateRevocation;
    private IPEndPoint localEndPoint;
    private IProxyClient proxyClient;
    private SslProtocols sslProtocols = SslProtocols.None;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeSmtpClient"/> class.
    /// </summary>
    /// <param name="logger">The logger instance to use for logging activities.</param>
    public FakeSmtpClient(ILogger<FakeSmtpClient> logger, FakeSmtpClientOptions options = null)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.options = options ?? new FakeSmtpClientOptions();
        this.AuthenticationMechanisms = ["PLAIN", "LOGIN", "XOAUTH2"]; // Mock common mechanisms
        this.Capabilities = SmtpCapabilities.Authentication | SmtpCapabilities.BinaryMime | SmtpCapabilities.UTF8 | SmtpCapabilities.Size; // Mock capabilities
        this.ClientCertificates = [];
        this.logger.LogInformation("{LogKey} FakeSmtpClient initialized.", Constants.LogKey);
    }

    // ISmtpClient specific properties
    public SmtpCapabilities Capabilities { get; private set; }

    public string LocalDomain
    {
        get => this.localDomain;
        set
        {
            this.logger.LogTrace("{LogKey} Setting LocalDomain to: {LocalDomainValue}", Constants.LogKey, value);
            this.localDomain = value;
        }
    }

    public uint MaxSize { get; private set; } = 50 * 1024 * 1024; // Mock a max size (e.g., 50MB)

    public DeliveryStatusNotificationType DeliveryStatusNotificationType
    {
        get => this._deliveryStatusNotificationType;
        set
        {
            this.logger.LogTrace("{LogKey} Setting DeliveryStatusNotificationType to: {DeliveryStatusNotificationTypeValue}", Constants.LogKey, value);
            this._deliveryStatusNotificationType = value;
        }
    }

    // IMailService properties
    //public IAuthenticationSecretDetector AuthenticationSecretDetector { get; set; }
    public HashSet<string> AuthenticationMechanisms { get; }
    public bool IsConnected => this.isConnected;
    public bool IsAuthenticated => this.isAuthenticated;
    public bool IsSecure { get; private set; }
    public bool IsEncrypted { get; private set; } // Calculated based on IsSecure and SslProtocol
    public bool IsSigned { get; private set; } // Typically false for SMTP, true if DKIM signed (but client doesn't sign)

    public SslProtocols SslProtocols // Corrected name and added setter
    {
        get => this.sslProtocols;
        set
        {
            this.logger.LogTrace("{LogKey} Setting SslProtocols to: {SslProtocolsValue}", Constants.LogKey, value);
            this.sslProtocols = value;
        }
    }
    public CipherAlgorithmType? CipherAlgorithm { get; private set; }
    public int? CipherStrength { get; private set; }
    public TlsCipherSuite? TlsCipherSuite { get; private set; }
    public string ServiceName => "smtp";

    public TimeSpan Timeout // MailKit uses TimeSpan, not int
    {
        get => this.timeout;
        set
        {
            this.logger.LogTrace("{LogKey} Setting Timeout to: {TimeoutValue}", Constants.LogKey, value);
            this.timeout = value;
        }
    }

    public X509CertificateCollection ClientCertificates { get; set; }

    public RemoteCertificateValidationCallback ServerCertificateValidationCallback { get; set; }

    // New Properties from IMailService
    public object SyncRoot { get; } = new object();

    public bool CheckCertificateRevocation
    {
        get => this._checkCertificateRevocation;
        set
        {
            this.logger.LogTrace("{LogKey} Setting CheckCertificateRevocation to: {CheckCertificateRevocationValue}", Constants.LogKey, value);
            this._checkCertificateRevocation = value;
        }
    }

    public IPEndPoint LocalEndPoint
    {
        get => this.localEndPoint;
        set
        {
            this.logger.LogTrace("{LogKey} Setting LocalEndPoint to: {LocalEndPointValue}", Constants.LogKey, value?.ToString() ?? "null");
            this.localEndPoint = value;
        }
    }

    public IProxyClient ProxyClient
    {
        get => this.proxyClient;
        set
        {
            this.logger.LogTrace("{LogKey} Setting ProxyClient to: {ProxyClientType}", Constants.LogKey, value?.GetType().Name ?? "null");
            this.proxyClient = value;
        }
    }

    int IMailService.Timeout { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    // Events
    public event EventHandler<ConnectedEventArgs> Connected;
    public event EventHandler<DisconnectedEventArgs> Disconnected;
    public event EventHandler<AuthenticatedEventArgs> Authenticated;
    public event EventHandler<MessageSentEventArgs> MessageSent;

    // ISmtpClient specific methods
    public InternetAddressList Expand(string alias, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("{LogKey} Expand called for alias: {Alias}", Constants.LogKey, alias);
        // Simulate finding no expansion or a predefined one if needed for tests
        return [];
    }

    public Task<InternetAddressList> ExpandAsync(string alias, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("{LogKey} ExpandAsync called for alias: {Alias}", Constants.LogKey, alias);
        return Task.FromResult(new InternetAddressList());
    }

    public MailboxAddress Verify(string address, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("{LogKey} Verify called for address: {Address}", Constants.LogKey, address);
        // Simulate address not verifiable or a predefined one
        return null;
    }

    public Task<MailboxAddress> VerifyAsync(string address, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("{LogKey} VerifyAsync called for address: {Address}", Constants.LogKey, address);
        return Task.FromResult<MailboxAddress>(null);
    }

    // IMailService methods
    private void SimulateConnectionSecurityDetails(SecureSocketOptions options)
    {
        this.IsSecure = options != SecureSocketOptions.None;
        if (this.IsSecure)
        {
            this.IsEncrypted = true; // Assume encryption if secure
                                     // SslProtocols property is now settable, so we don't override it here unless specifically needed.
                                     // If SslProtocols is still None, we can default it.
            if (this.SslProtocols == SslProtocols.None && options != SecureSocketOptions.StartTlsWhenAvailable && options != SecureSocketOptions.StartTls)
            {
                this.SslProtocols = SslProtocols.Tls12; // Default for SslOnConnect or Auto leading to SSL
            }
            this.CipherAlgorithm = CipherAlgorithmType.Aes256; // Mock common algorithm
            this.CipherStrength = 256; // Mock strength
            //this.TlsCipherSuite = MailKit.Security.TlsCipherSuite.TlsAes256GcmSha384; // Mock suite
        }
        else
        {
            this.IsEncrypted = false;
            // SslProtocols = SslProtocols.None; // Don't reset if it was set explicitly
            this.CipherAlgorithm = null;
            this.CipherStrength = null;
            this.TlsCipherSuite = null;
        }
    }
    public void Connect(string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("{LogKey} Connecting to {Host}:{Port} with options {Options}.", Constants.LogKey, host, port, options);
        this.isConnected = true;
        this.SimulateConnectionSecurityDetails(options);
        Connected?.Invoke(this, new ConnectedEventArgs(host, port, options));
    }

    public Task ConnectAsync(string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("{LogKey} ConnectAsync to {Host}:{Port} with options {Options}.", Constants.LogKey, host, port, options);
        this.isConnected = true;
        this.SimulateConnectionSecurityDetails(options);
        Connected?.Invoke(this, new ConnectedEventArgs(host, port, options));
        return Task.CompletedTask;
    }

    // Connect overload with bool useSsl (from IMailService, used by user's code)
    public void Connect(string host, int port, bool useSsl, CancellationToken cancellationToken = default)
    {
        var options = useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.None;
        if (port == 0) port = useSsl ? 465 : 25; // Default ports
        this.logger.LogInformation("{LogKey} Connecting to {Host}:{Port} with useSsl: {UseSsl} (mapped to options: {Options}).", Constants.LogKey, host, port, useSsl, options);
        this.Connect(host, port, options, cancellationToken);
    }

    public Task ConnectAsync(string host, int port, bool useSsl, CancellationToken cancellationToken = default)
    {
        var options = useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.None;
        if (port == 0) port = useSsl ? 465 : 25; // Default ports
        this.logger.LogInformation("{LogKey} ConnectAsync to {Host}:{Port} with useSsl: {UseSsl} (mapped to options: {Options}).", Constants.LogKey, host, port, useSsl, options);
        return this.ConnectAsync(host, port, options, cancellationToken);
    }

    public void Connect(Socket socket, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("{LogKey} Connecting via socket to {Host}:{Port} with options {Options}. Socket connected: {IsSocketConnected}", Constants.LogKey, host, port, options, socket?.Connected);
        this.isConnected = true;
        this.SimulateConnectionSecurityDetails(options);
        Connected?.Invoke(this, new ConnectedEventArgs(host, port, options));
    }

    public Task ConnectAsync(Socket socket, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("{LogKey} ConnectAsync via socket to {Host}:{Port} with options {Options}. Socket connected: {IsSocketConnected}", Constants.LogKey, host, port, options, socket?.Connected);
        this.isConnected = true;
        this.SimulateConnectionSecurityDetails(options);
        Connected?.Invoke(this, new ConnectedEventArgs(host, port, options));
        return Task.CompletedTask;
    }

    public void Connect(Stream stream, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("{LogKey} Connecting via stream to {Host}:{Port} with options {Options}. Stream type: {StreamType}", Constants.LogKey, host, port, options, stream?.GetType().Name);
        this.isConnected = true;
        this.SimulateConnectionSecurityDetails(options);
        Connected?.Invoke(this, new ConnectedEventArgs(host, port, options));
    }

    public Task ConnectAsync(Stream stream, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("{LogKey} ConnectAsync via stream to {Host}:{Port} with options {Options}. Stream type: {StreamType}", Constants.LogKey, host, port, options, stream?.GetType().Name);
        this.isConnected = true;
        this.SimulateConnectionSecurityDetails(options);
        Connected?.Invoke(this, new ConnectedEventArgs(host, port, options));
        return Task.CompletedTask;
    }

    public void Authenticate(Encoding encoding, string userName, string password, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("{LogKey} Authenticating with credentials. Username: {Username}", Constants.LogKey, userName ?? "N/A");
        this.isAuthenticated = true;
        Authenticated?.Invoke(this, new AuthenticatedEventArgs("LOGIN")); // Mock mechanism
    }

    public Task AuthenticateAsync(Encoding encoding, string userName, string password, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("{LogKey} Authenticating with credentials. Username: {Username}", Constants.LogKey, userName ?? "N/A");
        this.isAuthenticated = true;
        Authenticated?.Invoke(this, new AuthenticatedEventArgs("LOGIN")); // Mock mechanism
        return Task.CompletedTask;
    }

    public void Authenticate(string userName, string password, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("{LogKey} Authenticating with credentials. Username: {Username}", Constants.LogKey, userName ?? "N/A");
        this.isAuthenticated = true;
        Authenticated?.Invoke(this, new AuthenticatedEventArgs("LOGIN")); // Mock mechanism
    }

    public Task AuthenticateAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("{LogKey} Authenticating with credentials. Username: {Username}", Constants.LogKey, userName ?? "N/A");
        this.isAuthenticated = true;
        Authenticated?.Invoke(this, new AuthenticatedEventArgs("LOGIN")); // Mock mechanism
        return Task.CompletedTask;
    }

    public void Authenticate(ICredentials credentials, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("{LogKey} Authenticating with credentials.", Constants.LogKey);
        this.isAuthenticated = true;
        Authenticated?.Invoke(this, new AuthenticatedEventArgs("LOGIN")); // Mock mechanism
    }
    public Task AuthenticateAsync(ICredentials credentials, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("{LogKey} Authenticating with credentials.", Constants.LogKey);
        this.isAuthenticated = true;
        return Task.CompletedTask;
    }

    public void Authenticate(Encoding encoding, ICredentials credentials, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("{LogKey} Authenticating with credentials.", Constants.LogKey);
        this.isAuthenticated = true;
        Authenticated?.Invoke(this, new AuthenticatedEventArgs("LOGIN"));
    }

    public Task AuthenticateAsync(Encoding encoding, ICredentials credentials, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("{LogKey} Authenticating with credentials.", Constants.LogKey);
        this.isAuthenticated = true;
        return Task.CompletedTask;
    }

    public void Authenticate(SaslMechanism mechanism, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("{LogKey} Authenticating with SASL mechanism: {MechanismName}", Constants.LogKey, mechanism?.MechanismName);
        this.isAuthenticated = true;
        Authenticated?.Invoke(this, new AuthenticatedEventArgs(mechanism?.MechanismName ?? "SASL_UNKNOWN"));
    }

    public Task AuthenticateAsync(SaslMechanism mechanism, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("{LogKey} AuthenticateAsync with SASL mechanism: {MechanismName}", Constants.LogKey, mechanism?.MechanismName);
        this.isAuthenticated = true;
        Authenticated?.Invoke(this, new AuthenticatedEventArgs(mechanism?.MechanismName ?? "SASL_UNKNOWN"));
        return Task.CompletedTask;
    }

    public void Disconnect(bool quit, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("{LogKey} Disconnecting (quit: {Quit}). WasConnected: {WasConnected}", Constants.LogKey, quit, this.isConnected);
        const string host = "mockhost"; // Could store these from Connect
        const int port = 25;
        const SecureSocketOptions options = SecureSocketOptions.None;
        this.isConnected = false;
        this.isAuthenticated = false;
        Disconnected?.Invoke(this, new DisconnectedEventArgs(host, port, options, quit));
    }

    public Task DisconnectAsync(bool quit, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("{LogKey} DisconnectAsync (quit: {Quit}). WasConnected: {WasConnected}", Constants.LogKey, quit, this.isConnected);
        const string host = "mockhost";
        const int port = 25;
        const SecureSocketOptions options = SecureSocketOptions.None;
        this.isConnected = false;
        this.isAuthenticated = false;
        Disconnected?.Invoke(this, new DisconnectedEventArgs(host, port, options, quit));
        return Task.CompletedTask;
    }

    public void NoOp(CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("{LogKey} NoOp called.", Constants.LogKey);
    }

    public Task NoOpAsync(CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("{LogKey} NoOpAsync called.", Constants.LogKey);
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
        sb.AppendLine("--- Email Message ---");
        sb.AppendLine($"  Message-ID: {message.MessageId}");
        sb.AppendLine($"  From: {from}");
        sb.AppendLine($"  To: {string.Join("; ", to.Select(r => r.ToString()))}");
        if (cc.Any()) sb.AppendLine($"  Cc: {string.Join("; ", cc.Select(r => r.ToString()))}");
        if (bcc.Any()) sb.AppendLine($"  Bcc: {string.Join("; ", bcc.Select(r => r.ToString()))}"); // Note: BCC usually not in headers for actual send
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
            sb.AppendLine($"  Attachments: none");
        }
        this.logger.LogInformation(sb.ToString()); // LogInformation does not need LogKey prepended here as it's already in sb
    }

    public void Send(MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        this.logger.LogInformation("{LogKey} Send(MimeMessage) called. Subject: {Subject}", Constants.LogKey, message.Subject);
        if (!this.isConnected) this.logger.LogWarning("{LogKey} Attempted to send email while not connected.", Constants.LogKey);
        if (!this.isAuthenticated && this.AuthenticationMechanisms.Count != 0) this.logger.LogWarning("{LogKey} Attempted to send email while not authenticated (and auth is available).", Constants.LogKey);

        this.LogMessageDetails(message);
        MessageSent?.Invoke(this, new MessageSentEventArgs(message, "250 2.0.0 OK: Logged (sync)"));
    }

    public Task SendAsync(MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        this.logger.LogInformation("{LogKey} SendAsync(MimeMessage) called. Subject: {Subject}", Constants.LogKey, message.Subject);
        if (!this.isConnected) this.logger.LogWarning("{LogKey} Attempted to send email while not connected.", Constants.LogKey);
        // Note: Some servers allow sending without auth to local recipients or if whitelisted.
        // if (!_isAuthenticated && AuthenticationMechanisms.Any()) _logger.LogWarning("{LogKey} Attempted to send email while not authenticated (and auth is available).", Constants.LogKey);

        this.LogMessageDetails(message);
        MessageSent?.Invoke(this, new MessageSentEventArgs(message, "250 2.0.0 OK: Logged (async)"));
        return Task.CompletedTask;
    }

    public void Send(FormatOptions options, MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        this.logger.LogInformation("{LogKey} Send(FormatOptions, MimeMessage) called. Options International: {International}. Subject: {Subject}", Constants.LogKey, options.International, message.Subject);
        this.LogMessageDetails(message);
        MessageSent?.Invoke(this, new MessageSentEventArgs(message, "250 2.0.0 OK: Logged (sync, with FormatOptions)"));
    }

    public Task SendAsync(FormatOptions options, MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        this.logger.LogInformation("{LogKey} SendAsync(FormatOptions, MimeMessage) called. Options International: {International}. Subject: {Subject}", Constants.LogKey, options.International, message.Subject);
        this.LogMessageDetails(message);
        MessageSent?.Invoke(this, new MessageSentEventArgs(message, "250 2.0.0 OK: Logged (async, with FormatOptions)"));
        return Task.CompletedTask;
    }

    public void Send(MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        this.logger.LogInformation("{LogKey} Send(MimeMessage, sender, recipients) called. Sender: {Sender}, Recipients: {Recipients}. Subject: {Subject}", Constants.LogKey, sender, string.Join(";", recipients), message.Subject);
        this.LogMessageDetails(message, sender, recipients);
        MessageSent?.Invoke(this, new MessageSentEventArgs(message, "250 2.0.0 OK: Logged (sync, with sender/recipients)"));
    }

    public Task SendAsync(MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        this.logger.LogInformation("{LogKey} SendAsync(MimeMessage, sender, recipients) called. Sender: {Sender}, Recipients: {Recipients}. Subject: {Subject}", Constants.LogKey, sender, string.Join(";", recipients), message.Subject);
        this.LogMessageDetails(message, sender, recipients);
        MessageSent?.Invoke(this, new MessageSentEventArgs(message, "250 2.0.0 OK: Logged (async, with sender/recipients)"));
        return Task.CompletedTask;
    }

    public void Send(FormatOptions options, MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        this.logger.LogInformation("{LogKey} Send(FormatOptions, MimeMessage, sender, recipients) called. Options International: {International}. Sender: {Sender}, Recipients: {Recipients}. Subject: {Subject}", Constants.LogKey, options.International, sender, string.Join(";", recipients), message.Subject);
        this.LogMessageDetails(message, sender, recipients);
        MessageSent?.Invoke(this, new MessageSentEventArgs(message, "250 2.0.0 OK: Logged (sync, with FormatOptions, sender/recipients)"));
    }

    public Task SendAsync(FormatOptions options, MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default, ITransferProgress progress = null)
    {
        this.logger.LogInformation("{LogKey} SendAsync(FormatOptions, MimeMessage, sender, recipients) called. Options International: {International}. Sender: {Sender}, Recipients: {Recipients}. Subject: {Subject}", Constants.LogKey, options.International, sender, string.Join(";", recipients), message.Subject);
        this.LogMessageDetails(message, sender, recipients);
        MessageSent?.Invoke(this, new MessageSentEventArgs(message, "250 2.0.0 OK: Logged (async, with FormatOptions, sender/recipients)"));
        return Task.CompletedTask;
    }

    // IDisposable
    public void Dispose()
    {
    }
}

public class FakeSmtpClientOptions
{
    public bool LogMessageBody { get; set; } = true;

    public int LogMessageBodyLength { get; set; } = 512;
}