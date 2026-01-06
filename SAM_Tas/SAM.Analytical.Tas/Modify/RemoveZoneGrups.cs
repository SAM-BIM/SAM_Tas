// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas
{
    public static partial class Modify
    {
        public static bool RemoveZoneGroups(this TBD.Building building)
        {
            if (building == null)
                return false;

            bool result = false;

            TBD.ZoneGroup zoneGroup = building.GetZoneGroup(0);
            while (zoneGroup != null)
            {
                building.RemoveZoneGroup(0);
                zoneGroup = building.GetZoneGroup(0);
            }

            return result;
        }
    }
}
