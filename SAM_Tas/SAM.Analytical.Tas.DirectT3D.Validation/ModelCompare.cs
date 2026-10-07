// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SAM.Analytical.Tas.DirectT3D.Validation
{
    /// <summary>
    /// Compares the SAM models two routes hand back (the .result.sam each workflow run writes): the stamps and
    /// parameters on spaces, panels and apertures, the data downstream code reads. GUIDs that TAS mints (zone, surface,
    /// building element) differ between any two runs, so every GUID-shaped value is compared as "present", not by value -
    /// what matters is that the same things are stamped, not that TAS drew the same random numbers.
    /// </summary>
    public static class ModelCompare
    {
        private static readonly Regex regex_Guid = new Regex(@"^\{?[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\}?$", RegexOptions.Compiled);

        public static int Run(string[] args)
        {
            // models <gbxml.result.sam> <direct.result.sam> <outPrefix>
            AnalyticalModel a = SAM.Core.Convert.ToSAM<AnalyticalModel>(args[1])?.FirstOrDefault();
            AnalyticalModel b = SAM.Core.Convert.ToSAM<AnalyticalModel>(args[2])?.FirstOrDefault();
            if (a == null || b == null)
            {
                Console.WriteLine("cannot read the models");
                return 3;
            }

            StringBuilder sb = new StringBuilder();
            int differences = 0;

            differences += CompareGroup(sb, "spaces", Spaces(a), Spaces(b));
            differences += CompareGroup(sb, "panels", Panels(a), Panels(b));
            differences += CompareGroup(sb, "apertures", Apertures(a), Apertures(b));
            differences += CompareGroup(sb, "constructions", Constructions(a), Constructions(b));
            differences += CompareResults(sb, Results(a), Results(b));

            sb.AppendLine();
            sb.AppendLine("differences: " + differences);
            File.WriteAllText(args[3] + ".models.txt", sb.ToString());
            Console.Write(sb.ToString());
            return 0;
        }

        private static Dictionary<string, JsonObject> Spaces(AnalyticalModel model)
        {
            return model.AdjacencyCluster.GetSpaces().ToDictionary(x => x.Name, x => x.ToJsonObject());
        }

        private static Dictionary<string, JsonObject> Constructions(AnalyticalModel model)
        {
            return (model.AdjacencyCluster.GetConstructions() ?? new List<Construction>()).ToDictionary(x => x.Name, x => x.ToJsonObject());
        }

        // The simulation results the workflow attached (only present on a run that simulated), keyed by type and name.
        private static Dictionary<string, JsonObject> Results(AnalyticalModel model)
        {
            Dictionary<string, JsonObject> result = new Dictionary<string, JsonObject>();
            int index = 0;
            // Space results only: surface results carry TAS's surface numbering and zone references (identities, not outputs).
            foreach (SpaceSimulationResult r in model.AdjacencyCluster.GetObjects<SpaceSimulationResult>() ?? new List<SpaceSimulationResult>())
            {
                result[string.Format("{0}|{1}|{2}", r.GetType().Name, r.Name, index++)] = r.ToJsonObject();
            }

            return result;
        }

        // Parameters of a result that are not simulation outputs: the zone identity TAS minted for this run, and 'Dry Bulb Temperature', which
        // the result import reads from a field TAS does not fill (garbage in every run of either route - two runs of the SAME route differ in it).
        private static readonly HashSet<string> parameters_NotOutputs = new HashSet<string> { "Zone Guid", "Dry Bulb Temperature" };

        // Space results compared by parameter NAME, numerically: what a downstream consumer reads.
        private static int CompareResults(StringBuilder sb, Dictionary<string, JsonObject> a, Dictionary<string, JsonObject> b)
        {
            sb.AppendLine();
            sb.AppendLine(string.Format("## simulation results: gbXML {0}, direct {1}", a.Count, b.Count));

            int differences = 0;
            foreach (string key in a.Keys.Except(b.Keys)) { sb.AppendLine("  only in gbXML : " + key); differences++; }
            foreach (string key in b.Keys.Except(a.Keys)) { sb.AppendLine("  only in direct : " + key); differences++; }

            int count_Outputs = 0;
            int count_Identical = 0;
            Dictionary<string, int> count_Differing = new Dictionary<string, int>();
            double maxRelative = 0;
            foreach (string key in a.Keys.Intersect(b.Keys))
            {
                Dictionary<string, string> pa = Parameters(a[key]);
                Dictionary<string, string> pb = Parameters(b[key]);
                foreach (string name in pa.Keys.Union(pb.Keys).Where(x => !parameters_NotOutputs.Contains(x)))
                {
                    pa.TryGetValue(name, out string va);
                    pb.TryGetValue(name, out string vb);
                    count_Outputs++;
                    if (va == vb) { count_Identical++; continue; }

                    if (double.TryParse(va, NumberStyles.Float, CultureInfo.InvariantCulture, out double da) && double.TryParse(vb, NumberStyles.Float, CultureInfo.InvariantCulture, out double db))
                    {
                        double relative = Math.Abs(da - db) / Math.Max(Math.Max(Math.Abs(da), Math.Abs(db)), 1e-9);
                        maxRelative = Math.Max(maxRelative, relative);
                        if (relative <= 1e-3 || Math.Abs(da - db) <= 1e-6)
                        {
                            continue;
                        }
                    }

                    count_Differing.TryGetValue(name, out int count);
                    count_Differing[name] = count + 1;
                    differences++;
                }
            }

            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0} simulation outputs compared: {1} bit-identical, the rest within 1e-3 relative except {2}; largest relative difference {3:G3}", count_Outputs, count_Identical, count_Differing.Count == 0 ? "none" : string.Join(", ", count_Differing.Select(x => x.Key + " x" + x.Value)), maxRelative));
            return differences;
        }

        private static Dictionary<string, string> Parameters(JsonObject jsonObject)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            foreach (JsonNode parameterSet in jsonObject["ParameterSets"] as JsonArray ?? new JsonArray())
            {
                foreach (JsonNode parameter in parameterSet?["Parameters"] as JsonArray ?? new JsonArray())
                {
                    string name = parameter?["Name"]?.GetValue<string>();
                    if (name != null) result[name] = parameter["Value"]?.ToJsonString().Trim('"');
                }
            }

            return result;
        }

        private static Dictionary<string, JsonObject> Apertures(AnalyticalModel model)
        {
            Dictionary<string, JsonObject> result = new Dictionary<string, JsonObject>();
            foreach (Panel panel in model.AdjacencyCluster.GetPanels())
            {
                foreach (Aperture aperture in panel.Apertures ?? new List<Aperture>())
                {
                    result[aperture.Guid.ToString()] = aperture.ToJsonObject();
                }
            }

            return result;
        }

        private static Dictionary<string, JsonObject> Panels(AnalyticalModel model)
        {
            Dictionary<string, JsonObject> result = new Dictionary<string, JsonObject>();
            foreach (Panel panel in model.AdjacencyCluster.GetPanels())
            {
                string spaces = string.Join("|", (model.AdjacencyCluster.GetSpaces(panel) ?? new List<Space>()).Select(x => x.Name).OrderBy(x => x));
                Geometry.Spatial.Point3D centroid = panel.GetBoundingBox().GetCentroid();
                string key = string.Format(CultureInfo.InvariantCulture, "{0}|{1}|area={2:F2}|c=({3:F1},{4:F1},{5:F1})", spaces, panel.PanelType, panel.GetArea(), centroid.X, centroid.Y, centroid.Z);
                result[key] = panel.ToJsonObject();
            }

            return result;
        }

        private static int CompareGroup(StringBuilder sb, string title, Dictionary<string, JsonObject> a, Dictionary<string, JsonObject> b)
        {
            sb.AppendLine();
            sb.AppendLine(string.Format("## {0}: gbXML {1}, direct {2}", title, a.Count, b.Count));

            int differences = 0;
            foreach (string key in a.Keys.Except(b.Keys)) { sb.AppendLine("  only in gbXML : " + key); differences++; }
            foreach (string key in b.Keys.Except(a.Keys)) { sb.AppendLine("  only in direct : " + key); differences++; }

            // Per differing leaf PATH, how many objects differ and one example - the shape of the difference, not a wall of lines.
            Dictionary<string, List<string>> byPath = new Dictionary<string, List<string>>();
            int count_Numeric = 0;
            double maxRelative = 0;
            foreach (string key in a.Keys.Intersect(b.Keys))
            {
                Dictionary<string, string> fa = Flatten(a[key]);
                Dictionary<string, string> fb = Flatten(b[key]);
                foreach (string path in fa.Keys.Union(fb.Keys))
                {
                    fa.TryGetValue(path, out string va);
                    fb.TryGetValue(path, out string vb);
                    if (string.Equals(va, vb, StringComparison.Ordinal)) continue;

                    // Floating-point simulation outputs: equal within the precision TAS stores them (single) and the geometry it was given.
                    if (NumericallyEqual(va, vb, 1e-3, out double relative))
                    {
                        count_Numeric++;
                        maxRelative = Math.Max(maxRelative, relative);
                        continue;
                    }

                    if (!byPath.TryGetValue(path, out List<string> examples)) byPath[path] = examples = new List<string>();
                    examples.Add(string.Format("{0}: gbXML={1} | direct={2}", key, Shorten(va), Shorten(vb)));
                }
            }

            foreach (KeyValuePair<string, List<string>> entry in byPath.OrderBy(x => x.Key))
            {
                // TAS numbers a zone's surfaces in the order they were created, and the two routes create them in different orders (SAM's
                // panel order vs gbXML's). The number is an identifier that is consistent within each TBD, not a property of the model.
                bool surfaceNumber = entry.Key.EndsWith("SurfaceNumber") || (entry.Key == "Reference" && entry.Value[0].Contains("SurfaceSimulationResult"));
                bool timestamp = entry.Key == "DateTime";
                string classification = surfaceNumber ? "equivalent representation (TAS surface numbering follows creation order)" : timestamp ? "expected (the time the run was made)" : "UNRESOLVED";
                sb.AppendLine(string.Format("  [{0}] differs at '{1}' on {2} object(s); e.g. {3}", classification, entry.Key, entry.Value.Count, entry.Value[0]));
                if (!surfaceNumber && !timestamp) differences++;
            }

            if (count_Numeric != 0)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  [equivalent: numerically equal within 1e-3 relative] {0} numeric value set(s) differ in the last digits; largest relative difference {1:G3}", count_Numeric, maxRelative));
            }

            return differences;
        }

        // Both sides a comma-joined list of numbers of the same length, every pair within the relative tolerance.
        private static bool NumericallyEqual(string a, string b, double tolerance, out double maxRelative)
        {
            maxRelative = 0;
            if (a == null || b == null) return false;

            string[] xa = a.Split(',');
            string[] xb = b.Split(',');
            if (xa.Length != xb.Length) return false;

            for (int i = 0; i < xa.Length; i++)
            {
                if (xa[i] == xb[i]) continue;
                if (!double.TryParse(xa[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double da) || !double.TryParse(xb[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double db)) return false;

                double relative = Math.Abs(da - db) / Math.Max(Math.Max(Math.Abs(da), Math.Abs(db)), 1e-9);
                if (Math.Abs(da - db) > 1e-6 && relative > tolerance) return false;
                maxRelative = Math.Max(maxRelative, relative);
            }

            return true;
        }

        private static string Shorten(string text)
        {
            if (text == null) return "(absent)";
            return text.Length > 120 ? text.Substring(0, 120) + "..." : text;
        }

        private static Dictionary<string, string> Flatten(JsonNode node)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            Walk(node, string.Empty, result);
            return result;
        }

        private static void Walk(JsonNode node, string path, Dictionary<string, string> result)
        {
            if (node is JsonObject jsonObject)
            {
                foreach (KeyValuePair<string, JsonNode> keyValuePair in jsonObject)
                {
                    Walk(keyValuePair.Value, path.Length == 0 ? keyValuePair.Key : path + "." + keyValuePair.Key, result);
                }
            }
            else if (node is JsonArray jsonArray)
            {
                for (int i = 0; i < jsonArray.Count; i++)
                {
                    // Arrays of parameter sets etc: index-free path, so ordering differences do not drown real ones.
                    Walk(jsonArray[i], path + "[]", result);
                }
            }
            else if (node != null)
            {
                string value = node.ToJsonString();
                string unquoted = value.Trim('"');
                if (regex_Guid.IsMatch(unquoted))
                {
                    value = "<guid>";
                }

                // Several array elements share a path: append, so the multiset - not just the last element - is compared.
                result[path] = result.TryGetValue(path, out string existing) ? existing + "," + value : value;
            }
        }
    }
}
