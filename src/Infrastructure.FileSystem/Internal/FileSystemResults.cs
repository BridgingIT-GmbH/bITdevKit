namespace BridgingIT.DevKit.Infrastructure.FileSystem;

/// <summary>Classifies shared filesystem failures and maps blob or document operations to typed results while preserving cancellation.</summary>
/// <example><code>var result = await FileSystemResults.BlobAsync(() => provider.GetPropertiesAsync(key));</code></example>
public static class FileSystemResults
{
    /// <summary>Runs a blob action and maps known filesystem exceptions, including non-retryable uncertain mutations, to typed errors.</summary>
    /// <typeparam name="T">The successful result value type.</typeparam>
    /// <param name="action">The asynchronous operation to execute once.</param>
    /// <returns>The original result or a mapped failure. Unrecognized exceptions propagate.</returns>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    /// <example><code>var result = await FileSystemResults.BlobAsync(() => provider.GetPropertiesAsync(key));</code></example>
    public static async Task<Result<T>> BlobAsync<T>(Func<Task<Result<T>>> action)
    {
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (EncoderFallbackException)
        {
            return Result<T>.Failure(new BlobStoreValidationError("Logical identity contains malformed Unicode."));
        }
        catch (FileSystemIndeterminateException exception)
        {
            return Result<T>.Failure(new BlobStoreIndeterminateOperationError(exception.Operation, exception.MutationId));
        }
        catch (FileSystemPayloadSizeException exception)
        {
            return Result<T>.Failure(new BlobStoreSizeLimitExceededError(exception.Length, exception.Maximum));
        }
        catch (FileSystemMetadataLimitException exception)
        {
            return Result<T>.Failure(new BlobStoreQueryTooBroadError(exception.Message));
        }
        catch (FileSystemPublicationLimitException)
        {
            return Result<T>.Failure(new FileSystemStorageMetadataLimitError());
        }
        catch (FileSystemAuthorityTimeoutException exception)
        {
            return Result<T>.Failure(new BlobStoreTimeoutError("authority", exception.Timeout));
        }
        catch (TimeoutException)
        {
            return Result<T>.Failure(new BlobStoreTimeoutError("authority", TimeSpan.Zero));
        }
        catch (UnauthorizedAccessException)
        {
            return Result<T>.Failure(new FileSystemStoragePermissionError());
        }
        catch (FileSystemConfigurationException exception)
        {
            return Result<T>.Failure(new FileSystemStorageConfigurationError(exception.Message));
        }
        catch (Exception exception)when (exception is NotSupportedException or DllNotFoundException or EntryPointNotFoundException)
        {
            return Result<T>.Failure(new FileSystemStorageConfigurationError("Filesystem configuration or required platform primitive is unsupported."));
        }
        catch (Exception exception)when (exception is InvalidDataException or System.Text.Json.JsonException or FormatException)
        {
            return Result<T>.Failure(new FileSystemStorageCorruptionError("Managed filesystem metadata or content is corrupt."));
        }
        catch (IOException exception)
        {
            return Result<T>.Failure(FromException(exception));
        }
    }

    /// <summary>Runs a document action and maps known filesystem exceptions, including non-retryable uncertain mutations, to typed errors.</summary>
    /// <typeparam name="T">The successful result value type.</typeparam>
    /// <param name="action">The asynchronous operation to execute once.</param>
    /// <returns>The original result or a mapped failure. Unrecognized exceptions propagate.</returns>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    /// <example><code>var result = await FileSystemResults.DocumentAsync(action);</code></example>
    public static async Task<Result<T>> DocumentAsync<T>(Func<Task<Result<T>>> action)
    {
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (EncoderFallbackException)
        {
            return Result<T>.Failure(new ValidationError("Logical identity contains malformed Unicode."));
        }
        catch (FileSystemIndeterminateException exception)
        {
            return Result<T>.Failure(new DocumentStoreIndeterminateOperationError(exception.Operation, exception.MutationId));
        }
        catch (FileSystemMetadataLimitException exception)
        {
            return Result<T>.Failure(new DocumentStoreInvalidQueryError(exception.Message));
        }
        catch (FileSystemContinuationException exception)
        {
            return Result<T>.Failure(new DocumentStoreInvalidContinuationTokenError(exception.Message));
        }
        catch (FileSystemPublicationLimitException)
        {
            return Result<T>.Failure(new FileSystemStorageMetadataLimitError());
        }
        catch (FileSystemAuthorityTimeoutException)
        {
            return Result<T>.Failure(new DocumentStoreTimeoutError("Filesystem authority busy.") { IsTransient = true });
        }
        catch (TimeoutException)
        {
            return Result<T>.Failure(new DocumentStoreTimeoutError("Filesystem authority busy."));
        }
        catch (UnauthorizedAccessException)
        {
            return Result<T>.Failure(new FileSystemStoragePermissionError());
        }
        catch (FileSystemConfigurationException exception)
        {
            return Result<T>.Failure(new FileSystemStorageConfigurationError(exception.Message));
        }
        catch (Exception exception)when (exception is NotSupportedException or DllNotFoundException or EntryPointNotFoundException)
        {
            return Result<T>.Failure(new FileSystemStorageConfigurationError("Filesystem configuration or required platform primitive is unsupported."));
        }
        catch (Exception exception)when (exception is InvalidDataException or System.Text.Json.JsonException or FormatException)
        {
            return Result<T>.Failure(new FileSystemStorageCorruptionError("Managed filesystem metadata or content is corrupt."));
        }
        catch (IOException exception)
        {
            return Result<T>.Failure(FromException(exception));
        }
    }

    /// <summary>Maps an operating-system failure to the filesystem error vocabulary shared by all filesystem providers.</summary>
    /// <param name="exception">The failure to classify. Cancellation is propagated.</param>
    /// <returns>A permission, configuration, corruption, capacity, unavailable or unexpected-exception error.</returns>
    /// <exception cref="OperationCanceledException">The supplied failure is cancellation.</exception>
    /// <example><code>var failure = Result.Failure(FileSystemResults.FromException(exception));</code></example>
    public static IResultError FromException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            OperationCanceledException => throw exception,
            UnauthorizedAccessException => new FileSystemStoragePermissionError(),
            FileSystemConfigurationException => new FileSystemStorageConfigurationError(exception.Message),
            InvalidDataException => new FileSystemStorageCorruptionError(exception.Message),
            IOException io => IoError(io),
            _ => new ExceptionError(exception)
        };
    }

    private static IResultError IoError(IOException exception)
    {
        var code = exception is FileSystemNativeIOException native ? native.Code : exception.HResult & 0xffff;

        if (OperatingSystem.IsWindows() ? code == 5 : code is 1 or 13)
        {
            return new FileSystemStoragePermissionError();
        }

        if (OperatingSystem.IsWindows() ? code is 39 or 112 : code is 28 or 122)
        {
            return new FileSystemStorageCapacityError();
        }

        return new FileSystemStorageUnavailableError(exception is FileSystemNativeIOException ? exception.Message : "Filesystem I/O failed or storage is unavailable.");
    }
}