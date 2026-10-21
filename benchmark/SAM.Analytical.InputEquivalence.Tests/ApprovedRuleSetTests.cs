// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using System.Collections.Generic;

namespace SAM.Analytical.InputEquivalence.Tests
{
    /// <summary>
    /// S08 PR-C: OD-G1, OD-G2 and OD-G3 as the owner approved them on 2026-10-21 (S07 section 6.1 as drafted), and the one
    /// owner-approved refinement of G1, the verified inactive-profile transformation. The pre-decision rule sets are unchanged.
    /// </summary>
    public class ApprovedRuleSetTests
    {
        private static readonly GateConfiguration approved = new GateConfiguration { RuleSet = GateRuleSet.Approved };

        private static QuantityInput Input(string quantity, ExpectedValue expected, ReaderResult native, string inactive = null, double? listedDefault = null)
        {
            return new QuantityInput
            {
                Definition = QuantityCatalog.Get(quantity),
                FixtureId = "f",
                ObjectKey = "o",
                Expected = expected,
                Native = native,
                Identity = IdentityStatus.Verified,
                Alignment = AlignmentStatus.Verified,
                ListedDefault = listedDefault,
                InactiveNativeTransformation = inactive
            };
        }

        [Test]
        public void G1_OnAGatedQuantity_IsAnUnacceptedUnlistedDefault_Rank3_NotProposed()
        {
            QuantityResult result = Query.Evaluate(Input("6.hourly", ExpectedValue.Unspecified(), ReaderResult.FromValue(0)), approved);

            Assert.That(result.Outcome, Is.EqualTo(EquivalenceOutcome.UnlistedDefault));
            Assert.That(result.Status, Is.EqualTo(GateStatus.BlockedOwnerDecision));
            Assert.That(result.Proposed, Is.Null);
            Assert.That(result.OwnerDecisionNamed, Does.Contain("DocumentedDefault"));
            Assert.That(result.Reason, Does.Contain("Approved rule G1"));
        }

        [Test]
        public void G1_AcceptedByTheOwner_IsRank5_NeverAPass()
        {
            GateConfiguration accepted = new GateConfiguration
            {
                RuleSet = GateRuleSet.Approved,
                Acceptances = new List<OwnerAcceptance> { new OwnerAcceptance("6.hourly", "f", AcceptableLimitation.DocumentedDefault, "owner test decision") }
            };

            QuantityResult result = Query.Evaluate(Input("6.hourly", ExpectedValue.Unspecified(), ReaderResult.FromValue(0)), accepted);

            Assert.That(result.Status, Is.EqualTo(GateStatus.NotPassAcceptedLimitation));
            Assert.That(result.IsPass, Is.False);
        }

        [Test]
        public void G1_WithZeroAsTheListedDefault_IsADocumentedDefault()
        {
            QuantityResult result = Query.Evaluate(Input("4c", ExpectedValue.Unspecified(), ReaderResult.FromValue(0), listedDefault: 0), approved);

            Assert.That(result.Outcome, Is.EqualTo(EquivalenceOutcome.DocumentedDefault));
            Assert.That(result.Status, Is.EqualTo(GateStatus.BlockedOwnerDecision));
            Assert.That(result.Proposed, Is.Null);
        }

        [Test]
        public void G1_OnANonGatedQuantity_IsG2_Rank3_NotProposed()
        {
            QuantityResult result = Query.Evaluate(Input("3c.g", ExpectedValue.Unspecified(), ReaderResult.FromValue(0)), approved);

            Assert.That(result.Status, Is.EqualTo(GateStatus.BlockedOwnerDecision));
            Assert.That(result.Proposed, Is.Null);
            Assert.That(result.OwnerDecisionNamed, Does.Contain("approved rule G2"));
        }

        [Test]
        public void G1NaN_And_G3_AreExportDefects()
        {
            QuantityResult g1NaN = Query.Evaluate(Input("6.hourly", ExpectedValue.Unspecified(), ReaderResult.NotANumber()), approved);
            QuantityResult g3 = Query.Evaluate(Input("6.hourly", ExpectedValue.FromValue(0.5), ReaderResult.NotANumber()), approved);

            foreach (QuantityResult result in new[] { g1NaN, g3 })
            {
                Assert.That(result.Outcome, Is.EqualTo(EquivalenceOutcome.ExportDefect));
                Assert.That(result.Status, Is.EqualTo(GateStatus.Fail));
                Assert.That(result.Proposed, Is.Null);
            }
        }

