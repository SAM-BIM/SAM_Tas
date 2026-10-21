// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical.InputEquivalence;
using SAM.Analytical.Tas.DirectT3D.Validation.ReadBack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SAM.Analytical.Tas.DirectT3D.ReadBack.Tests
{
    /// <summary>
    /// S08 PR-C: the owner's 2026-10-21 decisions replayed on the committed real calib-two snapshot (zone A V1, zone B V0,
    /// each with a present ticV value profile of value 0 and factor 1) through the full <see cref="ReadBackCompare.Run"/>
    /// path: OD-G1..G3 as approved, and T-VENT-INACTIVE on rows 6 and 8b of a V0/V1 ticV only when every one of its 8760
    /// schedule values and delivered flows is read, finite and exactly 0. Nothing else is exempted.
    /// </summary>
    [TestFixture]
    public class InactiveVentilationTests
    {
        private const string Two = "calib-two";
        private const string Box = "calib-box";
        private static readonly string[] TicVRows = { "6.hourly", "6.eflh", "8b" };

        private static ReadBackReport FullCompare(string run, NativeSnapshot snapshot, GateRuleSet ruleSet)
        {
            SnapshotJson.Write(snapshot, Path.Combine(run, ReadBackFiles.Snapshot));
            return ReadBackCompare.Run(run, Evidence.Rules, "regression", "test", ruleSet, Path.Combine(run, "out-" + ruleSet + "-" + Guid.NewGuid().ToString("N").Substring(0, 6)));
        }

        private static IEnumerable<NativeProfile> TicV(NativeSnapshot snapshot)
        {
            return snapshot.Zones.Select(z => z.InternalConditions.Single(x => !x.DayTypes.Contains("HDD")).GainProfiles["ticV"]);
        }

        private static List<ReportResult> TicVItems(ReadBackReport report)
        {
            return report.Results.Where(x => TicVRows.Contains(x.QuantityId) && x.ObjectKey.Contains(".ticV.")).ToList();
        }

        private static NativeField[] Hours(double value)
        {
            return Enumerable.Range(0, 24).Select(_ => NativeField.Of(value)).ToArray();
        }

        // A value profile turned into a 24-hour profile with the given slot values (slots 1..24, the committed N-5 convention).
        private static void Hourly(NativeProfile profile, NativeField[] hours)
        {
            profile.Type = "ticHourlyProfile";
            profile.TypeCode = 2;
            profile.Hourly = new Dictionary<int, NativeField>();
            for (int h = 0; h < 24; h++) profile.Hourly[h + 1] = hours[h];
        }

        [Test]
        public void ApprovedRules_TheCommittedZeroTicV_IsAVerifiedInactiveProfile_OnRows6And8b_AndTheModelPasses()
        {
            string run = Evidence.Unpack(Two);
            ReadBackReport report = FullCompare(run, Evidence.Snapshot(run), GateRuleSet.Approved);

            List<ReportResult> items = TicVItems(report);
            Assert.That(items, Has.Count.EqualTo(6), "6.hourly, 6.eflh and 8b in each of the two zones");
            foreach (ReportResult item in items)
            {
                Assert.That(item.Outcome, Is.EqualTo(EquivalenceOutcome.AcceptedTransformation.ToString()), item.ObjectKey + ": " + item.Reason);
                Assert.That(item.Reason, Does.Contain(ReadBackComparison.InactiveVentilationTransformation));
            }

            Assert.That(report.ProposedCases, Is.Empty, "nothing is labelled proposed under the approved rules");
            Assert.That(report.ExitCode, Is.EqualTo(ReadBackExit.Pass), report.Status + " " + string.Join(" | ", report.StatusReasons));
            Assert.That(report.Supporting.Count(x => x.Contains(ReadBackComparison.InactiveVentilationTransformation + " verified")), Is.EqualTo(2));
        }

        [Test]
        public void PreDecisionRules_StillReportTheProposedG1_Unchanged()
        {
            foreach (GateRuleSet ruleSet in new[] { GateRuleSet.Interim, GateRuleSet.ProvisionalProposed })
            {
                string run = Evidence.Unpack(Two);
                ReadBackReport report = FullCompare(run, Evidence.Snapshot(run), ruleSet);

                Assert.That(report.ExitCode, Is.EqualTo(ReadBackExit.BlockedOwner), ruleSet + ": " + report.Status);
                Assert.That(TicVItems(report).All(x => x.Proposed != null && x.Proposed.Contains("OD-G1")), ruleSet.ToString());
            }
        }

        private static IEnumerable<TestCaseData> NoExemption()
        {
            yield return new TestCaseData((Action<NativeProfile>)(p => p.Value = NativeField.Of(0.25)), ReadBackExit.Fail, "value 0.25: native non-zero").SetName("TicV_NonZeroValue_Fails");
            yield return new TestCaseData((Action<NativeProfile>)(p => { NativeField[] h = Hours(0); h[13] = NativeField.Of(0.4); Hourly(p, h); }), ReadBackExit.Fail, "one non-zero hour").SetName("TicV_OneNonZeroHour_Fails");
            yield return new TestCaseData((Action<NativeProfile>)(p => p.Value = NativeField.Of(double.NaN)), ReadBackExit.Fail, "value NaN: G1-NaN").SetName("TicV_NaNValue_Fails");
            yield return new TestCaseData((Action<NativeProfile>)(p => { NativeField[] h = Hours(0); h[5] = NativeField.Unreadable("regression: hourly slot 6"); Hourly(p, h); }), ReadBackExit.BlockedTechnical, "one unreadable hour").SetName("TicV_OneUnreadableHour_BlocksTechnical");
            yield return new TestCaseData((Action<NativeProfile>)(p => { NativeField[] h = Hours(0); Hourly(p, h); p.Hourly.Remove(9); }), ReadBackExit.BlockedTechnical, "one missing hourly slot").SetName("TicV_MissingHourlySlot_BlocksTechnical");
            yield return new TestCaseData((Action<NativeProfile>)(p => { p.Kind = FieldKind.Unreadable; p.Detail = "regression: profile unreadable"; }), ReadBackExit.BlockedTechnical, "profile unreadable").SetName("TicV_ProfileUnreadable_BlocksTechnical");
            yield return new TestCaseData((Action<NativeProfile>)(p => p.Factor = NativeField.Of(double.NaN)), ReadBackExit.Fail, "factor NaN: delivered NaN").SetName("TicV_FactorNaN_Fails");
            yield return new TestCaseData((Action<NativeProfile>)(p => p.Factor = NativeField.Unreadable("regression: factor")), ReadBackExit.BlockedTechnical, "factor unreadable: delivered unreadable").SetName("TicV_FactorUnreadable_BlocksTechnical");
            yield return new TestCaseData((Action<NativeProfile>)(p => { p.Type = "ticYearlyProfile"; p.TypeCode = 3; p.Yearly = new NativeSeries { Kind = FieldKind.Finite, LowerBound = 1, Values = new double[8784] }; }), ReadBackExit.BlockedTechnical, "yearly array outside the domain").SetName("TicV_YearlyOutOfDomain_BlocksTechnical");
        }

        [TestCaseSource(nameof(NoExemption))]
        public void ApprovedRules_ATicVThatIsNotProvenInactive_GetsNoExemption(Action<NativeProfile> change, int exit, string what)
        {
            string run = Evidence.Unpack(Two);
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            change(TicV(snapshot).First());
            ReadBackReport report = FullCompare(run, snapshot, GateRuleSet.Approved);

            Assert.That(report.ExitCode, Is.EqualTo(exit), what + ": " + report.Status + " " + string.Join(" | ", report.StatusReasons));
            Assert.That(report.Supporting.Count(x => x.Contains(ReadBackComparison.InactiveVentilationTransformation + " not applied")), Is.EqualTo(1), what);
            Assert.That(report.Supporting.Count(x => x.Contains(ReadBackComparison.InactiveVentilationTransformation + " verified")), Is.EqualTo(1), "the other zone is unchanged");
        }

        [Test]
        public void ApprovedRules_ANonZeroFactorOverAnAllZeroSchedule_IsStillInactive()
        {
            // The owner's condition is on the schedule values and the delivered flows, both exactly 0; the factor may be anything finite.
            string run = Evidence.Unpack(Two);
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            TicV(snapshot).First().Factor = NativeField.Of(3.5);
            ReadBackReport report = FullCompare(run, snapshot, GateRuleSet.Approved);

            Assert.That(report.ExitCode, Is.EqualTo(ReadBackExit.Pass), report.Status);
        }

        [Test]
        public void ApprovedRules_ACalendarDayUnread_GivesNoExemption()
        {
            string run = Evidence.Unpack(Two);
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            snapshot.Calendar.Days.RemoveAll(x => x.Index == 100);
            ReadBackReport report = FullCompare(run, snapshot, GateRuleSet.Approved);

            Assert.That(report.ExitCode, Is.EqualTo(ReadBackExit.BlockedTechnical), report.Status);
            Assert.That(report.Supporting.Count(x => x.Contains(ReadBackComparison.InactiveVentilationTransformation + " not applied")), Is.EqualTo(2));
        }

        [Test]
        public void ApprovedRules_AnotherUnstatedScheduleAtZero_StaysG1_NoGlobalReinterpretation()
        {
            string run = Evidence.Unpack(Box);
            ExpectedModel expected = Evidence.Expected(run, "regression");
            expected.Spaces[0].Profiles.Remove("ticLG");
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            NativeProfile lighting = snapshot.Zones[0].InternalConditions.Single(x => !x.DayTypes.Contains("HDD")).GainProfiles["ticLG"];
            foreach (int slot in lighting.Hourly.Keys.ToList()) lighting.Hourly[slot] = NativeField.Of(0);

            ReadBackComparison comparison = ReadBackComparison.Run(expected, snapshot, CalibrationRules.Load(Evidence.Rules), GateRuleSet.Approved);
            QuantityResult hourly = Evidence.Result(comparison, "6.hourly", ".ticLG.hourly");

            Assert.That(hourly.Outcome, Is.EqualTo(EquivalenceOutcome.UnlistedDefault), hourly.Reason);
            Assert.That(hourly.Status, Is.EqualTo(GateStatus.BlockedOwnerDecision));
            Assert.That(hourly.Proposed, Is.Null);
        }

        [Test]
        public void ApprovedRules_V2IsUnchanged_TheBoxPasses_AndNoTransformationIsApplied()
        {
            string run = Evidence.Unpack(Box);
            ReadBackReport approved = FullCompare(run, Evidence.Snapshot(run), GateRuleSet.Approved);
            ReadBackReport interim = FullCompare(run, Evidence.Snapshot(run), GateRuleSet.Interim);

            Assert.That(approved.ExitCode, Is.EqualTo(ReadBackExit.Pass), approved.Status);
            Assert.That(approved.Results.Select(x => x.ObjectKey + "=" + x.Outcome), Is.EqualTo(interim.Results.Select(x => x.ObjectKey + "=" + x.Outcome)));
            Assert.That(approved.Supporting.Single(x => x.Contains(ReadBackComparison.InactiveVentilationTransformation)), Does.Contain("not applied: D-F2 state V2"));
        }

        [Test]
        public void ApprovedRules_ANaNInAnotherQuantity_IsAnExportDefect_NotProposed()
        {
            string run = Evidence.Unpack(Box);
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            snapshot.Zones[0].InternalConditions.Single(x => !x.DayTypes.Contains("HDD")).GainProfiles["ticI"].Factor = NativeField.Of(double.NaN);
            ReadBackReport report = FullCompare(run, snapshot, GateRuleSet.Approved);

            Assert.That(report.ExitCode, Is.EqualTo(ReadBackExit.Fail), report.Status);
            Assert.That(report.ProposedCases, Is.Empty);
        }
    }
}
