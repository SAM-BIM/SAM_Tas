// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical.Tas.DirectT3D.Validation.ReadBack;
using SAM.Analytical.Tas.DirectT3D.Validation.Results;
using System;
using System.Linq;

namespace SAM.Analytical.Tas.DirectT3D.ReadBack.Tests
{
    /// <summary>
    /// S10 reference arithmetic and its fail-closed series handling: a missing or unreadable native element is never a
    /// reference zero; bounds, lengths, finiteness and sign are checked before any value is derived.
    /// </summary>
    [TestFixture]
    public class S10ResultReferenceTests
    {
        [Test]
        public void NativeSeries_NullAndTextElements_AreUnread_NeverZero()
        {
            ResultSeries series = NativeConvert.Series(new object[] { 1.5f, null, "x", 2.0 });

            Assert.That(series.IsRead, Is.True);
            Assert.That(series.UnreadElements, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(double.IsNaN(series.Values[1]) && double.IsNaN(series.Values[2]), Is.True, "an unread element is held as NaN, never 0");
            Assert.That(ResultReference.Shape(series, "s"), Does.Contain("2 unread element(s), first at CLR index 1"));
        }

        [Test]
        public void NativeSeries_KeepsTheComLowerBound()
        {
            Array array = Array.CreateInstance(typeof(float), new[] { 3 }, new[] { 1 });
            array.SetValue(4f, 1);
            array.SetValue(5f, 2);
            array.SetValue(6f, 3);

            ResultSeries series = NativeConvert.Series(array);

            Assert.That(series.LowerBound, Is.EqualTo(1));
            Assert.That(series.Values, Is.EqualTo(new[] { 4.0, 5.0, 6.0 }));
            Assert.That(series.UnreadElements, Is.Empty);
        }

        [Test]
        public void NativeSeries_NoArray_IsAbsentOrUnreadable()
        {
            Assert.That(NativeConvert.Series(null).Kind, Is.EqualTo(FieldKind.Absent));
            Assert.That(NativeConvert.Series(3.0).Kind, Is.EqualTo(FieldKind.Unreadable));
            Assert.That(NativeConvert.Series(new double[2, 2]).Kind, Is.EqualTo(FieldKind.Unreadable));
        }

        [TestCase(8760, 1, null)]
        [TestCase(8759, 1, "8759 elements at lower bound 1")]
        [TestCase(8784, 1, "8784 elements at lower bound 1")]
        [TestCase(8760, 0, "8760 elements at lower bound 0")]
        public void AnnualSeries_MustBe8760ElementsAtTheCalibratedLowerBound(int length, int lowerBound, string problem)
        {
            ResultSeries series = ResultSeries.Of(new double[length], lowerBound);

            double[] values = ResultReference.Annual(series, 1, 8760, "s", out string found);

            if (problem == null)
            {
                Assert.That(found, Is.Null);
                Assert.That(values, Has.Length.EqualTo(8760));
            }
            else
            {
                Assert.That(values, Is.Null);
                Assert.That(found, Does.Contain(problem).And.Contain("never truncated, padded or shifted"));
            }
        }

        [Test]
        public void AnnualSeries_NonFinite_IsRefused()
        {
            double[] values = new double[8760];
            values[17] = double.PositiveInfinity;

            Assert.That(ResultReference.Annual(ResultSeries.Of(values, 1), 1, 8760, "s", out string problem), Is.Null);
            Assert.That(problem, Does.Contain("NaN/infinite").And.Contain("CLR index 18"));
        }

        [Test]
        public void NegativeLoad_LeavesTheSignUnresolved()
        {
            Assert.That(ResultReference.Sign(new[] { 0.0, 3, -0.5 }, "load"), Does.Contain("1 negative element(s), first -0.5 at position 2"));
            Assert.That(ResultReference.Sign(new[] { 0.0, 3 }, "load"), Is.Null);
        }

        [Test]
        public void Peak_KeepsEveryTiedMaximum()
        {
            (double max, System.Collections.Generic.List<int> ties) = ResultReference.Peak(new[] { 1.0, 5, 2, 5, 0 });

            Assert.That(max, Is.EqualTo(5));
            Assert.That(ties, Is.EqualTo(new[] { 1, 3 }));
        }

        [Test]
        public void UnmetHours_FollowSamsStatedDefinition()
        {
            double[] dryBulb = { 20.4, 20.5, 20.6, 26.6, 26.5 };
            double[] lower = { 21, 21, 21, 21, 21 };
            double[] upper = { 26, 26, 26, 26, 26 };

            Assert.That(ResultReference.UnmetHours(dryBulb, lower, heating: true), Is.EqualTo(1), "T + 0.5 < LL only for 20.4");
            Assert.That(ResultReference.UnmetHours(dryBulb, upper, heating: false), Is.EqualTo(1), "T - 0.5 > UL only for 26.6");
        }

        [Test]
        public void ZoneGuids_AreComparedAsGuids_NotAsText()
        {
            Assert.That(ResultReference.SameGuid("{A1000000-0000-0000-0000-0000000000AA}", "a1000000-0000-0000-0000-0000000000aa"), Is.True);
            Assert.That(ResultReference.SameGuid("a1000000-0000-0000-0000-0000000000aa", "a1000000-0000-0000-0000-0000000000ab"), Is.False);
            Assert.That(ResultReference.SameGuid(null, "a1000000-0000-0000-0000-0000000000aa"), Is.False);
        }

        [Test]
        public void BalanceTerms_AreTheManualsBreakdown()
        {
            // Tas Theory Manual, Sensible Load Breakdown: solar, lighting, occupancy, equipment, infiltration/ventilation,
            // air movement, building heat transfer, external conduction opaque and transparent (+ the AHU gain).
            Assert.That(ResultReference.BalanceTerms, Has.Length.EqualTo(10));
            Assert.That(ResultReference.BalanceTerms.Distinct().Count(), Is.EqualTo(10));
        }

        [Test]
        public void MissingDesignDayGroup_BlocksTheDesignDayRows()
        {
            ResultCase copy = ResultCase.From(S10SyntheticTriple.Inputs());
            copy.Coverage.Groups.RemoveAll(x => x.Role == "Heating");

            ResultReferenceModel reference = ResultReference.Build(copy.Tsd, copy.Tbd, copy.Coverage, S10SyntheticTriple.Inputs().Rules);

            Assert.That(reference.Checks.Single(x => x.Id == "S10-C10").Status, Is.EqualTo("unresolved"));
            Assert.That(reference.Checks.Single(x => x.Id == "S10-C10").Answer, Does.Contain("in the TSD but not a TBD group: 'HDD'"));
            Assert.That(reference.Rows.Where(x => x.LoadType == "Heating" && x.Quantity.StartsWith("designDayPeak", StringComparison.Ordinal)).Select(x => x.State), Is.All.EqualTo(ExpectedState.Blocked));
            Assert.That(reference.Rows.Where(x => x.LoadType == "Cooling" && x.Quantity == "designDayPeak.load").Select(x => x.State), Is.All.EqualTo(ExpectedState.Value));
        }

        [Test]
        public void UnverifiedAlignmentRule_BlocksEveryRow()
        {
            ResultCase copy = ResultCase.From(S10SyntheticTriple.Inputs());

            ResultReferenceModel reference = ResultReference.Build(copy.Tsd, copy.Tbd, copy.Coverage, CalibrationRules.None());

            Assert.That(reference.Blockers, Has.Some.Contains("N-5"));
            Assert.That(reference.Rows.Select(x => x.State), Is.All.EqualTo(ExpectedState.Blocked));
        }
    }
}
