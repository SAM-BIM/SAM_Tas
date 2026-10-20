// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Geometry.Spatial;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.InputEquivalence.Tests
{
    [TestFixture]
    public class ScheduleTests
    {
        private static double[] Day()
        {
            double[] day = new double[24];
            for (int h = 8; h < 19; h++)
            {
                day[h] = 1;
            }

            return day;
        }

        [Test]
        public void A_Single_Value_Is_Constant_Over_The_Year()
        {
            ExpandedSchedule schedule = Query.ExpandSchedule(new[] { 0.25 });
            Assert.That(schedule.IsOk, Is.True);
            Assert.That(schedule.Hours.Length, Is.EqualTo(8760));
            Assert.That(schedule.Hours.All(x => x == 0.25), Is.True);
        }

        [Test]
        public void A_24_Hour_Profile_Repeats_Every_Day_From_Midnight()
        {
            ExpandedSchedule schedule = Query.ExpandSchedule(Day());
            Assert.That(schedule.Hours[7], Is.EqualTo(0));
            Assert.That(schedule.Hours[8], Is.EqualTo(1));
            Assert.That(schedule.Hours[18], Is.EqualTo(1));
            Assert.That(schedule.Hours[19], Is.EqualTo(0));
            Assert.That(schedule.Hours[24 * 100 + 8], Is.EqualTo(1), "day 101 hour 8");
            Assert.That(schedule.Hours[8759], Is.EqualTo(0));
            Assert.That(schedule.Hours.Sum(), Is.EqualTo(11 * 365));
        }

        [Test]
        public void An_8760_Profile_Is_Taken_As_Given()
        {
            double[] year = Enumerable.Range(0, 8760).Select(x => (double)x).ToArray();
            ExpandedSchedule schedule = Query.ExpandSchedule(year);
            Assert.That(schedule.Hours, Is.EqualTo(year));
            Assert.That(ReferenceEquals(schedule.Hours, year), Is.False, "a copy, so the source cannot be altered through it");
        }

        [Test]
        public void An_8784_Hour_Leap_Year_Profile_Is_Unsupported_And_Never_Truncated()
        {
            ExpandedSchedule schedule = Query.ExpandSchedule(new double[8784]);
            Assert.That(schedule.Status, Is.EqualTo(ScheduleExpansionStatus.UnsupportedLeapYear));
            Assert.That(schedule.Hours, Is.Null);
            Assert.That(Query.ExpectedHourlySeries(schedule).Single().State, Is.EqualTo(SamInputState.Invalid));
        }

        [Test]
        public void A_Leap_Year_Schedule_Is_An_Unsupported_Mapping_At_Rank_3_Until_Accepted_And_Never_A_Pass()
        {
            QuantityInput prototype = TestData.Input("6.hourly", null, null);
            ExpandedSchedule schedule = Query.ExpandSchedule(new double[8784]);

            QuantityResult result = Query.EvaluateScheduleExpansion(schedule, prototype);
            Assert.That(result.Outcome, Is.EqualTo(EquivalenceOutcome.UnsupportedMapping));
            Assert.That(result.Status, Is.EqualTo(GateStatus.BlockedOwnerDecision));

            GateConfiguration accepted = new GateConfiguration { Acceptances = new[] { new OwnerAcceptance("6.hourly", TestData.Fixture, AcceptableLimitation.UnsupportedMapping, "owner") } };
            QuantityResult acceptedResult = Query.EvaluateScheduleExpansion(schedule, prototype, accepted);
            Assert.That(acceptedResult.Status, Is.EqualTo(GateStatus.NotPassAcceptedLimitation));
            Assert.That(acceptedResult.IsPass, Is.False);
        }

        [Test]
        public void A_Schedule_That_Expands_Needs_No_Extra_Result()
        {
            Assert.That(Query.EvaluateScheduleExpansion(Query.ExpandSchedule(Day()), TestData.Input("6.hourly", null, null)), Is.Null);
        }

        [TestCase(0)]
        [TestCase(2)]
        [TestCase(23)]
        [TestCase(25)]
        [TestCase(8759)]
        [TestCase(8761)]
        public void Lengths_S07_Does_Not_Define_Are_Rejected_Not_Guessed(int count)
        {
            ExpandedSchedule schedule = Query.ExpandSchedule(new double[count]);
            Assert.That(schedule.Status, Is.EqualTo(ScheduleExpansionStatus.UndefinedLength));
            QuantityResult result = Query.EvaluateScheduleExpansion(schedule, TestData.Input("6.hourly", null, null));
            Assert.That(result.Outcome, Is.EqualTo(EquivalenceOutcome.InputInvalid));
            Assert.That(result.IsPass, Is.False);
        }

        [Test]
        public void NaN_And_Infinity_In_A_Profile_Are_Rejected()
        {
            double[] day = Day();
            day[3] = double.NaN;
            Assert.That(Query.ExpandSchedule(day).Status, Is.EqualTo(ScheduleExpansionStatus.ContainsNaN));
            day[3] = double.PositiveInfinity;
            Assert.That(Query.ExpandSchedule(day).Status, Is.EqualTo(ScheduleExpansionStatus.ContainsNaN));
            Assert.That(Query.ExpandSchedule(null).Status, Is.EqualTo(ScheduleExpansionStatus.UndefinedLength));
        }

        [Test]
        public void Eflh_Is_The_Sum_And_An_Unexpandable_Schedule_Gives_No_Eflh()
        {
            Assert.That(Query.ExpectedEquivalentFullLoadHours(Query.ExpandSchedule(Day())).Value, Is.EqualTo(4015));
            Assert.That(Query.ExpectedEquivalentFullLoadHours(Query.ExpandSchedule(new double[3])).State, Is.EqualTo(SamInputState.Invalid));
            Assert.That(Query.ExpectedEquivalentFullLoadHours(Query.ExpandSchedule(new[] { 0.0 })).State, Is.EqualTo(SamInputState.Zero));
        }

        [Test]
        public void The_Hourly_Series_Is_Scaled_By_The_Profile_Factor()
        {
            IReadOnlyList<ExpectedValue> series = Query.ExpectedHourlySeries(Query.ExpandSchedule(Day()), 25);
            Assert.That(series.Count, Is.EqualTo(8760));
            Assert.That(series[8].Value, Is.EqualTo(25));
            Assert.That(series[0].State, Is.EqualTo(SamInputState.Zero));
        }

        [Test]
        public void The_Writer_Slot_Convention_Is_Hour_Plus_One_And_Invertible()
        {
            Assert.That(Query.WriterSlotForHour(0), Is.EqualTo(1));
            Assert.That(Query.WriterSlotForHour(23), Is.EqualTo(24));
            Assert.That(Query.WriterHourForSlot(Query.WriterSlotForHour(8759)), Is.EqualTo(8759));
        }

        // ---------------------------------------------------------------- alignment diagnosis

        [Test]
        public void A_One_Hour_Shift_Is_Detected_As_A_Shift_Not_As_Agreement()
        {
            double[] expected = new double[8760];
            expected[10] = 1;
            double[] native = new double[8760];
            native[9] = 1; // the pulse landed one hour early

            ToleranceRule rule = QuantityCatalog.Get("6.hourly").Tolerance;
            Assert.That(Query.FindHourShifts(expected, native, rule), Is.EqualTo(new[] { -1 }));
            Assert.That(Query.FindHourShifts(expected, expected, rule), Is.EqualTo(new[] { 0 }));

            native = new double[8760];
            native[11] = 1;
            Assert.That(Query.FindHourShifts(expected, native, rule), Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void A_Constant_Profile_Matches_At_Every_Shift_So_Shift_Evidence_Needs_A_Pulse()
        {
            double[] flat = Enumerable.Repeat(0.5, 8760).ToArray();
            Assert.That(Query.FindHourShifts(flat, flat, QuantityCatalog.Get("6.hourly").Tolerance, 2), Is.EqualTo(new[] { -2, -1, 0, 1, 2 }));
        }

        [Test]
        public void A_Mismatch_That_No_Shift_Explains_Gives_An_Empty_List()
        {
            double[] expected = new double[100];
            expected[5] = 1;
            double[] native = new double[100];
            native[5] = 0.5;
            Assert.That(Query.FindHourShifts(expected, native, QuantityCatalog.Get("6.hourly").Tolerance), Is.Empty);
            Assert.That(Query.FindHourShifts(expected, new double[99], QuantityCatalog.Get("6.hourly").Tolerance), Is.Empty);
        }

        // ---------------------------------------------------------------- series evaluation

        private static IReadOnlyList<ExpectedValue> Expect(IEnumerable<double> values) => values.Select(ExpectedValue.FromValue).ToList();
        private static IReadOnlyList<ReaderResult> Read(IEnumerable<double> values) => values.Select(ReaderResult.FromValue).ToList();

        [Test]
        public void A_Matching_Verified_Series_Passes()
        {
            double[] day = Day();
            SeriesResult result = Query.EvaluateSeries(TestData.Input("6.hourly", null, null), Expect(day), Read(day));
            Assert.That(result.Aggregate.IsPass, Is.True);
            Assert.That(result.NotPassCount, Is.EqualTo(0));
            Assert.That(result.Elements.Count, Is.EqualTo(24));
        }

        [Test]
        public void One_Wrong_Hour_Fails_The_Series_And_Is_Named()
        {
            double[] day = Day();
            double[] wrong = (double[])day.Clone();
            wrong[12] = 0;
            SeriesResult result = Query.EvaluateSeries(TestData.Input("6.hourly", null, null), Expect(day), Read(wrong));
            Assert.That(result.Aggregate.Status, Is.EqualTo(GateStatus.Fail));
            Assert.That(result.Aggregate.Input.ObjectKey, Does.EndWith("[12]"));
            Assert.That(result.NotPassCount, Is.EqualTo(1));
        }

        [Test]
        public void A_Series_Is_Never_Truncated_Or_Padded()
        {
            SeriesResult result = Query.EvaluateSeries(TestData.Input("6.hourly", null, null), Expect(Day()), Read(Day().Take(23)));
            Assert.That(result.Aggregate.Outcome, Is.EqualTo(EquivalenceOutcome.ExportDefect));
            Assert.That(result.Aggregate.Status, Is.EqualTo(GateStatus.Fail));
            Assert.That(result.Aggregate.Reason, Does.Contain("differ in length"));

            Assert.That(Query.EvaluateSeries(TestData.Input("6.hourly", null, null), null, null).Aggregate.IsPass, Is.False);
            Assert.That(Query.EvaluateSeries(TestData.Input("6.hourly", null, null), Expect(new double[0]), Read(new double[0])).Aggregate.IsPass, Is.False);
        }

        [Test]
        public void An_Unverified_Alignment_Caps_A_Perfectly_Matching_Series_At_Not_Run()
        {
            double[] day = Day();
            SeriesResult result = Query.EvaluateSeries(TestData.Input("6.hourly", null, null, alignment: AlignmentStatus.Unverified), Expect(day), Read(day));
            Assert.That(result.Aggregate.Status, Is.EqualTo(GateStatus.NotRun));
            Assert.That(result.Elements.All(x => x.Status == GateStatus.NotRun), Is.True);
        }

        [Test]
        public void An_Unreadable_Hour_Is_A_Technical_Block_Not_A_Zero()
        {
            double[] day = Day();
            List<ReaderResult> native = Read(day).ToList();
            native[5] = ReaderResult.Unreadable("slot 6");
            SeriesResult result = Query.EvaluateSeries(TestData.Input("6.hourly", null, null), Expect(day), native);
            Assert.That(result.Aggregate.Status, Is.EqualTo(GateStatus.BlockedTechnical));
        }

        [Test]
        public void A_Whole_Year_Series_Evaluates_Within_Reason()
        {
            double[] year = Query.ExpandSchedule(Day()).Hours;
            SeriesResult result = Query.EvaluateSeries(TestData.Input("6.hourly", null, null), Expect(year), Read(year));
            Assert.That(result.Elements.Count, Is.EqualTo(8760));
            Assert.That(result.Aggregate.IsPass, Is.True);
        }
    }

    [TestFixture]
    public class IdentityTests
    {
        private static Polygon3D Square(double x, double z)
        {
            return new Polygon3D(new[] { new Point3D(x, 0, z), new Point3D(x + 1, 0, z), new Point3D(x + 1, 1, z), new Point3D(x, 1, z) });
        }

        [Test]
        public void Polygons_Are_Matched_By_Vertices_Whatever_The_Order_The_Start_Vertex_Or_The_Winding()
        {
            List<Polygon3D> expected = new List<Polygon3D> { Square(0, 0), Square(5, 0), Square(10, 0) };

            // Native in reverse order, rotated start vertex and opposite winding on one of them.
            Polygon3D rotated = new Polygon3D(new[] { new Point3D(11, 1, 0), new Point3D(10, 1, 0), new Point3D(10, 0, 0), new Point3D(11, 0, 0) });
            Polygon3D reversed = new Polygon3D(new[] { new Point3D(5, 1, 0), new Point3D(6, 1, 0), new Point3D(6, 0, 0), new Point3D(5, 0, 0) });
            List<Polygon3D> native = new List<Polygon3D> { rotated, reversed, Square(0, 0) };

            GeometryMatchResult match = Query.MatchPolygons(expected, native);

            Assert.That(match.IsBijection, Is.True);
            Assert.That(match.PairForExpected(0).NativeIndex, Is.EqualTo(2));
            Assert.That(match.PairForExpected(1).NativeIndex, Is.EqualTo(1));
            Assert.That(match.PairForExpected(2).NativeIndex, Is.EqualTo(0));
            Assert.That(match.PairForExpected(1).SameFacing, Is.False, "the opposite winding is reported, not hidden");
        }

        [Test]
        public void Identity_Follows_The_Geometry_Not_The_Array_Order()
        {
            // The native array is in the same order as the expected one but the geometry is swapped: order-based identity would pair wrongly.
            List<Polygon3D> expected = new List<Polygon3D> { Square(0, 0), Square(5, 0) };
            List<Polygon3D> native = new List<Polygon3D> { Square(5, 0), Square(0, 0) };
            GeometryMatchResult match = Query.MatchPolygons(expected, native);

            Assert.That(match.PairForExpected(0).NativeIndex, Is.EqualTo(1));
            Assert.That(match.PairForExpected(1).NativeIndex, Is.EqualTo(0));
        }

        [Test]
        public void A_Native_Polygon_With_No_Authored_Counterpart_Is_Unmatched_And_Not_A_Bijection()
        {
            GeometryMatchResult match = Query.MatchPolygons(new[] { Square(0, 0) }, new[] { Square(0, 0), Square(50, 0) });
            Assert.That(match.IsBijection, Is.False);
            Assert.That(match.UnmatchedNative, Is.EqualTo(new[] { 1 }));
            Assert.That(match.UnmatchedExpected, Is.Empty);
        }

        [Test]
        public void An_Authored_Polygon_That_The_Native_Model_Lacks_Is_Unmatched()
        {
            GeometryMatchResult match = Query.MatchPolygons(new[] { Square(0, 0), Square(5, 0) }, new[] { Square(0, 0) });
            Assert.That(match.UnmatchedExpected, Is.EqualTo(new[] { 1 }));
            Assert.That(Query.AssessIdentity(match, 1, null), Is.EqualTo(IdentityStatus.Mismatch));
            Assert.That(Query.AssessIdentity(match, 0, null), Is.EqualTo(IdentityStatus.Verified), "the pair that did match is still verified; the missing one is reported separately");
        }

        [Test]
        public void Identical_Duplicate_Polygons_Are_Ambiguous_Many_To_One_Is_Never_Verified()
        {
            // Two authored polygons occupy the same place, one native polygon exists: many to one.
            GeometryMatchResult manyToOne = Query.MatchPolygons(new[] { Square(0, 0), Square(0, 0) }, new[] { Square(0, 0) });
            Assert.That(manyToOne.IsBijection, Is.False);
            Assert.That(manyToOne.AmbiguousExpected, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(Query.AssessIdentity(manyToOne, 0, true), Is.EqualTo(IdentityStatus.Mismatch), "even a writer GUID that agrees cannot verify an ambiguous pairing");

            // One authored polygon, two identical native polygons: one to many.
            GeometryMatchResult oneToMany = Query.MatchPolygons(new[] { Square(0, 0) }, new[] { Square(0, 0), Square(0, 0) });
            Assert.That(oneToMany.IsBijection, Is.False);
            Assert.That(oneToMany.AmbiguousExpected, Is.EqualTo(new[] { 0 }));
            Assert.That(oneToMany.AmbiguousNative, Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void The_Tolerance_Bounds_What_Counts_As_The_Same_Vertex()
        {
            Polygon3D near = new Polygon3D(new[] { new Point3D(0.004, 0, 0), new Point3D(1, 0, 0), new Point3D(1, 1, 0), new Point3D(0, 1, 0) });
            Assert.That(Query.MatchPolygons(new[] { Square(0, 0) }, new[] { near }, 0.01).IsBijection, Is.True);
            Assert.That(Query.MatchPolygons(new[] { Square(0, 0) }, new[] { near }, 0.001).IsBijection, Is.False);
        }

        [Test]
        public void Polygons_With_Different_Vertex_Counts_Do_Not_Match()
        {
            Polygon3D triangle = new Polygon3D(new[] { new Point3D(0, 0, 0), new Point3D(1, 0, 0), new Point3D(1, 1, 0) });
            Assert.That(Query.MatchPolygons(new[] { Square(0, 0) }, new[] { triangle }).IsBijection, Is.False);
        }

        // ---------------------------------------------------------------- the assessment

        [Test]
        public void Writer_Evidence_Alone_Is_Never_Verified()
        {
            Assert.That(Query.AssessIdentity(null, 0, true), Is.EqualTo(IdentityStatus.WriterOnly), "a [SpaceGuid] or element name that agrees, without geometry");
            Assert.That(Query.AssessIdentity(null, 0, null), Is.EqualTo(IdentityStatus.NotEstablished));
            Assert.That(Query.AssessIdentity(null, 0, false), Is.EqualTo(IdentityStatus.Mismatch));
        }

        [Test]
        public void Independent_Geometry_Verifies_Unless_The_Writer_Carrier_Contradicts_It()
        {
            GeometryMatchResult match = Query.MatchPolygons(new[] { Square(0, 0) }, new[] { Square(0, 0) });
            Assert.That(Query.AssessIdentity(match, 0, null), Is.EqualTo(IdentityStatus.Verified));
            Assert.That(Query.AssessIdentity(match, 0, true), Is.EqualTo(IdentityStatus.Verified));
            Assert.That(Query.AssessIdentity(match, 0, false), Is.EqualTo(IdentityStatus.Mismatch), "the carrier names another object: a mismatch to resolve, not a pass");
        }

        [Test]
        public void Only_Verified_Identity_Lets_A_Quantity_Pass_End_To_End()
        {
            GeometryMatchResult match = Query.MatchPolygons(new[] { Square(0, 0) }, new[] { Square(0, 0) });

            QuantityInput WithIdentity(IdentityStatus status) => TestData.Input("1b", ExpectedValue.FromValue(1), ReaderResult.FromValue(1), identity: status);

            Assert.That(Query.Evaluate(WithIdentity(Query.AssessIdentity(match, 0, true))).IsPass, Is.True);
            Assert.That(Query.Evaluate(WithIdentity(Query.AssessIdentity(null, 0, true))).IsPass, Is.False, "writer-only");
            Assert.That(Query.Evaluate(WithIdentity(Query.AssessIdentity(null, 0, null))).IsPass, Is.False, "nothing supplied");
            Assert.That(Query.Evaluate(WithIdentity(Query.AssessIdentity(match, 0, false))).Status, Is.EqualTo(GateStatus.Fail));
        }
    }
}
