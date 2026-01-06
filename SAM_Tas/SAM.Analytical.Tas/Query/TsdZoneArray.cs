// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using TSD;

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        public static tsdZoneArray? TsdZoneArray(this SpaceDataType spaceDataType)
        {
            if (spaceDataType == Tas.SpaceDataType.Undefined)
                return null;

            return (tsdZoneArray)(int)spaceDataType;
        }
    }
}
