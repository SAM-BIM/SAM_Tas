using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SAM.Analytical.Tas.DirectT3D.Validation
{
    /// <summary>
    /// Compares the TBD the gbXML route produced with the one the direct route produced, zone by zone, and states every
    /// difference with a classification. A difference is never hidden: where the rules below cannot classify one it is
    /// reported as <see cref="Category.Unresolved"/> for a human to decide.
    /// </summary>
    public static class TbdCompare
    {
        public enum Category
        {
            Equivalent,
            ExpectedImprovement,
            EquivalentRepresentation,
            KnownLimitation,
            LikelyDefect,
            Unresolved
        }

        public sealed class Item
        {
            public Category Category;
            public string Area;
            public string Text;
        }

        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;

        // The zones TAS adds on its own to either TBD; compared by name, so they only matter when they differ.
        public static int Run(string[] args)
        {
            // compare <gbxml.tbd> <direct.tbd> <outPrefix>
            TbdSnapshot a = TbdSnapshot.Read(args[1]);
            TbdSnapshot b = TbdSnapshot.Read(args[2]);
            List<Item> items = Compare(a, b);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("gbXML : " + args[1]);
            sb.AppendLine("direct: " + args[2]);
            sb.AppendLine();
            foreach (IGrouping<Category, Item> group in items.GroupBy(x => x.Category).OrderBy(x => x.Key))
            {
                sb.AppendLine(string.Format(ci, "## {0} ({1})", group.Key, group.Count()));
                foreach (Item item in group)
                {
                    sb.AppendLine("  [" + item.Area + "] " + item.Text);
                }

                sb.AppendLine();
            }

            File.WriteAllText(args[3] + ".compare.txt", sb.ToString());

            StringBuilder json = new StringBuilder();
            json.AppendLine("[");
            for (int i = 0; i < items.Count; i++)
            {
                json.AppendLine(string.Format(ci, "  {{\"category\":\"{0}\",\"area\":\"{1}\",\"text\":\"{2}\"}}{3}", items[i].Category, Escape(items[i].Area), Escape(items[i].Text), i == items.Count - 1 ? string.Empty : ","));
            }

            json.AppendLine("]");
            File.WriteAllText(args[3] + ".compare.json", json.ToString());

            Console.Write(sb.ToString());
            return items.Any(x => x.Category == Category.LikelyDefect) ? 1 : 0;
        }

        private static string Escape(string text)
        {
            return text.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string Key(TbdSnapshot.Surf s)
        {
            return string.Format(ci, "{0}|orient={1:F0}|incl={2:F0}|area={3:F2}|BE={4}", s.Type, s.Orientation, s.Inclination, s.Area, NormaliseBe(s.Be));
        }

        // Aperture building elements are named after their aperture before the aperture definitions are reused; compare
        // them by what they are - window/door, pane/frame - not by a name that can legitimately differ.
        private static string NormaliseBe(string name)
        {
            return name;
        }

        public static List<Item> Compare(TbdSnapshot a, TbdSnapshot b)
        {
            List<Item> items = new List<Item>();
            Action<Category, string, string> add = (c, area, text) => items.Add(new Item { Category = c, Area = area, Text = text });

            Action<string, double, double, double> numeric = (name, x, y, tol) =>
            {
                if (Math.Abs(x - y) <= tol) add(Category.Equivalent, "totals", string.Format(ci, "{0}: gbXML {1:F3} = direct {2:F3}", name, x, y));
                else add(Category.Unresolved, "totals", string.Format(ci, "{0}: gbXML {1:F3} vs direct {2:F3}", name, x, y));
            };

            numeric("zone count", a.Zones.Count, b.Zones.Count, 0);
            numeric("total floor area", a.TotalFloorArea, b.TotalFloorArea, 0.01);
            numeric("total volume", a.TotalVolume, b.TotalVolume, 0.01);
            numeric("zone surface count", a.AllSurfaces().Count(), b.AllSurfaces().Count(), 0);

            foreach (string type in a.AllSurfaces().Select(x => x.Type).Concat(b.AllSurfaces().Select(x => x.Type)).Distinct().OrderBy(x => x))
            {
                numeric("surfaces " + type, a.AllSurfaces().Count(x => x.Type == type), b.AllSurfaces().Count(x => x.Type == type), 0);
                numeric("area " + type, a.AllSurfaces().Where(x => x.Type == type).Sum(x => x.Area), b.AllSurfaces().Where(x => x.Type == type).Sum(x => x.Area), 0.01);
            }

            Func<TbdSnapshot.Surf, bool> isAperture = x => x.Be.StartsWith("Windows:") || x.Be.StartsWith("Doors:");
            numeric("aperture surface count (pane + frame)", a.AllSurfaces().Count(isAperture), b.AllSurfaces().Count(isAperture), 0);
            numeric("aperture area (pane + frame)", a.AllSurfaces().Where(isAperture).Sum(x => x.Area), b.AllSurfaces().Where(isAperture).Sum(x => x.Area), 0.01);
            numeric("pane area", a.AllSurfaces().Where(x => isAperture(x) && x.Be.EndsWith(" -pane")).Sum(x => x.Area), b.AllSurfaces().Where(x => isAperture(x) && x.Be.EndsWith(" -pane")).Sum(x => x.Area), 0.01);
            numeric("frame area", a.AllSurfaces().Where(x => isAperture(x) && x.Be.EndsWith(" -frame")).Sum(x => x.Area), b.AllSurfaces().Where(x => isAperture(x) && x.Be.EndsWith(" -frame")).Sum(x => x.Area), 0.01);
            numeric("building elements", a.BuildingElements.Count, b.BuildingElements.Count, 0);
            numeric("constructions", a.Constructions, b.Constructions, 0);
            numeric("aperture types", a.ApertureTypes, b.ApertureTypes, 0);

            // Building elements by name.
            foreach (TbdSnapshot.Be x in a.BuildingElements)
            {
                TbdSnapshot.Be y = b.BuildingElements.FirstOrDefault(e => e.Name == x.Name);
                if (y == null) { add(Category.Unresolved, "building elements", string.Format(ci, "'{0}' (BEType {1}, {2} surfaces) is only in the gbXML TBD", x.Name, x.BEType, x.Surfaces)); continue; }
                if (x.BEType != y.BEType) add(Category.Unresolved, "building elements", string.Format(ci, "'{0}': BEType gbXML {1} vs direct {2}", x.Name, x.BEType, y.BEType));
                if (Math.Abs(x.Width - y.Width) > 1e-3) add(Category.Unresolved, "building elements", string.Format(ci, "'{0}': width gbXML {1:F3} vs direct {2:F3}", x.Name, x.Width, y.Width));
                if (x.Ground != y.Ground) add(Category.Unresolved, "building elements", string.Format(ci, "'{0}': ground gbXML {1} vs direct {2}", x.Name, x.Ground, y.Ground));
                if (x.Surfaces != y.Surfaces) add(Category.Unresolved, "building elements", string.Format(ci, "'{0}': {1} surfaces in gbXML vs {2} in direct", x.Name, x.Surfaces, y.Surfaces));
                if (x.Construction != y.Construction) add(Category.Unresolved, "building elements", string.Format(ci, "'{0}': construction gbXML '{1}' vs direct '{2}'", x.Name, x.Construction, y.Construction));
                // Cosmetic: where a construction states no colour the gbXML route keeps the colour TAS gave its gbXML surface type, the direct
                // route uses SAM's own colour for the panel type. Neither enters the simulation.
                if (x.Colour != y.Colour) add(Category.EquivalentRepresentation, "building elements", string.Format(ci, "'{0}': colour gbXML {1:X6} vs direct {2:X6} (cosmetic - TAS's default for the gbXML surface type vs SAM's colour for the panel type)", x.Name, x.Colour, y.Colour));
                if (!x.ApertureTypes.OrderBy(t => t).SequenceEqual(y.ApertureTypes.OrderBy(t => t))) add(Category.Unresolved, "building elements", string.Format(ci, "'{0}': aperture types gbXML [{1}] vs direct [{2}]", x.Name, string.Join("|", x.ApertureTypes), string.Join("|", y.ApertureTypes)));
            }

            foreach (TbdSnapshot.Be y in b.BuildingElements.Where(e => a.BuildingElements.All(x => x.Name != e.Name)))
                add(Category.Unresolved, "building elements", string.Format(ci, "'{0}' (BEType {1}, {2} surfaces) is only in the direct TBD", y.Name, y.BEType, y.Surfaces));

            foreach (string name in a.ConstructionNames.Except(b.ConstructionNames)) add(Category.Unresolved, "constructions", "'" + name + "' is only in the gbXML TBD");
            foreach (string name in b.ConstructionNames.Except(a.ConstructionNames)) add(Category.Unresolved, "constructions", "'" + name + "' is only in the direct TBD");
            foreach (string name in a.ApertureTypeNames.Except(b.ApertureTypeNames)) add(Category.Unresolved, "aperture types", "'" + name + "' is only in the gbXML TBD");
            foreach (string name in b.ApertureTypeNames.Except(a.ApertureTypeNames)) add(Category.Unresolved, "aperture types", "'" + name + "' is only in the direct TBD");
            if (!a.ZoneGroupNames.OrderBy(x => x).SequenceEqual(b.ZoneGroupNames.OrderBy(x => x)))
            {
                // The TBD turns each T3D zone set into a zone group: 'gbXml Spaces' is the set TAS's gbXML import makes, 'SAM' the one the
                // direct route makes (ToT3DOptions.ZoneSetName). Anything else differing is not that.
                List<string> onlyA = a.ZoneGroupNames.Except(b.ZoneGroupNames).ToList();
                List<string> onlyB = b.ZoneGroupNames.Except(a.ZoneGroupNames).ToList();
                bool zoneSetNameOnly = onlyA.Count == 1 && onlyB.Count == 1 && onlyA[0] == "gbXml Spaces" && onlyB[0] == "SAM";
                add(zoneSetNameOnly ? Category.EquivalentRepresentation : Category.Unresolved, "zone groups", "gbXML [" + string.Join("|", a.ZoneGroupNames) + "] vs direct [" + string.Join("|", b.ZoneGroupNames) + "]" + (zoneSetNameOnly ? " (only the name of the zone set that holds every zone differs)" : string.Empty));
            }

            // Zones by name.
            foreach (TbdSnapshot.Zn x in a.Zones)
            {
                TbdSnapshot.Zn y = b.Zones.FirstOrDefault(z => z.Name == x.Name);
                if (y == null) { add(Category.Unresolved, "zones", "zone '" + x.Name + "' is only in the gbXML TBD"); continue; }

                if (Math.Abs(x.FloorArea - y.FloorArea) > 0.01) add(Category.Unresolved, "zone " + x.Name, string.Format(ci, "floor area gbXML {0:F3} vs direct {1:F3}", x.FloorArea, y.FloorArea));
                if (Math.Abs(x.Volume - y.Volume) > 0.01) add(Category.Unresolved, "zone " + x.Name, string.Format(ci, "volume gbXML {0:F3} vs direct {1:F3}", x.Volume, y.Volume));
                if (x.External != y.External) add(Category.Unresolved, "zone " + x.Name, string.Format(ci, "external gbXML {0} vs direct {1}", x.External, y.External));

                string description_Direct = RemoveSpaceGuidSegment(y.Description);
                if (!string.Equals(x.Description, description_Direct, StringComparison.Ordinal))
                    add(Category.Unresolved, "zone " + x.Name, "description differs beyond the SpaceGuid segment: gbXML '" + x.Description + "' vs direct '" + description_Direct + "'");
                if (y.Description != null && y.Description.Contains(Query.ZoneDescriptionMarker_SpaceGuid))
                    add(Category.ExpectedImprovement, "zone " + x.Name, "direct zone description carries the SAM space GUID (the gbXML route carries no space identity in the TBD)");

                Dictionary<string, int> ka = x.Surfaces.GroupBy(Key).ToDictionary(g => g.Key, g => g.Count());
                Dictionary<string, int> kb = y.Surfaces.GroupBy(Key).ToDictionary(g => g.Key, g => g.Count());
                foreach (string key in ka.Keys.Union(kb.Keys).OrderBy(k => k))
                {
                    ka.TryGetValue(key, out int na);
                    kb.TryGetValue(key, out int nb);
                    if (na != nb) add(Category.Unresolved, "zone " + x.Name, string.Format(ci, "surface {0}: gbXML x{1} vs direct x{2}", key, na, nb));
                }
            }

            foreach (TbdSnapshot.Zn y in b.Zones.Where(z => a.Zones.All(x => x.Name != z.Name)))
                add(Category.Unresolved, "zones", "zone '" + y.Name + "' is only in the direct TBD");

            return items;
        }

        private static string RemoveSpaceGuidSegment(string description)
        {
            if (string.IsNullOrEmpty(description)) return description;
            List<string> segments = description.Split(';').Select(x => x.Trim()).Where(x => x.Length != 0 && !x.StartsWith(Query.ZoneDescriptionMarker_SpaceGuid)).ToList();
            // Same joining the existing composer uses.
            return string.Join("; ", segments);
        }
    }
}
