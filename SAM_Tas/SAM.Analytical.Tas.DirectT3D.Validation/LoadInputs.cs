// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using TBD;

namespace SAM.Analytical.Tas.DirectT3D.Validation
{
    // SAM.Analytical has types of the same names; these are TAS's.
    using Construction = global::TBD.Construction;
    using ApertureType = global::TBD.ApertureType;
    using InternalCondition = global::TBD.InternalCondition;
    using Emitter = global::TBD.Emitter;

    /// <summary>
    /// Everything in a TBD that decides what TAS simulates, written as sorted <c>path = value</c> lines keyed by NAME (never by
    /// TAS's own numbering or GUIDs, which differ between any two builds): the building, general details, controls and weather;
    /// every zone with its internal condition, internal gain and thermostat profiles (hourly values and a hash of the yearly
    /// ones), emitters, room volume and floor area, and every surface with its area, orientation, inclination, reversed side,
    /// building element and link; every building element, its construction and the layers in order; every aperture type and
    /// its profile; every IZAM; every schedule; the zone groups.
    /// <para>
    /// Properties are read by reflection over the interop interfaces, so a property TAS adds - or one this code did not think
    /// of - is compared too. Two dumps are compared line by line: a line that differs is either on the short, justified list of
    /// <i>identity</i> properties (a GUID, a colour, a TAS surface number, the SAM space guid in the zone description) or it is
    /// reported as UNEXPECTED.
    /// </para>
    /// </summary>
    public static class LoadInputs
    {
        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;

        public static int Run(string[] args)
        {
            // inputs <a.tbd> <b.tbd> [out-prefix]
            SortedDictionary<string, string> a = Dump(args[1]);
            SortedDictionary<string, string> b = Dump(args[2]);

            string prefix = args.Length > 3 ? args[3] : null;
            if (prefix != null)
            {
                File.WriteAllLines(prefix + ".a.txt", a.Select(x => x.Key + " = " + x.Value));
                File.WriteAllLines(prefix + ".b.txt", b.Select(x => x.Key + " = " + x.Value));
            }

            StringBuilder sb = new StringBuilder();
            Compare(a, b, sb, out int unexpected);

            Console.Write(sb.ToString());
            if (prefix != null) File.WriteAllText(prefix + ".compare.txt", sb.ToString());
            return unexpected == 0 ? 0 : 6;
        }

        // ---- what is allowed to differ, and why. Anything else is UNEXPECTED. ---------------------------------------------

        private static string ExpectedReason(string key, string va, string vb)
        {
            if (key.EndsWith("/GUID")) return "identity: TAS mints a GUID per object";
            if (key.Contains("/surface[") && key.EndsWith("/number")) return "identity: TAS numbers surfaces in creation order";
            if (key.EndsWith("/colour") && key.Contains("/element[")) return "cosmetic: the direct route uses SAM's colour for the panel type, gbXML keeps TAS's default palette";
            if (key.EndsWith("/colour") && key.StartsWith("zone[")) return "cosmetic: zone colour";
            if (key.StartsWith("zone[") && key.EndsWith("/description") && (va?.Contains("[SpaceGuid]") == true || vb?.Contains("[SpaceGuid]") == true)) return "improvement: the direct zone description carries the SAM space guid; the gbXML one cannot";
            if (key.StartsWith("zonegroup[") && (key.Contains("gbXml Spaces") || key.Contains("[SAM]"))) return "equivalent representation: the name of the set holding every zone";

            // DIRECT_T3D_ROUTE.md, "known differences" 2: the direct route states adiabatic walls at import, so TAS leaves them out of the
            // zone's exposed perimeter and facade length; the gbXML route computes both before its adiabatic repair. The difference is the
            // adiabatic wall lengths: in the load-sensitive fixture 14 vs 11 m on Store_C, and its adiabatic east wall is 3 m long.
            if (key.StartsWith("zone[") && (key.EndsWith("/exposedPerimeter") || key.EndsWith("/facadeLength"))) return "known (DIRECT_T3D_ROUTE.md): adiabatic wall length excluded on the direct route; no effect on the simulation";
            // What TAS COMPUTED in the sizing run and wrote back into the TBD - plant capacity and peak air flow per zone - is a result, not an
            // input, and a result of an iterative solve. It is accepted only within the same fixed tolerance as the simulation series; a
            // larger difference stays UNEXPECTED.
            if (key.StartsWith("zone[") && (key.EndsWith("/maxHeatingLoad") || key.EndsWith("/maxCoolingLoad") || key.EndsWith("/peakFlowHeat") || key.EndsWith("/peakFlowCool")) &&
                double.TryParse(va, NumberStyles.Float, ci, out double sa) && double.TryParse(vb, NumberStyles.Float, ci, out double sb_) &&
                Math.Abs(sa - sb_) <= TsdCompare.DefaultTolerance * Math.Max(Math.Abs(sa), Math.Abs(sb_)))
            {
                return "computed by TAS sizing (a result of the design-day solve, not an input); equal within the simulation tolerance";
            }

            return null;
        }

