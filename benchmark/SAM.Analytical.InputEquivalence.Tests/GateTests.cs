// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.InputEquivalence.Tests
{
    /// <summary>The prerequisites (identity, alignment, interpretation), the owner acceptances and the six-rank gate.</summary>
    [TestFixture]
    public class GateTests
    {
        private static QuantityInput Base(string quantity = "1a", string fixture = TestData.Fixture, ExpectedValue expected = null, ReaderResult native = null,
            IdentityStatus identity = IdentityStatus.Verified, AlignmentStatus alignment = AlignmentStatus.Verified, ContractListing listing = ContractListing.None,
            ModelInterpretation interpretation = ModelInterpretation.NotRequired)
        {
            return new QuantityInput
            {
                Definition = QuantityCatalog.Get(quantity),
                FixtureId = fixture,
                ObjectKey = "object",
                Expected = expected ?? ExpectedValue.FromValue(20),
                Native = native ?? ReaderResult.FromValue(20),
                Identity = identity,
                Alignment = alignment,
                Listing = listing,
                Interpretation = interpretation
            };
        }

        private static QuantityResult Pass(string quantity = "1a") => Query.Evaluate(Base(quantity));

        private static QuantityResult Rank1() => Query.Evaluate(Base(native: ReaderResult.FromValue(25)));
        private static QuantityResult Rank2() => Query.Evaluate(Base(native: ReaderResult.Unreadable("COM")));
        private static QuantityResult Rank3() => Query.Evaluate(Base(native: ReaderResult.Absent(), listing: ContractListing.UnsupportedMapping));
        private static QuantityResult Rank4() => Query.Evaluate(Base(identity: IdentityStatus.NotEstablished));

        private static GateConfiguration Accepting(string quantity = "1a", string fixture = TestData.Fixture, AcceptableLimitation limitation = AcceptableLimitation.UnsupportedMapping)
        {
            return new GateConfiguration { Acceptances = new[] { new OwnerAcceptance(quantity, fixture, limitation, "owner decision 2026-10-21 chat") } };
        }

        private static QuantityResult Rank5() => Query.Evaluate(Base(native: ReaderResult.Absent(), listing: ContractListing.UnsupportedMapping), Accepting());

        // ---------------------------------------------------------------- the six ranks

        [Test]
        public void Each_Rank_Has_Its_Roadmap_Status()
        {
            Assert.That(Query.Gate(new[] { Rank1() }).Status, Is.EqualTo(GateStatus.Fail));
            Assert.That(Query.Gate(new[] { Rank2() }).Status, Is.EqualTo(GateStatus.BlockedTechnical));
            Assert.That(Query.Gate(new[] { Rank3() }).Status, Is.EqualTo(GateStatus.BlockedOwnerDecision));
            Assert.That(Query.Gate(new[] { Rank4() }).Status, Is.EqualTo(GateStatus.NotRun));
            Assert.That(Query.Gate(new[] { Rank5() }).Status, Is.EqualTo(GateStatus.NotPassAcceptedLimitation));
            Assert.That(Query.Gate(new[] { Pass() }).Status, Is.EqualTo(GateStatus.Pass));

            Assert.That(new[] { GateStatus.Fail, GateStatus.BlockedTechnical, GateStatus.BlockedOwnerDecision, GateStatus.NotRun, GateStatus.NotPassAcceptedLimitation, GateStatus.Pass }.Select(x => (int)x), Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6 }));
        }

        [Test]
        public void The_Highest_Ranked_Trigger_Decides_And_Every_Non_Pass_Item_Stays_Visible()
        {
            QuantityResult[] all = { Pass(), Rank5(), Rank4(), Rank3(), Rank2(), Rank1() };
            GateReport report = Query.Gate(all);
            Assert.That(report.Status, Is.EqualTo(GateStatus.Fail));
            Assert.That(report.NonPass.Count, Is.EqualTo(5));
            Assert.That(report.CountByOutcome.Values.Sum(), Is.EqualTo(6));

            Assert.That(Query.Gate(new[] { Pass(), Rank5(), Rank4(), Rank3(), Rank2() }).Status, Is.EqualTo(GateStatus.BlockedTechnical));
            Assert.That(Query.Gate(new[] { Pass(), Rank5(), Rank4(), Rank3() }).Status, Is.EqualTo(GateStatus.BlockedOwnerDecision));
            Assert.That(Query.Gate(new[] { Pass(), Rank5(), Rank4() }).Status, Is.EqualTo(GateStatus.NotRun));
            Assert.That(Query.Gate(new[] { Pass(), Rank5() }).Status, Is.EqualTo(GateStatus.NotPassAcceptedLimitation));
            Assert.That(Query.Gate(new[] { Pass(), Pass() }).Status, Is.EqualTo(GateStatus.Pass));
        }

        [Test]
        public void The_Order_Of_The_Results_Does_Not_Change_The_Verdict()
        {
            QuantityResult[] all = { Pass(), Rank5(), Rank4(), Rank3(), Rank2(), Rank1() };
            Assert.That(Query.Gate(Enumerable.Reverse(all)).Status, Is.EqualTo(Query.Gate(all).Status));
        }

        [Test]
        public void An_Empty_Result_Set_Is_Never_A_Pass()
        {
            Assert.That(Query.Gate(new QuantityResult[0]).Status, Is.EqualTo(GateStatus.NotRun));
            Assert.That(Query.Gate(null).Status, Is.EqualTo(GateStatus.NotRun));
        }

        [Test]
        public void A_Required_Quantity_That_Was_Never_Evaluated_Makes_The_Gate_Not_Run()
        {
            GateConfiguration configuration = new GateConfiguration { RequiredQuantityIds = new[] { "1a", "2" } };
            GateReport report = Query.Gate(new[] { Pass("1a") }, configuration);
            Assert.That(report.Status, Is.EqualTo(GateStatus.NotRun));
            Assert.That(report.MissingRequiredQuantityIds, Is.EqualTo(new[] { "2" }));
            Assert.That(report.RequiredListSupplied, Is.True);

            Assert.That(Query.Gate(new[] { Pass("1a"), Pass("2") }, configuration).Status, Is.EqualTo(GateStatus.Pass));
            Assert.That(Query.Gate(new[] { Rank1() }, configuration).Status, Is.EqualTo(GateStatus.Fail), "a more severe rank is not hidden by a missing quantity");
            Assert.That(Query.Gate(new[] { Pass("1a") }).RequiredListSupplied, Is.False, "without a list the gate says it cannot detect omissions");
        }

        [Test]
        public void The_Gate_Is_Per_Fixture()
        {
            Assert.Throws<ArgumentException>(() => Query.Gate(new[] { Query.Evaluate(Base(fixture: "A")), Query.Evaluate(Base(fixture: "B")) }));
            Assert.That(Query.Gate(new[] { Query.Evaluate(Base(fixture: "A")) }).FixtureId, Is.EqualTo("A"));
        }

        // ---------------------------------------------------------------- owner acceptances (rank 5)

        [Test]
        public void An_Owner_Acceptance_Moves_A_Limitation_From_Rank_3_To_Rank_5_And_Never_To_Pass()
        {
            QuantityResult accepted = Rank5();
            Assert.That(accepted.Outcome, Is.EqualTo(EquivalenceOutcome.UnsupportedMapping));
            Assert.That(accepted.Status, Is.EqualTo(GateStatus.NotPassAcceptedLimitation));
            Assert.That(accepted.IsPass, Is.False);
            Assert.That(accepted.AcceptedBy, Is.EqualTo("owner decision 2026-10-21 chat"));
            Assert.That(Query.Gate(new[] { accepted, Pass("2") }).IsPass, Is.False);
        }

        [Test]
        public void An_Acceptance_Is_Per_Quantity_Per_Fixture_And_Per_Limitation()
        {
            QuantityInput input = Base(native: ReaderResult.Absent(), listing: ContractListing.UnsupportedMapping);

            Assert.That(Query.Evaluate(input, Accepting(quantity: "2")).Status, Is.EqualTo(GateStatus.BlockedOwnerDecision), "another quantity");
            Assert.That(Query.Evaluate(input, Accepting(fixture: "other")).Status, Is.EqualTo(GateStatus.BlockedOwnerDecision), "another fixture");
            Assert.That(Query.Evaluate(input, Accepting(limitation: AcceptableLimitation.MappingDifference)).Status, Is.EqualTo(GateStatus.BlockedOwnerDecision), "another limitation");
            Assert.That(Query.Evaluate(input, Accepting(limitation: AcceptableLimitation.DocumentedDefault)).Status, Is.EqualTo(GateStatus.BlockedOwnerDecision), "another limitation");
            Assert.That(Query.Evaluate(input, Accepting()).Status, Is.EqualTo(GateStatus.NotPassAcceptedLimitation));
        }

        [Test]
        public void An_Acceptance_Needs_A_Recorded_Decision_Reference()
        {
            Assert.Throws<ArgumentException>(() => new OwnerAcceptance("1a", "f", AcceptableLimitation.UnsupportedMapping, " "));
            Assert.Throws<ArgumentException>(() => new OwnerAcceptance("", "f", AcceptableLimitation.UnsupportedMapping, "x"));
            Assert.Throws<ArgumentException>(() => new OwnerAcceptance("1a", null, AcceptableLimitation.UnsupportedMapping, "x"));
        }

        [Test]
        public void An_Acceptance_Does_Not_Hide_A_Fail_Or_A_Technical_Block()
        {
            GateConfiguration configuration = Accepting();
            QuantityResult accepted = Query.Evaluate(Base(native: ReaderResult.Absent(), listing: ContractListing.UnsupportedMapping), configuration);
            Assert.That(Query.Gate(new[] { accepted, Rank1() }, configuration).Status, Is.EqualTo(GateStatus.Fail));
            Assert.That(Query.Gate(new[] { accepted, Rank2() }, configuration).Status, Is.EqualTo(GateStatus.BlockedTechnical));
        }

        [Test]
        public void Acceptance_Of_A_Documented_Default_Works_For_Gated_And_For_G2_Quantities()
        {
            GateConfiguration gated = Accepting("4c", limitation: AcceptableLimitation.DocumentedDefault);
            QuantityInput input = Base("4c", expected: ExpectedValue.Unspecified(), native: ReaderResult.FromValue(0.3));
            QuantityInput withDefault = new QuantityInput { Definition = input.Definition, FixtureId = input.FixtureId, ObjectKey = "o", Expected = input.Expected, Native = input.Native, Identity = IdentityStatus.Verified, ListedDefault = 0.3 };

            Assert.That(Query.Evaluate(withDefault).Status, Is.EqualTo(GateStatus.BlockedOwnerDecision));
            Assert.That(Query.Evaluate(withDefault, gated).Status, Is.EqualTo(GateStatus.NotPassAcceptedLimitation));

            QuantityInput nonGated = new QuantityInput { Definition = QuantityCatalog.Get("3c.g"), FixtureId = TestData.Fixture, ObjectKey = "o", Expected = ExpectedValue.Unspecified(), Native = ReaderResult.FromValue(0.5), Identity = IdentityStatus.Verified, ListedDefault = 0.5 };
            Assert.That(Query.Evaluate(nonGated).Status, Is.EqualTo(GateStatus.BlockedOwnerDecision), "G2: blocked until accepted");
            Assert.That(Query.Evaluate(nonGated, Accepting("3c.g", limitation: AcceptableLimitation.DocumentedDefault)).Status, Is.EqualTo(GateStatus.NotPassAcceptedLimitation), "then rank 5");
        }

        // ---------------------------------------------------------------- identity safeguard

        [Test]
        public void No_Pass_Without_Independent_Identity_Evidence()
        {
            foreach (IdentityStatus identity in new[] { IdentityStatus.NotEstablished, IdentityStatus.WriterOnly })
            {
                QuantityResult result = Query.Evaluate(Base(identity: identity));
                Assert.That(result.Outcome, Is.EqualTo(EquivalenceOutcome.UnverifiedUnresolved), identity.ToString());
                Assert.That(result.Status, Is.EqualTo(GateStatus.NotRun), identity.ToString());
                Assert.That(result.Reason, Does.Contain("not independently verified"));
            }

            Assert.That(Query.Evaluate(Base(identity: IdentityStatus.Verified)).IsPass, Is.True);
        }

        [Test]
        public void The_Default_Identity_Is_Not_Established_So_Nothing_Passes_By_Omission()
        {
            QuantityInput input = new QuantityInput { Definition = QuantityCatalog.Get("1a"), FixtureId = "f", Expected = ExpectedValue.FromValue(20), Native = ReaderResult.FromValue(20) };
            Assert.That(input.Identity, Is.EqualTo(IdentityStatus.NotEstablished));
            Assert.That(Query.Evaluate(input).IsPass, Is.False);
        }

        [Test]
        public void An_Identity_Mismatch_Is_An_Export_Defect_Even_When_The_Numbers_Agree()
        {
            QuantityResult result = Query.Evaluate(Base(identity: IdentityStatus.Mismatch));
            Assert.That(result.Outcome, Is.EqualTo(EquivalenceOutcome.ExportDefect));
            Assert.That(result.Status, Is.EqualTo(GateStatus.Fail));
        }

        [Test]
        public void Identity_Does_Not_Hide_A_More_Severe_Numeric_Failure()
        {
            QuantityResult result = Query.Evaluate(Base(native: ReaderResult.FromValue(25), identity: IdentityStatus.NotEstablished));
            Assert.That(result.Status, Is.EqualTo(GateStatus.Fail));
            Assert.That(result.Reason, Does.Contain("Also:").And.Contain("not independently verified"));
        }

        // ---------------------------------------------------------------- hour alignment (S07 section 7)

        [Test]
        public void Hourly_Quantities_Are_At_Most_Not_Run_Until_The_Alignment_Is_Verified()
        {
            foreach (string id in new[] { "5.temperature", "5.humidity", "6.hourly", "8b" })
            {
                QuantityResult unverified = Query.Evaluate(Base(id, alignment: AlignmentStatus.Unverified));
                Assert.That(unverified.Status, Is.EqualTo(GateStatus.NotRun), id);
                Assert.That(unverified.Outcome, Is.EqualTo(EquivalenceOutcome.UnverifiedUnresolved), id);
                Assert.That(Query.Evaluate(Base(id, alignment: AlignmentStatus.Verified)).IsPass, Is.True, id);
            }
        }

        [Test]
        public void Alignment_Does_Not_Affect_Non_Hourly_Quantities()
        {
            Assert.That(Query.Evaluate(Base("1a", alignment: AlignmentStatus.Unverified)).IsPass, Is.True);
        }

        [Test]
        public void An_Eflh_Agreement_Never_Verifies_The_Hourly_Alignment()
        {
            // The EFLH quantity is not hourly-aligned and passes on its own; the hourly series stays NOT RUN regardless.
            Assert.That(Query.Evaluate(Base("6.eflh", expected: ExpectedValue.FromValue(3000), native: ReaderResult.FromValue(3000))).IsPass, Is.True);
            GateReport report = Query.Gate(new[]
            {
                Query.Evaluate(Base("6.eflh", expected: ExpectedValue.FromValue(3000), native: ReaderResult.FromValue(3000))),
                Query.Evaluate(Base("6.hourly", alignment: AlignmentStatus.Unverified))
            });
            Assert.That(report.Status, Is.EqualTo(GateStatus.NotRun));
        }

        // ---------------------------------------------------------------- model interpretation (D-F9)

        [Test]
        public void A_Required_Unrecorded_Interpretation_Blocks_On_The_Owner_Even_When_The_Numbers_Match()
        {
            QuantityResult result = Query.Evaluate(Base("2", interpretation: ModelInterpretation.RequiredNotRecorded));
            Assert.That(result.Outcome, Is.EqualTo(EquivalenceOutcome.ModelInterpretationRequired));
            Assert.That(result.Status, Is.EqualTo(GateStatus.BlockedOwnerDecision));
            Assert.That(result.OwnerDecisionNamed, Does.Contain("D-F9"));
        }

        [Test]
        public void A_Recorded_Interpretation_Never_Waives_A_Native_Versus_Geometric_Fail()
        {
            Assert.That(Query.Evaluate(Base("2", interpretation: ModelInterpretation.Recorded)).IsPass, Is.True);
            Assert.That(Query.Evaluate(Base("2", native: ReaderResult.FromValue(25), interpretation: ModelInterpretation.Recorded)).Status, Is.EqualTo(GateStatus.Fail));
            Assert.That(Query.Evaluate(Base("2", native: ReaderResult.FromValue(25), interpretation: ModelInterpretation.RequiredNotRecorded)).Status, Is.EqualTo(GateStatus.Fail), "the more severe rank decides");
        }

        // ---------------------------------------------------------------- report

        [Test]
        public void The_Report_Is_Labelled_Provisional_When_A_Proposed_Rule_Contributed()
        {
            QuantityResult g3 = Query.Evaluate(Base(native: ReaderResult.NotANumber()));
            GateReport report = Query.Gate(new[] { g3, Pass("2") });
            Assert.That(report.Provisional, Is.True);
            Assert.That(report.OwnerDecisionsNamed, Contains.Item("OD-G3"));
            Assert.That(report.RuleSet, Is.EqualTo(GateRuleSet.Interim));
            Assert.That(Query.Gate(new[] { Pass() }).Provisional, Is.False);
        }

        [Test]
        public void The_Report_Names_Technical_Blockers()
        {
            GateReport report = Query.Gate(new[] { Rank2() });
            Assert.That(report.TechnicalBlockersNamed.Single(), Does.Contain("COM"));
        }
    }
}
