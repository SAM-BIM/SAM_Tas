// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas
{
    public static partial class Modify
    {
        public static bool RemoveDayTypes(this TBD.Calendar calendar)
        {
            if (calendar == null)
                return false;

            bool result = false;
            while (calendar.GetDayTypeCount() != 0)
            {
                calendar.RemoveDayType(1);
                result = true;
            }

            return result;
        }
    }
}
