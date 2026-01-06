// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        public static TBD.dayType DayType(this TBD.TBDDocument tBDDocument, string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            return DayType(tBDDocument?.Building?.GetCalendar(), name);
        }

        public static TBD.dayType DayType(this TBD.Calendar calendar, string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            if (calendar == null)
                return null;

            int index = 1;
            TBD.dayType dayType = calendar.dayTypes(index);
            while (dayType != null)
            {
                if (dayType.name.Equals(name))
                    return dayType;

                index++;
                dayType = calendar.dayTypes(index);
            }

            return null;
        }
    }
}
