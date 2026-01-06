// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using TSD;

namespace SAM.Core.Tas
{
    public static partial class Query
    {
        public static double Volume(this BuildingData buildingData)
        {
            if(buildingData is null)
            {
                return double.NaN;
            }

            double result = 0;

            int index = 1;
            ZoneData zoneData = buildingData.GetZoneData(index);
            while (zoneData != null)
            {
                result += zoneData.volume;
                index++;
                zoneData = buildingData.GetZoneData(index);
            }

            return result;
        }
    }
}
