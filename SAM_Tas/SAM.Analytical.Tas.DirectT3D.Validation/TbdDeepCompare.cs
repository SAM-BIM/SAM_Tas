// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using TBD;

namespace SAM.Analytical.Tas.DirectT3D.Validation
{
    /// <summary>
    /// The TBD properties the simulation reads and the structural comparison does not: per zone (length, facade length,
    /// exposed perimeter, wall/floor ratio, peak flows) and per surface (altitude, altitude range, hydraulic diameter,
    /// internal area, reversed, and the shade proportion on every representative day). Compared between two TBDs by
    /// zone name and surface identity, with the differences stated per property.
    /// </summary>
    public static class TbdDeepCompare
    {
        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;

        private sealed class SurfaceData
        {
            public string Key;
            public Dictionary<string, string> Values = new Dictionary<string, string>();
        }

        private sealed class ZoneData
        {
            public Dictionary<string, string> Values = new Dictionary<string, string>();
            public List<SurfaceData> Surfaces = new List<SurfaceData>();
        }

        public static int Run(string[] args)
        {
            // deep <a.tbd> <b.tbd> <out.txt>
            Dictionary<string, ZoneData> a = Read(args[1]);
            Dictionary<string, ZoneData> b = Read(args[2]);

            StringBuilder sb = new StringBuilder();
            Dictionary<string, int> propertyCounts = new Dictionary<string, int>();
            Dictionary<string, double> propertyMaxDelta = new Dictionary<string, double>();
            List<string> samples = new List<string>();

            foreach (string zoneName in a.Keys.Union(b.Keys).OrderBy(x => x))
            {
                if (!a.TryGetValue(zoneName, out ZoneData za) || !b.TryGetValue(zoneName, out ZoneData zb))
                {
                    sb.AppendLine("zone only in one TBD: " + zoneName);
                    continue;
                }

                foreach (string property in za.Values.Keys.Union(zb.Values.Keys).OrderBy(x => x))
                {
                    za.Values.TryGetValue(property, out string va);
                    zb.Values.TryGetValue(property, out string vb);
                    Record("zone." + property, zoneName, va, vb, propertyCounts, propertyMaxDelta, samples);
                }

                Dictionary<string, List<SurfaceData>> sa = za.Surfaces.GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.ToList());
                Dictionary<string, List<SurfaceData>> sb2 = zb.Surfaces.GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.ToList());
                foreach (string key in sa.Keys.Union(sb2.Keys).OrderBy(x => x))
                {
                    sa.TryGetValue(key, out List<SurfaceData> la);
                    sb2.TryGetValue(key, out List<SurfaceData> lb);
                    if (la == null || lb == null || la.Count != lb.Count)
                    {
                        sb.AppendLine(string.Format(ci, "zone {0}: surface {1}: gbXML x{2} vs direct x{3}", zoneName, key, la?.Count ?? 0, lb?.Count ?? 0));
                        continue;
                    }

                    // Same key more than once (identical windows): compare the sorted value sets.
                    foreach (string property in la[0].Values.Keys.Union(lb[0].Values.Keys).OrderBy(x => x))
                    {
                        List<string> xa = la.Select(x => x.Values.TryGetValue(property, out string v) ? v : null).OrderBy(x => x, StringComparer.Ordinal).ToList();
                        List<string> xb = lb.Select(x => x.Values.TryGetValue(property, out string v) ? v : null).OrderBy(x => x, StringComparer.Ordinal).ToList();
                        for (int i = 0; i < xa.Count; i++)
                        {
                            Record("surface." + property, zoneName + " / " + key, xa[i], xb[i], propertyCounts, propertyMaxDelta, samples);
                        }
                    }
                }
            }

            sb.AppendLine("DIFFERING PROPERTIES (count of zone/surface instances; max numeric delta):");
            foreach (KeyValuePair<string, int> entry in propertyCounts.OrderByDescending(x => x.Value))
            {
                propertyMaxDelta.TryGetValue(entry.Key, out double delta);
                sb.AppendLine(string.Format(ci, "  {0,-34} {1,5}   max |delta| = {2:G4}", entry.Key, entry.Value, delta));
            }

            sb.AppendLine();
            sb.AppendLine("SAMPLES:");
            foreach (string sample in samples.Take(60)) sb.AppendLine("  " + sample);