        private static void Compare(SortedDictionary<string, string> a, SortedDictionary<string, string> b, StringBuilder sb, out int unexpected)
        {
            int identical = 0, expected = 0;
            unexpected = 0;
            List<string> expectedLines = new List<string>();
            List<string> unexpectedLines = new List<string>();

            foreach (string key in a.Keys.Union(b.Keys).OrderBy(x => x, StringComparer.Ordinal))
            {
                a.TryGetValue(key, out string va);
                b.TryGetValue(key, out string vb);
                if (string.Equals(va, vb, StringComparison.Ordinal)) { identical++; continue; }

                string reason = ExpectedReason(key, va, vb);
                string line = string.Format("{0}: gbXML={1} | direct={2}", key, Shorten(va), Shorten(vb));
                if (reason != null) { expected++; expectedLines.Add(line + "   [" + reason + "]"); }
                else { unexpected++; unexpectedLines.Add(line); }
            }

            sb.AppendLine(string.Format(ci, "TBD INPUT COMPARISON  gbXML properties={0}  direct properties={1}  union={2}", a.Count, b.Count, a.Keys.Union(b.Keys).Count()));
            sb.AppendLine(string.Format(ci, "  identical            {0}", identical));
            sb.AppendLine(string.Format(ci, "  different, expected  {0}", expected));
            sb.AppendLine(string.Format(ci, "  different, UNEXPECTED {0}", unexpected));
            sb.AppendLine();
            sb.AppendLine("UNEXPECTED:");
            foreach (string line in unexpectedLines) sb.AppendLine("  " + line);
            sb.AppendLine();
            sb.AppendLine("EXPECTED (identity / cosmetic):");
            foreach (string line in expectedLines.Take(200)) sb.AppendLine("  " + line);
        }

        private static string Shorten(string text)
        {
            if (text == null) return "(absent)";
            return text.Length > 140 ? text.Substring(0, 140) + "..." : text;
        }

        // ---- the dump ------------------------------------------------------------------------------------------------------

