// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using TSD;

namespace SAM.Analytical.Tas
{
    public static partial class Query
    { 
        public static T HourlySurfaceResult<T>(this SurfaceData surfaceData, tsdSurfaceArray tsdSurfaceArray, int index, T @default = default)
        {
            if(surfaceData == null || index == -1)
            {
                return @default;
            }

            float value = surfaceData.GetHourlySurfaceResult(index, (int)tsdSurfaceArray);
            if (!Core.Query.TryConvert(value, out T result))
            {
                return @default;
            }

            return result;
        }

        public static T HourlySurfaceResult<T>(this SurfaceData surfaceData, PanelDataType panelDataType, int index, T @default = default)
        {
            if (surfaceData == null || panelDataType == Tas.PanelDataType.Undefined)
            {
                return @default;
            }

            return HourlySurfaceResult<T>(surfaceData, panelDataType.TsdSurfaceArray().Value, index);
        }
    }
}
