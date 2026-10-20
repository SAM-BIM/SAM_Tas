// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using System;

namespace SAM.Analytical.InputEquivalence.Tests
{
    [TestFixture]
    public class ReaderResultTests
    {
        [Test]
        public void FromValue_Classifies_Every_Kind()
        {
            Assert.That(ReaderResult.FromValue(0).Kind, Is.EqualTo(ReaderKind.Zero));
            Assert.That(ReaderResult.FromValue(-0.0).Kind, Is.EqualTo(ReaderKind.Zero));
            Assert.That(ReaderResult.FromValue(1e-300).Kind, Is.EqualTo(ReaderKind.Finite));
            Assert.That(ReaderResult.FromValue(-3.5).Kind, Is.EqualTo(ReaderKind.Finite));
            Assert.That(ReaderResult.FromValue(double.NaN).Kind, Is.EqualTo(ReaderKind.NaN));
            Assert.That(ReaderResult.FromValue(double.PositiveInfinity).Kind, Is.EqualTo(ReaderKind.NaN), "an infinity is not a comparable number");
            Assert.That(ReaderResult.FromValue(double.NegativeInfinity).Kind, Is.EqualTo(ReaderKind.NaN));
        }

        [Test]
        public void Absent_Unreadable_And_NaN_Are_Three_Different_Kinds_With_No_Number()
        {
            ReaderResult absent = ReaderResult.Absent("no object");
            ReaderResult unreadable = ReaderResult.Unreadable("COM failed");
            ReaderResult nan = ReaderResult.NotANumber();

            Assert.That(new[] { absent.Kind, unreadable.Kind, nan.Kind }, Is.Unique);
            Assert.That(absent.HasNumber, Is.False);
            Assert.That(unreadable.HasNumber, Is.False);
            Assert.That(nan.HasNumber, Is.False);
            Assert.That(unreadable.Detail, Is.EqualTo("COM failed"));
        }

        [Test]
        public void Zero_And_Finite_Carry_A_Number()
        {
            Assert.That(ReaderResult.FromValue(0).HasNumber, Is.True);
            Assert.That(ReaderResult.FromValue(2).Value, Is.EqualTo(2));
        }
    }

    [TestFixture]
    public class NumericRuleTests
    {
        private static readonly ToleranceRule both = ToleranceRule.Create(0.005, 0.01, "m2");
        private static readonly ToleranceRule relOnly = ToleranceRule.Create(1e-5, null, "unit");
        private static readonly ToleranceRule absOnly = ToleranceRule.Create(null, 0.01, "K");

        [Test]
        public void A_Rule_Needs_At_Least_One_Criterion_And_Rejects_Negative_Or_Invalid_Ones()
        {
            Assert.Throws<ArgumentException>(() => ToleranceRule.Create(null, null, "x"));
            Assert.Throws<ArgumentOutOfRangeException>(() => ToleranceRule.Create(-0.1, null, "x"));
            Assert.Throws<ArgumentOutOfRangeException>(() => ToleranceRule.Create(null, double.NaN, "x"));
            Assert.Throws<ArgumentOutOfRangeException>(() => ToleranceRule.Create(double.PositiveInfinity, 1, "x"));
        }

        [Test]
        public void Identical_Values_Are_Identical_Even_When_Both_Are_Zero()
        {
            Assert.That(Query.Compare(0, 0, relOnly).Verdict, Is.EqualTo(NumericVerdict.Identical));
            Assert.That(Query.Compare(5, 5, relOnly).Verdict, Is.EqualTo(NumericVerdict.Identical));
            Assert.That(Query.Compare(-2, -2, absOnly).Verdict, Is.EqualTo(NumericVerdict.Identical));
        }

        [Test]
        public void Absolute_Boundary_Is_Inclusive()
        {
            // 0.25 and 1.25 are exactly representable, so the boundary d == abs is tested without rounding noise.
            ToleranceRule quarter = ToleranceRule.Create(null, 0.25, "K");
            Assert.That(Query.Compare(1.0, 1.0625, quarter).Verdict, Is.EqualTo(NumericVerdict.WithinTolerance));
            Assert.That(Query.Compare(1.0, 1.25, quarter).Verdict, Is.EqualTo(NumericVerdict.WithinTolerance), "d == abs is within (d <= abs)");
            Assert.That(Query.Compare(1.0, 1.2500001, quarter).Verdict, Is.EqualTo(NumericVerdict.OutsideTolerance));
            Assert.That(Query.Compare(1.25, 1.0, quarter).Verdict, Is.EqualTo(NumericVerdict.WithinTolerance), "symmetric");
        }

