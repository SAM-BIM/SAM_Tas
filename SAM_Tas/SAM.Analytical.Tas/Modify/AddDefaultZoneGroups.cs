// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Modify
    {
        public static List<TBD.ZoneGroup> AddDefaultZoneGroups(this TBD.Building building, AdjacencyCluster adjacencyCluster, string undefinedZoneGroupName = "Undefined", string allZoneGroupName = "All")
        {
            if (building == null || adjacencyCluster == null)
            {
                return null;
            }

            List<TBD.ZoneGroup> result = new List<TBD.ZoneGroup>();

            List<Space> spaces_Unzoned = adjacencyCluster.GetSpaces();

            // Pre-fetch TBD collections once. The original code re-enumerated them inside per-zone / per-space loops
            // (via building.ZoneGroups()?.Find(...) and building.Zone(name)) which made AddDefaultZoneGroups O(zones^2)
            // in COM round-trips. On a 125-space model this step took ~9 s.
            List<TBD.ZoneGroup> existingZoneGroups = building.ZoneGroups() ?? new List<TBD.ZoneGroup>();
            Dictionary<string, TBD.ZoneGroup> zoneGroupsByKey = new Dictionary<string, TBD.ZoneGroup>(existingZoneGroups.Count);
            foreach (TBD.ZoneGroup zg in existingZoneGroups)
            {
                if (!string.IsNullOrEmpty(zg?.name))
                    zoneGroupsByKey[zg.name + "|" + zg.type] = zg;
            }

            List<TBD.zone> tbdZones = building.Zones() ?? new List<TBD.zone>();
            Dictionary<string, TBD.zone> tbdZonesByName = new Dictionary<string, TBD.zone>(tbdZones.Count);
            foreach (TBD.zone z in tbdZones)
            {
                if (!string.IsNullOrEmpty(z?.name))
                    tbdZonesByName[z.name] = z;
            }

            // ALL zone group
            if (!string.IsNullOrEmpty(allZoneGroupName) && spaces_Unzoned != null && spaces_Unzoned.Count != 0)
            {
                string key = allZoneGroupName + "|" + (int)TBD.ZoneGroupType.tbdZoneSetZG;
                if (!zoneGroupsByKey.TryGetValue(key, out TBD.ZoneGroup zoneGroup) || zoneGroup == null)
                {
                    zoneGroup = CreateZoneGroupCached(building, allZoneGroupName, spaces_Unzoned, tbdZonesByName, TBD.ZoneGroupType.tbdZoneSetZG);
                    if (zoneGroup != null)
                        zoneGroupsByKey[key] = zoneGroup;
                }

                if (zoneGroup != null)
                    result.Add(zoneGroup);
            }

            // Per zone — and track all spaces that end up zoned so we can filter spaces_Unzoned in one pass at the end.
            List<Zone> zones = adjacencyCluster.GetZones();
            HashSet<System.Guid> zonedSpaceGuids = new HashSet<System.Guid>();
            if (zones != null && zones.Count != 0)
            {
                foreach (Zone zone in zones)
                {
                    Zone zone_Temp = adjacencyCluster.GetObject<Zone>(zone.Guid);
                    if (zone_Temp == null || string.IsNullOrEmpty(zone_Temp.Name))
                        continue;

                    List<Space> spacesInZone = adjacencyCluster.GetRelatedObjects<Space>(zone_Temp);

                    string key = zone_Temp.Name + "|" + (int)TBD.ZoneGroupType.tbdDefaultZG;
                    if (!zoneGroupsByKey.TryGetValue(key, out TBD.ZoneGroup zg) || zg == null)
                    {
                        zg = CreateZoneGroupCached(building, zone_Temp.Name, spacesInZone, tbdZonesByName, TBD.ZoneGroupType.tbdDefaultZG);
                        if (zg != null)
                            zoneGroupsByKey[key] = zg;
                    }
                    else
                    {
                        // Existing ZoneGroup with matching name — just add this zone's spaces.
                        AddSpacesToZoneGroup(zg, spacesInZone, tbdZonesByName);
                    }

                    if (zg != null && zone_Temp.TryGetValue(Analytical.ZoneParameter.ZoneCategory, out string zoneCategory))
                    {
                        zg.description = zoneCategory;
                    }

                    if (spacesInZone != null)
                    {
                        foreach (Space s in spacesInZone)
                        {
                            if (s != null)
                                zonedSpaceGuids.Add(s.Guid);
                        }
                    }
                }
            }

            // Single RemoveAll instead of N inside-the-loop RemoveAlls (was O(zones * spacesPerZone * unzonedCount)).
            if (spaces_Unzoned != null && zonedSpaceGuids.Count > 0)
            {
                spaces_Unzoned.RemoveAll(x => zonedSpaceGuids.Contains(x.Guid));
            }

            if (!string.IsNullOrEmpty(undefinedZoneGroupName) && spaces_Unzoned != null && spaces_Unzoned.Count != 0)
            {
                string key = undefinedZoneGroupName + "|" + (int)TBD.ZoneGroupType.tbdDefaultZG;
                if (!zoneGroupsByKey.TryGetValue(key, out TBD.ZoneGroup zg) || zg == null)
                {
                    zg = CreateZoneGroupCached(building, undefinedZoneGroupName, spaces_Unzoned, tbdZonesByName, TBD.ZoneGroupType.tbdDefaultZG);
                    if (zg != null)
                        zoneGroupsByKey[key] = zg;
                }

                if (zg != null)
                    result.Add(zg);
            }

            return result;
        }

        private static TBD.ZoneGroup CreateZoneGroupCached(TBD.Building building, string name, IEnumerable<Space> spaces, Dictionary<string, TBD.zone> tbdZonesByName, TBD.ZoneGroupType zoneGroupType)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            TBD.ZoneGroup zg = building.AddZoneGroup();
            zg.name = name;
            zg.type = (int)zoneGroupType;
            AddSpacesToZoneGroup(zg, spaces, tbdZonesByName);
            return zg;
        }

        private static void AddSpacesToZoneGroup(TBD.ZoneGroup zg, IEnumerable<Space> spaces, Dictionary<string, TBD.zone> tbdZonesByName)
        {
            if (zg == null || spaces == null)
                return;

            foreach (Space space in spaces)
            {
                if (string.IsNullOrWhiteSpace(space?.Name))
                    continue;
                if (!tbdZonesByName.TryGetValue(space.Name, out TBD.zone tbdZone) || tbdZone == null)
                    continue;
                zg.InsertZone(tbdZone);
            }
        }
    }
}