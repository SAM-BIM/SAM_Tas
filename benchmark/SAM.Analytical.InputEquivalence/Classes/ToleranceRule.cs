// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;

namespace SAM.Analytical.InputEquivalence
{
    /// <summary>
    /// The S07 section 5 numeric rule for one quantity: with <c>d = |n - s|</c> a value is within tolerance when
    /// <c>d &lt;= abs</c> OR <c>d / max(|n|,|s|) &lt;= rel</c>. A criterion that S07 marks "-" is absent (null) and
    /// then does not apply. At least one criterion must exist; the tolerance is never invented or relaxed here.
    /// </summary>
    public sealed class ToleranceRule
    {
        private ToleranceRule(double? relative, double? absolute, string unit)
        {
            Relative = relative;
            Absolute = absolute;
            Unit = unit;
        }

        /// <summary>The relative criterion (fraction, e.g. 0.005 for 0.5 %), or null when S07 gives none.</summary>
        public double? Relative { get; }

        /// <summary>The absolute criterion in <see cref="Unit"/>, or null when S07 gives none.</summary>
        public double? Absolute { get; }

        public string Unit { get; }

        /// <param name="relative">Fraction, not percent. Null when S07 gives no relative criterion.</param>
        /// <param name="absolute">In <paramref name="unit"/>. Null when S07 gives no absolute criterion.</param>
        public static ToleranceRule Create(double? relative, double? absolute, string unit)
        {
            if (relative == null && absolute == null)
            {
                throw new ArgumentException("A tolerance needs a relative or an absolute criterion; none is invented.");
            }

            if ((relative != null && (double.IsNaN(relative.Value) || double.IsInfinity(relative.Value) || relative.Value < 0)) ||
                (absolute != null && (double.IsNaN(absolute.Value) || double.IsInfinity(absolute.Value) || absolute.Value < 0)))
            {
                throw new ArgumentOutOfRangeException(nameof(relative), "A tolerance criterion must be finite and not negative.");
            }

            return new ToleranceRule(relative, absolute, unit);
        }

        /// <summary>The S07 "exact" rows (names, month and day): an absolute criterion of 0.</summary>
        public static ToleranceRule Exact(string unit)
        {
            return new ToleranceRule(null, 0, unit);
        }

        public override string ToString()
        {
            return "rel=" + (Relative.HasValue ? Relative.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : "-") +
                   " abs=" + (Absolute.HasValue ? Absolute.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : "-") +
                   (string.IsNullOrEmpty(Unit) ? string.Empty : " " + Unit);
        }
    }

    /// <summary>The result of the numeric test; see <see cref="Query.Compare(double, double, ToleranceRule)"/>.</summary>
    public enum NumericVerdict
    {
        /// <summary>d == 0.</summary>
        Identical,

        /// <summary>d &gt; 0 and d &lt;= abs or d / max(|n|,|s|) &lt;= rel.</summary>
        WithinTolerance,

        /// <summary>Both values are finite and neither criterion holds.</summary>
        OutsideTolerance,

        /// <summary>A value is NaN or infinite, or the rule is missing: no verdict is possible, and absence of one is never a pass.</summary>
        NotComparable
    }

    /// <summary>One numeric comparison with its working, so the report can show why.</summary>
    public sealed class NumericComparison
    {
        public NumericComparison(NumericVerdict verdict, double expected, double native, double difference, double relativeDifference, bool absolutePassed, bool relativePassed)
        {
            Verdict = verdict;
            Expected = expected;
            Native = native;
            Difference = difference;
            RelativeDifference = relativeDifference;
            AbsolutePassed = absolutePassed;
            RelativePassed = relativePassed;
        }

        public NumericVerdict Verdict { get; }
        public double Expected { get; }
        public double Native { get; }

        /// <summary>d = |native - expected|; NaN when not comparable.</summary>
        public double Difference { get; }

        /// <summary>d / max(|native|,|expected|); NaN when not comparable or when both are 0.</summary>
        public double RelativeDifference { get; }

        public bool AbsolutePassed { get; }
        public bool RelativePassed { get; }

        public bool IsWithin => Verdict == NumericVerdict.Identical || Verdict == NumericVerdict.WithinTolerance;
    }
}
