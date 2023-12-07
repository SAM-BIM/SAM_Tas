﻿using TPD;
using System.Linq;
using SAM.Core;
using System.Collections.Generic;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Convert
    {
        public static SpaceSystemCalculationResult ToSAM_SpaceSystemCalculationResult(this SystemZone systemZone, int start, int end, params FanCoilUnitDataType[] fanCoilUnitDataTypes)
        {
            if (systemZone == null)
            {
                return null;
            }

            ZoneLoad zoneLoad = systemZone.ZoneLoads()?.FirstOrDefault();
            if (zoneLoad == null)
            {
                return null;
            }

            IEnumerable<FanCoilUnitDataType> fanCoilUnitDataTypes_Temp = fanCoilUnitDataTypes == null ? System.Enum.GetValues(typeof(FanCoilUnitDataType)).Cast<FanCoilUnitDataType>() : fanCoilUnitDataTypes;

            Dictionary<string, IndexedDoubles> dictionary = new Dictionary<string, IndexedDoubles>();

            double heatingDuty = double.NaN;
            double coolingDuty = double.NaN;
            double designFlowRate = double.NaN;

            List<FanCoilUnit> fanCoilUnits = systemZone.ZoneComponents<ZoneComponent>()?.FindAll(x => x is FanCoilUnit)?.ConvertAll(x => (FanCoilUnit)x);
            if (fanCoilUnits != null)
            {
                foreach (FanCoilUnit fanCoilUnit in fanCoilUnits)
                {
                    if (fanCoilUnitDataTypes_Temp != null && fanCoilUnitDataTypes_Temp.Count() != 0)
                    {
                        foreach (FanCoilUnitDataType fanCoilUnitDataType in fanCoilUnitDataTypes_Temp)
                        {
                            IndexedDoubles indexedDoubles = Create.IndexedDoubles((ZoneComponent)fanCoilUnit, fanCoilUnitDataType, start, end);
                            if (indexedDoubles == null)
                            {
                                continue;
                            }

                            string uniqueId = Query.UniqueId(EquipmentType.FanCoilUnit, fanCoilUnitDataType);

                            if (!dictionary.TryGetValue(uniqueId, out IndexedDoubles indexedDoubles_Temp) || indexedDoubles_Temp == null)
                            {
                                dictionary[uniqueId] = indexedDoubles;
                            }
                            else
                            {
                                indexedDoubles_Temp.Sum(indexedDoubles);
                            }
                        }
                    }

                    heatingDuty = double.IsNaN(heatingDuty) ? System.Convert.ToDouble(fanCoilUnit.HeatingDuty) : heatingDuty + System.Convert.ToDouble(fanCoilUnit.HeatingDuty);
                    coolingDuty = double.IsNaN(coolingDuty) ? System.Convert.ToDouble(fanCoilUnit.CoolingDuty) : coolingDuty + System.Convert.ToDouble(fanCoilUnit.CoolingDuty);
                    designFlowRate = double.IsNaN(designFlowRate) ? System.Convert.ToDouble(fanCoilUnit.DesignFlowRate) : designFlowRate + System.Convert.ToDouble(fanCoilUnit.DesignFlowRate);
                }
            }

            return new SpaceSystemCalculationResult(zoneLoad.GUID, zoneLoad.Name, Query.Source(), zoneLoad.FloorArea, zoneLoad.Volume, heatingDuty, coolingDuty, designFlowRate, dictionary);
        }
    }
}