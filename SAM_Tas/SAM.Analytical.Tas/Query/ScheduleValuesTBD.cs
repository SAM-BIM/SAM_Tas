// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        /// <summary>
        /// All <see cref="ScheduleHourCount"/> hourly values of a TBD schedule, or null when it cannot be
        /// read. <c>TBD.ISchedule</c> has no count member, so the 24 indices are read explicitly.
        /// </summary>
        public static int[] HourlyValues(this TBD.schedule schedule)
        {
            if (schedule == null)
            {
                return null;
            }

            int[] result = new int[ScheduleHourCount];
            try
            {
                for (int hour = 0; hour < ScheduleHourCount; hour++)
                {
                    result[hour] = schedule.get_values(hour);
                }
            }
            catch
            {
                return null;
            }

            return result;
        }

        /// <summary>
        /// A TBD schedule read back as a SAM <see cref="DailyAvailabilitySchedule"/>, or null when its
        /// values are not binary.
        /// <para>
        /// A general-valued schedule - which only a user-authored TAS model can produce - is deliberately
        /// NOT coerced into a binary one. The TBD -&gt; SAM conversion keeps it as the legacy general-valued
        /// <see cref="ProfileOpeningProperties.Profile"/> instead, so a round trip does not change what the
        /// user authored.
        /// </para>
        /// </summary>
        public static DailyAvailabilitySchedule DailyAvailabilitySchedule(this TBD.schedule schedule)
        {
            int[] values = HourlyValues(schedule);
            if (values == null)
            {
                return null;
            }

            bool[] binaryValues = new bool[ScheduleHourCount];
            for (int hour = 0; hour < ScheduleHourCount; hour++)
            {
                if (values[hour] != 0 && values[hour] != 1)
                {
                    return null;
                }

                binaryValues[hour] = values[hour] == 1;
            }

            string name = string.IsNullOrWhiteSpace(schedule.name) ? null : schedule.name;
            return new DailyAvailabilitySchedule(name, binaryValues);
        }

        /// <summary>
        /// Every TBD schedule in the building paired with its <see cref="ScheduleHourCount"/> values, in
        /// building order - the input to value-based reuse.
        /// </summary>
        public static List<KeyValuePair<TBD.schedule, int[]>> SchedulesWithValues(this TBD.Building building)
        {
            List<KeyValuePair<TBD.schedule, int[]>> result = new List<KeyValuePair<TBD.schedule, int[]>>();

            List<TBD.schedule> schedules = building?.Schedules();
            if (schedules == null)
            {
                return result;
            }

            foreach (TBD.schedule schedule in schedules)
            {
                result.Add(new KeyValuePair<TBD.schedule, int[]>(schedule, HourlyValues(schedule)));
            }

            return result;
        }
    }
}
