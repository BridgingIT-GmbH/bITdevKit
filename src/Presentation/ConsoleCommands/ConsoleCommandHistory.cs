// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Manages persistent console command history storage and retrieval.
/// </summary>
public static class ConsoleCommandHistory
{
    private static readonly object sync = new();
    private static bool loaded;
    private static readonly List<string> items = [];
    private static string filePath;
    public static string LastError { get; private set; } = string.Empty;
    public static void Initialize(string assemblyName = null)
    {
        lock (sync)
        {
            if (loaded)
            {
                return;
            }

            var asmName = string.IsNullOrWhiteSpace(assemblyName) ? "unknown" : assemblyName;
            filePath = Path.Combine(Path.GetTempPath(), $"bitdevkit_cli_history_{asmName}.txt");
            if (File.Exists(filePath))
            {
                try { items.AddRange(File.ReadAllLines(filePath).Where(l => !string.IsNullOrWhiteSpace(l))); } catch (Exception ex) { LastError = ex.Message; }
            }
            loaded = true;
        }
    }
    public static void Append(string line)
    {
        lock (sync)
        {
            items.Add(line);
            try { File.AppendAllText(filePath, line + Environment.NewLine); }
            catch (Exception ex)
            {
                if (string.IsNullOrWhiteSpace(LastError))
                {
                    LastError = ex.Message;
                }
            }
        }
    }
    public static IReadOnlyList<string> GetAll() { lock (sync) { return items.ToList(); } }
    public static void ClearKeepLast(int keepLast)
    {
        lock (sync)
        {
            if (keepLast <= 0)
            {
                items.Clear();
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                }
                catch (Exception ex)
                {
                    if (string.IsNullOrWhiteSpace(LastError))
                    {
                        LastError = ex.Message;
                    }
                }
                return;
            }
            if (keepLast >= items.Count)
            {
                return;
            }

            var retained = items.Skip(Math.Max(0, items.Count - keepLast)).ToList();
            items.Clear(); items.AddRange(retained);
            try { File.WriteAllLines(filePath, retained); }
            catch (Exception ex)
            {
                if (string.IsNullOrWhiteSpace(LastError))
                {
                    LastError = ex.Message;
                }
            }
        }
    }
}