        [Test]
        public void Relative_Boundary_Is_Inclusive_And_Uses_The_Larger_Magnitude()
        {
            // d / max(|n|,|s|): 100 vs 100.5 -> 0.5 / 100.5 = 0.004975 <= 0.005 ; 100 vs 100.6 -> 0.6 / 100.6 = 0.005964 > 0.005.
            ToleranceRule relative = ToleranceRule.Create(0.005, null, "m3");
            Assert.That(Query.Compare(100, 100.5, relative).Verdict, Is.EqualTo(NumericVerdict.WithinTolerance));
            Assert.That(Query.Compare(100.5, 100, relative).Verdict, Is.EqualTo(NumericVerdict.WithinTolerance), "symmetric");
            Assert.That(Query.Compare(100, 100.6, relative).Verdict, Is.EqualTo(NumericVerdict.OutsideTolerance));
        }

        [Test]
        public void Either_Criterion_Passes_It_Is_An_Or()
        {
            // Small values: absolute passes though the relative difference is huge.
            NumericComparison small = Query.Compare(0.001, 0.009, both);
            Assert.That(small.AbsolutePassed, Is.True);
            Assert.That(small.RelativePassed, Is.False);
            Assert.That(small.IsWithin, Is.True);

            // Large values: relative passes though the absolute difference is large.
            NumericComparison large = Query.Compare(1000, 1004, both);
            Assert.That(large.AbsolutePassed, Is.False);
            Assert.That(large.RelativePassed, Is.True);
            Assert.That(large.IsWithin, Is.True);

            Assert.That(Query.Compare(1000, 1006, both).Verdict, Is.EqualTo(NumericVerdict.OutsideTolerance));
        }

        [Test]
        public void A_Criterion_S07_Marks_As_Absent_Does_Not_Apply()
        {
            // Relative-only: a near-zero pair that differs is judged by the relative rule alone; no absolute floor is invented.
            Assert.That(Query.Compare(0, 1e-9, relOnly).Verdict, Is.EqualTo(NumericVerdict.OutsideTolerance));
            Assert.That(Query.Compare(1e-9, 1.0000000001e-9, relOnly).Verdict, Is.EqualTo(NumericVerdict.WithinTolerance));

            // Absolute-only: a large value with a tiny relative difference still fails when d > abs.
            Assert.That(Query.Compare(1000, 1000.5, absOnly).Verdict, Is.EqualTo(NumericVerdict.OutsideTolerance));
        }

        [Test]
        public void Near_Zero_Pairs_Are_Handled_Without_A_Zero_Division()
        {
            NumericComparison zeroVersusTiny = Query.Compare(0, 1e-12, both);
            Assert.That(zeroVersusTiny.Verdict, Is.EqualTo(NumericVerdict.WithinTolerance), "d = 1e-12 <= abs 0.01");
            Assert.That(zeroVersusTiny.RelativeDifference, Is.EqualTo(1).Within(1e-12));

            NumericComparison zeroVersusZero = Query.Compare(0, 0, both);
            Assert.That(double.IsNaN(zeroVersusZero.RelativeDifference), Is.True, "0/0 is never formed");
            Assert.That(zeroVersusZero.IsWithin, Is.True);

            Assert.That(Query.Compare(0, 0.02, both).Verdict, Is.EqualTo(NumericVerdict.OutsideTolerance));
            Assert.That(Query.Compare(-1e-12, 1e-12, both).IsWithin, Is.True);
        }

        [Test]
        public void Sign_Differences_Are_Differences()
        {
            Assert.That(Query.Compare(5, -5, both).Verdict, Is.EqualTo(NumericVerdict.OutsideTolerance));
            Assert.That(Query.Compare(-100, -100.4, both).Verdict, Is.EqualTo(NumericVerdict.WithinTolerance));
        }

        [Test]
        public void NaN_Infinity_And_A_Missing_Rule_Are_Not_Comparable_And_Never_Within()
        {
            double[] bad = { double.NaN, double.PositiveInfinity, double.NegativeInfinity };
            foreach (double value in bad)
            {
                Assert.That(Query.Compare(1, value, both).Verdict, Is.EqualTo(NumericVerdict.NotComparable));
                Assert.That(Query.Compare(value, 1, both).Verdict, Is.EqualTo(NumericVerdict.NotComparable));
                Assert.That(Query.Compare(value, value, both).IsWithin, Is.False, "NaN == NaN is not a match");
            }

            Assert.That(Query.Compare(1, 1, null).Verdict, Is.EqualTo(NumericVerdict.NotComparable));
            Assert.That(Query.Compare(1, 1, null).IsWithin, Is.False);
        }

        [Test]
        public void A_Huge_Difference_That_Overflows_Is_Not_Comparable()
        {
            Assert.That(Query.Compare(double.MaxValue, -double.MaxValue, both).Verdict, Is.EqualTo(NumericVerdict.NotComparable));
        }

        [Test]
        public void Exact_Rule_Allows_No_Difference()
        {
            ToleranceRule exact = ToleranceRule.Exact("day");
            Assert.That(Query.Compare(100, 100, exact).Verdict, Is.EqualTo(NumericVerdict.Identical));
            Assert.That(Query.Compare(100, 101, exact).Verdict, Is.EqualTo(NumericVerdict.OutsideTolerance));
        }
    }
}
