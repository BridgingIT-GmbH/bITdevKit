namespace BridgingIT.DevKit.Application.Notifications;

public class SmtpSettings
{
    public string Host { get; set; }

    public int Port { get; set; }

    public string Username { get; set; }

    public string Password { get; set; }

    public bool UseSsl { get; set; }

    public string SenderName { get; set; }

    public string SenderAddress { get; set; }

    public SmtpSettings Credentials(string username, string password)
    {
        this.Username = username;
        this.Password = password;
        return this;
    }

    public SmtpSettings Sender(string name, string address)
    {
        this.SenderName = name;
        this.SenderAddress = address;
        return this;
    }
}
