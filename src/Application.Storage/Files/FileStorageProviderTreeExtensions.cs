// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using BridgingIT.DevKit.Common;
using Humanizer;

public static class FileStorageProviderTreeExtensions
{
    [DebuggerDisplay("Path={Path}")]
    private class DirectoryNode
    {
        public string Path { get; set; }
        public List<string> Files { get; set; } = new List<string>();
        public List<DirectoryNode> Subdirectories { get; set; } = new List<DirectoryNode>();
        public long TotalSize { get; set; }
        public int FileCount { get; set; }
    }

    public static async Task<Result<string>> RenderDirectoryAsync(
        this IFileStorageProvider provider,
        IFileStorageTreeRenderer renderer = null,
        string path = null,
        bool skipFiles = false,
        IProgress<FileProgress> progress = null,
        CancellationToken cancellationToken = default)
    {
        renderer ??= new TextFileStorageTreeRenderer();

        if (provider == null)
        {
            return Result<string>.Failure()
                .WithError(new ArgumentError("Provider cannot be null"))
                .WithMessage("Invalid provider provided for render");
        }

        if (renderer == null)
        {
            return Result<string>.Failure()
                .WithError(new ArgumentError("Renderer cannot be null"))
                .WithMessage("Invalid renderer provided for render");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result<string>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage("Cancelled rendering storage provider structure");
        }

        try
        {
            // Build the directory tree starting from the specified path (or root if null)
            var rootPath = string.IsNullOrEmpty(path) ? string.Empty : path;
            var rootNode = new DirectoryNode { Path = rootPath };
            await BuildDirectoryTree(provider, rootNode, skipFiles, progress, cancellationToken);

            // Convert the DirectoryNode tree to a TreeNode tree for rendering
            var treeNode = ConvertToTreeNode(provider, rootNode, skipFiles);
            RenderTreeNode(treeNode, renderer, level: 0, skipFiles);

            // Add global totals (if not skipping files)
            if (!skipFiles)
            {
                renderer.RenderTotals(rootNode.FileCount, rootNode.TotalSize);
            }

            return Result<string>.Success(renderer.ToString())
                .WithMessage($"Rendered storage provider structure starting at '{rootPath}'");
        }
        catch (OperationCanceledException)
        {
            return Result<string>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during render"))
                .WithMessage("Cancelled rendering storage provider structure");
        }
        catch (Exception ex)
        {
            return Result<string>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error rendering storage provider structure starting at '{path}'");
        }
    }

    private static async Task BuildDirectoryTree(
        IFileStorageProvider provider,
        DirectoryNode node,
        bool skipFiles,
        IProgress<FileProgress> progress,
        CancellationToken cancellationToken)
    {
        //if (cancellationToken.IsCancellationRequested)
        //{
        //    throw new OperationCanceledException("Operation cancelled during directory tree building");
        //}

        // List directories at this level
        var dirListResult = await provider.ListDirectoriesAsync(node.Path, null, false, cancellationToken);
        if (dirListResult.IsSuccess)
        {
            foreach (var dir in dirListResult.Value.OrderBy(d => d))
            {
                var subNode = new DirectoryNode { Path = dir };
                node.Subdirectories.Add(subNode);
                await BuildDirectoryTree(provider, subNode, skipFiles, progress, cancellationToken);
                node.TotalSize += subNode.TotalSize;
                node.FileCount += subNode.FileCount;
            }
        }

        // List files at this level (if not skipping files)
        if (!skipFiles)
        {
            var fileListResult = await provider.ListFilesAsync(node.Path, "*.*", false, null, cancellationToken);
            if (fileListResult.IsSuccess)
            {
                node.Files.AddRange(fileListResult.Value.Files.OrderBy(f => f));
                foreach (var file in node.Files)
                {
                    var metadataResult = await provider.GetFileMetadataAsync(file, cancellationToken);
                    if (metadataResult.IsSuccess)
                    {
                        node.TotalSize += metadataResult.Value.Length;
                        node.FileCount++;
                    }
                }

                // Report progress
                progress?.Report(new FileProgress
                {
                    BytesProcessed = node.TotalSize,
                    FilesProcessed = node.FileCount,
                    TotalFiles = -1 // Unknown total until the end
                });
            }
        }
    }

