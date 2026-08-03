// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Owns a temporary file and deletes it when disposed.
/// </summary>
/// <example>
/// <code>
/// await using var temporary = TemporaryFileHelper.Create();
/// var path = temporary.Path;
/// </code>
/// </example>
public sealed class TemporaryFileLease : IDisposable, IAsyncDisposable
{
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemporaryFileLease" /> class.
    /// </summary>
    /// <param name="path">The owned file path.</param>
    /// <param name="stream">The owned file stream.</param>
    /// <example>
    /// <code>
    /// var lease = new TemporaryFileLease(path, stream);
    /// </code>
    /// </example>
    public TemporaryFileLease(string path, FileStream stream)
    {
        this.Path = path ?? throw new ArgumentNullException(nameof(path));
        this.Stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    /// <summary>
    /// Gets the temporary file path.
    /// </summary>
    /// <example>
    /// <code>
    /// var path = lease.Path;
    /// </code>
    /// </example>
    public string Path { get; }

    /// <summary>
    /// Gets the temporary file stream.
    /// </summary>
    /// <example>
    /// <code>
    /// await source.CopyToAsync(lease.Stream);
    /// </code>
    /// </example>
    public FileStream Stream { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        try
        {
            this.Stream.Dispose();
        }
        finally
        {
            DeleteIfExists(this.Path);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        try
        {
            await this.Stream.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            DeleteIfExists(this.Path);
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
