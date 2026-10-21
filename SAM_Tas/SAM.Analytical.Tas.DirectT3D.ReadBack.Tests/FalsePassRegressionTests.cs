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
    /// The false-PASS routes found by the independent review of SAM_Tas#105 (2026-10-21), each replayed on the committed real
    /// snapshots through the run verdict (<see cref="ReadBackReport"/>) or the full <see cref="ReadBackCompare.Run"/> path:
    /// a schedule SAM does not state, a SAM space without an internal condition, native annual arrays outside the calibrated
    /// domain, an extra native design day, and provenance that does not prove a fresh, unchanged read. None may PASS.
    /// </summary>
    [TestFixture]
    public class FalsePassRegressionTests
    {
        private const string Box = "calib-box";

        private static CalibrationRules Rules => CalibrationRules.Load(Evidence.Rules);

        private static ReadBackReport Verdict(ExpectedModel expected, NativeSnapshot snapshot, GateRuleSet ruleSet, out ReadBackComparison comparison)
        {
            comparison = ReadBackComparison.Run(expected, snapshot, Rules, ruleSet);
            return ReadBackReport.Create(comparison, "regression", null);
        }

        /// <summary>The full CLI path on a run folder whose snapshot (and optionally manifest) was rewritten.</summary>
        private static ReadBackReport FullCompare(string run, NativeSnapshot snapshot, GateRuleSet ruleSet = GateRuleSet.Interim, ExportManifest manifest = null)
        {
            SnapshotJson.Write(snapshot, Path.Combine(run, ReadBackFiles.Snapshot));
            if (manifest != null)
            {
                SnapshotJson.Write(manifest, Path.Combine(run, ReadBackFiles.ExportManifest));
            }

            return ReadBackCompare.Run(run, Evidence.Rules, "regression", "test", ruleSet, Path.Combine(run, "out-" + ruleSet + "-" + Guid.NewGuid().ToString("N").Substring(0, 6)));
        }

        private static NativeInternalCondition Annual(NativeSnapshot snapshot)
        {
            return snapshot.Zones[0].InternalConditions.Single(x => !x.DayTypes.Contains("HDD"));
        }

        private static ReportResult Item(ReadBackReport report, string quantityId, string keyEndsWith)
        {
            return report.Results.Single(x => x.QuantityId == quantityId && x.ObjectKey.EndsWith(keyEndsWith, StringComparison.Ordinal));
        }

        private static readonly GateRuleSet[] BothRuleSets = { GateRuleSet.Interim, GateRuleSet.ProvisionalProposed };

        // ---- 1. schedules SAM does not state ------------------------------------------------------------------------------

        [Test]
        public void UnstatedSamSchedule_IsStillRequired_FromTheModelAndTheBoundedContract()
        {
            string run = Evidence.Unpack(Box);
            ExpectedModel expected = Evidence.Expected(run, "regression");
            expected.Spaces[0].Profiles.Remove("ticLG");
            List<RequiredComparison> required = ReadBackComparison.RequiredFromModel(expected).ToList();

            Assert.That(required.Any(x => x.QuantityId == "6.hourly" && x.ObjectKey.EndsWith(".ticLG.hourly", StringComparison.Ordinal)));
            Assert.That(required.Any(x => x.QuantityId == "6.eflh" && x.ObjectKey.EndsWith(".ticLG.eflh", StringComparison.Ordinal)));
        }

        [TestCaseSource(nameof(BothRuleSets))]
        public void UnstatedSamSchedule_NativeNonZeroHours_IsAnExportDefect_NotPass(GateRuleSet ruleSet)
        {
            string run = Evidence.Unpack(Box);
            ExpectedModel expected = Evidence.Expected(run, "regression");
            expected.Spaces[0].Profiles.Remove("ticLG");
            ReadBackReport report = Verdict(expected, Evidence.Snapshot(run), ruleSet, out ReadBackComparison comparison);

            QuantityResult hourly = Evidence.Result(comparison, "6.hourly", ".ticLG.hourly");
            Assert.That(hourly.Outcome, Is.EqualTo(EquivalenceOutcome.ExportDefect), hourly.Reason);
            Assert.That(hourly.Status, Is.EqualTo(GateStatus.Fail));
            Assert.That(comparison.Gate.Coverage, Is.EqualTo(CoverageStatus.Complete));
            Assert.That(report.ExitCode, Is.EqualTo(ReadBackExit.Fail), report.Status);
        }

        [Test]
        public void UnstatedSamSchedule_NativeZeroHours_IsTheProposedG1_BlockedUnderBothRuleSets()
        {
            foreach (GateRuleSet ruleSet in BothRuleSets)
            {
                string run = Evidence.Unpack(Box);
                ExpectedModel expected = Evidence.Expected(run, "regression");
                expected.Spaces[0].Profiles.Remove("ticLG");
                NativeSnapshot snapshot = Evidence.Snapshot(run);
                NativeProfile lighting = Annual(snapshot).GainProfiles["ticLG"];
                foreach (int slot in lighting.Hourly.Keys.ToList()) lighting.Hourly[slot] = NativeField.Of(0);
                ReadBackReport report = Verdict(expected, snapshot, ruleSet, out ReadBackComparison comparison);

                QuantityResult hourly = Evidence.Result(comparison, "6.hourly", ".ticLG.hourly");
                Assert.That(hourly.Status, Is.EqualTo(GateStatus.BlockedOwnerDecision), hourly.Reason);
                Assert.That(hourly.Proposed, Does.Contain("OD-G1"), "zero is never treated as absence");
                Assert.That(Evidence.Result(comparison, "6.eflh", ".ticLG.eflh").Proposed, Does.Contain("OD-G1"));
                Assert.That(report.ExitCode, Is.EqualTo(ReadBackExit.BlockedOwner), report.Status);
                Assert.That(report.ProposedCases.Keys.Any(x => x.Contains("OD-G1")), "the provisional label is kept in the report");
            }
        }

        [Test]
        public void UnstatedSamSchedule_AnUnreadableNativeHour_IsATechnicalBlocker()
        {
            string run = Evidence.Unpack(Box);
            ExpectedModel expected = Evidence.Expected(run, "regression");
            expected.Spaces[0].Profiles.Remove("ticLG");
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            NativeProfile lighting = Annual(snapshot).GainProfiles["ticLG"];
            foreach (int slot in lighting.Hourly.Keys.ToList()) lighting.Hourly[slot] = NativeField.Of(0);
            lighting.Hourly[5] = NativeField.Unreadable("regression: hourly slot 5");
            ReadBackReport report = Verdict(expected, snapshot, GateRuleSet.Interim, out ReadBackComparison comparison);

            QuantityResult hourly = Evidence.Result(comparison, "6.hourly", ".ticLG.hourly");
            Assert.That(hourly.Status, Is.EqualTo(GateStatus.BlockedTechnical), hourly.Reason);
            Assert.That(hourly.TechnicalBlockerNamed, Does.Contain("hourly slot 5"));
            SeriesRecord series = comparison.Series.Single(x => x.ObjectKey.EndsWith(".ticLG.hourly", StringComparison.Ordinal));
            Assert.That(series.Result.Elements, Has.Count.EqualTo(8760), "every element is kept");
            Assert.That(series.Result.Elements.Count(x => x.Status == GateStatus.BlockedOwnerDecision && x.Proposed != null), Is.GreaterThan(0), "the non-decisive G1 elements stay in the evidence");
            Assert.That(report.ExitCode, Is.EqualTo(ReadBackExit.BlockedTechnical));
        }

        [Test]
        public void UnstatedSamSchedule_AMissingNativeHourlySlot_IsNeverReadAsAbsence()
        {
            string run = Evidence.Unpack(Box);
            ExpectedModel expected = Evidence.Expected(run, "regression");
            expected.Spaces[0].Profiles.Remove("ticLG");
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            NativeProfile lighting = Annual(snapshot).GainProfiles["ticLG"];
            foreach (int slot in lighting.Hourly.Keys.ToList()) lighting.Hourly[slot] = NativeField.Of(0);
            lighting.Hourly.Remove(7);
            ReadBackReport report = Verdict(expected, snapshot, GateRuleSet.Interim, out ReadBackComparison comparison);

            QuantityResult hourly = Evidence.Result(comparison, "6.hourly", ".ticLG.hourly");
            Assert.That(hourly.Status, Is.EqualTo(GateStatus.BlockedTechnical), hourly.Reason);
            Assert.That(hourly.TechnicalBlockerNamed, Does.Contain("hourly slot 7"));
            Assert.That(report.ExitCode, Is.Not.EqualTo(ReadBackExit.Pass));
        }

        [Test]
        public void SamSpaceWithoutInternalCondition_NativeConditionsStayRequired_AndNonZeroGainsFail()
        {
            string run = Evidence.Unpack(Box);
            ExpectedModel expected = Evidence.Expected(run, "regression");
            ExpectedSpace space = expected.Spaces[0];
            space.HasInternalCondition = false;
            space.InternalConditionName = null;
            space.Parameters.Clear();
            space.Profiles.Clear();
            space.Occupancy = double.NaN;

            List<RequiredComparison> required = ReadBackComparison.RequiredFromModel(expected).ToList();
            foreach (string quantity in new[] { "4a.density", "4b", "4a.people", "4c", "5.temperature", "5.humidity", "6.hourly", "6.eflh", "7a", "7b", "8a", "8b" })
            {
                Assert.That(required.Any(x => x.QuantityId == quantity && x.ObjectKey.StartsWith(ReadBackComparison.Keys.Space(space.Guid), StringComparison.Ordinal)), quantity + " is required for every space");
            }

            ReadBackReport report = Verdict(expected, Evidence.Snapshot(run), GateRuleSet.Interim, out ReadBackComparison comparison);
            QuantityResult lighting = Evidence.Result(comparison, "4a.density", ".ticLG.factor");
            Assert.That(lighting.Outcome, Is.EqualTo(EquivalenceOutcome.ExportDefect), "SAM states no gain and the native holds 8.25 W/m2");
            Assert.That(Evidence.Result(comparison, "6.hourly", ".ticLG.hourly").Status, Is.EqualTo(GateStatus.Fail));
            Assert.That(comparison.Gate.Coverage, Is.EqualTo(CoverageStatus.Complete));
            Assert.That(report.ExitCode, Is.EqualTo(ReadBackExit.Fail), report.Status);
        }

        // ---- 2. native annual cardinality and calibrated index domain -----------------------------------------------------

        private static double[] Resized(double[] values, int length)
        {
            double[] result = new double[length];
            for (int i = 0; i < length; i++) result[i] = i < values.Length ? values[i] : 9999;
            return result;
        }

        [TestCase(8761, 1)]
        [TestCase(8784, 1)]
        [TestCase(8759, 1)]
        [TestCase(8760, 0)]
        public void NativeAnnualWeather_OutsideTheCalibratedDomain_IsNamed_AndNeverPass(int length, int lowerBound)
        {
            string run = Evidence.Unpack(Box);
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            KeyValuePair<int, Weather.WeatherDataType> parameter = ReadBackExpected.AnnualWeather[0];
            NativeSeries series = snapshot.Weather.Annual[parameter.Key];
            snapshot.Weather.Annual[parameter.Key] = NativeSeries.Of(Resized(series.Values, length), lowerBound);
            ReadBackReport report = FullCompare(run, snapshot);

            ReportResult result = Item(report, "9", "annual." + parameter.Value);
            Assert.That(result.Status, Is.EqualTo(GateStatus.BlockedTechnical.ToString()), result.Reason);
            Assert.That(result.TechnicalBlocker, Does.Contain(length + " elements").And.Contain("lower bound " + lowerBound));
            Assert.That(report.ExitCode, Is.EqualTo(ReadBackExit.BlockedTechnical), report.Status);
        }

        [TestCase(8761, 1)]
        [TestCase(8784, 1)]
        [TestCase(8759, 1)]
        [TestCase(8760, 0)]
        public void NativeYearlyProfile_OutsideTheCalibratedDomain_IsNamed_AndNeverPass(int length, int lowerBound)
        {
            string run = Evidence.Unpack(Box);
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            NativeProfile equipment = Annual(snapshot).GainProfiles.Values.Single(x => x.TypeCode == 3);
            equipment.Yearly = NativeSeries.Of(Resized(equipment.Yearly.Values, length), lowerBound);
            ReadBackReport report = FullCompare(run, snapshot);

            foreach (string quantity in new[] { "6.hourly", "6.eflh" })
            {
                ReportResult result = Item(report, quantity, "." + equipment.Slot + (quantity == "6.hourly" ? ".hourly" : ".eflh"));
                Assert.That(result.Status, Is.EqualTo(GateStatus.BlockedTechnical.ToString()), quantity + ": " + result.Reason);
                Assert.That(result.TechnicalBlocker, Does.Contain(length + " elements").And.Contain("lower bound " + lowerBound));
            }

            Assert.That(report.ExitCode, Is.EqualTo(ReadBackExit.BlockedTechnical), report.Status);
        }

        [Test]
        public void SupportedEncodings_ConstantHourlyAndYearly_StillPass()
        {
            string run = Evidence.Unpack(Box);
            ReadBackComparison comparison = Evidence.Compare(run, Evidence.Snapshot(run));

            Dictionary<string, string> encodings = comparison.Series.Where(x => x.QuantityId == "6.hourly").ToDictionary(x => x.ObjectKey.Split('.')[^2], x => x.NativeEncoding);
            Assert.That(encodings["ticELG"], Is.EqualTo("ticValueProfile"));
            Assert.That(encodings["ticLG"], Is.EqualTo("ticHourlyProfile"));
            Assert.That(encodings["ticESG"], Is.EqualTo("ticYearlyProfile"));
            Assert.That(comparison.Results.Where(x => x.Definition.Id == "6.hourly" || x.Definition.Id == "9").All(x => x.Status == GateStatus.Pass));
            Assert.That(comparison.Gate.Status, Is.EqualTo(GateStatus.Pass));
        }

        [Test]
        public void ExtraNativeDesignDay_InTheGroup_IsANativeObjectSamNeverStated()
        {
            string run = Evidence.Unpack(Box);
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            Assert.That(snapshot.CoolingDesignDays.All(x => x.Days.Count == 1), "the calibration runs persist one day per design day");
            snapshot.CoolingDesignDays[0].Days.Add(snapshot.CoolingDesignDays[0].Days[0]);
            ReadBackReport report = FullCompare(run, snapshot);

            ReportResult extra = report.Results.Single(x => x.ObjectKey.Contains("designday.cooling[0]") && x.ObjectKey.Contains(".day[1]"));
            Assert.That(extra.Status, Is.EqualTo(GateStatus.Fail.ToString()), extra.Reason);
            Assert.That(report.Results.Any(x => x.ObjectKey.Contains(".day[1]") && x.Status == GateStatus.Pass.ToString()), Is.False, "the extra day is never compared against the one SAM day");
            Assert.That(report.ExitCode, Is.EqualTo(ReadBackExit.Fail));
        }

        // ---- 3. provenance ------------------------------------------------------------------------------------------------

        private static List<string> ChecksAfter(Action<ExportManifest, NativeSnapshot> change)
        {
            string run = Evidence.Unpack(Box);
            ExportManifest manifest = SnapshotJson.Read<ExportManifest>(Path.Combine(run, ReadBackFiles.ExportManifest));
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            change(manifest, snapshot);
            return ReadBackCompare.Checks(manifest, snapshot, Path.Combine(run, ReadBackFiles.FrozenModel), Path.Combine(run, ReadBackFiles.FrozenWeather)).ToList();
        }

        private static IEnumerable<TestCaseData> ProvenanceFailures()
        {
            yield return new TestCaseData((Action<ExportManifest, NativeSnapshot>)((m, s) => m.EndedUtc = null), "export end time").SetName("Provenance_MissingExportEnd");
            yield return new TestCaseData((Action<ExportManifest, NativeSnapshot>)((m, s) => m.EndedUtc = "yesterday"), "export end time").SetName("Provenance_InvalidExportEnd");
            yield return new TestCaseData((Action<ExportManifest, NativeSnapshot>)((m, s) => s.Reader.StartedUtc = null), "reader start time").SetName("Provenance_MissingReaderStart");
            yield return new TestCaseData((Action<ExportManifest, NativeSnapshot>)((m, s) => s.Reader.StartedUtc = "not a time"), "reader start time").SetName("Provenance_InvalidReaderStart");
            yield return new TestCaseData((Action<ExportManifest, NativeSnapshot>)((m, s) => s.Reader.StartedUtc = m.StartedUtc), "before the export ended").SetName("Provenance_ReaderBeforeExportEnd");
            yield return new TestCaseData((Action<ExportManifest, NativeSnapshot>)((m, s) => s.Reader.ProcessId = 0), "process id").SetName("Provenance_MissingReaderProcess");
            yield return new TestCaseData((Action<ExportManifest, NativeSnapshot>)((m, s) => m.ProcessId = 0), "process id").SetName("Provenance_MissingExportProcess");
            yield return new TestCaseData((Action<ExportManifest, NativeSnapshot>)((m, s) => m.Outputs.Remove(ReadBackFiles.Tbd)), "TBD").SetName("Provenance_TbdHashMissingFromExport");
            yield return new TestCaseData((Action<ExportManifest, NativeSnapshot>)((m, s) => s.Reader.TbdSha256Before = "0000"), "TBD the reader opened").SetName("Provenance_TbdHashMismatch");
            yield return new TestCaseData((Action<ExportManifest, NativeSnapshot>)((m, s) => s.Reader.TbdSha256After = null), "TBD").SetName("Provenance_TbdAfterHashMissing");
            yield return new TestCaseData((Action<ExportManifest, NativeSnapshot>)((m, s) => s.Reader.TbdSha256After = "changed"), "TBD changed").SetName("Provenance_TbdChangedDuringRead");
            yield return new TestCaseData((Action<ExportManifest, NativeSnapshot>)((m, s) => s.Reader.T3dSha256Before = null), "T3D").SetName("Provenance_RequiredT3dUnverified");
            yield return new TestCaseData((Action<ExportManifest, NativeSnapshot>)((m, s) => s.Reader.T3dSha256Before = "0000"), "T3D the reader opened").SetName("Provenance_T3dHashMismatch");
            yield return new TestCaseData((Action<ExportManifest, NativeSnapshot>)((m, s) => s.Reader.T3dSha256After = null), "T3D").SetName("Provenance_T3dAfterHashMissing");
            yield return new TestCaseData((Action<ExportManifest, NativeSnapshot>)((m, s) => s.Reader.T3dSha256After = "changed"), "T3D changed").SetName("Provenance_T3dChangedDuringRead");
        }

        [TestCaseSource(nameof(ProvenanceFailures))]
        public void Provenance_UnprovenOrChanged_IsAnInfrastructureProblem(Action<ExportManifest, NativeSnapshot> change, string expected)
        {
            List<string> problems = ChecksAfter(change);
            Assert.That(problems.Any(x => x.Contains(expected)), "expected a problem naming '" + expected + "'; got: " + string.Join(" | ", problems));
        }

        [Test]
        public void Provenance_TheCommittedRunsAreValid_AndAnOptionalT3DLeftUnreadIsNotAProblem()
        {
            Assert.That(ChecksAfter((m, s) => { }), Is.Empty);

            // No T3D in the export's outputs: the T3D is optional, its absence is not a changed or unverified required file.
            List<string> optional = ChecksAfter((m, s) =>
            {
                m.Outputs.Remove(ReadBackFiles.T3d);
                s.Reader.T3dSha256Before = null;
                s.Reader.T3dSha256After = null;
                s.T3D = new NativeT3D { Kind = FieldKind.Absent, Detail = "no T3D path given" };
            });
            Assert.That(optional, Is.Empty);
        }

        [Test]
        public void FullCompare_AT3DChangedWhileRead_IsInfrastructureExit70_EvenThoughT3DChecksAreInformational()
        {
            string run = Evidence.Unpack(Box);
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            snapshot.Reader.T3dSha256After = "changed";
            snapshot.T3D.Kind = FieldKind.Unreadable;
            snapshot.T3D.Detail = "the T3D changed while it was read";
            ReadBackReport report = FullCompare(run, snapshot);

            Assert.That(report.ExitCode, Is.EqualTo(ReadBackExit.Infrastructure), report.Status);
            Assert.That(report.Infrastructure.Any(x => x.Contains("T3D changed")));
        }

        [Test]
        public void FullCompare_MissingTimingEvidence_IsInfrastructureExit70()
        {
            string run = Evidence.Unpack(Box);
            ExportManifest manifest = SnapshotJson.Read<ExportManifest>(Path.Combine(run, ReadBackFiles.ExportManifest));
            manifest.EndedUtc = null;
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            snapshot.Reader.StartedUtc = null;
            ReadBackReport report = FullCompare(run, snapshot, GateRuleSet.Interim, manifest);

            Assert.That(report.ExitCode, Is.EqualTo(ReadBackExit.Infrastructure), report.Status);
        }
    }
}
