// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

// COM-free: compiled into the licensed harness AND linked into the offline read-back tests.
namespace SAM.Analytical.Tas.DirectT3D.Validation.ReadBack
{
    public sealed class SnapshotDifference
    {
        public string Path { get; set; }
        public string A { get; set; }
        public string B { get; set; }
    }

    public sealed class SnapshotDiffResult
    {
        public string A { get; set; }
        public string B { get; set; }
        public string Purpose { get; set; }
        public List<string> Ignored { get; set; } = new List<string>();
        public int Compared { get; set; }
        public int DifferenceCount { get; set; }

        /// <summary>Differences grouped by path pattern (indices removed), so a whole-series change is one line.</summary>
        public Dictionary<string, int> ByPattern { get; set; } = new Dictionary<string, int>();
        public List<SnapshotDifference> Differences { get; set; } = new List<SnapshotDifference>();
    }

    /// <summary>
    /// Field-by-field difference of two native snapshots. Volatile identity fields TAS mints on every write (GUIDs) and the
    /// reader block (process, time, paths, hashes) are ignored by name and listed as such; everything else counts.
    /// Used for the reader-twice and export-twice reproducibility controls and for the informational Sizing=true diff.
    /// </summary>
    public static class SnapshotDiff
    {
        public static readonly string[] IgnoredProperties = { "Reader", "Guid", "BuildingElementGuid", "LinkZoneGuid", "LinkSurfaceGuid" };

        public static SnapshotDiffResult Compare(string pathA, string pathB, string purpose, int keep = 2000)
        {
            JsonNode a = JsonNode.Parse(File.ReadAllText(pathA)), b = JsonNode.Parse(File.ReadAllText(pathB));
            SnapshotDiffResult result = new SnapshotDiffResult { A = pathA, B = pathB, Purpose = purpose, Ignored = IgnoredProperties.ToList() };
            Walk(a, b, "$", result, keep);
            return result;
        }

        private static void Walk(JsonNode a, JsonNode b, string path, SnapshotDiffResult result, int keep)
        {
            if (a is JsonObject oa && b is JsonObject ob)
            {
                foreach (string key in oa.Select(x => x.Key).Union(ob.Select(x => x.Key)).Distinct())
                {
                    if (IgnoredProperties.Contains(key)) continue;
                    oa.TryGetPropertyValue(key, out JsonNode va);
                    ob.TryGetPropertyValue(key, out JsonNode vb);
                    Walk(va, vb, path + "." + key, result, keep);
                }

                return;
            }

            if (a is JsonArray la && b is JsonArray lb)
            {
                for (int i = 0; i < System.Math.Max(la.Count, lb.Count); i++)
                {
                    Walk(i < la.Count ? la[i] : null, i < lb.Count ? lb[i] : null, path + "[" + i + "]", result, keep);
                }

                return;
            }

            result.Compared++;
            string sa = a?.ToJsonString(), sb = b?.ToJsonString();
            if (string.Equals(sa, sb, StringComparison.Ordinal))
            {
                return;
            }

            result.DifferenceCount++;
            string pattern = System.Text.RegularExpressions.Regex.Replace(path, @"\[\d+\]", "[*]");
            result.ByPattern.TryGetValue(pattern, out int count);
            result.ByPattern[pattern] = count + 1;
            if (result.Differences.Count < keep)
            {
                result.Differences.Add(new SnapshotDifference { Path = path, A = sa ?? "(absent)", B = sb ?? "(absent)" });
            }
        }
    }
}
