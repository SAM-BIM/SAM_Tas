// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Text.Json.Nodes;
using SAM.Core;
using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Convert
    {
        public static Space ToSAM(this TAS3D.Zone zone)
        {
            if (zone == null)
                return null;

            ParameterSet parameterSet = Create.ParameterSet(ActiveSetting.Setting, zone);

            Space space = new Space(zone.name, null);
            space.Add(parameterSet);

            return space;
        }

        public static Space ToSAM(this TSD.ZoneData zoneData, IEnumerable<SpaceDataType> spaceDataTypes = null)
        {
            ParameterSet parameterSet = Create.ParameterSet_Space(ActiveSetting.Setting, zoneData);

            if(spaceDataTypes != null)
            {
                foreach(SpaceDataType spaceDataType in spaceDataTypes)
                {
                    List<double> values = zoneData.AnnualZoneResult<double>(spaceDataType);
                    if (values == null)
                        continue;

                    JsonArray jArray = new JsonArray();
                    values.ForEach(x => jArray.Add(x));

                    parameterSet.Add(spaceDataType.Text(), jArray);
                }
            }

            Space space = new Space(zoneData.name, null);
            space.Add(parameterSet);

            // Stamp the zone identity explicitly, as the TBD conversion below does. TAS retains the TBD zone's
            // guid on the zone in the TSD, so this is the same identity the design side carries - the
            // surface-result attachment in Modify.AddResults already relies on that equality.
            // The generic Create.ParameterSet_Space above cannot supply it: the TypeMap entry is registered,
            // but the mapper reads the source property by the SAM-side parameter name ("Zone Guid") rather
            // than the TAS-side one ("zoneGUID"), so the value is silently never carried across.
            // Blank stays blank - a space with no stated identity must fall back to the unique-name rule in
            // SimulationSpaceMap rather than state an empty key, which that class refuses instead of matching.
            if (!string.IsNullOrWhiteSpace(zoneData.zoneGUID))
            {
                space.SetValue(SpaceParameter.ZoneGuid, zoneData.zoneGUID);
            }

            return space;
        }

        public static Space ToSAM(this TBD.zone zone, out List<InternalCondition> internalConditions)
        {
            internalConditions = null;

            if(zone == null)
            {
                return null;
            }

            Space result = new Space(zone.name);

            double area = zone.floorArea;

            result.SetValue(Analytical.SpaceParameter.Area, area);
            result.SetValue(Analytical.SpaceParameter.Volume, zone.volume);
            result.SetValue(SpaceParameter.ZoneGuid, zone.GUID);

            // Round-trip the TBD zone colour (export writes it back from SpaceParameter.Color).
            result.SetValue(Analytical.SpaceParameter.Color, new SAMColor(zone.colour.ToColor()));

            List<TBD.InternalCondition> internalConditions_TBD = zone.InternalConditions();
            if(internalConditions_TBD != null)
            {
                internalConditions = new List<InternalCondition>();
                
                foreach(TBD.InternalCondition internalCondition_TBD in internalConditions_TBD)
                {
                    InternalCondition internalCondition = internalCondition_TBD.ToSAM(area);
                    if(internalCondition == null)
                    {
                        continue;
                    }

                    internalConditions.Add(internalCondition);
                }
            }

            return result;
        }
    }
}
