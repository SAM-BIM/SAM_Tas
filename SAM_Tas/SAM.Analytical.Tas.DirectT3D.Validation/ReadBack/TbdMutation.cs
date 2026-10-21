// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using TBD;

namespace SAM.Analytical.Tas.DirectT3D.Validation.ReadBack
{
    /// <summary>
    /// The licensed negative checks: one deliberate change to a persisted field of a DISPOSABLE TBD copy (never an
    /// original), so the independent reader and comparison can be shown to fail or block on what is actually saved.
    /// </summary>
    public static class TbdMutation
    {
        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;

        public static readonly string[] Cases = { "swap-space-guid", "layer-conductivity", "zero-lighting-factor", "nan-infiltration", "shift-heating-slot", "add-izam", "flip-partition" };

        public static string Apply(string path, string mutation)
        {
            if (!File.Exists(path))
            {
                return "not applied: missing " + path;
            }

            TBDDocument document = new TBDDocument();
            try
            {
                if (document.open(path) == 0)
                {
                    return "not applied: open returned 0";
                }

                string applied = Mutate(document.Building, mutation);
                if (applied.StartsWith("not applied", StringComparison.Ordinal))
                {
                    return applied;
                }

                if (document.save() == 0)
                {
                    return "not applied: save returned 0";
                }

                return applied;
            }
            finally
            {
                try { document.close(); } catch { }
                Marshal.FinalReleaseComObject(document);
            }
        }

        private static string Mutate(Building building, string mutation)
        {
            zone zone0 = building.GetZone(0);
            switch (mutation)
            {
                case "swap-space-guid":
                    {
                        zone zone1 = building.GetZone(1);
                        if (zone0 == null || zone1 == null) return "not applied: needs two zones";
                        string a = zone0.description, b = zone1.description;
                        string ga = Segment(a), gb = Segment(b);
                        if (ga == null || gb == null) return "not applied: a zone has no [SpaceGuid] segment";
                        zone0.description = a.Replace(ga, gb);
                        zone1.description = b.Replace(gb, ga);
                        return "swapped the [SpaceGuid] carriers of zones '" + zone0.name + "' and '" + zone1.name + "'";
                    }

                case "layer-conductivity":
                    for (int i = 0; ; i++)
                    {
                        buildingElement be = building.GetBuildingElement(i);
                        if (be == null) return "not applied: no external wall element";
                        if (be.BEType != 2) continue;
                        material m = be.GetConstruction()?.materials(1);
                        if (m == null) continue;
                        float before = m.conductivity;
                        m.conductivity = before * 1.5f;
                        return "element '" + be.name + "' materials(1).conductivity " + before.ToString("R", ci) + " -> " + m.conductivity.ToString("R", ci);
                    }

                case "zero-lighting-factor":
                case "nan-infiltration":
                case "shift-heating-slot":
                    {
                        TBD.InternalCondition ic = Annual(zone0);
                        if (ic == null) return "not applied: no annual internal condition";
                        if (mutation == "zero-lighting-factor")
                        {
                            profile p = ic.GetInternalGain().GetProfile((int)Profiles.ticLG);
                            float before = p.factor;
                            p.factor = 0;
                            return "zone '" + zone0.name + "' ticLG factor " + before.ToString("R", ci) + " -> 0";
                        }

                        if (mutation == "nan-infiltration")
                        {
                            profile p = ic.GetInternalGain().GetProfile((int)Profiles.ticI);
                            float before = p.factor;
                            p.factor = float.NaN;
                            return "zone '" + zone0.name + "' ticI factor " + before.ToString("R", ci) + " -> NaN (read back as " + p.factor.ToString("R", ci) + ")";
                        }

                        profile t = ic.GetThermostat().GetProfile((int)Profiles.ticLL);
                        float first = t.hourlyValues[1];
                        for (int s = 1; s < 24; s++) t.hourlyValues[s] = t.hourlyValues[s + 1];
                        t.hourlyValues[24] = first;
                        return "zone '" + zone0.name + "' ticLL hourlyValues rotated one slot earlier (slot s takes slot s + 1)";
                    }

                case "add-izam":
                    {
                        IZAM izam = building.AddIZAM(null);
                        izam.name = "S08 negative IZAM";
                        izam.fromOutside = 1;
                        izam.SetSourceZone(zone0);
                        return "added IZAM '" + izam.name + "' (from outside) on zone '" + zone0.name + "'";
                    }

                case "flip-partition":
                    for (int j = 0; ; j++)
                    {
                        zoneSurface zs = zone0.GetSurface(j);
                        if (zs == null) return "not applied: zone 0 has no linked internal surface";
                        if (zs.linkSurface == null) continue;
                        int before = zs.reversed;
                        zs.reversed = before == 0 ? 1 : 0;
                        return "zone '" + zone0.name + "' surface " + j + " (" + zs.buildingElement?.name + ") reversed " + before + " -> " + zs.reversed;
                    }

                default:
                    return "not applied: unknown case '" + mutation + "' (cases: " + string.Join(", ", Cases) + ")";
            }
        }

        private static TBD.InternalCondition Annual(zone zone)
        {
            for (int k = 0; zone != null && k < 64; k++)
            {
                TBD.InternalCondition ic = zone.GetIC(k);
                if (ic == null) return null;
                bool hdd = false;
                for (int d = 0; d < 64; d++)
                {
                    dayType dt = ic.GetDayType(d);
                    if (dt == null) break;
                    hdd |= dt.name == "HDD";
                }

                if (!hdd) return ic;
            }

            return null;
        }

        private static string Segment(string description)
        {
            foreach (string segment in (description ?? string.Empty).Split(';'))
            {
                if (segment.Trim().StartsWith(ReadBackIdentity.SpaceGuidMarker, StringComparison.Ordinal)) return segment.Trim();
            }

            return null;
        }
    }
}
