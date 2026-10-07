// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Tas;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SAM.Analytical.Tas.DirectT3D.Validation
{
    /// <summary>
    /// Which zone volume is which. For each T3D given, a COPY is opened and read three ways - as saved, then exported to a TBD
    /// with the document's widths setting left as it is, OFF and ON - and the T3D zone's own floor area / volume (what TAS3D
    /// displays) and the exported TBD zone's are listed beside the volume of SAM's own shell for the space. The storeys
    /// (TAS3D <c>Floor</c>) are listed too: name, level, wall height and the zones on each.
    /// </summary>
    public static class VolumeExperiment
    {
        public static int Run(string[] args)
        {
            // volumes <model.sam> <outDir> <label>=<file.t3d> [<label>=<file.t3d> ...]
            CultureInfo ci = CultureInfo.InvariantCulture;
            AnalyticalModel model = SAM.Core.Convert.ToSAM<AnalyticalModel>(args[1])?.FirstOrDefault();
            string directory = args[2];
            Directory.CreateDirectory(directory);

            StringBuilder sb = new StringBuilder();
            Dictionary<string, double> volumes_SAM = new Dictionary<string, double>();
            Dictionary<string, double> areas_SAM = new Dictionary<string, double>();
            foreach (Space space in model.AdjacencyCluster.GetSpaces())
            {
                Geometry.Spatial.Shell shell = model.AdjacencyCluster.Shell(space);
                volumes_SAM[space.Name] = shell == null ? double.NaN : Geometry.Spatial.Query.Volume(shell);
                areas_SAM[space.Name] = space.TryGetValue(Analytical.SpaceParameter.Area, out double area) ? area : double.NaN;
            }

            // label -> mode -> zone -> (t3d floor, t3d volume, tbd floor, tbd volume)
            Dictionary<string, Dictionary<string, Dictionary<string, double[]>>> all = new Dictionary<string, Dictionary<string, Dictionary<string, double[]>>>();

            foreach (string arg in args.Skip(3))
            {
                int index = arg.IndexOf('=');
                string label = arg.Substring(0, index);
                string path_Source = arg.Substring(index + 1);
                all[label] = new Dictionary<string, Dictionary<string, double[]>>();

                foreach (string mode in new[] { "default", "off", "on" })
                {
                    string stem = label + "_" + mode;
                    string path_T3D = Path.Combine(directory, stem + ".t3d");
                    string path_TBD = Path.Combine(directory, stem + ".tbd");
                    File.Copy(path_Source, path_T3D, true);
                    if (File.Exists(path_TBD)) File.Delete(path_TBD);

                    Dictionary<string, double[]> zones = new Dictionary<string, double[]>();
                    using (SAMT3DDocument sAMT3DDocument = new SAMT3DDocument(path_T3D))
                    {
                        TAS3D.T3DDocument document = sAMT3DDocument.T3DDocument;
                        TAS3D.Building building = document.Building;

                        if (mode == "default")
                        {
                            sb.AppendLine(string.Format(ci, "== {0}: {1}", label, path_Source));
                            for (int i = 1; ; i++)
                            {
                                TAS3D.Zone zone = building.GetZone(i);
                                if (zone == null) break;
                                if (zone.isUsed == 0) continue;
                                sb.AppendLine(string.Format(ci, "  as saved: zone '{0}' floor={1:F3} volume={2:F3} colour={3:X6} desc='{4}'", zone.name, zone.floorArea, zone.volume, zone.colour, zone.description));
                            }

                            AppendFloors(sb, building, ci);
                        }

                        if (mode == "off") document.SetUseBEWidths(false);
                        if (mode == "on") document.SetUseBEWidths(true);

                        bool exported = Convert.ToTBD(document, path_TBD, 1, 1, 1, true, true);
                        sb.AppendLine(string.Format(ci, "  export widths={0}: {1}", mode, exported));

                        for (int i = 1; ; i++)
                        {
                            TAS3D.Zone zone = building.GetZone(i);
                            if (zone == null) break;
                            if (zone.isUsed == 0) continue;
                            zones[zone.name] = new[] { zone.floorArea, zone.volume, double.NaN, double.NaN };
                        }

                        if (mode == "default")
                        {
                            sb.AppendLine("  storeys after export:");
                            AppendFloors(sb, building, ci);
                        }
                    }

                    if (File.Exists(path_TBD))
                    {
                        foreach (TbdSnapshot.Zn zn in TbdSnapshot.Read(path_TBD).Zones)
                        {
                            if (zones.TryGetValue(zn.Name, out double[] values))
                            {
                                values[2] = zn.FloorArea;
                                values[3] = zn.Volume;
                            }
                        }
                    }

                    all[label][mode] = zones;
                }
            }

            sb.AppendLine();
            sb.AppendLine("T3D = the TAS3D zone after the export (what TAS3D shows); TBD = the exported TBD zone. floor / volume.");
            List<string> labels = all.Keys.ToList();
            StringBuilder header = new StringBuilder(string.Format(ci, "{0,-12} {1,15}", "zone", "SAM shell"));
            foreach (string label in labels)
            {
                foreach (string mode in new[] { "default", "off", "on" })
                {
                    header.Append(string.Format(ci, " | {0,-30}", label + " " + mode + " T3D ; TBD"));
                }
            }

            sb.AppendLine(header.ToString());
            foreach (string name in volumes_SAM.Keys)
            {
                StringBuilder line = new StringBuilder(string.Format(ci, "{0,-12} {1,6:F2}/{2,8:F3}", name, areas_SAM[name], volumes_SAM[name]));
                foreach (string label in labels)
                {
                    foreach (string mode in new[] { "default", "off", "on" })
                    {
                        all[label][mode].TryGetValue(name, out double[] v);
                        line.Append(v == null ? " | (missing)                     " : string.Format(ci, " | {0,6:F2}/{1,8:F3} ; {2,6:F2}/{3,8:F3}", v[0], v[1], v[2], v[3]));
                    }
                }

                sb.AppendLine(line.ToString());
            }

            File.WriteAllText(Path.Combine(directory, "volumes.txt"), sb.ToString());
            Console.Write(sb.ToString());
            return 0;
        }

        private static void AppendFloors(StringBuilder sb, TAS3D.Building building, CultureInfo ci)
        {
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < 16; i++)
            {
                TAS3D.Floor floor;
                try { floor = building.GetFloor(i); }
                catch (Exception) { continue; }
                if (floor == null) continue;

                string key = floor.name + "|" + floor.level.ToString(ci);
                if (!seen.Add(key)) continue;

                List<string> names = new List<string>();
                try
                {
                    if (floor.GetZonesOnFloor() is object[] zones)
                    {
                        foreach (object @object in zones)
                        {
                            if (@object is TAS3D.Zone zone) names.Add(zone.name);
                        }
                    }
                }
                catch (Exception exception) { names.Add("(GetZonesOnFloor: " + exception.Message + ")"); }

                sb.AppendLine(string.Format(ci, "  storey index {0}: name='{1}' level={2:F3} wallHeight={3:F3} ground={4} description='{5}' zones=[{6}]", i, floor.name, floor.level, floor.wallHeight, floor.ground, floor.description, string.Join(", ", names)));
            }
        }
    }
}
