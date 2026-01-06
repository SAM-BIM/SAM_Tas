// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using TPD;
using SAM.Analytical.Systems;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Convert
    {
        public static ScheduleMode ToSAM(this tpdOptimiserScheduleMode tpdOptimiserScheduleMode)
        {
            switch(tpdOptimiserScheduleMode)
            {
                case tpdOptimiserScheduleMode.tpdOptimiserScheduleRecirc:
                    return ScheduleMode.Recirc;

                case tpdOptimiserScheduleMode.tpdOptimiserScheduleNoMinimum:
                    return ScheduleMode.NoMinimum;

                case tpdOptimiserScheduleMode.tpdOptimiserScheduleThrough:
                    return ScheduleMode.Through;
            }

            throw new System.NotImplementedException();
        }
    }
}
