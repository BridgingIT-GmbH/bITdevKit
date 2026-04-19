namespace BridgingIT.DevKit.Application.Notifications;

/// <summary>
/// Configures the SMTP connection and default sender identity used by <see cref="NotificationEmailService" />.
/// </summary>
/// <example>
/// <code>
/// var settings = new SmtpSettings()
///     .Credentials("smtp-user", "smtp-password")
///     .Sender("DoFiesta", "noreply@example.com");
/// </code>
/// </example>
public class SmtpSettings
{
    /// <summary>
    /// Gets or sets the SMTP server host name.
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// Gets or sets the SMTP server port.
    /// </summary>
    public int Port { get; set; } = 587;

    /// <summary>
    /// Gets or sets the SMTP user name.
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    /// Gets or sets the SMTP password.
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether SSL/TLS should be used when connecting.
    /// </summary>
    public bool UseSsl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether server certificate validation should be bypassed.
    /// </summary>
    public bool SkipServerCertificateValidation { get; set; }

    /// <summary>
    /// Gets or sets the default sender display name.
    /// </summary>
    public string SenderName { get; set; }

    /// <summary>
    /// Gets or sets the default sender email address.
    /// </summary>
    public string SenderAddress { get; set; }

    /// <summary>
    /// Sets the SMTP credentials.
    /// </summary>
    /// <param name="username">The SMTP user name.</param>
    /// <param name="password">The SMTP password.</param>
    /// <returns>The current settings instance.</returns>
    public SmtpSettings Credentials(string username, string password)
    {
        this.Username = username;
        this.Password = password;
        return this;
    }

    /// <summary>
    /// Sets the default sender identity used when a message does not override it.
    /// </summary>
    /// <param name="name">The sender display name.</param>
    /// <param name="address">The sender email address.</param>
    /// <returns>The current settings instance.</returns>
    public SmtpSettings Sender(string name, string address)
    {
        this.SenderName = name;
        this.SenderAddress = address;
        return this;
    }
}
