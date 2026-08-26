// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Tas;
using System.Collections.Generic;
using TBD;
using System.Linq;
using SAM.Core;
using SAM.Geometry.Object.Spatial;
using System;

namespace SAM.Analytical.Tas
{
    public static partial class Modify
    {
        public static List<string> UpdateIZAMs(this string path_TBD, AdjacencyCluster adjacencyCluster)
        {
            if (string.IsNullOrWhiteSpace(path_TBD))
                return null;

            List<string> result = null;

            using (SAMTBDDocument sAMTBDDocument = new SAMTBDDocument(path_TBD))
            {
                result = UpdateIZAMs(sAMTBDDocument, adjacencyCluster);
                if (result != null)
                {
                    sAMTBDDocument.Save();
                }
            }

            return result;
        }

        public static List<string> UpdateIZAMs(this SAMTBDDocument sAMTBDDocument, AdjacencyCluster adjacencyCluster)
        {
            if (sAMTBDDocument == null)
            {
                return null;
            }

            return UpdateIZAMs(sAMTBDDocument.TBDDocument, adjacencyCluster);
        }

        public static List<string> UpdateIZAMs(this TBDDocument tBDDocument, AdjacencyCluster adjacencyCluster)
        {
            if(tBDDocument == null || adjacencyCluster == null)
            {
                return null;
            }

            Building building = tBDDocument.Building;

            List<Space> spaces = adjacencyCluster.GetSpaces();
            if(spaces == null)
            {
                return null;
            }

            List<AirHandlingUnit> airHandlingUnits = adjacencyCluster.GetObjects<AirHandlingUnit>();
            if(airHandlingUnits == null)
            {
                return null;
            }

            List<dayType> dayTypes = building.DayTypes();
            dayTypes.RemoveAll(x => x.name.Equals("CDD") || x.name.Equals("HDD"));

            List<string> result = new List<string>();

            double height = 2;

            double elevation = 0;
            Geometry.Spatial.BoundingBox3D boundingBox3D = adjacencyCluster?.GetPanels()?.BoundingBox3D(1);
            if(boundingBox3D != null)
            {
                elevation = boundingBox3D.Min.Z - height -  1;
            }

            // Pre-fetch and index zones once. Was being refetched (full COM rebuild) inside the AHU loop
            // AND linear-scanned via zones.Match() per AHU / per space / per movement.
            // Lookup key matches zones.Match(name, caseSensitive: false, trim: true) semantics.
            List<zone> zonesList = building.Zones() ?? new List<zone>();
            Dictionary<string, zone> zonesByKey = new Dictionary<string, zone>(zonesList.Count);
            foreach (zone z in zonesList)
            {
                string n = z?.name;
                if (!string.IsNullOrWhiteSpace(n))
                    zonesByKey[n.Trim().ToUpper()] = z;
            }

            // Pre-resolve everything the loops need so we can:
            //  (a) bulk-remove all to-be-replaced ICs and IZAMs in two calls (was per-iteration → O(N^2)),
            //  (b) avoid re-fetching GetRelatedObjects / GetObjects inside the modify loops.
            Dictionary<AirHandlingUnit, AirHandlingUnitAirMovement> ahuMovements = new Dictionary<AirHandlingUnit, AirHandlingUnitAirMovement>();
            HashSet<string> icNamesToReplace = new HashSet<string>();
            HashSet<string> izamNamesToReplace = new HashSet<string>();

            foreach (AirHandlingUnit ahu in airHandlingUnits)
            {
                AirHandlingUnitAirMovement m = adjacencyCluster.GetRelatedObjects<AirHandlingUnitAirMovement>(ahu)?.FirstOrDefault();
                if (m == null)
                    continue;
                ahuMovements[ahu] = m;
                if (!string.IsNullOrEmpty(m.Name))
                {
                    icNamesToReplace.Add(m.Name);
                    izamNamesToReplace.Add(string.Format("IZAM {0} FROM OUTSIDE", m.Name));
                }
            }

            // Resolved space-air-movement metadata, keyed by Space so the loop is O(spaces).
            Dictionary<Space, List<SpaceMovementInfo>> spaceMovements = new Dictionary<Space, List<SpaceMovementInfo>>();
            foreach (Space space in spaces)
            {
                List<SpaceAirMovement> sams = adjacencyCluster.GetRelatedObjects<SpaceAirMovement>(space);
                if (sams == null || sams.Count == 0)
                    continue;

                List<SpaceMovementInfo> infos = new List<SpaceMovementInfo>(sams.Count);
                foreach (SpaceAirMovement sam in sams)
                {
                    ObjectReference refFrom = Core.Convert.ComplexReference<ObjectReference>(sam.From);
                    ObjectReference refTo = Core.Convert.ComplexReference<ObjectReference>(sam.To);

                    SAMObject sFrom = adjacencyCluster.GetObjects<SAMObject>(refFrom)?.FirstOrDefault();
                    SAMObject sTo = adjacencyCluster.GetObjects<SAMObject>(refTo)?.FirstOrDefault();

                    if (sFrom == null)
                        continue;

                    string izamName = string.Format("IZAM {0}", sFrom.Name);
                    izamName = sTo == null ? string.Format("{0} TO OUTSIDE", izamName) : string.Format("{0} TO {1}", izamName, sTo.Name);

                    izamNamesToReplace.Add(izamName);
                    infos.Add(new SpaceMovementInfo { Movement = sam, From = sFrom, To = sTo, IZAMName = izamName });
                }
                if (infos.Count > 0)
                    spaceMovements[space] = infos;
            }

            // Single bulk remove instead of per-iteration removes (which each scanned the full IC/IZAM list).
            // Note: this assumes distinct names per AHU / movement — the original code would have only kept
            // the last-added when names collide; with bulk-remove + simple adds, colliding names would yield
            // duplicates. In practice these names derive from unique AirHandlingUnitAirMovement / SAMObject
            // names so collisions don't occur.
            if (icNamesToReplace.Count > 0)
                RemoveInternalConditions(building, icNamesToReplace);
            if (izamNamesToReplace.Count > 0)
                RemoveIZAMs(building, izamNamesToReplace);

            foreach (AirHandlingUnit airHandlingUnit in airHandlingUnits)
            {
                if (!ahuMovements.TryGetValue(airHandlingUnit, out AirHandlingUnitAirMovement airHandlingUnitAirMovement) || airHandlingUnitAirMovement == null)
                    continue;

                AdjacencyCluster adjacencyCluster_Temp = Create.AdjacencyCluster(elevation, 3, height, 3);
                elevation -= height - 1;

                int zoneCountBefore = zonesList.Count;
                Update(building, adjacencyCluster_Temp, Analytical.Query.DefaultMaterialLibrary(), true);

                Space space = adjacencyCluster_Temp.GetSpaces().FirstOrDefault();
                if (space == null || string.IsNullOrWhiteSpace(space.Name))
                    continue;

                // Refresh local zone tracking. Update() appends one new zone — pick it up and add to the dict
                // without doing a per-iteration zones.Match scan over hundreds of zones.
                zonesList = building.Zones() ?? new List<zone>();
                for (int i = zoneCountBefore; i < zonesList.Count; i++)
                {
                    zone newZ = zonesList[i];
                    string n = newZ?.name;
                    if (!string.IsNullOrWhiteSpace(n))
                        zonesByKey[n.Trim().ToUpper()] = newZ;
                }

                if (!zonesByKey.TryGetValue(space.Name.Trim().ToUpper(), out zone zone) || zone == null)
                    continue;

                zone.name = airHandlingUnit.Name;
                if (!string.IsNullOrWhiteSpace(airHandlingUnit.Name))
                    zonesByKey[airHandlingUnit.Name.Trim().ToUpper()] = zone;

                zone.sizeHeating = (int)TBD.SizingType.tbdSizing;

                string name = string.Format("{0}", airHandlingUnitAirMovement.Name);

                TBD.InternalCondition internalCondition = building.AddIC(null);
                internalCondition.name = name;
                foreach (dayType dayType in dayTypes)
                {
                    internalCondition.SetDayType(dayType, true);
                }

                Thermostat thermostat = internalCondition.GetThermostat();

                if(thermostat != null)
                {
                    Profile heating = airHandlingUnitAirMovement.Heating;
                    if (heating != null)
                    {
                        profile profile_TBD = thermostat.GetProfile((int)Profiles.ticLL);
                        if (profile_TBD != null)
                        {
                            Update(profile_TBD, heating, 1);
                        }
                    }

                    Profile cooling = airHandlingUnitAirMovement.Cooling;
                    if (cooling != null)
                    {
                        profile profile_TBD = thermostat.GetProfile((int)Profiles.ticUL);
                        if (profile_TBD != null)
                        {
                            Update(profile_TBD, cooling, 1);
                        }
                    }

                    Profile humidification = airHandlingUnitAirMovement.Humidification;
                    if (humidification != null)
                    {
                        profile profile_TBD = thermostat.GetProfile((int)Profiles.ticHLL);
                        if (profile_TBD != null)
                        {
                            Update(profile_TBD, humidification, 1);
                        }
                    }

                    Profile dehumidification = airHandlingUnitAirMovement.Dehumidification;
                    if (dehumidification != null)
                    {
                        profile profile_TBD = thermostat.GetProfile((int)Profiles.ticHUL);
                        if (profile_TBD != null)
                        {
                            Update(profile_TBD, dehumidification, 1);
                        }
                    }

                    zone.AssignIC(internalCondition, true);
                }

                double airFlow = Analytical.Query.AirFlow(adjacencyCluster, airHandlingUnitAirMovement, out Profile profile_AirHandlingUnit);
                if(profile_AirHandlingUnit != null)
                {
                    name = string.Format("IZAM {0} FROM OUTSIDE", airHandlingUnitAirMovement.Name);

                    IZAM iZAM = building.AddIZAM(null);
                    iZAM.fromOutside = 1;
                    iZAM.name = name;
                    result.Add(iZAM.name);

                    foreach (dayType dayType in dayTypes)
                    {
                        iZAM.SetDayType(dayType, true);
                    }

                    profile profile = iZAM.GetProfile();
                    profile.Update(profile_AirHandlingUnit, airFlow);

                    zone.AssignIZAM(iZAM, true);
                }

            }

            foreach(Space space in spaces)
            {
                if (string.IsNullOrWhiteSpace(space?.Name))
                    continue;
                if (!zonesByKey.TryGetValue(space.Name.Trim().ToUpper(), out zone zone) || zone == null)
                    continue;

                zone.sizeHeating = (int)TBD.SizingType.tbdNoSizing;

                if (!spaceMovements.TryGetValue(space, out List<SpaceMovementInfo> movements) || movements == null)
                    continue;

                foreach (SpaceMovementInfo info in movements)
                {
                    // The zone the movement delivers INTO.
                    //
                    // A TBD inter-zone air movement only ever moves air INTO the zones it is assigned to,
                    // from a source zone or from outside - TBD.IIZAM has a source zone and target zones and
                    // a fromOutside flag, and no outward direction of any kind. So the target has to be read
                    // from the movement's To endpoint rather than assumed to be the space the movement is
                    // related to. An EXTRACT movement is "room -> air handling unit": an IZAM on the UNIT's
                    // zone, sourced from the room. Writing it onto the room's own zone with neither a source
                    // zone nor fromOutside - which is what a To of null produces, and what every movement
                    // produced before this - is an IZAM that moves no air at all.
                    //
                    // Where To does not resolve to a zone, including where it is null, the target is the
                    // space's own zone, which is exactly the behaviour every existing caller relies on: a
                    // supply movement's To IS the space, so it resolves to the same zone either way.
                    zone zone_Target = zone;
                    string key_Target = space.Name.Trim().ToUpper();

                    if (info.To != null && !string.IsNullOrWhiteSpace(info.To.Name))
                    {
                        string key_To = info.To.Name.Trim().ToUpper();
                        if (zonesByKey.TryGetValue(key_To, out zone zone_To) && zone_To != null)
                        {
                            zone_Target = zone_To;
                            key_Target = key_To;
                        }
                    }

                    IZAM iZAM = building.AddIZAM(null);

                    foreach (dayType dayType in dayTypes)
                    {
                        iZAM.SetDayType(dayType, true);
                    }

                    iZAM.name = info.IZAMName;
                    iZAM.fromOutside = 0;
                    result.Add(iZAM.name);

                    profile profile = iZAM.GetProfile();
                    profile.Update(info.Movement.Profile, info.Movement.AirFlow);

                    zone_Target.AssignIZAM(iZAM, true);

                    // Compared by resolved zone key rather than by COM object identity: two runtime callable
                    // wrappers over the same TAS zone are not reference-equal.
                    if (info.From != null && !string.IsNullOrWhiteSpace(info.From.Name))
                    {
                        string key_From = info.From.Name.Trim().ToUpper();
                        if (key_From != key_Target && zonesByKey.TryGetValue(key_From, out zone zoneFrom) && zoneFrom != null)
                        {
                            iZAM.SetSourceZone(zoneFrom);
                        }
                    }
                }
            }

            return result;
        }

        private sealed class SpaceMovementInfo
        {
            public SpaceAirMovement Movement;
            public SAMObject From;
            public SAMObject To;
            public string IZAMName;
        }
    }
}