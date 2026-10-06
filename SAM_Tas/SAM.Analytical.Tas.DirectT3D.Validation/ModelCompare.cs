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
            differences += CompareGroup(sb, "simulation results", Results(a), Results(b));

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
            foreach (SAM.Core.Result r in model.AdjacencyCluster.GetObjects<SAM.Core.Result>() ?? new List<SAM.Core.Result>())
            {
                result[string.Format("{0}|{1}|{2}", r.GetType().Name, r.Name, r is SpaceSimulationResult ? ((SpaceSimulationResult)r).Name : index++.ToString())] = r.ToJsonObject();
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
            foreach (string key in a.Keys.Intersect(b.Keys))
            {
                Dictionary<string, string> fa = Flatten(a[key]);
                Dictionary<string, string> fb = Flatten(b[key]);
                foreach (string path in fa.Keys.Union(fb.Keys))
                {
                    fa.TryGetValue(path, out string va);
                    fb.TryGetValue(path, out string vb);
                    if (string.Equals(va, vb, StringComparison.Ordinal)) continue;

                    if (!byPath.TryGetValue(path, out List<string> examples)) byPath[path] = examples = new List<string>();
                    examples.Add(string.Format("{0}: gbXML={1} | direct={2}", key, Shorten(va), Shorten(vb)));
                }
            }

            foreach (KeyValuePair<string, List<string>> entry in byPath.OrderBy(x => x.Key))
            {
                sb.AppendLine(string.Format("  differs at '{0}' on {1} object(s); e.g. {2}", entry.Key, entry.Value.Count, entry.Value[0]));
                differences++;
            }

            return differences;
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