    private static TreeNode ConvertToTreeNode(IFileStorageProvider provider, DirectoryNode node, bool skipFiles)
    {
        var treeNode = new TreeNode
        {
            Name = string.IsNullOrEmpty(node.Path) ? "/" : Path.GetFileName(node.Path),
            IsDirectory = true,
            FileCount = node.FileCount,
            TotalSize = node.TotalSize
        };

        if (!skipFiles)
        {
            foreach (var file in node.Files)
            {
                var metadataResult = Task.Run(() => provider.GetFileMetadataAsync(file)).Result;
                if (metadataResult.IsSuccess)
                {
                    treeNode.Children.Add(new TreeNode
                    {
                        Name = Path.GetFileName(file),
                        IsDirectory = false,
                        Size = metadataResult.Value.Length,
                        LastModified = metadataResult.Value.LastModified
                    });
                }
            }
        }

        foreach (var subDir in node.Subdirectories)
        {
            var subTreeNode = ConvertToTreeNode(provider, subDir, skipFiles);
            treeNode.Children.Add(subTreeNode);
        }

        // Set IsLast for each child
        for (var i = 0; i < treeNode.Children.Count; i++)
        {
            treeNode.Children[i].IsLast = i == treeNode.Children.Count - 1;
        }

        return treeNode;
    }

    private static void RenderTreeNode(TreeNode node, IFileStorageTreeRenderer renderer, int level, bool skipFiles)
    {
        renderer.RenderNode(node, level);

        foreach (var child in node.Children)
        {
            RenderTreeNode(child, renderer, level + 1, skipFiles);
        }
    }
}

public class TreeNode
{
    public string Name { get; set; }
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public DateTimeOffset? LastModified { get; set; }
    public int FileCount { get; set; }
    public long TotalSize { get; set; }
    public List<TreeNode> Children { get; set; } = new List<TreeNode>();
    public bool IsLast { get; set; }
    public string Prefix { get; set; } // For tracking indentation levels
}

public interface IFileStorageTreeRenderer
{
    void RenderNode(TreeNode node, int level);
    void RenderTotals(int totalFiles, long totalSize);
    string ToString();
}

public class TextFileStorageTreeRenderer : IFileStorageTreeRenderer
{
    private readonly StringBuilder builder = new StringBuilder();

    public void RenderNode(TreeNode node, int level)
    {
        var prefix = node.Prefix + (node.IsLast ? "└── " : "├── ");
        var line = node.IsDirectory
            ? $"{prefix}{node.Name} ({node.FileCount} files, {FormatSize(node.TotalSize)})"
            : $"{prefix}{node.Name} ({FormatSize(node.Size)}, {node.LastModified?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A"})";
        this.builder.AppendLine(line);

        foreach (var child in node.Children)
        {
            child.Prefix = node.Prefix + (node.IsLast ? "    " : "│   ");
        }
    }

    public void RenderTotals(int totalFiles, long totalSize)
    {
        var line = $"Total: {totalFiles} files, {FormatSize(totalSize)}";
        this.builder.AppendLine(line);
    }

    public override string ToString()
    {
        return this.builder.ToString();
    }

    private static string FormatSize(long bytes)
    {
        return bytes.Bytes().ToString("#.##");
    }
}

public class HtmlFileStorageTreeRenderer : IFileStorageTreeRenderer
{
    private readonly StringBuilder builder = new StringBuilder();

    public HtmlFileStorageTreeRenderer()
    {
        this.builder.AppendLine("<ul>");
    }

    public void RenderNode(TreeNode node, int level)
    {
        var className = node.IsDirectory ? "directory" : "file";
        var content = node.IsDirectory
            ? $"{node.Name} ({node.FileCount} files, {FormatSize(node.TotalSize)})"
            : $"{node.Name} ({FormatSize(node.Size)}, {node.LastModified?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A"})";
        this.builder.AppendLine($"{new string(' ', level * 2)}<li class=\"{className}\">{content}");
        if (node.Children.Any())
        {
            this.builder.AppendLine($"{new string(' ', level * 2)}<ul>");
        }
    }

    public void RenderTotals(int totalFiles, long totalSize)
    {
        this.builder.AppendLine($"</ul>");
        this.builder.AppendLine($"<li class=\"totals\">Total: {totalFiles} files, {FormatSize(totalSize)}</li>");
        this.builder.AppendLine("</ul>");
    }

    public override string ToString()
    {
        return this.builder.ToString();
    }

    private static string FormatSize(long bytes)
    {
        return bytes.Bytes().ToString("#.##");
    }
}