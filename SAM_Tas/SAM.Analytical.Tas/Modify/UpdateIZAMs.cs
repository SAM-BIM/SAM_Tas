﻿using SAM.Core.Tas;
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

            List<zone> zones = building.Zones();

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

            double elevation = 0;
            Geometry.Spatial.BoundingBox3D boundingBox3D = adjacencyCluster?.GetPanels()?.BoundingBox3D(1);
            if(boundingBox3D != null)
            {
                elevation = boundingBox3D.Min.Z - 1;
            }

            foreach (AirHandlingUnit airHandlingUnit in airHandlingUnits)
            {
                AirHandlingUnitAirMovement airHandlingUnitAirMovement = adjacencyCluster.GetRelatedObjects<AirHandlingUnitAirMovement>(airHandlingUnit)?.FirstOrDefault();
                if(airHandlingUnitAirMovement == null)
                {
                    continue;
                }

                AdjacencyCluster adjacencyCluster_Temp = Create.AdjacencyCluster(elevation);
                elevation += 4;

                Update(building, adjacencyCluster_Temp, Analytical.Query.DefaultMaterialLibrary(), true);

                Space space = adjacencyCluster_Temp.GetSpaces().FirstOrDefault();

                zones = building.Zones();
                zone zone = zones.Match(space.Name, false, true);


                //zone zone = building.AddZone();
                zone.name = airHandlingUnit.Name;

                TBD.InternalCondition internalCondition = building.AddIC(null);
                internalCondition.name = string.Format("{0}", airHandlingUnitAirMovement.Name);
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

                Profile airFlow = Analytical.Query.AirFlow(adjacencyCluster, airHandlingUnitAirMovement);
                if(airFlow != null)
                {
                    IZAM iZAM = building.AddIZAM(null);
                    iZAM.fromOutside = 1;
                    iZAM.name = string.Format("IZAM {0} FROM OUTSIDE", airHandlingUnitAirMovement.Name);
                    result.Add(iZAM.name);

                    foreach (dayType dayType in dayTypes)
                    {
                        iZAM.SetDayType(dayType, true);
                    }

                    profile profile = iZAM.GetProfile();
                    profile.Update(airFlow, 1);

                    zone.AssignIZAM(iZAM, true);
                }

            }

            List<Tuple<IZAM, SAMObject>> tuples = new List<Tuple<IZAM, SAMObject>>();
            foreach(Space space in spaces)
            {
                zone zone = zones.Match(space.Name, false, true);
                if (zone == null)
                {
                    continue;
                }

                List<SpaceAirMovement> spaceAirMovements = adjacencyCluster.GetRelatedObjects<SpaceAirMovement>(space);
                if (spaceAirMovements == null || spaceAirMovements.Count == 0)
                {
                    continue;
                }

                ObjectReference objectReference_Space = new ObjectReference(space);

                foreach(SpaceAirMovement spaceAirMovement in spaceAirMovements)
                {
                    IZAM iZAM = building.AddIZAM(null);

                    foreach (dayType dayType in dayTypes)
                    {
                        iZAM.SetDayType(dayType, true);
                    }

                    ObjectReference objectReference_From = Core.Convert.ComplexReference<ObjectReference>(spaceAirMovement.From);
                    ObjectReference objectReference_To = Core.Convert.ComplexReference<ObjectReference>(spaceAirMovement.To);

                    SAMObject sAMObject_From = adjacencyCluster.GetObjects<SAMObject>(objectReference_From)?.FirstOrDefault();
                    SAMObject sAMObject_To = adjacencyCluster.GetObjects<SAMObject>(objectReference_To)?.FirstOrDefault();

                    string name = string.Format("IZAM {0}", sAMObject_From.Name);
                    name = sAMObject_To == null ? string.Format("{0} TO OUTSIDE", name) : string.Format("{0} TO {1}", name, sAMObject_To.Name);
                    iZAM.name = name;
                    result.Add(iZAM.name);

                    profile profile = iZAM.GetProfile();
                    profile.Update(spaceAirMovement.AirFlow, 1);

                    zone.AssignIZAM(iZAM, true);

                    if(sAMObject_From != null)
                    {
                        zone zone_From = zones.Match(sAMObject_From.Name, false, true);
                        if(zone_From != null)
                        {
                            iZAM.fromOutside = 0;
                            iZAM.SetSourceZone(zone_From);
                        }
                    }
                }
            }

            return result;
        }
    }
}