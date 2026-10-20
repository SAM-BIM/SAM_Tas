// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Geometry.Spatial;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.InputEquivalence.Tests
{
    /// <summary>Shared builders for the regression tests of the four independent-review findings on PR-A.</summary>
    internal static class Review
    {
        public static QuantityResult Evaluate(string quantity, ExpectedValue expected, ReaderResult native, string objectKey = "object", ContractListing listing = ContractListing.None,
            IdentityStatus identity = IdentityStatus.Verified, GateConfiguration configuration = null, double? listedDefault = null, string fixture = TestData.Fixture)
        {
            return Query.Evaluate(new QuantityInput
            {
                Definition = QuantityCatalog.Get(quantity),
                FixtureId = fixture,
                ObjectKey = objectKey,
                Expected = expected,
                Native = native,
                Identity = identity,
                Alignment = AlignmentStatus.Verified,
                Listing = listing,
                ListedDefault = listedDefault
            }, configuration);
        }

        public static QuantityResult Match(string quantity, string objectKey = "object")
        {
            return Evaluate(quantity, ExpectedValue.FromValue(20), ReaderResult.FromValue(20), objectKey);
        }

        public static GateConfiguration Requiring(params (string quantity, string objectKey)[] pairs)
        {
            return new GateConfiguration { RequiredComparisons = pairs.Select(x => new RequiredComparison(x.quantity, x.objectKey)).ToList() };
        }
    }

    /// <summary>Finding 1: a non-zero S-VALUE must not become a pass against a zeroed native object through the absolute tolerance.</summary>
    [TestFixture]
    public class ZeroedNativeObjectTests
    {
        [Test]
        public void Row_7a_Expected_0_00005_Against_Native_Zero_Is_Not_A_Pass()
        {
            // The reproduction from the review: 7a has abs 1e-4, and |0.00005 - 0| = 5e-5 is within it.
            Assert.That(Query.Compare(0.00005, 0, QuantityCatalog.Get("7a").Tolerance).IsWithin, Is.True, "the generic rule is unchanged: abs-or-rel mathematics");

            QuantityResult result = Review.Evaluate("7a", ExpectedValue.FromValue(0.00005), ReaderResult.FromValue(0));
            Assert.That(result.IsPass, Is.False);
            Assert.That(result.Outcome, Is.EqualTo(EquivalenceOutcome.ExportDefect));
            Assert.That(result.Status, Is.EqualTo(GateStatus.Fail));
            Assert.That(result.Reason, Does.Contain("zeroed native object"));
            Assert.That(result.Comparison.Verdict, Is.EqualTo(NumericVerdict.WithinTolerance), "the numeric working stays visible, but is not the verdict");
        }

        [Test]
        public void The_Generic_Compare_Is_Unchanged_For_Zero()
        {
            ToleranceRule rule = QuantityCatalog.Get("7a").Tolerance;
            Assert.That(Query.Compare(0.00005, 0, rule).Verdict, Is.EqualTo(NumericVerdict.WithinTolerance));
            Assert.That(Query.Compare(0, 0.00005, rule).Verdict, Is.EqualTo(NumericVerdict.WithinTolerance));
            Assert.That(Query.Compare(0, 0, rule).Verdict, Is.EqualTo(NumericVerdict.Identical));
        }

        [Test]
        public void A_Finite_Non_Zero_Native_Within_The_Tolerance_Still_Passes()
        {
            Assert.That(Review.Evaluate("7a", ExpectedValue.FromValue(0.00005), ReaderResult.FromValue(0.00006)).Outcome, Is.EqualTo(EquivalenceOutcome.WithinTolerance));
            Assert.That(Review.Evaluate("7a", ExpectedValue.FromValue(0.00005), ReaderResult.FromValue(0.00005)).Outcome, Is.EqualTo(EquivalenceOutcome.Match));
            Assert.That(Review.Evaluate("7a", ExpectedValue.FromValue(0.00005), ReaderResult.FromValue(1e-300)).IsPass, Is.True, "tiny but non-zero is not a zeroed object");
        }

        [Test]
        public void S_Zero_Behaviour_Is_Preserved()
        {
            Assert.That(Review.Evaluate("7a", ExpectedValue.Zero(), ReaderResult.FromValue(0)).Outcome, Is.EqualTo(EquivalenceOutcome.Match));
            Assert.That(Review.Evaluate("7a", ExpectedValue.Zero(), ReaderResult.FromValue(0.00005)).Outcome, Is.EqualTo(EquivalenceOutcome.WithinTolerance), "S-ZERO against a tiny native stays within abs");
            Assert.That(Review.Evaluate("7a", ExpectedValue.Zero(), ReaderResult.FromValue(0.001)).Outcome, Is.EqualTo(EquivalenceOutcome.OutOfTolerance));
            Assert.That(Review.Evaluate("7a", ExpectedValue.Zero(), ReaderResult.Absent()).Outcome, Is.EqualTo(EquivalenceOutcome.ExportDefect));
        }

        [Test]
        public void A_Large_S_Value_Against_A_Zeroed_Native_Is_The_Same_Defect()
        {
            QuantityResult result = Review.Evaluate("1a", ExpectedValue.FromValue(20), ReaderResult.FromValue(0));
            Assert.That(result.Outcome, Is.EqualTo(EquivalenceOutcome.ExportDefect));
            Assert.That(result.Status, Is.EqualTo(GateStatus.Fail));
        }

        [Test]
        public void A_Zeroed_Object_Under_A_Listed_Limitation_Is_Rank_3_And_Accepted_Is_Rank_5_Never_A_Pass()
        {
            QuantityResult unsupported = Review.Evaluate("7a", ExpectedValue.FromValue(0.00005), ReaderResult.FromValue(0), listing: ContractListing.UnsupportedMapping);
            Assert.That(unsupported.Outcome, Is.EqualTo(EquivalenceOutcome.UnsupportedMapping));
            Assert.That(unsupported.Status, Is.EqualTo(GateStatus.BlockedOwnerDecision));

            QuantityResult difference = Review.Evaluate("7a", ExpectedValue.FromValue(0.00005), ReaderResult.FromValue(0), listing: ContractListing.MappingDifference);
            Assert.That(difference.Outcome, Is.EqualTo(EquivalenceOutcome.MappingDifference));

            GateConfiguration accepted = new GateConfiguration { Acceptances = new[] { new OwnerAcceptance("7a", TestData.Fixture, AcceptableLimitation.UnsupportedMapping, "owner") } };
            QuantityResult acceptedResult = Review.Evaluate("7a", ExpectedValue.FromValue(0.00005), ReaderResult.FromValue(0), listing: ContractListing.UnsupportedMapping, configuration: accepted);
            Assert.That(acceptedResult.Status, Is.EqualTo(GateStatus.NotPassAcceptedLimitation));
            Assert.That(acceptedResult.IsPass, Is.False);
        }

        [Test]
        public void No_Catalogued_Quantity_Passes_A_Zeroed_Native_Whatever_Its_Absolute_Tolerance()
        {
            foreach (QuantityDefinition definition in QuantityCatalog.All.Where(x => x.Tolerance.Absolute > 0))
            {
                double smallSamValue = definition.Tolerance.Absolute.Value / 2;
                QuantityResult result = Query.Evaluate(new QuantityInput
                {
                    Definition = definition, FixtureId = "f", ObjectKey = "o", Expected = ExpectedValue.FromValue(smallSamValue), Native = ReaderResult.FromValue(0),
                    Identity = IdentityStatus.Verified, Alignment = AlignmentStatus.Verified
                });

                Assert.That(Query.Compare(smallSamValue, 0, definition.Tolerance).IsWithin, Is.True, definition.Id + ": the probe value is inside the absolute tolerance");
                Assert.That(result.IsPass, Is.False, definition.Id);
            }
        }

        [Test]
        public void One_Zeroed_Hour_Fails_An_Otherwise_Matching_Series()
        {
            List<ExpectedValue> expected = Enumerable.Repeat(ExpectedValue.FromValue(0.00005), 24).ToList();
            List<ReaderResult> native = Enumerable.Repeat(ReaderResult.FromValue(0.00005), 24).ToList();
            native[7] = ReaderResult.FromValue(0);

            SeriesResult series = Query.EvaluateSeries(TestData.Input("6.hourly", null, null), expected, native);
            Assert.That(series.Aggregate.Status, Is.EqualTo(GateStatus.Fail));
            Assert.That(series.Aggregate.Input.ObjectKey, Does.EndWith("[7]"));
            Assert.That(series.NotPassCount, Is.EqualTo(1));
        }
    }

    /// <summary>Finding 2: no fixture PASS without explicit, complete coverage of the required comparisons.</summary>
    [TestFixture]
    public class CoverageTests
    {
        [Test]
        public void A_Lone_Matching_Row_1a_With_The_Default_Configuration_Is_Not_A_Pass()
        {
            GateReport report = Query.Gate(new[] { Review.Match("1a") });

            Assert.That(report.IsPass, Is.False);
            Assert.That(report.Status, Is.EqualTo(GateStatus.NotRun));
            Assert.That(report.Coverage, Is.EqualTo(CoverageStatus.NotSupplied));
            Assert.That(Query.Gate(new[] { Review.Match("1a") }, new GateConfiguration()).IsPass, Is.False);
        }

        [Test]
        public void An_Empty_Coverage_List_Is_Rejected_Not_Taken_As_Complete()
        {
            GateReport report = Query.Gate(new[] { Review.Match("1a") }, new GateConfiguration { RequiredComparisons = new RequiredComparison[0] });
            Assert.That(report.IsPass, Is.False);
            Assert.That(report.Status, Is.EqualTo(GateStatus.NotRun));
            Assert.That(report.Coverage, Is.EqualTo(CoverageStatus.Empty));

            // A list holding only nulls is empty too.
            GateReport nulls = Query.Gate(new[] { Review.Match("1a") }, new GateConfiguration { RequiredComparisons = new RequiredComparison[] { null, null } });
            Assert.That(nulls.Coverage, Is.EqualTo(CoverageStatus.Empty));
            Assert.That(nulls.IsPass, Is.False);
        }

        [Test]
        public void A_Complete_Explicit_List_Allows_The_Pass()
        {
            GateReport report = Query.Gate(new[] { Review.Match("1a") }, Review.Requiring(("1a", "object")));
            Assert.That(report.Coverage, Is.EqualTo(CoverageStatus.Complete));
            Assert.That(report.Status, Is.EqualTo(GateStatus.Pass));
            Assert.That(report.MissingComparisons, Is.Empty);
        }

        [Test]
        public void Quantity_Presence_Alone_Is_Not_All_Zone_Coverage()
        {
            // Row 1a is present for zone A, but the fixture also has zone B: presence of the quantity category proves nothing about B.
            QuantityResult zoneA = Review.Match("1a", "zone-A");
            GateReport report = Query.Gate(new[] { zoneA }, Review.Requiring(("1a", "zone-A"), ("1a", "zone-B")));

            Assert.That(report.Coverage, Is.EqualTo(CoverageStatus.Incomplete));
            Assert.That(report.Status, Is.EqualTo(GateStatus.NotRun));
            Assert.That(report.MissingComparisons.Select(x => x.ToString()), Is.EqualTo(new[] { "1a @ zone-B" }));

            QuantityResult zoneB = Review.Match("1a", "zone-B");
            Assert.That(Query.Gate(new[] { zoneA, zoneB }, Review.Requiring(("1a", "zone-A"), ("1a", "zone-B"))).Status, Is.EqualTo(GateStatus.Pass));
        }

        [Test]
        public void The_Same_Object_With_A_Different_Quantity_Does_Not_Cover_The_Required_One()
        {
            GateReport report = Query.Gate(new[] { Review.Match("1a", "zone-A") }, Review.Requiring(("1a", "zone-A"), ("2", "zone-A")));
            Assert.That(report.Status, Is.EqualTo(GateStatus.NotRun));
            Assert.That(report.MissingComparisons.Single().QuantityId, Is.EqualTo("2"));
        }

        [Test]
        public void A_Result_Without_An_Object_Key_Cannot_Satisfy_A_Required_Comparison()
        {
            QuantityResult anonymous = Query.Evaluate(new QuantityInput
            {
                Definition = QuantityCatalog.Get("1a"), FixtureId = TestData.Fixture, ObjectKey = null, Expected = ExpectedValue.FromValue(20), Native = ReaderResult.FromValue(20),
                Identity = IdentityStatus.Verified
            });

            Assert.That(Query.Gate(new[] { anonymous }, Review.Requiring(("1a", "object"))).Status, Is.EqualTo(GateStatus.NotRun));
        }

        [Test]
        public void A_Required_Comparison_Needs_A_Quantity_And_An_Object()
        {
            Assert.Throws<ArgumentException>(() => new RequiredComparison("", "zone"));
            Assert.Throws<ArgumentException>(() => new RequiredComparison("1a", " "));
            Assert.Throws<ArgumentException>(() => new RequiredComparison(null, "zone"));
        }

        [Test]
        public void Duplicate_Required_Entries_Count_Once()
        {
            GateReport report = Query.Gate(Array.Empty<QuantityResult>(), Review.Requiring(("1a", "z"), ("1a", "z"), ("1a", "z")));
            Assert.That(report.MissingComparisons.Count, Is.EqualTo(1));
        }

        [TestCase(CoverageStatus.NotSupplied)]
        [TestCase(CoverageStatus.Empty)]
        [TestCase(CoverageStatus.Incomplete)]
        public void A_Higher_Priority_Outcome_Is_Never_Hidden_By_A_Coverage_Cap(CoverageStatus coverage)
        {
            GateConfiguration configuration;
            switch (coverage)
            {
                case CoverageStatus.NotSupplied: configuration = new GateConfiguration(); break;
                case CoverageStatus.Empty: configuration = new GateConfiguration { RequiredComparisons = new RequiredComparison[0] }; break;
                default: configuration = Review.Requiring(("1a", "object"), ("2", "object")); break;
            }

            QuantityResult fail = Review.Evaluate("1a", ExpectedValue.FromValue(20), ReaderResult.FromValue(25));
            QuantityResult technical = Review.Evaluate("1a", ExpectedValue.FromValue(20), ReaderResult.Unreadable("COM"));
            QuantityResult owner = Review.Evaluate("1a", ExpectedValue.FromValue(20), ReaderResult.Absent(), listing: ContractListing.UnsupportedMapping);
            QuantityResult notRun = Review.Evaluate("1a", ExpectedValue.FromValue(20), ReaderResult.FromValue(20), identity: IdentityStatus.NotEstablished);

            Assert.That(Query.Gate(new[] { fail }, configuration).Status, Is.EqualTo(GateStatus.Fail));
            Assert.That(Query.Gate(new[] { technical }, configuration).Status, Is.EqualTo(GateStatus.BlockedTechnical));
            Assert.That(Query.Gate(new[] { owner }, configuration).Status, Is.EqualTo(GateStatus.BlockedOwnerDecision));
            Assert.That(Query.Gate(new[] { notRun }, configuration).Status, Is.EqualTo(GateStatus.NotRun));
            Assert.That(Query.Gate(new[] { Review.Match("1a") }, configuration).Status, Is.EqualTo(GateStatus.NotRun), "only the pass is capped");
        }

        [Test]
        public void An_Accepted_Limitation_Is_Capped_To_Not_Run_When_Coverage_Is_Unknown_But_Never_Becomes_A_Pass()
        {
            GateConfiguration accepted = new GateConfiguration { Acceptances = new[] { new OwnerAcceptance("1a", TestData.Fixture, AcceptableLimitation.UnsupportedMapping, "owner") } };
            QuantityResult result = Review.Evaluate("1a", ExpectedValue.FromValue(20), ReaderResult.Absent(), listing: ContractListing.UnsupportedMapping, configuration: accepted);
            Assert.That(result.Status, Is.EqualTo(GateStatus.NotPassAcceptedLimitation));

            Assert.That(Query.Gate(new[] { result }, accepted).Status, Is.EqualTo(GateStatus.NotRun), "unknown coverage is rank 4, ahead of rank 5");
        }

        [Test]
        public void Results_Nobody_Asked_For_Are_Reported_And_Still_Count()
        {
            QuantityResult asked = Review.Match("1a", "zone-A");
            QuantityResult extraFail = Review.Evaluate("2", ExpectedValue.FromValue(60), ReaderResult.FromValue(70), "zone-A");

            GateReport report = Query.Gate(new[] { asked, extraFail }, Review.Requiring(("1a", "zone-A")));
            Assert.That(report.Coverage, Is.EqualTo(CoverageStatus.Complete));
            Assert.That(report.Unrequested, Is.EqualTo(new[] { extraFail }));
            Assert.That(report.Status, Is.EqualTo(GateStatus.Fail));
        }

        [Test]
        public void Cross_Builds_Every_Quantity_On_Every_Object()
        {
            IReadOnlyCollection<RequiredComparison> all = RequiredComparison.Cross(new[] { "1a", "2" }, new[] { "A", "B", "C" });
            Assert.That(all.Count, Is.EqualTo(6));
            Assert.That(all.Select(x => x.ToString()), Is.EquivalentTo(new[] { "1a @ A", "1a @ B", "1a @ C", "2 @ A", "2 @ B", "2 @ C" }));
            Assert.That(RequiredComparison.Cross(null, new[] { "A" }), Is.Empty);

            // With three objects and one result missing, the gate names exactly it.
            List<QuantityResult> results = all.Where(x => x.ToString() != "2 @ C").Select(x => Review.Match(x.QuantityId, x.ObjectKey)).ToList();
            GateReport report = Query.Gate(results, new GateConfiguration { RequiredComparisons = all });
            Assert.That(report.Status, Is.EqualTo(GateStatus.NotRun));
            Assert.That(report.MissingComparisons.Single().ToString(), Is.EqualTo("2 @ C"));
        }

        [Test]
        public void A_Series_Is_Covered_By_Its_Gate_Result_Keyed_By_The_Series_Object_Not_By_An_Hour()
        {
            double[] day = Enumerable.Range(0, 24).Select(x => x % 3 == 0 ? 1.0 : 0.0).ToArray();
            QuantityInput prototype = Review.Evaluate("6.hourly", ExpectedValue.Zero(), ReaderResult.FromValue(0), "profile-heating").Input;
            SeriesResult series = Query.EvaluateSeries(prototype, day.Select(ExpectedValue.FromValue).ToList(), day.Select(ReaderResult.FromValue).ToList());

            GateConfiguration required = Review.Requiring(("6.hourly", "profile-heating"));
            Assert.That(Query.Gate(new[] { series.GateResult }, required).Status, Is.EqualTo(GateStatus.Pass));
            Assert.That(Query.Gate(new[] { series.Aggregate }, required).Status, Is.EqualTo(GateStatus.NotRun), "an element key is not the series object");
        }

        [Test]
        public void A_Failing_Series_Keeps_Its_Verdict_And_Names_The_Decisive_Hour()
        {
            double[] day = new double[24];
            double[] wrong = new double[24];
            wrong[5] = 1;
            QuantityInput prototype = Review.Evaluate("6.hourly", ExpectedValue.Zero(), ReaderResult.FromValue(0), "profile-heating").Input;
            SeriesResult series = Query.EvaluateSeries(prototype, day.Select(ExpectedValue.FromValue).ToList(), wrong.Select(ReaderResult.FromValue).ToList());

            Assert.That(series.GateResult.Status, Is.EqualTo(GateStatus.Fail));
            Assert.That(series.GateResult.Input.ObjectKey, Is.EqualTo("profile-heating"));
            Assert.That(series.GateResult.Reason, Does.Contain("profile-heating[5]"));
        }

        [Test]
        public void Coverage_Of_Several_Objects_Passes_Only_When_Every_Object_Passes()
        {
            string[] zones = { "zone-A", "zone-B", "zone-C" };
            GateConfiguration required = new GateConfiguration { RequiredComparisons = RequiredComparison.Cross(new[] { "1a", "2" }, zones) };

            List<QuantityResult> good = zones.SelectMany(z => new[] { Review.Match("1a", z), Review.Match("2", z) }).ToList();
            Assert.That(Query.Gate(good, required).Status, Is.EqualTo(GateStatus.Pass));

            List<QuantityResult> oneBad = zones.SelectMany(z => new[] { Review.Match("1a", z), z == "zone-C" ? Review.Evaluate("2", ExpectedValue.FromValue(60), ReaderResult.FromValue(80), z) : Review.Match("2", z) }).ToList();
            Assert.That(Query.Gate(oneBad, required).Status, Is.EqualTo(GateStatus.Fail));
        }
    }

    /// <summary>Finding 3: informational numeric mismatches stay visible and do not gate; everything else on a non-gated quantity still gates.</summary>
    [TestFixture]
    public class InformationalMismatchTests
    {
        private static readonly GateConfiguration both = Review.Requiring(("1a", "object"), ("3b.ground", "object"));

        [Test]
        public void Row_1a_Matching_Plus_3b_Ground_0_2_Versus_0_4_Does_Not_Force_Fail()
        {
            QuantityResult row1a = Review.Match("1a");
            QuantityResult ground = Review.Evaluate("3b.ground", ExpectedValue.FromValue(0.2), ReaderResult.FromValue(0.4));

            Assert.That(ground.Definition.Gated, Is.False);
            Assert.That(ground.IsInformational, Is.True);
            Assert.That(ground.GatesFixture, Is.False);
            Assert.That(ground.Outcome, Is.EqualTo(EquivalenceOutcome.OutOfTolerance), "its own verdict stays visible");
            Assert.That(ground.Status, Is.EqualTo(GateStatus.Fail));
            Assert.That(ground.IsPass, Is.False, "an informational mismatch is never reported as a pass");
            Assert.That(ground.Comparison.Difference, Is.EqualTo(0.2).Within(1e-12));
            Assert.That(ground.Reason, Does.Contain("informational"));

            GateReport report = Query.Gate(new[] { row1a, ground }, both);
            Assert.That(report.Status, Is.EqualTo(GateStatus.Pass));
            Assert.That(report.Coverage, Is.EqualTo(CoverageStatus.Complete), "the informational comparison still counts as coverage");
            Assert.That(report.InformationalMismatches, Is.EqualTo(new[] { ground }));
            Assert.That(report.NonPass, Is.Empty);
            Assert.That(report.CountByOutcome[EquivalenceOutcome.OutOfTolerance], Is.EqualTo(1), "visible in the outcome counts");
        }

        [Test]
        public void The_Same_Mismatch_On_A_Gated_Quantity_Is_A_Fail()
        {
            QuantityResult gated = Review.Evaluate("3b", ExpectedValue.FromValue(0.2), ReaderResult.FromValue(0.4));
            Assert.That(gated.IsInformational, Is.False);
            Assert.That(Query.Gate(new[] { Review.Match("1a"), gated }, Review.Requiring(("1a", "object"), ("3b", "object"))).Status, Is.EqualTo(GateStatus.Fail));
        }

        [Test]
        public void An_Informational_Mismatch_Does_Not_Hide_A_Real_Failure()
        {
            QuantityResult row1a = Review.Evaluate("1a", ExpectedValue.FromValue(20), ReaderResult.FromValue(25));
            QuantityResult ground = Review.Evaluate("3b.ground", ExpectedValue.FromValue(0.2), ReaderResult.FromValue(0.4));
            GateReport report = Query.Gate(new[] { row1a, ground }, both);

            Assert.That(report.Status, Is.EqualTo(GateStatus.Fail));
            Assert.That(report.NonPass, Is.EqualTo(new[] { row1a }));
            Assert.That(report.InformationalMismatches, Is.EqualTo(new[] { ground }));
        }

        [TestCase("3b.ground", 0.2, 0.4)]
        [TestCase("3c.g", 0.5, 0.7)]
        [TestCase("9.ground", 10.0, 12.0)]
        public void Every_Approved_Exclusion_Is_Informational(string quantity, double expected, double native)
        {
            QuantityResult result = Review.Evaluate(quantity, ExpectedValue.FromValue(expected), ReaderResult.FromValue(native));
            Assert.That(result.IsInformational, Is.True, quantity);
            Assert.That(QuantityCatalog.Get(quantity).NotGatedReason, Is.Not.Empty);
        }

        [Test]
        public void The_Non_Gated_Set_Is_Exactly_The_Approved_Exclusions()
        {
            Assert.That(QuantityCatalog.All.Where(x => !x.Gated).Select(x => x.Id), Is.EquivalentTo(new[] { "3b.ground", "3c.g", "9.ground" }));
        }

        [Test]
        public void An_S_Zero_Mismatch_On_A_Non_Gated_Quantity_Is_Informational_Too()
        {
            QuantityResult result = Review.Evaluate("9.ground", ExpectedValue.Zero(), ReaderResult.FromValue(5));
            Assert.That(result.IsInformational, Is.True);
        }

        [Test]
        public void Only_A_Numeric_Mismatch_Is_Informational_Other_Defects_On_A_Non_Gated_Quantity_Still_Gate()
        {
            QuantityResult absent = Review.Evaluate("3b.ground", ExpectedValue.FromValue(0.2), ReaderResult.Absent());
            Assert.That(absent.IsInformational, Is.False);
            Assert.That(absent.Status, Is.EqualTo(GateStatus.Fail));

            QuantityResult zeroed = Review.Evaluate("3b.ground", ExpectedValue.FromValue(0.2), ReaderResult.FromValue(0));
            Assert.That(zeroed.IsInformational, Is.False, "a zeroed object is a defect, not a numeric difference");

            QuantityResult nan = Review.Evaluate("3b.ground", ExpectedValue.FromValue(0.2), ReaderResult.NotANumber());
            Assert.That(nan.IsInformational, Is.False);
            Assert.That(nan.Status, Is.EqualTo(GateStatus.BlockedOwnerDecision), "the proposed G3 case still blocks");

            QuantityResult unreadable = Review.Evaluate("3b.ground", ExpectedValue.FromValue(0.2), ReaderResult.Unreadable("COM"));
            Assert.That(unreadable.IsInformational, Is.False);
            Assert.That(unreadable.Status, Is.EqualTo(GateStatus.BlockedTechnical));

            QuantityResult invalid = Review.Evaluate("3b.ground", ExpectedValue.Invalid("no layers"), ReaderResult.FromValue(0.4));
            Assert.That(invalid.IsInformational, Is.False);
            Assert.That(invalid.Status, Is.EqualTo(GateStatus.BlockedTechnical));

            foreach (QuantityResult gating in new[] { absent, zeroed, nan, unreadable, invalid })
            {
                Assert.That(Query.Gate(new[] { Review.Match("1a"), gating }, both).IsPass, Is.False);
            }
        }

        [Test]
        public void A_Listed_Limitation_On_A_Non_Gated_Quantity_Still_Needs_An_Acceptance()
        {
            QuantityResult result = Review.Evaluate("3b.ground", ExpectedValue.FromValue(0.2), ReaderResult.FromValue(0.4), listing: ContractListing.MappingDifference);
            Assert.That(result.IsInformational, Is.False);
            Assert.That(result.Outcome, Is.EqualTo(EquivalenceOutcome.MappingDifference));
            Assert.That(result.Status, Is.EqualTo(GateStatus.BlockedOwnerDecision));
        }

        [Test]
        public void Identity_And_Alignment_Prerequisites_Still_Gate_An_Informational_Quantity()
        {
            QuantityResult unverified = Review.Evaluate("3b.ground", ExpectedValue.FromValue(0.2), ReaderResult.FromValue(0.4), identity: IdentityStatus.NotEstablished);
            Assert.That(unverified.IsInformational, Is.False);
            Assert.That(unverified.Status, Is.EqualTo(GateStatus.NotRun));
            Assert.That(unverified.Reason, Does.Contain("Also:").And.Contain("informational"));

            QuantityResult mismatch = Review.Evaluate("3b.ground", ExpectedValue.FromValue(0.2), ReaderResult.FromValue(0.4), identity: IdentityStatus.Mismatch);
            Assert.That(mismatch.Status, Is.EqualTo(GateStatus.Fail));
            Assert.That(mismatch.IsInformational, Is.False);
        }

        // ---------------------------------------------------------------- provisional defaults (G2) are preserved

        [Test]
        public void An_Unaccepted_Default_On_A_Non_Gated_Quantity_Is_Still_The_Provisional_G2_Case()
        {
            QuantityResult g2 = Review.Evaluate("3c.g", ExpectedValue.Unspecified(), ReaderResult.FromValue(0.5), listedDefault: 0.5);
            Assert.That(g2.IsInformational, Is.False);
            Assert.That(g2.Outcome, Is.EqualTo(EquivalenceOutcome.DocumentedDefault));
            Assert.That(g2.Status, Is.EqualTo(GateStatus.BlockedOwnerDecision));
            Assert.That(g2.Proposed, Does.Contain("G2"));
        }

        [Test]
        public void An_Informational_Mismatch_And_A_Provisional_G2_Default_Coexist_And_Neither_Is_Dropped()
        {
            QuantityResult row1a = Review.Match("1a");
            QuantityResult ground = Review.Evaluate("3b.ground", ExpectedValue.FromValue(0.2), ReaderResult.FromValue(0.4));
            QuantityResult g2 = Review.Evaluate("3c.g", ExpectedValue.Unspecified(), ReaderResult.FromValue(0.5), listedDefault: 0.5);

            GateReport report = Query.Gate(new[] { row1a, ground, g2 }, Review.Requiring(("1a", "object"), ("3b.ground", "object"), ("3c.g", "object")));

            Assert.That(report.Status, Is.EqualTo(GateStatus.BlockedOwnerDecision), "G2 blocks; the informational mismatch does not turn it into FAIL");
            Assert.That(report.Provisional, Is.True);
            Assert.That(report.OwnerDecisionsNamed.Any(x => x.Contains("OD-G2")), Is.True);
            Assert.That(report.InformationalMismatches, Is.EqualTo(new[] { ground }));
            Assert.That(report.NonPass, Is.EqualTo(new[] { g2 }));
            Assert.That(report.Results.Count, Is.EqualTo(3), "nothing is dropped from the report");
        }

        [Test]
        public void An_Accepted_Default_On_A_Non_Gated_Quantity_Is_Rank_5_Beside_An_Informational_Mismatch()
        {
            GateConfiguration accepted = new GateConfiguration
            {
                Acceptances = new[] { new OwnerAcceptance("3c.g", TestData.Fixture, AcceptableLimitation.DocumentedDefault, "owner") },
                RequiredComparisons = Review.Requiring(("3c.g", "object"), ("3b.ground", "object")).RequiredComparisons
            };

            QuantityResult g2 = Review.Evaluate("3c.g", ExpectedValue.Unspecified(), ReaderResult.FromValue(0.5), listedDefault: 0.5, configuration: accepted);
            QuantityResult ground = Review.Evaluate("3b.ground", ExpectedValue.FromValue(0.2), ReaderResult.FromValue(0.4));

            GateReport report = Query.Gate(new[] { g2, ground }, accepted);
            Assert.That(report.Status, Is.EqualTo(GateStatus.NotPassAcceptedLimitation));
            Assert.That(report.IsPass, Is.False);
        }

        [Test]
        public void Only_Informational_Results_Cannot_Make_A_Fixture_Pass()
        {
            QuantityResult ground = Review.Evaluate("3b.ground", ExpectedValue.FromValue(0.2), ReaderResult.FromValue(0.4));
            GateReport report = Query.Gate(new[] { ground }, Review.Requiring(("3b.ground", "object")));
            Assert.That(report.Status, Is.EqualTo(GateStatus.NotRun), "nothing gating was compared");

            QuantityResult groundMatch = Review.Evaluate("3b.ground", ExpectedValue.FromValue(0.2), ReaderResult.FromValue(0.2));
            Assert.That(Query.Gate(new[] { groundMatch }, Review.Requiring(("3b.ground", "object"))).Status, Is.EqualTo(GateStatus.Pass), "a matching diagnostic is a normal pass");
        }

        [Test]
        public void Many_Informational_Mismatches_Do_Not_Escalate()
        {
            List<QuantityResult> results = new List<QuantityResult> { Review.Match("1a") };
            List<(string, string)> keys = new List<(string, string)> { ("1a", "object") };
            for (int i = 0; i < 5; i++)
            {
                string key = "wall-" + i;
                results.Add(Review.Evaluate("3b.ground", ExpectedValue.FromValue(0.2), ReaderResult.FromValue(0.9), key));
                keys.Add(("3b.ground", key));
            }

            GateReport report = Query.Gate(results, Review.Requiring(keys.ToArray()));
            Assert.That(report.Status, Is.EqualTo(GateStatus.Pass));
            Assert.That(report.InformationalMismatches.Count, Is.EqualTo(5));
        }
    }

    /// <summary>Finding 4: polygon identity follows the boundary (cyclic order, either winding), not the vertex set.</summary>
    [TestFixture]
    public class BoundaryIdentityTests
    {
        private static readonly Point3D A = new Point3D(0, 0, 0);
        private static readonly Point3D B = new Point3D(4, 0, 0);
        private static readonly Point3D C = new Point3D(4, 4, 0);
        private static readonly Point3D D = new Point3D(2, 1, 0);
        private static readonly Point3D E = new Point3D(0, 4, 0);

        private static Polygon3D Poly(params Point3D[] points) => new Polygon3D(points);

        [Test]
        public void Same_Vertices_With_Rewired_Edges_Are_Different_Polygons_And_Are_Not_Verified()
        {
            Polygon3D abcde = Poly(A, B, C, D, E);
            Polygon3D abdce = Poly(A, B, D, C, E);

            Assert.That(Query.GrossArea(abcde), Is.EqualTo(10).Within(1e-9));
            Assert.That(Query.GrossArea(abdce), Is.EqualTo(12).Within(1e-9));

            GeometryMatchResult match = Query.MatchPolygons(new[] { abcde }, new[] { abdce });
            Assert.That(match.IsBijection, Is.False);
            Assert.That(match.Pairs, Is.Empty);
            Assert.That(match.UnmatchedExpected, Is.EqualTo(new[] { 0 }));
            Assert.That(match.UnmatchedNative, Is.EqualTo(new[] { 0 }));
            Assert.That(Query.AssessIdentity(match, 0, null), Is.EqualTo(IdentityStatus.Mismatch));
            Assert.That(Query.AssessIdentity(match, 0, true), Is.EqualTo(IdentityStatus.Mismatch), "a writer carrier that agrees cannot verify different geometry");
        }

        [Test]
        public void The_Rewired_Pair_Cannot_Pass_End_To_End()
        {
            GeometryMatchResult match = Query.MatchPolygons(new[] { Poly(A, B, C, D, E) }, new[] { Poly(A, B, D, C, E) });
            QuantityResult result = Review.Evaluate("1b", ExpectedValue.FromValue(10), ReaderResult.FromValue(12), identity: Query.AssessIdentity(match, 0, null));
            Assert.That(result.IsPass, Is.False);
            Assert.That(result.Status, Is.EqualTo(GateStatus.Fail));
        }

        [Test]
        public void Rotated_Reversed_And_Rotated_Reversed_Boundaries_Still_Match()
        {
            Polygon3D original = Poly(A, B, C, D, E);

            Polygon3D[] same =
            {
                Poly(A, B, C, D, E),
                Poly(C, D, E, A, B),                 // rotated
                Poly(E, A, B, C, D),                 // rotated
                Poly(E, D, C, B, A),                 // reversed
                Poly(B, A, E, D, C),                 // reversed and rotated
                Poly(D, C, B, A, E)                  // reversed and rotated
            };

            foreach (Polygon3D candidate in same)
            {
                GeometryMatchResult match = Query.MatchPolygons(new[] { original }, new[] { candidate });
                Assert.That(match.IsBijection, Is.True, string.Join(",", candidate.GetPoints()));
                Assert.That(Query.AssessIdentity(match, 0, null), Is.EqualTo(IdentityStatus.Verified));
            }
        }

        [Test]
        public void Any_Other_Order_Of_The_Same_Vertices_Is_Not_The_Same_Boundary()
        {
            Polygon3D original = Poly(A, B, C, D, E);
            Point3D[] points = { A, B, C, D, E };

            int usable = 0;
            int unusable = 0;
            int matching = 0;
            foreach (int[] permutation in Permutations(5))
            {
                Polygon3D candidate = Poly(permutation.Select(i => points[i]).ToArray());
                if (candidate.GetPoints() == null)
                {
                    // SAM's own Polygon3D constructor builds no plane for four of the 120 orders (for example A-C-D-B-E):
                    // such a polygon has no points, so it is unusable and must never match.
                    unusable++;
                    Assert.That(Query.MatchPolygons(new[] { original }, new[] { candidate }).IsBijection, Is.False);
                    continue;
                }

                usable++;
                if (Query.MatchPolygons(new[] { original }, new[] { candidate }).IsBijection)
                {
                    matching++;
                }
            }

            // 5! = 120 orders; exactly 5 rotations x 2 windings = 10 describe the same boundary, all other usable orders differ.
            Assert.That(usable + unusable, Is.EqualTo(120));
            Assert.That(unusable, Is.EqualTo(4));
            Assert.That(matching, Is.EqualTo(10));
        }

        [Test]
        public void Swapping_Two_Adjacent_Vertices_Is_A_Different_Polygon()
        {
            Assert.That(Query.MatchPolygons(new[] { Poly(A, B, C, D, E) }, new[] { Poly(A, C, B, D, E) }).IsBijection, Is.False);
        }

        [Test]
        public void A_Square_And_A_Bow_Tie_On_The_Same_Four_Vertices_Do_Not_Match()
        {
            Point3D p = new Point3D(0, 0, 0), q = new Point3D(1, 0, 0), r = new Point3D(1, 1, 0), s = new Point3D(0, 1, 0);
            Assert.That(Query.MatchPolygons(new[] { Poly(p, q, r, s) }, new[] { Poly(p, r, q, s) }).IsBijection, Is.False);
            Assert.That(Query.MatchPolygons(new[] { Poly(p, q, r, s) }, new[] { Poly(r, s, p, q) }).IsBijection, Is.True, "a rotation of the square is the same square");
        }

        [Test]
        public void Triangles_Match_In_Every_Order_Because_Every_Order_Is_A_Rotation_Or_A_Reversal()
        {
            Point3D[] t = { A, B, C };
            foreach (int[] permutation in Permutations(3))
            {
                Assert.That(Query.MatchPolygons(new[] { Poly(t) }, new[] { Poly(permutation.Select(i => t[i]).ToArray()) }).IsBijection, Is.True);
            }
        }

        [Test]
        public void The_Tolerance_Applies_Vertex_By_Vertex_Along_The_Boundary()
        {
            Polygon3D original = Poly(A, B, C, D, E);
            Point3D nearD = new Point3D(2.004, 1, 0);
            Polygon3D shiftedAndRotated = Poly(C, nearD, E, A, B);

            Assert.That(Query.MatchPolygons(new[] { original }, new[] { shiftedAndRotated }, 0.01).IsBijection, Is.True);
            Assert.That(Query.MatchPolygons(new[] { original }, new[] { shiftedAndRotated }, 0.001).IsBijection, Is.False);

            // One vertex out of tolerance in an otherwise rotated copy never matches.
            Assert.That(Query.MatchPolygons(new[] { original }, new[] { Poly(C, new Point3D(2.5, 1, 0), E, A, B) }, 0.01).IsBijection, Is.False);
        }

        [Test]
        public void Rotated_Boundaries_Are_Matched_To_The_Right_Partner_In_A_Set()
        {
            Polygon3D p1 = Poly(A, B, C, D, E);
            Polygon3D p2 = Poly(new Point3D(10, 0, 0), new Point3D(14, 0, 0), new Point3D(14, 4, 0), new Point3D(12, 1, 0), new Point3D(10, 4, 0));
            Polygon3D p2Rewired = Poly(new Point3D(10, 0, 0), new Point3D(14, 0, 0), new Point3D(12, 1, 0), new Point3D(14, 4, 0), new Point3D(10, 4, 0));

            // Native set: p2 reversed and rotated, p1 reversed. Order and start vertices differ from the authored set.
            Polygon3D n1 = Poly(E, D, C, B, A);
            Polygon3D n2 = Poly(new Point3D(14, 4, 0), new Point3D(14, 0, 0), new Point3D(10, 0, 0), new Point3D(10, 4, 0), new Point3D(12, 1, 0));
            GeometryMatchResult good = Query.MatchPolygons(new[] { p1, p2 }, new[] { n2, n1 });
            Assert.That(good.IsBijection, Is.True);
            Assert.That(good.PairForExpected(0).NativeIndex, Is.EqualTo(1));
            Assert.That(good.PairForExpected(1).NativeIndex, Is.EqualTo(0));

            // Replace one native polygon by the edge-rewired version: exactly that pair fails; the other stays verified.
            GeometryMatchResult bad = Query.MatchPolygons(new[] { p1, p2 }, new[] { p2Rewired, n1 });
            Assert.That(bad.IsBijection, Is.False);
            Assert.That(bad.UnmatchedExpected, Is.EqualTo(new[] { 1 }));
            Assert.That(bad.UnmatchedNative, Is.EqualTo(new[] { 0 }));
            Assert.That(Query.AssessIdentity(bad, 0, null), Is.EqualTo(IdentityStatus.Verified));
            Assert.That(Query.AssessIdentity(bad, 1, null), Is.EqualTo(IdentityStatus.Mismatch));
        }

        [Test]
        public void One_To_One_Ambiguity_And_Carrier_Contradiction_Checks_Are_Preserved()
        {
            Polygon3D p = Poly(A, B, C, D, E);

            // Two authored copies, one native copy (rotated): many to one.
            GeometryMatchResult manyToOne = Query.MatchPolygons(new[] { p, Poly(A, B, C, D, E) }, new[] { Poly(C, D, E, A, B) });
            Assert.That(manyToOne.IsBijection, Is.False);
            Assert.That(manyToOne.AmbiguousExpected, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(Query.AssessIdentity(manyToOne, 0, true), Is.EqualTo(IdentityStatus.Mismatch));

            // One authored polygon, two native copies (one reversed): one to many.
            GeometryMatchResult oneToMany = Query.MatchPolygons(new[] { p }, new[] { Poly(A, B, C, D, E), Poly(E, D, C, B, A) });
            Assert.That(oneToMany.IsBijection, Is.False);
            Assert.That(oneToMany.AmbiguousNative, Is.EqualTo(new[] { 0, 1 }));

            // A matching boundary whose writer carrier names another object is still a mismatch.
            GeometryMatchResult match = Query.MatchPolygons(new[] { p }, new[] { Poly(C, D, E, A, B) });
            Assert.That(Query.AssessIdentity(match, 0, false), Is.EqualTo(IdentityStatus.Mismatch));
            Assert.That(Query.AssessIdentity(match, 0, true), Is.EqualTo(IdentityStatus.Verified));
        }

        [Test]
        public void Reversed_Winding_Is_Reported_As_Facing_Not_Hidden()
        {
            GeometryMatchResult match = Query.MatchPolygons(new[] { Poly(A, B, C, D, E) }, new[] { Poly(E, D, C, B, A) });
            Assert.That(match.PairForExpected(0).SameFacing, Is.False);
            Assert.That(Query.MatchPolygons(new[] { Poly(A, B, C, D, E) }, new[] { Poly(C, D, E, A, B) }).PairForExpected(0).SameFacing, Is.True);
        }

        [Test]
        public void Polygons_With_Different_Vertex_Counts_Or_Fewer_Than_Three_Vertices_Never_Match()
        {
            Assert.That(Query.MatchPolygons(new[] { Poly(A, B, C, D, E) }, new[] { Poly(A, B, C, E) }).IsBijection, Is.False);
            Assert.That(Query.MatchPolygons(new Polygon3D[] { null }, new Polygon3D[] { null }).IsBijection, Is.False);
        }

        private static IEnumerable<int[]> Permutations(int n)
        {
            int[] items = Enumerable.Range(0, n).ToArray();
            return Permute(items, 0);
        }

        private static IEnumerable<int[]> Permute(int[] items, int start)
        {
            if (start == items.Length)
            {
                yield return (int[])items.Clone();
                yield break;
            }

            for (int i = start; i < items.Length; i++)
            {
                (items[start], items[i]) = (items[i], items[start]);
                foreach (int[] result in Permute(items, start + 1))
                {
                    yield return result;
                }

                (items[start], items[i]) = (items[i], items[start]);
            }
        }
    }
}
