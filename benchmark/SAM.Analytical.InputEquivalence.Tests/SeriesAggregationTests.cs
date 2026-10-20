// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.InputEquivalence.Tests
{
    /// <summary>
    /// Mixed-series aggregation: an informational mismatch (a non-gated quantity) must never outrank, tie with or hide a
    /// gating element, whatever the element order, so a mixed series can never give a fixture PASS while a gating element
    /// is not a pass. Row 9.ground is the non-gated quantity (S07: ground temperature is not gated); its tolerance is 1e-5 relative.
    /// </summary>
    [TestFixture]
    public class SeriesAggregationTests
    {
        private const string Quantity = "9.ground";
        private const string Series = "ground";

        private static readonly ExpectedValue ten = ExpectedValue.FromValue(10);

        // One element = (expected, native). The named kinds cover every outcome class an element can have.
        private static readonly (ExpectedValue expected, ReaderResult native) Info = (ten, ReaderResult.FromValue(12));           // informational mismatch
        private static readonly (ExpectedValue expected, ReaderResult native) Match = (ten, ReaderResult.FromValue(10));
        private static readonly (ExpectedValue expected, ReaderResult native) Within = (ten, ReaderResult.FromValue(10.0000001));
        private static readonly (ExpectedValue expected, ReaderResult native) Unreadable = (ten, ReaderResult.Unreadable("field unavailable")); // BlockedTechnical
        private static readonly (ExpectedValue expected, ReaderResult native) NaN = (ten, ReaderResult.NotANumber());              // proposed G3
        private static readonly (ExpectedValue expected, ReaderResult native) Absent = (ten, ReaderResult.Absent());              // ExportDefect
        private static readonly (ExpectedValue expected, ReaderResult native) Zeroed = (ten, ReaderResult.FromValue(0));          // ExportDefect (zeroed object)
        private static readonly (ExpectedValue expected, ReaderResult native) Invalid = (ExpectedValue.Invalid("no layers"), ReaderResult.FromValue(12)); // BlockedTechnical
        private static readonly (ExpectedValue expected, ReaderResult native) Default = (ExpectedValue.Unspecified(), ReaderResult.FromValue(0.5));       // listed default 0.5: G2

        private static GateConfiguration Rules(GateRuleSet ruleSet = GateRuleSet.Interim, bool acceptDefault = false)
        {
            return new GateConfiguration
            {
                RuleSet = ruleSet,
                Acceptances = acceptDefault ? new[] { new OwnerAcceptance(Quantity, TestData.Fixture, AcceptableLimitation.DocumentedDefault, "owner") } : new OwnerAcceptance[0]
            };
        }

        private static SeriesResult Evaluate(IEnumerable<(ExpectedValue expected, ReaderResult native)> elements, GateConfiguration configuration = null, IdentityStatus identity = IdentityStatus.Verified)
        {
            List<(ExpectedValue expected, ReaderResult native)> list = elements.ToList();
            QuantityInput prototype = Review.Evaluate(Quantity, ExpectedValue.Zero(), ReaderResult.FromValue(0), Series, identity: identity, listedDefault: 0.5).Input;
            return Query.EvaluateSeries(prototype, list.Select(x => x.expected).ToList(), list.Select(x => x.native).ToList(), configuration);
        }

        /// <summary>The fixture gate with complete coverage: floor area 20 vs 20 on 'zone' plus the series on 'ground'.</summary>
        private static GateReport Fixture(SeriesResult series, GateConfiguration configuration = null)
        {
            configuration = configuration ?? new GateConfiguration();
            return Query.Gate(new[] { Review.Match("1a", "zone"), series.GateResult }, new GateConfiguration
            {
                RuleSet = configuration.RuleSet,
                Acceptances = configuration.Acceptances,
                RequiredComparisons = Review.Requiring(("1a", "zone"), (Quantity, Series)).RequiredComparisons
            });
        }

        // ---------------------------------------------------------------- the reviewed reproductions

        [Test]
        public void Reproduction_Ground_10_10_Versus_12_Unreadable_Is_Blocked_Not_Pass()
        {
            SeriesResult series = Evaluate(new[] { Info, Unreadable });
            GateReport report = Fixture(series);

            Assert.That(series.Elements[0].IsInformational, Is.True, "scalar: informational Fail");
            Assert.That(series.Elements[0].Status, Is.EqualTo(GateStatus.Fail));
            Assert.That(series.Elements[1].Status, Is.EqualTo(GateStatus.BlockedTechnical), "scalar: gating BlockedTechnical");

            Assert.That(series.Aggregate, Is.SameAs(series.Elements[1]), "the gating element decides");
            Assert.That(series.GateResult.IsInformational, Is.False);
            Assert.That(series.GateResult.Status, Is.EqualTo(GateStatus.BlockedTechnical));
            Assert.That(report.IsPass, Is.False);
            Assert.That(report.Status, Is.EqualTo(GateStatus.BlockedTechnical));
            Assert.That(report.TechnicalBlockersNamed.Single(), Does.Contain("field unavailable"));
        }

        [Test]
        public void Reproduction_Ground_12_Absent_And_Absent_12_Give_The_Same_Fail()
        {
            GateReport informationalFirst = Fixture(Evaluate(new[] { Info, Absent }));
            GateReport defectFirst = Fixture(Evaluate(new[] { Absent, Info }));

            Assert.That(informationalFirst.Status, Is.EqualTo(GateStatus.Fail), "was Pass");
            Assert.That(defectFirst.Status, Is.EqualTo(GateStatus.Fail));
            Assert.That(informationalFirst.IsPass, Is.False);
        }

        // ---------------------------------------------------------------- every gating defect against an informational mismatch, both orders

        private static IEnumerable<TestCaseData> Defects()
        {
            // name, defect element, interim status, provisional status, outcome (interim)
            yield return new TestCaseData("unreadable", Unreadable, GateStatus.BlockedTechnical, GateStatus.BlockedTechnical, EquivalenceOutcome.UnverifiedUnreadable);
            yield return new TestCaseData("NaN (proposed G3)", NaN, GateStatus.BlockedOwnerDecision, GateStatus.Fail, EquivalenceOutcome.ExportDefect);
            yield return new TestCaseData("absent", Absent, GateStatus.Fail, GateStatus.Fail, EquivalenceOutcome.ExportDefect);
            yield return new TestCaseData("zeroed", Zeroed, GateStatus.Fail, GateStatus.Fail, EquivalenceOutcome.ExportDefect);
            yield return new TestCaseData("invalid input", Invalid, GateStatus.BlockedTechnical, GateStatus.BlockedTechnical, EquivalenceOutcome.InputInvalid);
            yield return new TestCaseData("unaccepted G2 default", Default, GateStatus.BlockedOwnerDecision, GateStatus.BlockedOwnerDecision, EquivalenceOutcome.DocumentedDefault);
        }

        [TestCaseSource(nameof(Defects))]
        public void A_Gating_Defect_Decides_Whatever_Its_Position_Beside_An_Informational_Mismatch(string name, (ExpectedValue expected, ReaderResult native) defect, GateStatus interim, GateStatus provisional, EquivalenceOutcome outcome)
        {
            foreach (GateRuleSet ruleSet in new[] { GateRuleSet.Interim, GateRuleSet.ProvisionalProposed })
            {
                GateStatus expectedStatus = ruleSet == GateRuleSet.Interim ? interim : provisional;
                foreach (bool defectFirst in new[] { false, true })
                {
                    GateConfiguration configuration = Rules(ruleSet);
                    SeriesResult series = Evaluate(defectFirst ? new[] { defect, Info } : new[] { Info, defect }, configuration);
                    GateReport report = Fixture(series, configuration);
                    string context = name + ", " + ruleSet + ", defectFirst=" + defectFirst;

                    Assert.That(series.GateResult.IsInformational, Is.False, context);
                    Assert.That(series.GateResult.Outcome, Is.EqualTo(outcome), context);
                    Assert.That(series.GateResult.Status, Is.EqualTo(expectedStatus), context);
                    Assert.That(report.Status, Is.EqualTo(expectedStatus), context);
                    Assert.That(report.IsPass, Is.False, context);
                    Assert.That(series.InformationalElements.Count, Is.EqualTo(1), "the informational mismatch is kept as evidence: " + context);
                    Assert.That(report.InformationalMismatches.Count, Is.EqualTo(1), "and reaches the fixture report: " + context);
                    Assert.That(report.NonPass.Single().Input.ObjectKey, Is.EqualTo(Series), context);
                }
            }
        }

        [Test]
        public void The_Provisional_And_Owner_Metadata_Of_The_Decisive_Element_Is_Carried()
        {
            SeriesResult nan = Evaluate(new[] { Info, NaN });
            Assert.That(nan.GateResult.Proposed, Does.Contain("G3").And.Contain("proposed"));
            Assert.That(nan.GateResult.OwnerDecisionNamed, Is.EqualTo("OD-G3"));
            Assert.That(Fixture(nan).Provisional, Is.True);

            SeriesResult g2 = Evaluate(new[] { Info, Default });
            Assert.That(g2.GateResult.Proposed, Does.Contain("G2"));
            Assert.That(g2.GateResult.OwnerDecisionNamed, Does.Contain("OD-G2"));

            SeriesResult technical = Evaluate(new[] { Info, Unreadable });
            Assert.That(technical.GateResult.TechnicalBlockerNamed, Does.Contain("field unavailable"));
        }

        [Test]
        public void An_Accepted_Default_Beside_An_Informational_Mismatch_Is_Rank_5_Not_Informational_And_Not_Pass()
        {
            GateConfiguration accepted = Rules(acceptDefault: true);
            foreach (SeriesResult series in new[] { Evaluate(new[] { Info, Default }, accepted), Evaluate(new[] { Default, Info }, accepted) })
            {
                GateReport report = Fixture(series, accepted);
                Assert.That(series.GateResult.IsInformational, Is.False);
                Assert.That(series.GateResult.Status, Is.EqualTo(GateStatus.NotPassAcceptedLimitation));
                Assert.That(series.GateResult.AcceptedBy, Is.EqualTo("owner"));
                Assert.That(report.Status, Is.EqualTo(GateStatus.NotPassAcceptedLimitation));
                Assert.That(report.IsPass, Is.False);
                Assert.That(report.InformationalMismatches.Count, Is.EqualTo(1));
            }
        }

        [Test]
        public void Identity_Not_Established_Makes_Every_Element_Gating_Not_Run()
        {
            SeriesResult series = Evaluate(new[] { Info, Match }, identity: IdentityStatus.NotEstablished);
            Assert.That(series.GateResult.Status, Is.EqualTo(GateStatus.NotRun));
            Assert.That(series.GateResult.IsInformational, Is.False, "an unresolved prerequisite still gates a non-gated quantity");
            Assert.That(Fixture(series).Status, Is.EqualTo(GateStatus.NotRun));
        }

        // ---------------------------------------------------------------- rank ties and precedence among gating elements

        [Test]
        public void An_Informational_Mismatch_Cannot_Tie_With_A_Fail_Whichever_Comes_First()
        {
            // The informational element is Status Fail, the same numeric rank as a gating EXPORT-DEFECT.
            foreach (var order in new[] { new[] { Info, Absent }, new[] { Absent, Info }, new[] { Info, Zeroed, Info }, new[] { Info, Info, Absent, Info } })
            {
                SeriesResult series = Evaluate(order);
                Assert.That(series.GateResult.IsInformational, Is.False);
                Assert.That(series.GateResult.Outcome, Is.EqualTo(EquivalenceOutcome.ExportDefect));
                Assert.That(Fixture(series).Status, Is.EqualTo(GateStatus.Fail));
            }
        }

        [Test]
        public void Among_Gating_Elements_The_Lowest_Rank_Decides_And_The_First_Wins_A_Tie()
        {
            SeriesResult mixed = Evaluate(new[] { Match, Info, Default, Info, Unreadable, NaN, Absent });
            Assert.That(mixed.GateResult.Status, Is.EqualTo(GateStatus.Fail), "the Fail (absent) outranks BlockedTechnical, BlockedOwner and the informational Fails");
            Assert.That(mixed.GateResult.Outcome, Is.EqualTo(EquivalenceOutcome.ExportDefect));
            Assert.That(mixed.Aggregate.Input.ObjectKey, Is.EqualTo("ground[6]"));

            // Two gating elements of the same rank: the first decides.
            SeriesResult tie = Evaluate(new[] { Info, Absent, Zeroed });
            Assert.That(tie.Aggregate.Input.ObjectKey, Is.EqualTo("ground[1]"));

            SeriesResult technicalBeforeOwner = Evaluate(new[] { Default, Info, Unreadable });
            Assert.That(technicalBeforeOwner.GateResult.Status, Is.EqualTo(GateStatus.BlockedTechnical), "rank 2 beats rank 3 whatever the order");
            Assert.That(Evaluate(new[] { Unreadable, Info, Default }).GateResult.Status, Is.EqualTo(GateStatus.BlockedTechnical));
        }

        [Test]
        public void The_Series_Key_And_Element_Evidence_Are_Preserved()
        {
            SeriesResult series = Evaluate(new[] { Match, Info, Unreadable });
            Assert.That(series.Elements.Count, Is.EqualTo(3));
            Assert.That(series.Elements.Select(x => x.Input.ObjectKey), Is.EqualTo(new[] { "ground[0]", "ground[1]", "ground[2]" }));
            Assert.That(series.GateResult.Input.ObjectKey, Is.EqualTo(Series), "the coverage key is the series object");
            Assert.That(series.GateResult.Reason, Does.Contain("decisive element ground[2] of 3").And.Contain("informational mismatch"));
            Assert.That(series.InformationalElements.Single(), Is.SameAs(series.Elements[1]));
            Assert.That(series.InformationalElements.Single().Comparison.Difference, Is.EqualTo(2).Within(1e-12), "the mismatch keeps its numeric working");
            Assert.That(series.InformationalElements.Single().Outcome, Is.EqualTo(EquivalenceOutcome.OutOfTolerance), "and is not relabelled as a match");
        }

        // ---------------------------------------------------------------- series without a gating defect

        [Test]
        public void An_All_Match_Or_Within_Tolerance_Series_Passes_And_Prefers_The_Within_Tolerance_Element()
        {
            SeriesResult matches = Evaluate(new[] { Match, Match, Match });
            Assert.That(matches.GateResult.Outcome, Is.EqualTo(EquivalenceOutcome.Match));
            Assert.That(Fixture(matches).Status, Is.EqualTo(GateStatus.Pass));
            Assert.That(matches.InformationalElements, Is.Empty);

            SeriesResult within = Evaluate(new[] { Match, Within, Match });
            Assert.That(within.GateResult.Outcome, Is.EqualTo(EquivalenceOutcome.WithinTolerance));
            Assert.That(Fixture(within).Status, Is.EqualTo(GateStatus.Pass));
        }

        [Test]
        public void Gating_Passes_Beside_An_Informational_Mismatch_Pass_With_The_Mismatch_Still_Reported()
        {
            foreach (var order in new[] { new[] { Match, Info, Within }, new[] { Info, Match, Match }, new[] { Within, Match, Info } })
            {
                SeriesResult series = Evaluate(order);
                GateReport report = Fixture(series);

                Assert.That(series.GateResult.IsInformational, Is.False);
                Assert.That(series.GateResult.IsPass, Is.True);
                Assert.That(report.Status, Is.EqualTo(GateStatus.Pass));
                Assert.That(report.NonPass, Is.Empty);
                Assert.That(report.InformationalMismatches.Single().Outcome, Is.EqualTo(EquivalenceOutcome.OutOfTolerance), "visible, with its own verdict");
                Assert.That(report.InformationalMismatches.Single().IsPass, Is.False, "never reported as a pass");
                Assert.That(series.GateResult.Reason, Does.Contain("1 informational mismatch element(s) do not gate"));
            }
        }

        [Test]
        public void An_Only_Informational_Series_Stays_Informational_And_Cannot_Make_A_Fixture_Pass_By_Itself()
        {
            SeriesResult series = Evaluate(new[] { Info, Info });
            Assert.That(series.GateResult.IsInformational, Is.True);
            Assert.That(series.GateResult.Outcome, Is.EqualTo(EquivalenceOutcome.OutOfTolerance));
            Assert.That(series.InformationalElements.Count, Is.EqualTo(2));
            Assert.That(series.Aggregate.Input.ObjectKey, Is.EqualTo("ground[0]"));

            // Beside a matching gating row the fixture passes and both element mismatches are reported.
            GateReport report = Fixture(series);
            Assert.That(report.Status, Is.EqualTo(GateStatus.Pass));
            Assert.That(report.InformationalMismatches.Count, Is.EqualTo(2));
            Assert.That(report.NonPass, Is.Empty);

            // On its own there is nothing that gates.
            GateReport alone = Query.Gate(new[] { series.GateResult }, Review.Requiring((Quantity, Series)));
            Assert.That(alone.Status, Is.EqualTo(GateStatus.NotRun));
            Assert.That(alone.IsPass, Is.False);
        }

        [Test]
        public void An_Ordinary_Gated_Series_Is_Unchanged()
        {
            QuantityInput prototype = Review.Evaluate("3b", ExpectedValue.Zero(), ReaderResult.FromValue(0), "wall").Input;
            List<ExpectedValue> expected = new List<ExpectedValue> { ten, ten, ten };

            SeriesResult mismatch = Query.EvaluateSeries(prototype, expected, new List<ReaderResult> { ReaderResult.FromValue(10), ReaderResult.FromValue(12), ReaderResult.FromValue(10) });
            Assert.That(mismatch.GateResult.IsInformational, Is.False, "a gated quantity's mismatch gates");
            Assert.That(mismatch.GateResult.Status, Is.EqualTo(GateStatus.Fail));
            Assert.That(mismatch.InformationalElements, Is.Empty);

            SeriesResult unreadable = Query.EvaluateSeries(prototype, expected, new List<ReaderResult> { ReaderResult.FromValue(12), ReaderResult.Unreadable("x"), ReaderResult.FromValue(10) });
            Assert.That(unreadable.GateResult.Status, Is.EqualTo(GateStatus.Fail), "Fail (first, rank 1) still outranks BlockedTechnical in a gated series");

            SeriesResult good = Query.EvaluateSeries(prototype, expected, new List<ReaderResult> { ReaderResult.FromValue(10), ReaderResult.FromValue(10), ReaderResult.FromValue(10) });
            Assert.That(good.GateResult.IsPass, Is.True);
        }

        [Test]
        public void Coverage_Safeguards_Still_Apply_To_A_Mixed_Series()
        {
            SeriesResult mixed = Evaluate(new[] { Match, Info, Match });

            Assert.That(Query.Gate(new[] { Review.Match("1a", "zone"), mixed.GateResult }).Status, Is.EqualTo(GateStatus.NotRun), "no coverage list");
            Assert.That(Query.Gate(new[] { Review.Match("1a", "zone"), mixed.GateResult }, new GateConfiguration { RequiredComparisons = new RequiredComparison[0] }).Status, Is.EqualTo(GateStatus.NotRun), "empty list");
            Assert.That(Query.Gate(new[] { Review.Match("1a", "zone"), mixed.Aggregate }, Review.Requiring(("1a", "zone"), (Quantity, Series))).Status, Is.EqualTo(GateStatus.NotRun), "an element key is not the series object");
            Assert.That(Fixture(mixed).Status, Is.EqualTo(GateStatus.Pass), "complete coverage by the series object");
        }

        // ---------------------------------------------------------------- the property: never a pass while a gating element is not a pass

        [Test]
        public void Exhaustively_A_Mixed_Series_Never_Passes_While_A_Gating_Element_Is_Not_A_Pass_And_Order_Is_Irrelevant()
        {
            (string name, (ExpectedValue expected, ReaderResult native) element)[] kinds =
            {
                ("info", Info), ("match", Match), ("within", Within), ("unreadable", Unreadable), ("nan", NaN),
                ("absent", Absent), ("zeroed", Zeroed), ("invalid", Invalid), ("default", Default)
            };

            int evaluated = 0;
            foreach (GateRuleSet ruleSet in new[] { GateRuleSet.Interim, GateRuleSet.ProvisionalProposed })
            foreach (bool accept in new[] { false, true })
            {
                GateConfiguration configuration = Rules(ruleSet, accept);
                Dictionary<string, HashSet<GateStatus>> statusByMultiset = new Dictionary<string, HashSet<GateStatus>>();

                for (int a = 0; a < kinds.Length; a++)
                for (int b = 0; b < kinds.Length; b++)
                for (int c = 0; c < kinds.Length; c++)
                {
                    int[] pick = { a, b, c };
                    SeriesResult series = Evaluate(pick.Select(i => kinds[i].element), configuration);
                    GateReport report = Fixture(series, configuration);
                    evaluated++;
                    string context = string.Join(",", pick.Select(i => kinds[i].name)) + " " + ruleSet + " accepted=" + accept;

                    // Standalone verdict of every element, by the unchanged scalar evaluation.
                    List<QuantityResult> gating = series.Elements.Where(x => x.GatesFixture).ToList();
                    List<QuantityResult> informational = series.Elements.Where(x => x.IsInformational).ToList();

                    Assert.That(series.InformationalElements.Count, Is.EqualTo(informational.Count), context);
                    Assert.That(series.GateResult.IsInformational, Is.EqualTo(gating.Count == 0), context);

                    if (gating.Count > 0)
                    {
                        GateStatus lowest = gating.Min(x => x.Status);
                        Assert.That(series.GateResult.Status, Is.EqualTo(lowest), "the lowest gating rank decides: " + context);

                        if (gating.Any(x => !x.IsPass))
                        {
                            Assert.That(report.IsPass, Is.False, "a gating element that is not a pass: " + context);
                            Assert.That(series.GateResult.IsPass, Is.False, context);
                        }
                        else
                        {
                            Assert.That(series.GateResult.IsPass, Is.True, context);
                            Assert.That(report.IsPass, Is.True, "all gating elements pass; an informational mismatch does not block: " + context);
                        }
                    }
                    else
                    {
                        Assert.That(series.GateResult.IsPass, Is.False, context);
                        Assert.That(series.GateResult.Outcome, Is.EqualTo(EquivalenceOutcome.OutOfTolerance), context);
                    }

                    // Order independence: the status depends on the multiset of elements only.
                    string multiset = string.Join(",", pick.OrderBy(i => i).Select(i => kinds[i].name));
                    if (!statusByMultiset.TryGetValue(multiset, out HashSet<GateStatus> seen))
                    {
                        seen = new HashSet<GateStatus>();
                        statusByMultiset[multiset] = seen;
                    }

                    seen.Add(report.Status);
                }

                foreach (KeyValuePair<string, HashSet<GateStatus>> pair in statusByMultiset)
                {
                    Assert.That(pair.Value.Count, Is.EqualTo(1), "the verdict must not depend on the element order: " + pair.Key + " " + ruleSet + " accepted=" + accept + " -> " + string.Join("/", pair.Value));
                }
            }

            Assert.That(evaluated, Is.EqualTo(2 * 2 * 9 * 9 * 9));
        }
    }
}
