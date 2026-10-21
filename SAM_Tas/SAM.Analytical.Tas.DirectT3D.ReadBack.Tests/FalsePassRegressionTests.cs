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
    /// domain, an extra native design day, and provenance that does not prove a fresh, unchanged read. The review of revision 2
    /// added two: a derived U whose native film inputs (inclination, surface type) are not usable, and a snapshot without
    /// the per-run calendar that decides day-type applicability. None may PASS.
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

        // ---- 4. derived U: the native film-selection inputs (review of revision 2) ----------------------------------------

        private static NativeSurface OpaqueWall(NativeSnapshot snapshot)
        {
            return snapshot.Zones[0].Surfaces.First(x => !ReadBackIdentity.IsOpening(x) && x.Inclination.HasNumber && x.Inclination.Value == 90);
        }

        private static IEnumerable<TestCaseData> FilmDependencyFailures()
        {
            foreach (GateRuleSet ruleSet in BothRuleSets)
            {
                yield return new TestCaseData((Action<NativeSurface>)(s => s.Inclination = NativeField.Unreadable("regression: inclination getter failed")), ruleSet, "inclination, which is Unreadable [regression: inclination getter failed]").SetName("FilmInput_InclinationUnreadable_" + ruleSet);
                yield return new TestCaseData((Action<NativeSurface>)(s => s.Inclination = NativeField.Absent("regression: no inclination")), ruleSet, "inclination, which is Absent [regression: no inclination]").SetName("FilmInput_InclinationAbsent_" + ruleSet);
                yield return new TestCaseData((Action<NativeSurface>)(s => s.Inclination = NativeField.Of(double.NaN)), ruleSet, "inclination, which is NaN").SetName("FilmInput_InclinationNaN_" + ruleSet);
                yield return new TestCaseData((Action<NativeSurface>)(s => s.Inclination = null), ruleSet, "inclination, which was not read").SetName("FilmInput_InclinationNotRead_" + ruleSet);
                yield return new TestCaseData((Action<NativeSurface>)(s => s.Inclination = NativeField.Of(-999)), ruleSet, "inclination -999 is outside 0..180").SetName("FilmInput_InclinationBelowDomain_" + ruleSet);
                yield return new TestCaseData((Action<NativeSurface>)(s => s.Inclination = NativeField.Of(270)), ruleSet, "inclination 270 is outside 0..180").SetName("FilmInput_InclinationAboveDomain_" + ruleSet);
                yield return new TestCaseData((Action<NativeSurface>)(s => s.Type = null), ruleSet, "surface type, which was not read").SetName("FilmInput_SurfaceTypeNotRead_" + ruleSet);
                yield return new TestCaseData((Action<NativeSurface>)(s => s.Type = "tbdShade"), ruleSet, "surface type, which is 'tbdShade'").SetName("FilmInput_SurfaceTypeUnrecognised_" + ruleSet);
            }
        }

        /// <summary>
        /// The derived U (3b) depends on the native film selection (inclination and surface type). A dependency that is not
        /// a usable number or recognised type leaves the native U undeterminable: BLOCKED technical with the dependency's own
        /// reader state named, never an assumed Horizontal or External film. Only that side's U changes.
        /// </summary>
        [TestCaseSource(nameof(FilmDependencyFailures))]
        public void FilmInput_NotUsable_BlocksTheDerivedU_ThroughTheFullCompare(Action<NativeSurface> change, GateRuleSet ruleSet, string named)
        {
            string run = Evidence.Unpack(Box);
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            change(OpaqueWall(snapshot));
            ReadBackReport report = FullCompare(run, snapshot, ruleSet);

            Assert.That(report.NotPass, Has.Count.EqualTo(1), string.Join(" | ", report.NotPass));
            Assert.That(report.NotPass[0], Does.StartWith(GateStatus.BlockedTechnical + " 3b panel:").And.EndWith(".U"));
            ReportResult u = report.Results.Single(x => x.QuantityId == "3b" && report.NotPass[0].EndsWith(" " + x.ObjectKey, StringComparison.Ordinal));
            Assert.That(u.Outcome, Is.EqualTo(EquivalenceOutcome.UnverifiedUnreadable.ToString()), u.Reason);
            Assert.That(u.TechnicalBlocker, Does.Contain(named).And.Contain("no film is assumed"));
            Assert.That(report.ExitCode, Is.EqualTo(ReadBackExit.BlockedTechnical), report.Status);
        }

        [TestCaseSource(nameof(BothRuleSets))]
        public void FilmInput_TheMeasuredInclinationsAndRecognisedTypes_StillPass(GateRuleSet ruleSet)
        {
            // Box: inclinations 0, 90 and 180 with tbdExposed and tbdGround. Two-zone: also the tbdLink partition sides.
            string box = Evidence.Unpack(Box);
            ReadBackReport boxReport = FullCompare(box, Evidence.Snapshot(box), ruleSet);
            Assert.That(boxReport.ExitCode, Is.EqualTo(ReadBackExit.Pass), string.Join(" | ", boxReport.NotPass));

            string two = Evidence.Unpack("calib-two");
            NativeSnapshot twoSnapshot = Evidence.Snapshot(two);
            Assert.That(twoSnapshot.Zones.SelectMany(x => x.Surfaces).Select(x => x.Type).Distinct(), Is.EquivalentTo(new[] { "tbdExposed", "tbdGround", "tbdLink" }));
            ReadBackReport twoReport = FullCompare(two, twoSnapshot, ruleSet);
            List<ReportResult> u = twoReport.Results.Where(x => x.QuantityId.StartsWith("3b", StringComparison.Ordinal)).ToList();
            Assert.That(u, Is.Not.Empty);
            Assert.That(u.All(x => x.Status == GateStatus.Pass.ToString()), string.Join(" | ", u.Where(x => x.Status != GateStatus.Pass.ToString()).Select(x => x.ObjectKey + " " + x.Reason)));
        }

        // ---- 5. calendar applicability evidence (review of revision 2) ----------------------------------------------------

        private static readonly string[] ScheduleRows = { "5.temperature", "5.humidity", "6.hourly", "6.eflh", "8b" };

        private static IEnumerable<TestCaseData> CalendarEvidenceMissing()
        {
            foreach (GateRuleSet ruleSet in BothRuleSets)
            {
                yield return new TestCaseData((Action<NativeCalendar, NativeSnapshot>)((c, s) => s.Calendar = null), ruleSet, "the snapshot holds no native calendar").SetName("Calendar_Omitted_" + ruleSet);
                yield return new TestCaseData((Action<NativeCalendar, NativeSnapshot>)((c, s) => c.Days = null), ruleSet, "the native calendar holds no days").SetName("Calendar_DaysOmitted_" + ruleSet);
            }
        }

        /// <summary>
        /// Without the per-run calendar the day-type applicability of every annual schedule is unread: every hour of rows 5,
        /// 6 and 8b is BLOCKED technical with the calendar named, never unmasked profiles certified by the alignment rules.
        /// </summary>
        [TestCaseSource(nameof(CalendarEvidenceMissing))]
        public void Calendar_EvidenceMissing_BlocksEveryScheduleRow_ThroughTheFullCompare(Action<NativeCalendar, NativeSnapshot> change, GateRuleSet ruleSet, string named)
        {
            string run = Evidence.Unpack(Box);
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            change(snapshot.Calendar, snapshot);
            ReadBackReport report = FullCompare(run, snapshot, ruleSet);

            List<ReportResult> rows = report.Results.Where(x => ScheduleRows.Contains(x.QuantityId)).ToList();
            Assert.That(rows.Select(x => x.QuantityId).Distinct(), Is.EquivalentTo(ScheduleRows));
            foreach (ReportResult row in rows)
            {
                Assert.That(row.Status, Is.EqualTo(GateStatus.BlockedTechnical.ToString()), row.QuantityId + " " + row.ObjectKey + ": " + row.Reason);
                Assert.That(row.TechnicalBlocker, Does.Contain(named), row.ObjectKey);
            }

            Assert.That(report.NotPass.All(x => ScheduleRows.Any(q => x.Contains(" " + q + " "))), "nothing outside the schedule rows changes: " + string.Join(" | ", report.NotPass.Where(x => !ScheduleRows.Any(q => x.Contains(" " + q + " ")))));
            Assert.That(report.ExitCode, Is.EqualTo(ReadBackExit.BlockedTechnical), report.Status);
        }

        private static IEnumerable<TestCaseData> CalendarDayUnread()
        {
            foreach (GateRuleSet ruleSet in BothRuleSets)
            {
                yield return new TestCaseData((Action<NativeCalendar>)(c => c.Days.RemoveAll(x => x.Index == 100)), ruleSet, "calendar day 100 was read 0 times").SetName("Calendar_DayMissing_" + ruleSet);
                yield return new TestCaseData((Action<NativeCalendar>)(c => { NativeCalendarDay d = c.Days.Single(x => x.Index == 100); d.DayType = null; d.Detail = "unreadable: COMException"; }), ruleSet, "calendar day 100 has no day type (unreadable: COMException)").SetName("Calendar_DayTypeUnreadable_" + ruleSet);
                yield return new TestCaseData((Action<NativeCalendar>)(c => c.Days.Add(new NativeCalendarDay { Index = 100, DayType = "Sunday" })), ruleSet, "calendar day 100 was read 2 times").SetName("Calendar_DayDuplicated_" + ruleSet);
            }
        }

        /// <summary>One unread calendar day (index 100 = SAM day 100) blocks exactly that day's hours; every element is kept.</summary>
        [TestCaseSource(nameof(CalendarDayUnread))]
        public void Calendar_OneDayUnread_BlocksThatDaysHours(Action<NativeCalendar> change, GateRuleSet ruleSet, string named)
        {
            string run = Evidence.Unpack(Box);
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            change(snapshot.Calendar);
            ReadBackReport report = FullCompare(run, snapshot, ruleSet);

            ReportResult hourly = Item(report, "6.hourly", ".ticLG.hourly");
            Assert.That(hourly.Status, Is.EqualTo(GateStatus.BlockedTechnical.ToString()), hourly.Reason);
            Assert.That(hourly.TechnicalBlocker, Does.Contain(named));
            Assert.That(hourly.Series.ElementCount, Is.EqualTo(8760), "every element is kept");
            Assert.That(hourly.Series.NotPassCount, Is.EqualTo(24), "only the unread day's hours");
            Assert.That(hourly.Series.FirstNotPass.First(), Is.EqualTo(99 * 24));
            Assert.That(report.ExitCode, Is.EqualTo(ReadBackExit.BlockedTechnical), report.Status);
        }

        [Test]
        public void Calendar_ADayTypeTheConditionDoesNotCarry_IsStillMaskedAsAbsent_NotUnread()
        {
            // Preserved: a read day whose type the annual condition does not carry masks the profile (native absent), so a
            // stated SAM schedule is an export defect on that day - a different, named outcome from an unread calendar.
            string run = Evidence.Unpack(Box);
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            snapshot.Calendar.Days.Single(x => x.Index == 100).DayType = "HDD";
            ReadBackReport report = FullCompare(run, snapshot);

            ReportResult hourly = Item(report, "6.hourly", ".ticLG.hourly");
            Assert.That(hourly.Status, Is.EqualTo(GateStatus.Fail.ToString()), hourly.Reason);
            Assert.That(hourly.Series.NotPassCount, Is.EqualTo(24));
            Assert.That(report.ExitCode, Is.EqualTo(ReadBackExit.Fail), report.Status);
        }

        [Test]
        public void Calendar_Missing_UnderAnUnverifiedCalendarRule_StaysNotRun()
        {
            // Preserved: with N-6 (and the alignment rules) unverified the schedules are NOT RUN on the convention, as before.
            string run = Evidence.Unpack(Box);
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            snapshot.Calendar = null;
            ReadBackComparison comparison = Evidence.Compare(run, snapshot, rules: CalibrationRules.None());

            Assert.That(comparison.Results.Where(x => x.Definition.Id == "6.hourly").All(x => x.Status == GateStatus.NotRun));
            Assert.That(comparison.Gate.Status, Is.EqualTo(GateStatus.NotRun));
        }
    }
}
