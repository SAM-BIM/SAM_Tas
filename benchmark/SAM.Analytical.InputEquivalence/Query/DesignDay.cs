// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;

namespace SAM.Analytical.InputEquivalence
{
    public static partial class Query
    {
        /// <summary>
        /// S07 row 10: the design day's expected <c>yearDay</c> (1..365/366) from the SAM date in its own year, so that
        /// a leap-year date after 28 February is not shifted by an unrelated reference year (the SAM_Tas reader's default
        /// reference year is 2018). Compared as an integer, never as a reconstructed date.
        /// </summary>
        public static ExpectedValue ExpectedYearDay(int year, int month, int day)
        {
            if (year < 1 || year > 9999 || month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
            {
                return ExpectedValue.Invalid("the design-day date is not a valid calendar date");
            }

            return ExpectedValue.FromValue(new DateTime(year, month, day).DayOfYear);
        }
    }
}
