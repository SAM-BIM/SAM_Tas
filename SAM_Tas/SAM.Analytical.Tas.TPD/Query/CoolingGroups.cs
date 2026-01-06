// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Query
    {
        public static List<global::TPD.CoolingGroup> CoolingGroups(this global::TPD.PlantRoom plantRoom)
        {
            if (plantRoom is null)
            {
                return null;
            }

            List<global::TPD.CoolingGroup> result = new List<global::TPD.CoolingGroup>();
            for (int i = 1; i <= plantRoom.GetCoolingGroupCount(); i++)
            {
                global::TPD.CoolingGroup coolingGroup = plantRoom.GetCoolingGroup(i);
                if(coolingGroup == null)
                {
                    continue;
                }

                result.Add(coolingGroup);
            }

            return result;
        }
    }
}
