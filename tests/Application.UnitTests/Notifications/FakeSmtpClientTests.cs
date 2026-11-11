namespace BridgingIT.DevKit.Application.UnitTests.Notifications;

using BridgingIT.DevKit.Application.Notifications;
using MailKit;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;

public class FakeSmtpClientTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Send_Request_CompletesCallbacksAndRaisesMessageSent(bool asynchronous, bool formatOptions)
    {
        using var sut = new FakeSmtpClient(NullLogger<FakeSmtpClient>.Instance);
        using var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.com"));
        message.To.Add(MailboxAddress.Parse("recipient@example.com"));
        var recipient = MailboxAddress.Parse("envelope@example.com");
        var request = new RecordingSendRequest(message) { Sender = message.From.Mailboxes.First(), Recipients = [recipient] };
        MessageSentEventArgs sent = null;
        sut.MessageSent += (_, args) => sent = args;

        var result = asynchronous
            ? await (formatOptions ? sut.SendAsync(FormatOptions.Default, request) : sut.SendAsync(request))
            : formatOptions ? sut.Send(FormatOptions.Default, request) : sut.Send(request);

        result.ResponseText.ShouldStartWith("250");
        result.AcceptedRecipients.ShouldBe([recipient]);
        result.RejectedRecipients.ShouldBeEmpty();
        request.Callbacks.ShouldBe(["started", "completed"]);
        sent.ShouldNotBeNull();
        sent.Request.ShouldBeSameAs(request);
        sent.Result.ShouldBeSameAs(result);
        sut.Capabilities.Contains(SmtpCapability.Size).ShouldBeTrue();
        sut.MaxSize.ShouldBe(ByteSize.Megabytes(50));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Send_CanceledRequest_DoesNotInvokeCallbacks(bool asynchronous, bool formatOptions)
    {
        using var sut = new FakeSmtpClient(NullLogger<FakeSmtpClient>.Instance);
        using var message = new MimeMessage();
        var request = new RecordingSendRequest(message);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            if (asynchronous)
            {
                await (formatOptions ? sut.SendAsync(FormatOptions.Default, request, cancellation.Token) : sut.SendAsync(request, cancellation.Token));
            }
            else if (formatOptions)
            {
                sut.Send(FormatOptions.Default, request, cancellation.Token);
            }
            else
            {
                sut.Send(request, cancellation.Token);
            }
        });

        request.Callbacks.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_NoEnvelopeRecipients_UsesMessageRecipients()
    {
        await using var sut = new FakeSmtpClient(NullLogger<FakeSmtpClient>.Instance);
        using var message = new MimeMessage();
        message.To.Add(MailboxAddress.Parse("recipient@example.com"));
        message.Cc.Add(MailboxAddress.Parse("copy@example.com"));
        message.Bcc.Add(MailboxAddress.Parse("hidden@example.com"));

        var result = await sut.SendAsync(new SendRequest(message));

        result.AcceptedRecipients.Select(recipient => recipient.Address).ShouldBe(["recipient@example.com", "copy@example.com", "hidden@example.com"]);
    }

    private sealed class RecordingSendRequest(MimeMessage message) : SendRequest(message)
    {
        public List<string> Callbacks { get; } = [];

        public override void OnStarted(IMailTransport transport)
        {
            this.Callbacks.Add("started");
        }

        public override void OnCompleted(IMailTransport transport, SendResult result)
        {
            this.Callbacks.Add("completed");
        }
    }
}