        private static SortedDictionary<string, string> Dump(string path)
        {
            SortedDictionary<string, string> result = new SortedDictionary<string, string>(StringComparer.Ordinal);
            TBDDocument doc = new TBDDocument();
            try
            {
                if (doc.open(path) == 0 && doc.openReadOnly(path) == 0) throw new IOException("cannot open " + path);

                TBD.Building building = doc.Building;
                Simple(building, typeof(IBuilding), "building", result, "GUID", "TBDGUID", "path3DFile", "name", "description");
                Simple(building.GetGeneralDetails(), typeof(IGeneralDetails), "details", result, "client", "engineer1", "engineer2");
                Simple(building.GetControls(), typeof(IControls), "controls", result);

                WeatherYear weather = building.GetWeatherYear();
                if (weather != null)
                {
                    Simple(weather, typeof(IWeatherYear), "weather", result);
                    Try(() => result["weather/checksum"] = weather.CalculateDataChecksum());
                }

                // building elements and their constructions
                for (int i = 0; ; i++)
                {
                    buildingElement be = building.GetBuildingElement(i);
                    if (be == null) break;
                    string key = "element[" + be.name + "]";
                    Simple(be, typeof(IBuildingElement), key, result, "name");
                    Construction construction = be.GetConstruction();
                    result[key + "/construction"] = construction?.name ?? "(none)";
                    for (int j = 0; ; j++)
                    {
                        ApertureType apertureType = be.GetApertureType(j);
                        if (apertureType == null) break;
                        result[key + "/apertureType[" + j + "]"] = apertureType.name;
                    }
                }

                for (int i = 0; ; i++)
                {
                    Construction c = building.GetConstruction(i);
                    if (c == null) break;
                    string key = "construction[" + c.name + "]";
                    Simple(c, typeof(IConstruction), key, result, "name");
                    Try(() => result[key + "/uvalue"] = Flatten(c.GetUValue()));
                    // TAS counts a construction's layers from 1.
                    for (int j = 1; ; j++)
                    {
                        material m = c.materials(j);
                        if (m == null) break;
                        Simple(m, typeof(IMaterial), key + "/layer[" + j + "]", result);

                        // The layer's thickness in THIS construction (material.width is only the material's default).
                        Try(() => result[key + "/layer[" + j + "]/thickness"] = ((float)c.materialWidth[j]).ToString("R", ci));
                    }
                }

                for (int i = 0; ; i++)
                {
                    ApertureType t = building.GetApertureType(i);
                    if (t == null) break;
                    string key = "aperturetype[" + t.name + "]";
                    Simple(t, typeof(IApertureType), key, result, "name");
                    DumpProfile(t.GetProfile(), key + "/profile", result);
                    for (int j = 0; ; j++) { dayType dt = t.GetDayType(j); if (dt == null) break; result[key + "/daytype[" + dt.name + "]"] = "1"; }
                }

                for (int i = 0; ; i++)
                {
                    schedule s = building.GetSchedule(i);
                    if (s == null) break;
                    Simple(s, typeof(ISchedule), "schedule[" + s.name + "]", result, "name");
                }

                // internal conditions at building level (name list; the content is read through each zone)
                for (int i = 0; ; i++)
                {
                    InternalCondition ic = building.GetIC(i);
                    if (ic == null) break;
                    result["ic[" + ic.name + "]"] = "present";
                }

                // IZAMs
                for (int i = 0; ; i++)
                {
                    IZAM izam = building.GetIZAM(i);
                    if (izam == null) break;
                    string key = "izam[" + izam.name + "]";
                    Simple(izam, typeof(IIZAM), key, result, "name");
                    result[key + "/source"] = izam.GetSourceZone()?.name ?? "(none)";
                    List<string> targets = new List<string>();
                    for (int j = 0; ; j++) { zone t = izam.GetTargetZone(j); if (t == null) break; targets.Add(t.name); }
                    result[key + "/targets"] = string.Join(",", targets.OrderBy(x => x));
                    DumpProfile(izam.GetProfile(), key + "/profile", result);
                    for (int j = 0; ; j++) { dayType dt = izam.GetDayType(j); if (dt == null) break; result[key + "/daytype[" + dt.name + "]"] = "1"; }
                }

                // zone groups
                for (int i = 0; ; i++)
                {
                    ZoneGroup g = building.GetZoneGroup(i);
                    if (g == null) break;
                    string key = "zonegroup[" + g.name + "]";
                    Simple(g, typeof(IZoneGroup), key, result, "name");
                    List<string> members = new List<string>();
                    for (int j = 0; ; j++) { zone z = g.GetZone(j); if (z == null) break; members.Add(z.name); }
                    result[key + "/members"] = string.Join(",", members.OrderBy(x => x));
                }

                // zones
                for (int i = 0; ; i++)
                {
                    zone z = building.GetZone(i);
                    if (z == null) break;
                    DumpZone(z, "zone[" + z.name + "]", result);
                }
            }
            finally
            {
                try { doc.close(); } catch { }
                Marshal.FinalReleaseComObject(doc);
            }

            return result;
        }

