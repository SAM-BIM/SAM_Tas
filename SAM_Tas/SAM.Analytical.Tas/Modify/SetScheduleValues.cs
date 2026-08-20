// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Tas
{
    public static partial class Modify
    {
        /// <summary>
        /// <b>The one place in this assembly that writes <c>TBD.schedule</c> values</b>, and the one place
        /// that verifies the write actually took.
        /// <para>
        /// <b>On the two spellings of the write.</b> <c>schedule.values[i] = x</c> and
        /// <c>schedule.set_values(i, x)</c> are the same operation, not a correct one and a defective one:
        /// C# supports indexed properties only for COM interop types and lowers both to
        /// <c>TBD.ISchedule::set_values(int32, int32)</c>. Verified by disassembling this assembly's own IL,
        /// where the two former call sites emitted the identical instruction. Neither spelling was the cause
        /// of a schedule that reached a real TBD holding 24 zeros.
        /// </para>
        /// <para>
        /// <b>Why the read-back is not optional.</b> A TBD schedule that persists its name while losing its
        /// values is indistinguishable, downstream, from a schedule that was meant to be all-zero - and
        /// <c>TBD.Building</c> exposes no <c>RemoveSchedule</c>, so a bad schedule cannot be cleaned up
        /// afterwards. Reading all written indices straight back is 24 cheap COM calls and converts that
        /// class of silent corruption into a reported refusal.
        /// </para>
        /// </summary>
        /// <param name="values">
        /// The values to write, at indices 0..n-1. Callers writing an availability schedule pass exactly
        /// <see cref="Query.ScheduleHourCount"/> values; the legacy <c>Create.Schedule</c> route may pass
        /// fewer, and then only the indices it wrote are verified.
        /// </param>
        /// <param name="refusal">Why the write is not trustworthy, or null on success.</param>
        public static bool SetScheduleValues(this TBD.schedule schedule, IEnumerable<int> values, out string refusal)
        {
            refusal = null;

            if (schedule == null)
            {
                refusal = "No TBD schedule to write to.";
                return false;
            }

            if (values == null)
            {
                refusal = "No schedule values to write.";
                return false;
            }

            int[] array = values.ToArray();
            if (array.Length == 0)
            {
                refusal = "No schedule values to write.";
                return false;
            }

            if (array.Length > Query.ScheduleHourCount)
            {
                refusal = string.Format("A TBD schedule holds at most {0} hourly values; {1} were supplied.", Query.ScheduleHourCount, array.Length);
                return false;
            }

            for (int hour = 0; hour < array.Length; hour++)
            {
                schedule.set_values(hour, array[hour]);
            }

            //Read every written index back through the same COM object. A mismatch here is the exact
            //failure that previously reached a real TBD unreported.
            for (int hour = 0; hour < array.Length; hour++)
            {
                int value = schedule.get_values(hour);
                if (value != array[hour])
                {
                    refusal = string.Format("TBD schedule '{0}' did not persist its values: hour {1} was written as {2} but reads back as {3}. The schedule was left unassigned rather than exporting a control that does not match the model.", schedule.name, hour, array[hour], value);
                    return false;
                }
            }

            return true;
        }
    }
}
