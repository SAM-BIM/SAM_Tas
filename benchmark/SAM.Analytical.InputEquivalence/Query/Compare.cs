// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.InputEquivalence
{
    public static partial class Query
    {
        /// <summary>
        /// The S07 section 5 numeric test, used only when both values are present: <c>d = |n - s|</c>; within
        /// tolerance when <c>d &lt;= abs</c> or <c>d / max(|n|,|s|) &lt;= rel</c>.
        /// <para>
        /// Zero: <c>d == 0</c> is always <see cref="NumericVerdict.Identical"/> (the relative quotient 0/0 is never
        /// formed). With <c>d &gt; 0</c> the denominator is positive, so a near-zero pair that differs by more than
        /// <c>abs</c> fails a relative-only rule: no absolute floor is invented.
        /// </para>
        /// <para>
        /// NaN, infinities and a missing rule give <see cref="NumericVerdict.NotComparable"/>, never a pass: neither
        /// <c>abs</c> nor <c>rel</c> can rescue them.
        /// </para>
        /// </summary>
        public static NumericComparison Compare(double expected, double native, ToleranceRule rule)
        {
            if (rule == null || double.IsNaN(expected) || double.IsNaN(native) || double.IsInfinity(expected) || double.IsInfinity(native))
            {
                return new NumericComparison(NumericVerdict.NotComparable, expected, native, double.NaN, double.NaN, false, false);
            }

            double d = System.Math.Abs(native - expected);
            if (double.IsInfinity(d))
            {
                return new NumericComparison(NumericVerdict.NotComparable, expected, native, double.NaN, double.NaN, false, false);
            }

            if (d == 0)
            {
                return new NumericComparison(NumericVerdict.Identical, expected, native, 0, double.NaN, true, true);
            }

            double max = System.Math.Max(System.Math.Abs(native), System.Math.Abs(expected));
            double relative = d / max;

            bool absolutePassed = rule.Absolute.HasValue && d <= rule.Absolute.Value;
            bool relativePassed = rule.Relative.HasValue && relative <= rule.Relative.Value;

            return new NumericComparison(absolutePassed || relativePassed ? NumericVerdict.WithinTolerance : NumericVerdict.OutsideTolerance, expected, native, d, relative, absolutePassed, relativePassed);
        }
    }
}
