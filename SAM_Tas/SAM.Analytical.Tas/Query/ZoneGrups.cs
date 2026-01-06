// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        public static List<TBD.ZoneGroup> ZoneGroups(this TBD.Building building)
        {
            if (building == null)
                return null;

            List<TBD.ZoneGroup> result = new List<TBD.ZoneGroup>();

            TBD.ZoneGroup zoneGroup = building.GetZoneGroup(result.Count);
            while (zoneGroup != null)
            {
                result.Add(zoneGroup);
                zoneGroup = building.GetZoneGroup(result.Count);
            }

            return result;
        }
    }
}
