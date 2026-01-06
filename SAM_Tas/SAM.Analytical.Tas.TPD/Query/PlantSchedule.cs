// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Query
    {
        public static global::TPD.PlantSchedule PlantSchedule(this global::TPD.EnergyCentre energyCentre, string name)
        {
            if (energyCentre is null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            for (int i = 1; i <= energyCentre.GetScheduleCount(); i++)
            {
                global::TPD.PlantSchedule plantSchedule = energyCentre.GetSchedule(i);
                if(plantSchedule == null)
                {
                    continue;
                }

                if(name.Equals((plantSchedule as dynamic).Name))
                {
                    return plantSchedule;
                }
            }

            return null;
        }
    }
}
