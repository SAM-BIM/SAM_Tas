// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Query
    {
        public static global::TPD.DHWGroup DHWGroup(this global::TPD.PlantRoom plantRoom, string name)
        {
            if (plantRoom is null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            for (int i = 1; i <= plantRoom.GetDHWGroupCount(); i++)
            {
                global::TPD.DHWGroup dHWGroup = plantRoom.GetDHWGroup(i);
                if(dHWGroup == null)
                {
                    continue;
                }

                if(name.Equals((dHWGroup as dynamic).Name))
                {
                    return dHWGroup;
                }
            }

            return null;
        }
    }
}