        [Test]
        public void TheInactiveTransformation_PassesOnlyAnExactNativeZero_AgainstSUnspec()
        {
            QuantityResult zero = Query.Evaluate(Input("8b", ExpectedValue.Unspecified(), ReaderResult.FromValue(0), "T-VENT-INACTIVE"), approved);
            Assert.That(zero.Outcome, Is.EqualTo(EquivalenceOutcome.AcceptedTransformation));
            Assert.That(zero.Status, Is.EqualTo(GateStatus.Pass));

            // No exemption for a non-zero, NaN, unreadable or absent native, or for a SAM value.
            Assert.That(Query.Evaluate(Input("8b", ExpectedValue.Unspecified(), ReaderResult.FromValue(1e-9), "T-VENT-INACTIVE"), approved).Status, Is.EqualTo(GateStatus.Fail));
            Assert.That(Query.Evaluate(Input("8b", ExpectedValue.Unspecified(), ReaderResult.NotANumber(), "T-VENT-INACTIVE"), approved).Status, Is.EqualTo(GateStatus.Fail));
            Assert.That(Query.Evaluate(Input("8b", ExpectedValue.Unspecified(), ReaderResult.Unreadable("x"), "T-VENT-INACTIVE"), approved).Status, Is.EqualTo(GateStatus.BlockedTechnical));
            Assert.That(Query.Evaluate(Input("8b", ExpectedValue.Unspecified(), ReaderResult.Absent(), "T-VENT-INACTIVE"), approved).Outcome, Is.EqualTo(EquivalenceOutcome.NotApplicable));
            Assert.That(Query.Evaluate(Input("8b", ExpectedValue.FromValue(0.01), ReaderResult.FromValue(0), "T-VENT-INACTIVE"), approved).Status, Is.EqualTo(GateStatus.Fail));
        }

        [Test]
        public void TheInactiveTransformation_IsIgnoredByThePreDecisionRuleSets()
        {
            foreach (GateRuleSet ruleSet in new[] { GateRuleSet.Interim, GateRuleSet.ProvisionalProposed })
            {
                QuantityResult result = Query.Evaluate(Input("8b", ExpectedValue.Unspecified(), ReaderResult.FromValue(0), "T-VENT-INACTIVE"), new GateConfiguration { RuleSet = ruleSet });
                Assert.That(result.Outcome, Is.EqualTo(EquivalenceOutcome.UnlistedDefault), ruleSet.ToString());
                Assert.That(result.Status, Is.EqualTo(GateStatus.BlockedOwnerDecision), ruleSet.ToString());
                Assert.That(result.Proposed, Is.Not.Null, ruleSet.ToString());
            }
        }

        [Test]
        public void WithoutTheTransformation_AZeroStaysG1_UnderTheApprovedRules()
        {
            QuantityResult result = Query.Evaluate(Input("8b", ExpectedValue.Unspecified(), ReaderResult.FromValue(0)), approved);

            Assert.That(result.Outcome, Is.EqualTo(EquivalenceOutcome.UnlistedDefault));
            Assert.That(result.IsPass, Is.False);
        }

        [Test]
        public void TheApprovedRules_NeverPassWhatThePreDecisionRulesFailOrBlock_ExceptThroughTheTransformation()
        {
            ExpectedValue[] expecteds = { ExpectedValue.Unspecified(), ExpectedValue.Zero(), ExpectedValue.FromValue(0.5), ExpectedValue.Invalid("x") };
            ReaderResult[] natives = { ReaderResult.Absent(), ReaderResult.Unreadable("x"), ReaderResult.NotANumber(), ReaderResult.FromValue(0), ReaderResult.FromValue(0.5), ReaderResult.FromValue(3) };
            foreach (string quantity in new[] { "6.hourly", "8b", "4c", "3c.g" })
            foreach (ExpectedValue expected in expecteds)
            foreach (ReaderResult native in natives)
            {
                QuantityResult interim = Query.Evaluate(Input(quantity, expected, native), new GateConfiguration());
                QuantityResult result = Query.Evaluate(Input(quantity, expected, native), approved);
                Assert.That(result.IsPass, Is.EqualTo(interim.IsPass), quantity + " " + expected + " / " + native);
            }
        }
    }
}