            Console.Write(sb.ToString());
            if (args.Length > 3) File.WriteAllText(args[3], sb.ToString());
            return 0;
        }

        private static void Record(string property, string where, string va, string vb, Dictionary<string, int> counts, Dictionary<string, double> maxDelta, List<string> samples)
        {
            if (string.Equals(va, vb, StringComparison.Ordinal)) return;

            // Numeric values equal to the precision TAS stores (single) are equal.
            if (double.TryParse(va, NumberStyles.Float, ci, out double da) && double.TryParse(vb, NumberStyles.Float, ci, out double db))
            {
                double delta = Math.Abs(da - db);
                if (delta <= 1e-4 * Math.Max(1.0, Math.Max(Math.Abs(da), Math.Abs(db)))) return;
                maxDelta.TryGetValue(property, out double current);
                maxDelta[property] = Math.Max(current, delta);
            }

            counts.TryGetValue(property, out int count);
            counts[property] = count + 1;
            if (samples.Count < 200) samples.Add(string.Format("{0} @ {1}: gbXML={2} | direct={3}", property, where, Shorten(va), Shorten(vb)));
        }

        private static string Shorten(string text)
        {
            if (text == null) return "(absent)";
            return text.Length > 110 ? text.Substring(0, 110) + "..." : text;
        }

        private static Dictionary<string, ZoneData> Read(string path)
        {
            Dictionary<string, ZoneData> result = new Dictionary<string, ZoneData>();
            TBDDocument doc = new TBDDocument();
            try
            {
                // Read-write, as FromTBD reads it.
                if (doc.open(path) == 0 && doc.openReadOnly(path) == 0) throw new IOException("cannot open " + path);

                TBD.Building b = doc.Building;
                for (int i = 0; ; i++)
                {
                    zone z = b.GetZone(i);
                    if (z == null) break;

                    ZoneData data = new ZoneData();
                    data.Values["volume"] = z.volume.ToString("F4", ci);
                    data.Values["floorArea"] = z.floorArea.ToString("F4", ci);
                    data.Values["length"] = z.length.ToString("F4", ci);
                    data.Values["facadeLength"] = z.facadeLength.ToString("F4", ci);
                    data.Values["wallFloorAreaRatio"] = z.wallFloorAreaRatio.ToString("F4", ci);
                    data.Values["exposedPerimeter"] = z.exposedPerimeter.ToString("F4", ci);
                    data.Values["fixedConvectionCoefficient"] = z.fixedConvectionCoefficient.ToString("F4", ci);
                    data.Values["daylightFactor"] = z.daylightFactor.ToString("F4", ci);
                    data.Values["external"] = z.external.ToString(ci);

                    for (int j = 0; ; j++)
                    {
                        zoneSurface zs = z.GetSurface(j);
                        if (zs == null) break;

                        buildingElement be = zs.buildingElement;
                        string beName = be?.name ?? string.Empty;
                        SurfaceData surface = new SurfaceData
                        {
                            Key = string.Format(ci, "{0}|{1}|area={2:F3}|orient={3:F1}|incl={4:F1}", zs.type, beName, zs.area, zs.orientation, zs.inclination)
                        };

                        surface.Values["altitude"] = zs.altitude.ToString("F4", ci);
                        surface.Values["altitudeRange"] = zs.altitudeRange.ToString("F4", ci);
                        surface.Values["planHydraulicDiameter"] = zs.planHydraulicDiameter.ToString("F4", ci);
                        surface.Values["internalArea"] = zs.internalArea.ToString("F4", ci);
                        surface.Values["reversed"] = zs.reversed.ToString(ci);
                        surface.Values["orientation"] = zs.orientation.ToString("F4", ci);
                        surface.Values["inclination"] = zs.inclination.ToString("F4", ci);

                        if (zs.type == SurfaceType.tbdExposed)
                        {
                            StringBuilder shade = new StringBuilder();
                            for (int day = 1; day <= 365; day += 15)
                            {
                                dynamic proportion = b.GetShadeProportion(z.number, zs.number, day);
                                if (proportion == null) { shade.Append("n;"); continue; }
                                foreach (float value in proportion) shade.Append(value.ToString("F3", ci)).Append(',');
                                shade.Append(';');
                            }

                            surface.Values["shade"] = shade.ToString();
                        }

                        data.Surfaces.Add(surface);
                    }

                    result[z.name] = data;
                }
            }
            finally
            {
                try { doc.close(); } catch { }
                Marshal.FinalReleaseComObject(doc);
            }

            return result;
        }
    }
}
