// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Tas
{
    public static partial class Create
    {
        /// <summary>
        /// Creates a new TBD schedule under the given name, writing up to
        /// <see cref="Query.ScheduleHourCount"/> values.
        /// <para>
        /// <b>Legacy route - new code should use
        /// <see cref="GetOrCreateSchedule(TBD.Building, DailyAvailabilitySchedule, out string)"/> instead.</b>
        /// This one always creates a schedule rather than reusing a value-identical existing one, and
        /// tolerates fewer than 24 supplied values, because <c>Modify.AssignApertureTypes</c> and
        /// <c>Modify.New.AssignOpeningTypes</c> depend on both behaviours for the
        /// <c>SAM_ApertureScheduleDay</c>/<c>Night</c> construction parameters. Those semantics are
        /// unchanged here on purpose.
        /// </para>
        /// <para>
        /// What did change: the value write itself now goes through
        /// <see cref="Modify.SetScheduleValues(TBD.schedule, IEnumerable{int}, out string)"/>, so this
        /// assembly has exactly one schedule-writing implementation - and one read-back verification - rather
        /// than two independent ones.
        /// </para>
        /// </summary>
        public static TBD.schedule Schedule(this TBD.Building building, string name, IEnumerable<int> values = null)
        {
            if (building == null || string.IsNullOrEmpty(name))
                return null;

            TBD.schedule result = building.AddSchedule();
            if (result == null)
                return null;

            result.name = name;

            if (values == null)
                return result;

            List<int> values_Temp = values.Take(Query.ScheduleHourCount).ToList();
            if (values_Temp.Count == 0)
                return result;

            result.SetScheduleValues(values_Temp, out string _);

            return result;
        }
    }
}
