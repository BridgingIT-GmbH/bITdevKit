// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Application.Notifications;
using BridgingIT.DevKit.Common;

/// <summary>Persists email notifications and exposes existing storage and operational outbox contracts.</summary>
/// <param name="database">The notification database and declared collections.</param>
/// <param name="outboxOptions">The existing notification worker settings.</param>
/// <param name="clock">The claim and retention clock.</param>
public sealed partial class LiteDbNotificationStorageProvider(LiteDbFeatureDatabase database, OutboxNotificationEmailOptions outboxOptions, TimeProvider clock)
    : INotificationStorageProvider, INotificationEmailOutboxService
{
    private const string LeaseProperty = "__NotificationOutboxLeaseOwner";
    private readonly LiteDbKeyedCollection<Guid, LiteDbNotificationRecord> messages = database.Collection<Guid, LiteDbNotificationRecord>("emails");

    /// <inheritdoc />
    public Task<Result> SaveAsync<TMessage>(TMessage message, CancellationToken cancellationToken) where TMessage : class, INotificationMessage => this.GuardAsync(() => database.ExecuteAsync(() =>
    {
        if (message is not EmailMessage email) { return Task.FromResult(Result.Failure(new InvalidInputError("Only email notifications are supported."))); }

        var row = new LiteDbNotificationRecord { Message = email, RetryLimit = outboxOptions.RetryCount };
        return Task.FromResult(this.messages.TryAdd(email.Id, row) ? Result.Success() : Result.Failure(new ConflictError("The notification identity already exists.")));
    }, cancellationToken));

    /// <inheritdoc />
    public Task<Result> UpdateAsync<TMessage>(TMessage message, CancellationToken cancellationToken) where TMessage : class, INotificationMessage => this.GuardAsync(() => database.ExecuteAsync(() =>
    {
        if (message is not EmailMessage email) { return Task.FromResult(Result.Failure(new InvalidInputError("Only email notifications are supported."))); }

        if (!this.messages.TryGetValue(email.Id, out var row)) { return Task.FromResult(Result.Failure(new NotFoundError())); }

        if (!this.Owns(row, email)) { return Task.FromResult(Result.Failure(new ConcurrencyError("The notification delivery lease is no longer owned."))); }

        email.Properties.Remove(LeaseProperty);
        row.Message = email; row.LeaseOwner = null; row.LockedUntil = null;
        return Task.FromResult(Result.Success());
    }, cancellationToken));

    /// <inheritdoc />
    public Task<Result> DeleteAsync<TMessage>(TMessage message, CancellationToken cancellationToken) where TMessage : class, INotificationMessage => this.GuardAsync(() => database.ExecuteAsync(() =>
    {
        if (message is not EmailMessage email) { return Task.FromResult(Result.Failure(new InvalidInputError("Only email notifications are supported."))); }

        if (!this.messages.TryGetValue(email.Id, out var row)) { return Task.FromResult(Result.Failure(new NotFoundError())); }

        if (!this.Owns(row, email)) { return Task.FromResult(Result.Failure(new ConcurrencyError("The notification delivery lease is no longer owned."))); }

        row.IsArchived = true; row.ArchivedDate = clock.GetUtcNow(); row.LeaseOwner = null; row.LockedUntil = null;
        return Task.FromResult(Result.Success());
    }, cancellationToken));

    /// <inheritdoc />
    public async Task<Result<IEnumerable<TMessage>>> GetPendingAsync<TMessage>(int batchSize, CancellationToken cancellationToken) where TMessage : class, INotificationMessage
    {
        if (typeof(TMessage) != typeof(EmailMessage)) { return Result<IEnumerable<TMessage>>.Failure(new InvalidInputError("Only email notifications are supported.")); }

        if (batchSize <= 0) { return Result<IEnumerable<TMessage>>.Success([]); }

        return await database.ExecuteAsync(() =>
        {
            var selected = database.Query<LiteDbNotificationRecord>("emails", BsonExpression.Create("$.Ready = true AND $.LeaseUntilTicks <= @0", clock.GetUtcNow().UtcTicks), Math.Min(batchSize, database.Options.MaxMaterializedRows));
            var result = new List<TMessage>();
            foreach (var row in selected)
            {
                result.Add((TMessage)(object)this.Claim(row).Message);
            }

            return Task.FromResult(Result<IEnumerable<TMessage>>.Success(result));
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Selects pending identities without reserving delivery leases for queued emails.</summary>
    /// <param name="batchSize">The maximum emails selected for this invocation.</param>
    /// <param name="preferredId">An optional pending identity to process first.</param>
    /// <param name="cancellationToken">Cancels native selection.</param>
    /// <returns>A bounded detached snapshot of eligible identities.</returns>
    public Task<IReadOnlyList<Guid>> GetPendingIdsAsync(int batchSize, Guid? preferredId = null, CancellationToken cancellationToken = default) => database.ExecuteAsync(() =>
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        var limit = Math.Min(batchSize, database.Options.MaxMaterializedRows);
        var now = clock.GetUtcNow().UtcTicks;
        var result = new List<Guid>();
        if (preferredId.HasValue)
        {
            var preferred = database.Query<LiteDbNotificationRecord>("emails", BsonExpression.Create(
                "$.Ready = true AND $.LeaseUntilTicks <= @0 AND $.MessageId = @1", now, preferredId.Value.ToString("D")), 1);
            result.AddRange(preferred.Select(row => row.Message.Id));
        }

        if (result.Count < limit)
        {
            var predicate = preferredId.HasValue
                ? BsonExpression.Create("$.Ready = true AND $.LeaseUntilTicks <= @0 AND $.MessageId != @1", now, preferredId.Value.ToString("D"))
                : BsonExpression.Create("$.Ready = true AND $.LeaseUntilTicks <= @0", now);
            result.AddRange(database.Query<LiteDbNotificationRecord>("emails", predicate, limit - result.Count).Select(row => row.Message.Id));
        }

        return Task.FromResult<IReadOnlyList<Guid>>(result);
    }, cancellationToken);

    /// <summary>Claims one still-eligible email immediately before its external delivery.</summary>
    /// <param name="id">The selected email identity.</param>
    /// <param name="cancellationToken">Cancels native acquisition.</param>
    /// <returns>The current claim, or null when another operation changed eligibility.</returns>
    public Task<LiteDbNotificationEmailClaim> TryClaimAsync(Guid id, CancellationToken cancellationToken = default) => database.ExecuteAsync(() =>
    {
        if (!this.messages.TryGetValue(id, out var row) || row.IsArchived || row.LockedUntil > clock.GetUtcNow() ||
            !(row.Message.Status is EmailMessageStatus.Pending or EmailMessageStatus.Locked || row.Message.Status == EmailMessageStatus.Failed && row.Message.RetryCount < row.RetryLimit))
        {
            return Task.FromResult<LiteDbNotificationEmailClaim>(null);
        }

        return Task.FromResult(this.Claim(row));
    }, cancellationToken);

    /// <summary>Extends an unexpired claim only while its original token still owns the email.</summary>
    /// <param name="claim">The immutable delivery ownership token.</param>
    /// <param name="cancellationToken">Cancels native renewal.</param>
    /// <returns>Whether this claim remains current.</returns>
    public Task<bool> RenewAsync(LiteDbNotificationEmailClaim claim, CancellationToken cancellationToken = default) => database.ExecuteAsync(() =>
    {
        ArgumentNullException.ThrowIfNull(claim);
        if (!this.messages.TryGetValue(claim.Message.Id, out var row) || row.IsArchived || row.LeaseOwner != claim.Token || row.LockedUntil <= clock.GetUtcNow())
        {
            return Task.FromResult(false);
        }

        row.LockedUntil = clock.GetUtcNow().Add(outboxOptions.LeaseDuration);
        return Task.FromResult(true);
    }, cancellationToken);

    private LiteDbNotificationEmailClaim Claim(LiteDbNotificationRecord row)
    {
        row.LeaseOwner = Guid.NewGuid().ToString("N");
        row.LockedUntil = clock.GetUtcNow().Add(outboxOptions.LeaseDuration);
        row.Message.Status = EmailMessageStatus.Locked;
        var copy = database.Options.Serializer.Deserialize<EmailMessage>(database.Options.Serializer.SerializeToString(row.Message));
        copy.Properties[LeaseProperty] = row.LeaseOwner;
        return new(copy, row.LeaseOwner);
    }

    private bool Owns(LiteDbNotificationRecord row, EmailMessage message)
    {
        var supplied = message.Properties.GetValueOrDefault(LeaseProperty)?.ToString();
        if (supplied is not null) { return row.LeaseOwner == supplied && row.LockedUntil > clock.GetUtcNow(); }

        return row.LeaseOwner is null;
    }

    private async Task<Result> GuardAsync(Func<Task<Result>> work)
    {
        try { return await work().ConfigureAwait(false); }
        catch (OperationCanceledException) { throw; }
        catch (LiteDbProviderException exception) { return Result.Failure().WithErrors(exception.Errors).WithMessages(exception.Messages); }
        catch (Exception exception) when (!exception.IsTransientException()) { return Result.Failure(new ExceptionError(exception)); }
    }
}
