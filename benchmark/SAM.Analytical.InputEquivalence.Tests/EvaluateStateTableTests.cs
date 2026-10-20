// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.InputEquivalence.Tests
{
    /// <summary>The S07 section 6 table: every SAM input state against every native reader state.</summary>
    [TestFixture]
    public class EvaluateStateTableTests
    {
        private static QuantityResult Run(ExpectedValue expected, ReaderResult native, ContractListing listing = ContractListing.None, double? listedDefault = null,
            string transformation = null, bool transformationVerified = false, GateConfiguration configuration = null, string quantity = "1a")
        {
            QuantityInput input = new QuantityInput
            {
                Definition = QuantityCatalog.Get(quantity),
                FixtureId = TestData.Fixture,
                ObjectKey = "object",
                Expected = expected,
                Native = native,
                Identity = IdentityStatus.Verified,
                Alignment = AlignmentStatus.Verified,
                Listing = listing,
                ListedDefault = listedDefault,
                TransformationId = transformation,
                TransformationVerified = transformationVerified
            };

            return Query.Evaluate(input, configuration);
        }

        private static readonly GateConfiguration provisional = new GateConfiguration { RuleSet = GateRuleSet.ProvisionalProposed };

        private static void Expect(QuantityResult result, EquivalenceOutcome outcome, GateStatus status)
        {
            Assert.That(result.Outcome, Is.EqualTo(outcome), result.Reason);
            Assert.That(result.Status, Is.EqualTo(status), result.Reason);
        }

        // ---------------------------------------------------------------- S-VALUE

        [Test]
        public void SValue_Equal_Is_Match_And_Within_Is_WithinTolerance()
        {
            Expect(Run(ExpectedValue.FromValue(20), ReaderResult.FromValue(20)), EquivalenceOutcome.Match, GateStatus.Pass);
            Expect(Run(ExpectedValue.FromValue(20), ReaderResult.FromValue(20.05)), EquivalenceOutcome.WithinTolerance, GateStatus.Pass);
        }

        [Test]
        public void SValue_Outside_Tolerance_Fails_Without_A_Contract_Explanation()
        {
            QuantityResult result = Run(ExpectedValue.FromValue(20), ReaderResult.FromValue(21));
            Expect(result, EquivalenceOutcome.OutOfTolerance, GateStatus.Fail);
            Assert.That(result.Comparison.Verdict, Is.EqualTo(NumericVerdict.OutsideTolerance));
        }

        [Test]
        public void SValue_Native_Zero_Against_A_Value_Is_A_Zeroed_Object_Not_A_Match()
        {
            // Intentional expectation change (review finding 1): this used to be OutOfTolerance. A zeroed native object is
            // judged like a missing one (S07 section 6), so it is an EXPORT-DEFECT; the status (FAIL) is unchanged.
            Expect(Run(ExpectedValue.FromValue(20), ReaderResult.FromValue(0)), EquivalenceOutcome.ExportDefect, GateStatus.Fail);
        }

        [Test]
        public void SValue_Missing_Native_Is_An_Export_Defect_Unless_A_Limitation_Is_Listed()
        {
            Expect(Run(ExpectedValue.FromValue(20), ReaderResult.Absent()), EquivalenceOutcome.ExportDefect, GateStatus.Fail);
            Expect(Run(ExpectedValue.FromValue(20), ReaderResult.Absent(), ContractListing.UnsupportedMapping), EquivalenceOutcome.UnsupportedMapping, GateStatus.BlockedOwnerDecision);
            Expect(Run(ExpectedValue.FromValue(20), ReaderResult.Absent(), ContractListing.MappingDifference), EquivalenceOutcome.MappingDifference, GateStatus.BlockedOwnerDecision);
        }

        [Test]
        public void SValue_A_Listed_Limitation_Explains_A_Difference_But_Is_Never_A_Pass()
        {
            QuantityResult unsupported = Run(ExpectedValue.FromValue(20), ReaderResult.FromValue(25), ContractListing.UnsupportedMapping);
            Expect(unsupported, EquivalenceOutcome.UnsupportedMapping, GateStatus.BlockedOwnerDecision);
            Assert.That(unsupported.Comparison, Is.Not.Null);
            Assert.That(unsupported.OwnerDecisionNamed, Does.Contain("owner acceptance"));

            Expect(Run(ExpectedValue.FromValue(20), ReaderResult.FromValue(25), ContractListing.MappingDifference), EquivalenceOutcome.MappingDifference, GateStatus.BlockedOwnerDecision);
        }

        [Test]
        public void SValue_A_Listed_Limitation_That_Did_Not_Manifest_Is_Judged_On_The_Numbers()
        {
            Expect(Run(ExpectedValue.FromValue(20), ReaderResult.FromValue(20), ContractListing.UnsupportedMapping), EquivalenceOutcome.Match, GateStatus.Pass);
        }

        [Test]
        public void SValue_Unreadable_Is_A_Technical_Block_Not_A_Nan_And_Not_A_Zero()
        {
            QuantityResult result = Run(ExpectedValue.FromValue(20), ReaderResult.Unreadable("TBD.exe not licensed"));
            Expect(result, EquivalenceOutcome.UnverifiedUnreadable, GateStatus.BlockedTechnical);
            Assert.That(result.TechnicalBlockerNamed, Does.Contain("TBD.exe not licensed"));
        }

        [Test]
        public void SValue_Native_NaN_Is_The_Proposed_G3_Case_Interim_Is_Blocked_Provisional_Is_Fail()
        {
            QuantityResult interim = Run(ExpectedValue.FromValue(20), ReaderResult.NotANumber());
            Expect(interim, EquivalenceOutcome.ExportDefect, GateStatus.BlockedOwnerDecision);
            Assert.That(interim.Proposed, Does.Contain("G3").And.Contain("proposed"));
            Assert.That(interim.OwnerDecisionNamed, Is.EqualTo("OD-G3"));

            QuantityResult proposed = Run(ExpectedValue.FromValue(20), ReaderResult.NotANumber(), configuration: provisional);
            Expect(proposed, EquivalenceOutcome.ExportDefect, GateStatus.Fail);
            Assert.That(proposed.Proposed, Is.Not.Null, "still labelled proposed");
        }

        [Test]
        public void SValue_Native_NaN_Is_Not_Covered_By_A_Listed_Limitation()
        {
            QuantityResult result = Run(ExpectedValue.FromValue(20), ReaderResult.NotANumber(), ContractListing.UnsupportedMapping, configuration: provisional);
            Expect(result, EquivalenceOutcome.ExportDefect, GateStatus.Fail);
        }

        [Test]
        public void SValue_A_Transformation_Counts_Only_When_Verified()
        {
            Expect(Run(ExpectedValue.FromValue(20), ReaderResult.FromValue(20), transformation: "T-DD", transformationVerified: true), EquivalenceOutcome.AcceptedTransformation, GateStatus.Pass);
            Expect(Run(ExpectedValue.FromValue(20), ReaderResult.FromValue(20), transformation: "T-DD", transformationVerified: false), EquivalenceOutcome.UnverifiedUnresolved, GateStatus.NotRun);
            Expect(Run(ExpectedValue.FromValue(20), ReaderResult.FromValue(30), transformation: "T-DD", transformationVerified: true), EquivalenceOutcome.OutOfTolerance, GateStatus.Fail);
        }

        // ---------------------------------------------------------------- S-ZERO

        [Test]
        public void SZero_Native_Zero_Is_A_Match()
        {
            Expect(Run(ExpectedValue.Zero(), ReaderResult.FromValue(0)), EquivalenceOutcome.Match, GateStatus.Pass);
        }

        [Test]
        public void SZero_Tiny_Native_Is_Within_The_Tolerance_Of_Zero_And_Large_Native_Fails()
        {
            Expect(Run(ExpectedValue.Zero(), ReaderResult.FromValue(0.005)), EquivalenceOutcome.WithinTolerance, GateStatus.Pass);
            Expect(Run(ExpectedValue.Zero(), ReaderResult.FromValue(0.5)), EquivalenceOutcome.OutOfTolerance, GateStatus.Fail);
        }

        [Test]
        public void SZero_Relative_Only_Tolerance_Gives_No_Absolute_Floor_Near_Zero()
        {
            // Row 9 (Tas weather) has a relative criterion only: a native 1e-9 against SAM zero is outside.
            Expect(Run(ExpectedValue.Zero(), ReaderResult.FromValue(1e-9), quantity: "9"), EquivalenceOutcome.OutOfTolerance, GateStatus.Fail);
        }

        [Test]
        public void SZero_Omission_Needs_A_Verified_Listed_Transformation_Otherwise_Export_Defect()
        {
            Expect(Run(ExpectedValue.Zero(), ReaderResult.Absent()), EquivalenceOutcome.ExportDefect, GateStatus.Fail);
            Expect(Run(ExpectedValue.Zero(), ReaderResult.Absent(), transformation: "T-OMIT0", transformationVerified: true), EquivalenceOutcome.AcceptedTransformation, GateStatus.Pass);
            Expect(Run(ExpectedValue.Zero(), ReaderResult.Absent(), transformation: "T-OMIT0", transformationVerified: false), EquivalenceOutcome.UnverifiedUnresolved, GateStatus.NotRun);
        }

        [Test]
        public void SZero_Omission_Is_Not_Rescued_By_A_Limitation_Listing()
        {
            Expect(Run(ExpectedValue.Zero(), ReaderResult.Absent(), ContractListing.UnsupportedMapping), EquivalenceOutcome.ExportDefect, GateStatus.Fail);
        }

        [Test]
        public void SZero_Native_NaN_Is_An_Approved_Export_Defect_In_Both_Rule_Sets_And_Not_Proposed()
        {
            foreach (GateConfiguration configuration in new[] { new GateConfiguration(), provisional })
            {
                QuantityResult result = Run(ExpectedValue.Zero(), ReaderResult.NotANumber(), configuration: configuration);
                Expect(result, EquivalenceOutcome.ExportDefect, GateStatus.Fail);
                Assert.That(result.Proposed, Is.Null);
            }
        }

        [Test]
        public void SZero_Unreadable_Is_A_Technical_Block()
        {
            Expect(Run(ExpectedValue.Zero(), ReaderResult.Unreadable("x")), EquivalenceOutcome.UnverifiedUnreadable, GateStatus.BlockedTechnical);
        }

        // ---------------------------------------------------------------- S-UNSPEC

        [Test]
        public void SUnspec_Absent_Native_Is_Not_Applicable_And_Passes()
        {
            Expect(Run(ExpectedValue.Unspecified(), ReaderResult.Absent()), EquivalenceOutcome.NotApplicable, GateStatus.Pass);
        }

        [Test]
        public void SUnspec_The_Listed_Default_Is_A_Documented_Default_That_Needs_An_Owner_Acceptance()
        {
            QuantityResult result = Run(ExpectedValue.Unspecified(), ReaderResult.FromValue(0.3), listedDefault: 0.3, quantity: "4c");
            Expect(result, EquivalenceOutcome.DocumentedDefault, GateStatus.BlockedOwnerDecision);
            Assert.That(result.Proposed, Is.Null, "a gated quantity follows the approved rule, not a proposal");
            Assert.That(result.OwnerDecisionNamed, Does.Contain("owner acceptance of DocumentedDefault for 4c"));
        }

        [Test]
        public void SUnspec_An_Unaccepted_Default_On_A_Non_Gated_Quantity_Is_The_Proposed_G2_Case()
        {
            QuantityResult result = Run(ExpectedValue.Unspecified(), ReaderResult.FromValue(0.5), listedDefault: 0.5, quantity: "3c.g");
            Expect(result, EquivalenceOutcome.DocumentedDefault, GateStatus.BlockedOwnerDecision);
            Assert.That(result.Proposed, Does.Contain("G2"));
            Assert.That(result.OwnerDecisionNamed, Does.Contain("OD-G2"));
        }

        [Test]
        public void SUnspec_A_Non_Zero_Value_That_Is_Not_The_Listed_Default_Is_An_Export_Defect()
        {
            Expect(Run(ExpectedValue.Unspecified(), ReaderResult.FromValue(0.7), listedDefault: 0.3, quantity: "4c"), EquivalenceOutcome.ExportDefect, GateStatus.Fail);
            Expect(Run(ExpectedValue.Unspecified(), ReaderResult.FromValue(0.7), quantity: "4c"), EquivalenceOutcome.ExportDefect, GateStatus.Fail);
        }

        [Test]
        public void SUnspec_Native_Zero_Is_Never_Not_Applicable_And_Never_A_Match()
        {
            QuantityResult unlisted = Run(ExpectedValue.Unspecified(), ReaderResult.FromValue(0), quantity: "4c");
            Expect(unlisted, EquivalenceOutcome.UnlistedDefault, GateStatus.BlockedOwnerDecision);
            Assert.That(unlisted.Proposed, Does.Contain("G1").And.Contain("UNLISTED-DEFAULT").And.Contain("proposed"));
            Assert.That(unlisted.OwnerDecisionNamed, Is.EqualTo("OD-G1"));

            QuantityResult listedZero = Run(ExpectedValue.Unspecified(), ReaderResult.FromValue(0), listedDefault: 0, quantity: "4c");
            Expect(listedZero, EquivalenceOutcome.DocumentedDefault, GateStatus.BlockedOwnerDecision);
            Assert.That(listedZero.Proposed, Does.Contain("G1"));
        }

        [Test]
        public void SUnspec_Native_NaN_Is_The_Proposed_G1_NaN_Case()
        {
            QuantityResult interim = Run(ExpectedValue.Unspecified(), ReaderResult.NotANumber());
            Expect(interim, EquivalenceOutcome.ExportDefect, GateStatus.BlockedOwnerDecision);
            Assert.That(interim.Proposed, Does.Contain("G1-NaN"));

            Expect(Run(ExpectedValue.Unspecified(), ReaderResult.NotANumber(), configuration: provisional), EquivalenceOutcome.ExportDefect, GateStatus.Fail);
        }

        [Test]
        public void SUnspec_Unreadable_Is_Unverified_Not_Applicable_Is_Not_Assumed()
        {
            Expect(Run(ExpectedValue.Unspecified(), ReaderResult.Unreadable("COM")), EquivalenceOutcome.UnverifiedUnreadable, GateStatus.BlockedTechnical);
        }

        // ---------------------------------------------------------------- S-INVALID

        [Test]
        public void SInvalid_Is_Never_Compared_Whatever_The_Native_Holds()
        {
            ExpectedValue invalid = ExpectedValue.Invalid("stored Area missing");
            foreach (ReaderResult native in new[] { ReaderResult.Absent(), ReaderResult.FromValue(20), ReaderResult.FromValue(0), ReaderResult.NotANumber(), ReaderResult.Unreadable("x") })
            {
                QuantityResult result = Run(invalid, native);
                Expect(result, EquivalenceOutcome.InputInvalid, GateStatus.BlockedTechnical);
                Assert.That(result.Comparison, Is.Null);
                Assert.That(result.TechnicalBlockerNamed, Does.Contain("stored Area missing"));
            }
        }

        [Test]
        public void Missing_Expected_Or_Native_Objects_Fail_Closed()
        {
            Expect(Run(null, ReaderResult.FromValue(1)), EquivalenceOutcome.InputInvalid, GateStatus.BlockedTechnical);
            Expect(Run(ExpectedValue.FromValue(1), null), EquivalenceOutcome.UnverifiedUnreadable, GateStatus.BlockedTechnical);
        }

        [Test]
        public void Evaluate_Rejects_Inputs_That_Cannot_Be_Gated()
        {
            Assert.Throws<ArgumentNullException>(() => Query.Evaluate(null));
            Assert.Throws<ArgumentException>(() => Query.Evaluate(new QuantityInput { FixtureId = "f", Expected = ExpectedValue.Zero(), Native = ReaderResult.FromValue(0) }));
            Assert.Throws<ArgumentException>(() => Query.Evaluate(new QuantityInput { Definition = QuantityCatalog.Get("1a"), Expected = ExpectedValue.Zero(), Native = ReaderResult.FromValue(0) }));
        }
    }
}
