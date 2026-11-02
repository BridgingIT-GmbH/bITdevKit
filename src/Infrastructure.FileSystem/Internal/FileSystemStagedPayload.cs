namespace BridgingIT.DevKit.Infrastructure.FileSystem;

/// <summary>Owns one staged immutable payload and its generation authority until publication or disposal.</summary>
/// <param name="directory">The absolute provider-owned record directory containing the payload.</param>
/// <param name="file">A <c>payload-{generation}.bin</c> filename created by this generation.</param>
/// <param name="length">The staged byte length.</param>
/// <param name="hash">The SHA-256 content hash prefixed with <c>sha256:</c>.</param>
/// <param name="authority">The live stage owner for this exact generation; ownership transfers to this instance.</param>
/// <remarks>Prefer <see cref="FileSystemStore.StageAsync"/>. Direct construction requires an existing payload and its matching generation authority. Dispose only once.</remarks>
/// <example><code>using var payload = await store.StageAsync(path, content, null, cancellationToken);</code></example>
public sealed class FileSystemStagedPayload(string directory, string file, long length, string hash, FileSystemAuthority authority) : IDisposable
{
    private bool preserve;

    /// <summary>Gets the immutable payload filename relative to its record directory.</summary>
    /// <example><code>var filename = payload.File;</code></example>
    public string File { get; } = file;

    /// <summary>Gets the staged byte length.</summary>
    /// <example><code>var bytes = payload.Length;</code></example>
    public long Length { get; } = length;

    /// <summary>Gets the SHA-256 content hash prefixed with <c>sha256:</c>.</summary>
    /// <example><code>var hash = payload.Hash;</code></example>
    public string Hash { get; } = hash;

    /// <summary>Preserves the payload on disposal once namespace publication may have started.</summary>
    /// <remarks>Called by publication before its uncertain boundary; abandoned preserved generations are collected later under ownership rules.</remarks>
    /// <example><code>payload.Preserve();</code></example>
    public void Preserve()
    {
        this.preserve = true;
    }

    /// <summary>Deletes an unpreserved payload when possible and releases its generation authority.</summary>
    /// <example><code>payload.Dispose();</code></example>
    public void Dispose()
    {
        if (!this.preserve)
        {
            try
            {
                System.IO.File.Delete(Path.Combine(directory, this.File));
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        authority.Dispose();

        try
        {
            if (!authority.IsShared)
            {
                System.IO.File.Delete(Path.Combine(directory, $"stage-{this.File[8..^4]}.owner"));
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}