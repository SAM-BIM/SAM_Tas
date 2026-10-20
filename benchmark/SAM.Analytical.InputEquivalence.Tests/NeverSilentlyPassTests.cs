// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.InputEquivalence.Tests
{
    /// <summary>
    /// The PASS allow-list, checked exhaustively: every combination of SAM state, native reader state, listing,
    /// listed default, transformation, identity, alignment, interpretation, rule set, quantity class and owner
    /// acceptance is evaluated, and a pass must satisfy every invariant. A failure here means an unsupported,
    /// unverified, unresolved or accepted-limitation item became PASS.
    /// </summary>
    [TestFixture]
    public class NeverSilentlyPassTests
    {
        private static readonly EquivalenceOutcome[] passOutcomes =
        {
            EquivalenceOutcome.Match, EquivalenceOutcome.WithinTolerance, EquivalenceOutcome.AcceptedTransformation, EquivalenceOutcome.NotApplicable
        };

        private static IEnumerable<ExpectedValue> Expecteds()
        {
            yield return ExpectedValue.FromValue(10);
            yield return ExpectedValue.FromValue(0.00005); // below the 7a absolute tolerance of 1e-4: the zeroed-native probe
            yield return ExpectedValue.Zero();
            yield return ExpectedValue.Unspecified();
            yield return ExpectedValue.Invalid("invalid");
            yield return null;
        }

        private static IEnumerable<ReaderResult> Natives()
        {
            yield return ReaderResult.Absent();
            yield return ReaderResult.Unreadable("x");
            yield return ReaderResult.NotANumber();
            yield return ReaderResult.FromValue(0);
            yield return ReaderResult.FromValue(10);
            yield return ReaderResult.FromValue(10.004);
            yield return ReaderResult.FromValue(11);
            yield return null;
        }

        [Test]
        public void Every_Pass_Satisfies_The_Allow_List_Invariants()
        {
            int evaluated = 0;
            int passes = 0;

            foreach (string quantity in new[] { "1a", "3c.g", "6.hourly", "7a" })
            foreach (ExpectedValue expected in Expecteds())
            foreach (ReaderResult native in Natives())
            foreach (ContractListing listing in Enum.GetValues(typeof(ContractListing)))
            foreach (double? listedDefault in new double?[] { null, 0, 10 })
            foreach (string transformation in new[] { null, "T-X" })
            foreach (bool verified in new[] { false, true })
            foreach (IdentityStatus identity in Enum.GetValues(typeof(IdentityStatus)))
            foreach (AlignmentStatus alignment in Enum.GetValues(typeof(AlignmentStatus)))
            foreach (ModelInterpretation interpretation in Enum.GetValues(typeof(ModelInterpretation)))
            foreach (GateRuleSet ruleSet in Enum.GetValues(typeof(GateRuleSet)))
            foreach (bool accepted in new[] { false, true })
            {
                QuantityInput input = new QuantityInput
                {
                    Definition = QuantityCatalog.Get(quantity),
                    FixtureId = "f",
                    ObjectKey = "o",
                    Expected = expected,
                    Native = native,
                    Listing = listing,
                    ListedDefault = listedDefault,
                    TransformationId = transformation,
                    TransformationVerified = verified,
                    Identity = identity,
                    Alignment = alignment,
                    Interpretation = interpretation
                };

                GateConfiguration configuration = new GateConfiguration
                {
                    RuleSet = ruleSet,
                    Acceptances = accepted
                        ? Enum.GetValues(typeof(AcceptableLimitation)).Cast<AcceptableLimitation>().Select(x => new OwnerAcceptance(quantity, "f", x, "owner")).ToList()
                        : new List<OwnerAcceptance>()
                };

                QuantityResult result = Query.Evaluate(input, configuration);
                evaluated++;
                string context = quantity + " " + expected + " / " + native + " listing=" + listing + " default=" + listedDefault + " T=" + transformation + "/" + verified +
                                 " id=" + identity + " align=" + alignment + " interp=" + interpretation + " rules=" + ruleSet + " accepted=" + accepted + " => " + result.Outcome + "/" + result.Status;

                if (result.IsPass)
                {
                    passes++;
                    Assert.That(passOutcomes, Contains.Item(result.Outcome), context);
                    Assert.That(identity, Is.EqualTo(IdentityStatus.Verified), "identity: " + context);
                    Assert.That(input.Definition.HourlyAligned && alignment != AlignmentStatus.Verified, Is.False, "alignment: " + context);
                    Assert.That(interpretation, Is.Not.EqualTo(ModelInterpretation.RequiredNotRecorded), "interpretation: " + context);
                    Assert.That(expected, Is.Not.Null, context);
                    Assert.That(expected.State, Is.Not.EqualTo(SamInputState.Invalid), "invalid input: " + context);
                    Assert.That(native, Is.Not.Null, context);
                    Assert.That(native.Kind, Is.Not.EqualTo(ReaderKind.Unreadable), context);
                    Assert.That(native.Kind, Is.Not.EqualTo(ReaderKind.NaN), context);
                    Assert.That(result.Proposed, Is.Null, "a proposed rule must never be a pass: " + context);
                    Assert.That(result.AcceptedBy, Is.Null, context);

                    if (result.Outcome == EquivalenceOutcome.AcceptedTransformation)
                    {
                        Assert.That(verified, Is.True, "an unverified transformation must not pass: " + context);
                    }

                    if (result.Outcome == EquivalenceOutcome.NotApplicable)
                    {
                        Assert.That(expected.State, Is.EqualTo(SamInputState.Unspecified), context);
                        Assert.That(native.Kind, Is.EqualTo(ReaderKind.Absent), context);
                    }

                    if (result.Outcome == EquivalenceOutcome.Match || result.Outcome == EquivalenceOutcome.WithinTolerance)
                    {
                        Assert.That(native.HasNumber, Is.True, context);
                        Assert.That(expected.State, Is.AnyOf(SamInputState.Value, SamInputState.Zero), context);

                        // Review finding 1: abs never turns a zeroed native object into a pass for a non-zero SAM value.
                        if (expected.State == SamInputState.Value)
                        {
                            Assert.That(native.Kind, Is.EqualTo(ReaderKind.Finite), "a non-zero S-VALUE must not pass against a zeroed native: " + context);
                        }
                    }
                }
                else
                {
                    // Everything that is not a pass has a status below 6.
                    Assert.That((int)result.Status, Is.LessThan((int)GateStatus.Pass), context);
                }

                // The limitation outcomes can never be a pass, with or without an acceptance.
                if (result.Outcome == EquivalenceOutcome.UnsupportedMapping || result.Outcome == EquivalenceOutcome.MappingDifference ||
                    result.Outcome == EquivalenceOutcome.DocumentedDefault || result.Outcome == EquivalenceOutcome.UnlistedDefault ||
                    result.Outcome == EquivalenceOutcome.ModelInterpretationRequired || result.Outcome == EquivalenceOutcome.InputInvalid ||
                    result.Outcome == EquivalenceOutcome.UnverifiedUnreadable || result.Outcome == EquivalenceOutcome.UnverifiedUnresolved ||
                    result.Outcome == EquivalenceOutcome.OutOfTolerance || result.Outcome == EquivalenceOutcome.ExportDefect)
                {
                    Assert.That(result.IsPass, Is.False, context);
                }

                // An informational result is only ever a numeric mismatch on a non-gated quantity, and never a pass.
                if (result.IsInformational)
                {
                    Assert.That(result.Outcome, Is.EqualTo(EquivalenceOutcome.OutOfTolerance), context);
                    Assert.That(input.Definition.Gated, Is.False, context);
                    Assert.That(result.IsPass, Is.False, context);
                    Assert.That(result.Comparison, Is.Not.Null, context);
                }

                // An accepted limitation is rank 5 and an unaccepted one at best rank 3, never better.
                if (result.AcceptedBy != null)
                {
                    Assert.That(result.Status, Is.EqualTo(GateStatus.NotPassAcceptedLimitation), context);
                }

                // The proposed (unapproved) cases are always at best BLOCKED, whatever the acceptance and the rule set.
                if (result.Proposed != null)
                {
                    Assert.That((int)result.Status, Is.LessThanOrEqualTo((int)GateStatus.BlockedOwnerDecision), context);
                    Assert.That(result.AcceptedBy, Is.Null, "an acceptance must not lift a proposed case: " + context);
                }
            }

            Assert.That(evaluated, Is.GreaterThan(100000), "the combinatorial sweep must really have run");
            Assert.That(passes, Is.GreaterThan(0), "and some combinations must pass, or the invariants are vacuous");
            TestContext.Out.WriteLine("evaluated " + evaluated + " combinations, " + passes + " pass");
        }

        [Test]
        public void The_Provisional_Rule_Set_Is_Never_More_Lenient_Than_The_Interim_One()
        {
            foreach (ExpectedValue expected in Expecteds())
            foreach (ReaderResult native in Natives())
            foreach (double? listedDefault in new double?[] { null, 0, 10 })
            {
                QuantityInput input = new QuantityInput
                {
                    Definition = QuantityCatalog.Get("4c"), FixtureId = "f", ObjectKey = "o", Expected = expected, Native = native, ListedDefault = listedDefault, Identity = IdentityStatus.Verified, Alignment = AlignmentStatus.Verified
                };

                QuantityResult interim = Query.Evaluate(input, new GateConfiguration());
                QuantityResult provisional = Query.Evaluate(input, new GateConfiguration { RuleSet = GateRuleSet.ProvisionalProposed });

                Assert.That((int)provisional.Status, Is.LessThanOrEqualTo((int)interim.Status), expected + " / " + native);
                Assert.That(provisional.IsPass, Is.EqualTo(interim.IsPass), "the rule set never changes pass versus not pass");
            }
        }
    }
}
