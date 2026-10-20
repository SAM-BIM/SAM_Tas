// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.InputEquivalence
{
    public enum ScheduleExpansionStatus
    {
        Ok,

        /// <summary>The raw profile has a length S07 does not define (only 1, 24 and 8760 values are expanded).</summary>
        UndefinedLength,

        ContainsNaN,

        /// <summary>8784 values: the leap-year handling is unverified and the Tas weather writer keeps 365 days only. Never silently truncated.</summary>
        UnsupportedLeapYear
    }

    /// <summary>A SAM profile expanded to the 8760 hours of a non-leap year, or the reason it could not be.</summary>
    public sealed class ExpandedSchedule
    {
        internal ExpandedSchedule(ScheduleExpansionStatus status, double[] hours, string detail)
        {
            Status = status;
            Hours = hours;
            Detail = detail;
        }

        public ScheduleExpansionStatus Status { get; }

        /// <summary>The 8760 hourly values (index 0 = 1 January 00:00 to 01:00 in SAM's hour-beginning convention); null unless <see cref="IsOk"/>.</summary>
        public double[] Hours { get; }

        public string Detail { get; }

        public bool IsOk => Status == ScheduleExpansionStatus.Ok;
    }

    /// <summary>
    /// Schedule expansion and hourly comparison that need no TAS. The slot mapping is the <b>writer's</b> convention;
    /// it is not evidence that TAS reads the slot as intended, which only S07 section 7 verification can show.
    /// </summary>
    public static partial class Query
    {
        public const int HoursPerYear = 8760;
        public const int HoursPerLeapYear = 8784;
        public const int HoursPerDay = 24;

        /// <summary>
        /// Expands raw profile values: 1 value is constant, 24 values repeat daily from 1 January, 8760 values are
        /// taken as given. Any other length is undefined by S07; 8784 is reported as an unsupported leap year.
        /// </summary>
        public static ExpandedSchedule ExpandSchedule(IReadOnlyList<double> values)
        {
            if (values == null || values.Count == 0)
            {
                return new ExpandedSchedule(ScheduleExpansionStatus.UndefinedLength, null, "the profile has no values");
            }

            if (values.Count == HoursPerLeapYear)
            {
                return new ExpandedSchedule(ScheduleExpansionStatus.UnsupportedLeapYear, null, "8784 hourly values: leap-year handling is unverified and not truncated");
            }

            if (values.Count != 1 && values.Count != HoursPerDay && values.Count != HoursPerYear)
            {
                return new ExpandedSchedule(ScheduleExpansionStatus.UndefinedLength, null, values.Count + " values: only 1, 24 and 8760 are defined");
            }

            if (values.Any(x => double.IsNaN(x) || double.IsInfinity(x)))
            {
                return new ExpandedSchedule(ScheduleExpansionStatus.ContainsNaN, null, "the profile contains NaN or an infinite value");
            }

            double[] hours = new double[HoursPerYear];
            for (int i = 0; i < HoursPerYear; i++)
            {
                hours[i] = values.Count == 1 ? values[0] : values.Count == HoursPerDay ? values[i % HoursPerDay] : values[i];
            }

            return new ExpandedSchedule(ScheduleExpansionStatus.Ok, hours, null);
        }

        /// <summary>
        /// EFLH as the sum of the hourly values (h), valid for fraction profiles whose value is already the fraction of
        /// the gain factor. Supporting evidence only: an EFLH match never proves hourly alignment (S07 section 7).
        /// </summary>
        public static ExpectedValue ExpectedEquivalentFullLoadHours(ExpandedSchedule schedule)
        {
            if (schedule == null || !schedule.IsOk)
            {
                return ExpectedValue.Invalid("the schedule could not be expanded: " + schedule?.Detail);
            }

            return ExpectedValue.FromValue(schedule.Hours.Sum());
        }

        /// <summary>The expected hourly series: each expanded hour times a scale (for example the profile factor). Invalid when the schedule is not expandable.</summary>
        public static IReadOnlyList<ExpectedValue> ExpectedHourlySeries(ExpandedSchedule schedule, double scale = 1)
        {
            if (schedule == null || !schedule.IsOk)
            {
                return new[] { ExpectedValue.Invalid("the schedule could not be expanded: " + schedule?.Detail) };
            }

            return schedule.Hours.Select(x => ExpectedValue.FromValue(x * scale)).ToList();
        }

        /// <summary>The TBD hourly slot the writer uses for SAM hour h: <c>h + 1</c> (1-based hour-ending). A writer convention, not a verified native fact.</summary>
        public static int WriterSlotForHour(int hour)
        {
            return hour + 1;
        }

        /// <summary>The inverse of <see cref="WriterSlotForHour"/>.</summary>
        public static int WriterHourForSlot(int slot)
        {
            return slot - 1;
        }

        /// <summary>
        /// Evaluates a schedule that could not be expanded. An 8784-hour profile is a contract-style UNSUPPORTED-MAPPING
        /// (rank 3 until the owner accepts it, never a pass, never truncated); any other failure is S-INVALID. Returns
        /// null when the schedule is fine.
        /// </summary>
        public static QuantityResult EvaluateScheduleExpansion(ExpandedSchedule schedule, QuantityInput prototype, GateConfiguration configuration = null)
        {
            if (prototype == null)
            {
                throw new ArgumentNullException(nameof(prototype));
            }

            if (schedule == null || schedule.IsOk)
            {
                return null;
            }

            if (schedule.Status != ScheduleExpansionStatus.UnsupportedLeapYear)
            {
                return Evaluate(With(prototype, ExpectedValue.Invalid(schedule.Detail), ReaderResult.Absent("no 8760-hour series exists"), prototype.ObjectKey, prototype.Listing), configuration);
            }

            // The SAM series exists (8784 values) but the Tas route cannot carry it: S-VALUE with an absent native object under a listed limitation.
            return Evaluate(With(prototype, ExpectedValue.FromValue(HoursPerLeapYear), ReaderResult.Absent("the Tas route keeps 365 days"), prototype.ObjectKey, ContractListing.UnsupportedMapping), configuration);
        }

        /// <summary>
        /// Diagnoses a mis-alignment: the cyclic shifts <c>s</c> in [-maxShift, maxShift] for which
        /// <c>native[(i + s) mod n]</c> equals <c>expected[i]</c> within the rule for every hour. Shift 0 is returned
        /// when the series are aligned. An empty result means no shift explains the difference.
        /// </summary>
        public static IReadOnlyList<int> FindHourShifts(IReadOnlyList<double> expected, IReadOnlyList<double> native, ToleranceRule rule, int maxShift = 3)
        {
            List<int> result = new List<int>();
            if (expected == null || native == null || expected.Count == 0 || expected.Count != native.Count || rule == null)
            {
                return result;
            }

            int n = expected.Count;
            for (int s = -maxShift; s <= maxShift; s++)
            {
                bool all = true;
                for (int i = 0; i < n && all; i++)
                {
                    int j = ((i + s) % n + n) % n;
                    all = Compare(expected[i], native[j], rule).IsWithin;
                }

                if (all)
                {
                    result.Add(s);
                }
            }

            return result;
        }
    }
}