        private static void DumpZone(zone z, string key, SortedDictionary<string, string> result)
        {
            Simple(z, typeof(IZone), key, result, "name", "number", "markDelete");

            room r = z.GetRoom(0);
            if (r != null) Simple(r, typeof(IRoom), key + "/room", result, "name");

            Try(() => result[key + "/occupancyScheduleType"] = z.GetOccupancyScheduleType().ToString(ci));
            Try(() => result[key + "/upperLimit"] = Flatten(z.GetUpperLimit()));
            Try(() => result[key + "/lowerLimit"] = Flatten(z.GetLowerLimit()));
            Try(() => result[key + "/minimumDeadBand"] = z.GetMinimumDeadBand().ToString("R", ci));

            InternalCondition ic = z.GetIC(0);
            if (ic != null)
            {
                string k = key + "/ic";
                Simple(ic, typeof(IInternalCondition), k, result);
                Try(() => result[k + "/limits"] = ic.GetLowerLimit().ToString("R", ci) + ".." + ic.GetUpperLimit().ToString("R", ci));
                for (int d = 0; ; d++) { dayType dt = ic.GetDayType(d); if (dt == null) break; result[k + "/daytype[" + dt.name + "]"] = "1"; }

                InternalGain gain = ic.GetInternalGain();
                if (gain != null)
                {
                    Simple(gain, typeof(IInternalGain), k + "/gain", result);
                    foreach (Profiles p in Enum.GetValues(typeof(Profiles)))
                    {
                        profile profile = null;
                        try { profile = gain.GetProfile((int)p); } catch { }
                        if (profile != null) DumpProfile(profile, k + "/gain/" + p, result);
                    }
                }

                Thermostat thermostat = ic.GetThermostat();
                if (thermostat != null)
                {
                    Simple(thermostat, typeof(IThermostat), k + "/thermostat", result);
                    foreach (Profiles p in Enum.GetValues(typeof(Profiles)))
                    {
                        profile profile = null;
                        try { profile = thermostat.GetProfile((int)p); } catch { }
                        if (profile != null) DumpProfile(profile, k + "/thermostat/" + p, result);
                    }
                }

                Emitter heating = ic.GetHeatingEmitter();
                if (heating != null) Simple(heating, typeof(IEmitter), k + "/heatingEmitter", result);
                Emitter cooling = ic.GetCoolingEmitter();
                if (cooling != null) Simple(cooling, typeof(IEmitter), k + "/coolingEmitter", result);
            }

            // surfaces: identified by what they are, never by TAS's number; identical keys are numbered in sorted order.
            List<KeyValuePair<string, zoneSurface>> surfaces = new List<KeyValuePair<string, zoneSurface>>();
            for (int j = 0; ; j++)
            {
                zoneSurface zs = z.GetSurface(j);
                if (zs == null) break;
                string beName = zs.buildingElement?.name ?? string.Empty;
                surfaces.Add(new KeyValuePair<string, zoneSurface>(string.Format(ci, "{0}|{1}|a={2:F3}|o={3:F1}|i={4:F1}|ia={5:F3}", zs.type, beName, zs.area, zs.orientation, zs.inclination, zs.internalArea), zs));
            }

            foreach (IGrouping<string, KeyValuePair<string, zoneSurface>> group in surfaces.GroupBy(x => x.Key))
            {
                int n = 0;
                foreach (KeyValuePair<string, zoneSurface> item in group)
                {
                    string k = key + "/surface[" + group.Key + "#" + (n++) + "]";
                    zoneSurface zs = item.Value;
                    Simple(zs, typeof(IZoneSurface), k, result, "number");
                    result[k + "/element"] = zs.buildingElement?.name ?? "(none)";
                    result[k + "/reversed"] = zs.reversed != 0 ? "1" : "0";
                    zoneSurface link = null;
                    try { link = zs.linkSurface; } catch { }
                    result[k + "/link"] = link == null ? "(none)" : (link.zone?.name ?? "?") + "|" + (link.buildingElement?.name ?? "?");
                    // the room surfaces behind it: their count and areas
                    List<string> rooms = new List<string>();
                    for (int m = 0; ; m++)
                    {
                        RoomSurface rs = zs.GetRoomSurface(m);
                        if (rs == null) break;
                        rooms.Add(rs.area.ToString("F3", ci));
                    }
                    result[k + "/roomSurfaces"] = string.Join(",", rooms.OrderBy(x => x));
                }
            }
        }

        private static void DumpProfile(profile p, string key, SortedDictionary<string, string> result)
        {
            if (p == null) return;
            Simple(p, typeof(IProfile), key, result);

            try
            {
                StringBuilder hourly = new StringBuilder();
                for (int h = 1; h <= 24; h++) hourly.Append(((float)p.hourlyValues[h]).ToString("R", ci)).Append(';');
                result[key + "/hourly"] = hourly.ToString();
            }
            catch { }

            try
            {
                object yearly = p.GetYearlyValues();
                if (yearly is Array array)
                {
                    List<float> values = new List<float>();
                    foreach (object o in array) values.Add(System.Convert.ToSingle(o, ci));
                    byte[] bytes = new byte[values.Count * 4];
                    Buffer.BlockCopy(values.ToArray(), 0, bytes, 0, bytes.Length);
                    using (SHA1 sha = SHA1.Create())
                    {
                        result[key + "/yearly"] = string.Format(ci, "n={0} sum={1:R} sha1={2}", values.Count, values.Sum(x => (double)x), BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""));
                    }
                }
            }
            catch { }
        }

        private static string Flatten(object value)
        {
            if (value == null) return "(null)";
            if (value is Array array)
            {
                List<string> parts = new List<string>();
                foreach (object o in array) parts.Add(o is float f ? f.ToString("R", ci) : System.Convert.ToString(o, ci));
                return string.Join(";", parts);
            }

            return System.Convert.ToString(value, ci);
        }

        private static void Try(Action action)
        {
            try { action(); } catch { }
        }

        // Every simple-typed (primitive, string, enum) readable non-indexed property of an interop interface.
        private static void Simple(object o, Type iface, string key, SortedDictionary<string, string> result, params string[] skip)
        {
            if (o == null) return;
            foreach (PropertyInfo property in iface.GetProperties())
            {
                if (property.GetIndexParameters().Length != 0 || !property.CanRead) continue;
                if (skip.Contains(property.Name)) continue;
                Type type = property.PropertyType;
                if (!(type.IsPrimitive || type == typeof(string) || type.IsEnum)) continue;

                string text;
                try
                {
                    object value = property.GetValue(o);
                    text = value is float f ? f.ToString("R", ci) : value is double d ? d.ToString("R", ci) : System.Convert.ToString(value, ci);
                }
                catch (Exception exception)
                {
                    text = "(unreadable: " + (exception.InnerException ?? exception).GetType().Name + ")";
                }

                result[key + "/" + property.Name] = text;
            }
        }
    }
}
