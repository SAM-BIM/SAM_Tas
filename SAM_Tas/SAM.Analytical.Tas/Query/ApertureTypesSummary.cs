// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        /// <summary>
        /// The summary lines <c>Modify.SetApertureTypes</c> puts first in its notes. COM-free, so the diagnostic policy is
        /// unit-tested:
        /// <list type="bullet">
        /// <item>one plain line with every count, whatever happened;</item>
        /// <item>an <see cref="Modify.NotePrefix_Issue"/> line per gate that stopped an aperture (unmatched, no building element,
        /// refused write ...), saying how many were listed individually;</item>
        /// <item>an issue line when a requested availability schedule did not arrive.</item>
        /// </list>
        /// An aperture with no OpeningProperties requests no aperture control - a fixed window, an ordinary door - and is valid:
        /// it is in the plain line only (<paramref name="count_NoOpeningProperties"/>), never a gate, so a model of fixed windows
        /// gets one line rather than one warning per aperture (house model: 25).
        /// </summary>
        public static List<string> ApertureTypesSummary(int count_Seen, int count_Matched, int count_NoOpeningProperties, int count_Updated, int count_ScheduleRequested, int count_ScheduleWritten, IDictionary<string, int> counts_Gate, IDictionary<string, int> counts_GateReported)
        {
            List<string> result = [];
            result.Add(string.Format("Aperture types: {0} TBD apertures seen, {1} matched to their SAM aperture, {2} with no OpeningProperties (no aperture control requested - valid for fixed windows and doors), {3} updated, {4} opening(s) requested an availability schedule, {5} of those read a schedule back off the TBD profile.", count_Seen, count_Matched, count_NoOpeningProperties, count_Updated, count_ScheduleRequested, count_ScheduleWritten));

            if (counts_Gate != null)
            {
                foreach (KeyValuePair<string, int> keyValuePair in counts_Gate)
                {
                    int reported = 0;
                    counts_GateReported?.TryGetValue(keyValuePair.Key, out reported);
                    result.Add(string.Format("{0}Aperture types: {1} aperture(s) stopped at gate {2}{3}.", keyValuePair.Value == 0 ? string.Empty : Modify.NotePrefix_Issue, keyValuePair.Value, keyValuePair.Key, reported < keyValuePair.Value ? string.Format("; {0} listed individually above, the remaining {1} are counted only", reported, keyValuePair.Value - reported) : string.Empty));
                }
            }

            if (count_ScheduleRequested != count_ScheduleWritten)
            {
                result.Add(Modify.NotePrefix_Issue + string.Format("Aperture types: {0} of {1} requested availability schedules did NOT read one back off the TBD. The per-aperture lines above name which opening, and at which gate.", count_ScheduleRequested - count_ScheduleWritten, count_ScheduleRequested));
            }

            return result;
        }
    }
}
