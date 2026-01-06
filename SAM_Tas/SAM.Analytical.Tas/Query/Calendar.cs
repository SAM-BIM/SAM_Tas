// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        public static TCR.Calendar Calendar(this TCR.Document document, string name, Core.TextComparisonType textComparisonType = Core.TextComparisonType.Equals, bool caseSensitive = true)
        {
            List<TCR.Calendar> calendars = document?.Calendars();
            if(calendars == null)
            {
                return null;
            }

            return calendars.Find(x => Core.Query.Compare(x.name, name, textComparisonType, caseSensitive));
        }
    }
}
