// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical.Tas.DirectT3D.Validation.Results;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Tas.DirectT3D.ReadBack.Tests
{
    /// <summary>
    /// S10 comparison on the consistent synthetic triple: PASS with complete coverage, the documented semantics row by row,
    /// and every negative control failing or blocking by its intended mechanism (the same matrix the licensed runs use).
    /// </summary>
    [TestFixture]
    public class S10ResultCompareTests
    {
        private static ResultComparison Baseline(out ResultReferenceModel reference)
        {
            return ResultCase.From(S10SyntheticTriple.Inputs()).Compare(S10SyntheticTriple.Inputs().Rules, null, out reference);
        }

        private static ResultRow Row(ResultComparison comparison, string layer, string zone, string loadType, string quantity)
        {
            return comparison.Rows.Single(x => x.Layer == layer && x.ZoneName == zone && x.LoadType == loadType && x.Quantity == quantity);
        }

        private static string Failures(ResultComparison comparison)
        {
            return string.Join("\n", comparison.Rows.Where(x => x.Status != RowStatus.Pass).Take(30).Select(x => x.Id + " (" + x.ZoneName + "): " + x.Status + " expected " + x.Expected + " actual " + x.Actual + " - " + x.Reason));
        }

        [Test]
        public void ConsistentTriple_Passes_WithEveryCalibrationCheckVerified()
        {
            ResultComparison comparison = Baseline(out ResultReferenceModel reference);

            Assert.That(reference.Blockers, Is.Empty);
            Assert.That(reference.Checks.Where(x => x.Status != "verified").Select(x => x.Id + ": " + x.Answer), Is.Empty);
            Assert.That(comparison.Verdict, Is.EqualTo("PASS"), Failures(comparison));
            Assert.That(comparison.ExitCode, Is.EqualTo(ResultCompare.ExitPass));
        }

        [Test]
        public void Coverage_EveryZoneLoadTypeAndLayer_IsARequiredRow()
        {
            ResultComparison comparison = Baseline(out _);
            string[] sam = { "tasResultCount", "reference", "annualPeak.load", "annualPeak.hourOfYear", "annualPeak.hourOfDay", "designDayPeak.load", "designDayPeak.hourOfDay", "designDayPeak.name", "legacy.sizingMethod", "legacy.load", "legacy.loadIndex", "designLoad", "unmetHours" };
            string[] b1a = { "peakLoad", "peakHour", "designLoad", "unmetHours" };
            foreach (string zone in new[] { S10SyntheticTriple.ZoneA, S10SyntheticTriple.ZoneB })
            {
                foreach (string type in new[] { "Heating", "Cooling" })
                {
                    foreach (string quantity in sam) Assert.That(Row(comparison, "SAM", zone, type, quantity), Is.Not.Null);
                    foreach (string quantity in b1a) Assert.That(Row(comparison, "B1a", zone, type, quantity), Is.Not.Null);
                }
            }

            foreach (string layer in new[] { "SAM", "B1a" })
            {
                foreach (string quantity in new[] { "consumption", "peakLoad", "peakHour" })
                {
                    Assert.That(comparison.Rows.Count(x => x.Layer == layer && x.Scope == "model" && x.Quantity == quantity), Is.EqualTo(2));
                }
            }
        }

        [Test]
        public void PeakInTheLastHour_IsHour8759_InBothLayers()
        {
            ResultComparison comparison = Baseline(out _);

            Assert.That(Row(comparison, "SAM", S10SyntheticTriple.ZoneA, "Heating", "annualPeak.hourOfYear").Expected, Is.EqualTo("8759"));
            Assert.That(Row(comparison, "B1a", S10SyntheticTriple.ZoneA, "Heating", "peakHour").Status, Is.EqualTo(RowStatus.Pass));
            Assert.That(Row(comparison, "SAM", S10SyntheticTriple.ZoneA, "Heating", "legacy.loadIndex").Expected, Is.EqualTo("8760"), "the legacy index stays Tas's raw 1-based index");
        }

        [Test]
        public void LaterTiedMaximum_IsAccepted_AndReportedAsATie()
        {
            ResultComparison comparison = Baseline(out ResultReferenceModel reference);
            ResultRow hour = Row(comparison, "SAM", S10SyntheticTriple.ZoneA, "Cooling", "annualPeak.hourOfYear");

            Assert.That(hour.Status, Is.EqualTo(RowStatus.Pass));
            Assert.That(hour.Expected, Is.EqualTo("one of 4000,4010"));
            Assert.That(hour.Reason, Does.Contain("a later of 2 tied"));
            Assert.That(reference.Observations, Has.Some.Contains("'" + S10SyntheticTriple.ZoneA + "' Cooling annual: max 2000 W, 2 tied hour(s), earliest 4000"));

            // The model tie rule is the earliest hour, exactly.
            Assert.That(comparison.Rows.Single(x => x.Layer == "SAM" && x.Scope == "model" && x.LoadType == "Cooling" && x.Quantity == "peakHour").Expected, Is.EqualTo("4000"));
        }

        [Test]
        public void ZeroAnnualPeak_IsAZeroLoad_WithNoHour()
        {
            ResultComparison comparison = Baseline(out _);

            Assert.That(Row(comparison, "B1a", S10SyntheticTriple.ZoneB, "Cooling", "peakLoad").Expected, Is.EqualTo("0"));
            Assert.That(Row(comparison, "B1a", S10SyntheticTriple.ZoneB, "Cooling", "peakHour").Expected, Is.EqualTo("no value"));
            Assert.That(Row(comparison, "SAM", S10SyntheticTriple.ZoneB, "Cooling", "annualPeak.hourOfYear").Expected, Is.EqualTo("no value"));
            Assert.That(Row(comparison, "SAM", S10SyntheticTriple.ZoneB, "Cooling", "legacy.load").Expected, Is.EqualTo("-1"), "documented legacy sentinel");
        }

        [Test]
        public void GoverningDesignDay_StaysInTheLegacyFields_AndB1aKeepsTheAnnualPeak()
        {
            ResultComparison comparison = Baseline(out _);

            Assert.That(Row(comparison, "SAM", S10SyntheticTriple.ZoneB, "Heating", "legacy.sizingMethod").Expected, Is.EqualTo("'Heating Design Day'"));
            Assert.That(Row(comparison, "SAM", S10SyntheticTriple.ZoneB, "Heating", "legacy.load").Expected, Is.EqualTo("1200"));
            Assert.That(Row(comparison, "B1a", S10SyntheticTriple.ZoneB, "Heating", "peakLoad").Expected, Is.EqualTo("800"));
            Assert.That(Row(comparison, "B1a", S10SyntheticTriple.ZoneB, "Heating", "peakHour").Expected, Is.EqualTo("30"));
        }

        [Test]
        public void NegativeControls_AllFailOrBlock_ByTheirIntendedMechanism()
        {
            List<NegativeOutcome> outcomes = ResultNegatives.Run(S10SyntheticTriple.Inputs(), "PASS");
            TestContext.Out.WriteLine(ResultReport.Negatives(outcomes));

            Assert.That(outcomes.Single(x => x.Case == ResultNegatives.ControlName).Verdict, Is.EqualTo("PASS"));
            Assert.That(outcomes.Where(x => !x.Applicable).Select(x => x.Case + ": " + x.Applied), Is.Empty, "the synthetic triple exercises every case");
            Assert.That(outcomes.Where(x => !x.AsIntended).Select(x => x.Case + " " + x.Verdict + ": " + string.Join("; ", x.Evidence)), Is.Empty);
            Assert.That(outcomes.Count, Is.EqualTo(ResultNegatives.Names.Count + 1));
        }

        [Test]
        public void HoursAreJudgedSeparatelyFromLoads()
        {
            NegativeOutcome outcome = ResultNegatives.Run(S10SyntheticTriple.Inputs()).Single(x => x.Case == "sam-hour-plus-1");
            ResultRow hour = outcome.Comparison.Rows.Single(x => x.Id == outcome.IntendedRows.Single());
            ResultRow load = outcome.Comparison.Rows.Single(x => x.Layer == hour.Layer && x.Scope == hour.Scope && x.LoadType == hour.LoadType && x.Quantity == "annualPeak.load");

            Assert.That(hour.Status, Is.EqualTo(RowStatus.Fail));
            Assert.That(load.Status, Is.EqualTo(RowStatus.Pass), "the load is right; only the hour is wrong");
        }

        [Test]
        public void UnitsUnresolved_BlocksTheLoadRows_NeverPasses()
        {
            NegativeOutcome outcome = ResultNegatives.Run(S10SyntheticTriple.Inputs()).Single(x => x.Case == "tsd-loads-in-kw");
            ResultReferenceModel reference = ResultReference.Build(outcome.Copy.Tsd, outcome.Copy.Tbd, outcome.Copy.Coverage, S10SyntheticTriple.Inputs().Rules);

            Assert.That(reference.Checks.Single(x => x.Id == "S10-C5").Status, Is.EqualTo("unresolved"));
            Assert.That(outcome.Verdict, Is.Not.EqualTo("PASS"));
            Assert.That(outcome.Comparison.Rows.Where(x => x.Scope == "model" && x.Quantity == "consumption").Select(x => x.Status), Is.All.EqualTo(RowStatus.Blocked));
        }

        [Test]
        public void ASubjectThatCouldNotBeCaptured_BlocksEveryRow()
        {
            ResultCase copy = ResultCase.From(S10SyntheticTriple.Inputs());
            ResultReferenceModel reference = ResultReference.Build(copy.Tsd, copy.Tbd, copy.Coverage, S10SyntheticTriple.Inputs().Rules);
            ResultSubjectSnapshot subject = copy.Subject(null);
            subject.Errors.Add("the TSD changed during the subject capture");

            ResultComparison comparison = ResultCompare.Run(reference, subject);

            Assert.That(comparison.Verdict, Is.EqualTo("BLOCKED"));
            Assert.That(comparison.Rows.Select(x => x.Status), Is.All.EqualTo(RowStatus.Blocked));
        }

        [Test]
        public void SyntheticTriple_RoundTripsThroughTheHarnessFiles()
        {
            // The layout results-read and results-subject write, so the harness's COM-free modes can be replayed on it.
            ResultInputs inputs = S10SyntheticTriple.Inputs();
            string root = System.IO.Path.Combine(TestContext.CurrentContext.WorkDirectory, "s10-synthetic");
            string read = System.IO.Path.Combine(root, "read");
            System.IO.Directory.CreateDirectory(read);
            System.IO.File.WriteAllText(System.IO.Path.Combine(read, "tsd-result.json"), inputs.TsdJson);
            System.IO.File.WriteAllText(System.IO.Path.Combine(read, "tbd-snapshot.json"), inputs.TbdJson);
            System.IO.File.WriteAllText(System.IO.Path.Combine(read, "tbd-design-coverage.json"), inputs.CoverageJson);
            string model = System.IO.Path.Combine(root, "fixture.json"), b1a = System.IO.Path.Combine(root, "b1a.json");
            System.IO.File.WriteAllText(model, inputs.ModelJson);
            System.IO.File.WriteAllText(b1a, inputs.B1aJson);

            ResultSubjectSnapshot subject = ResultCase.From(inputs).Subject(null);
            subject.ModelJsonPath = model;
            subject.ModelJsonSha256 = Validation.ReadBack.SnapshotJson.Sha256(model);
            subject.B1aPath = b1a;
            subject.B1aSha256 = Validation.ReadBack.SnapshotJson.Sha256(b1a);
            Validation.ReadBack.SnapshotJson.Write(subject, System.IO.Path.Combine(root, "subject.json"));

            ResultSubjectSnapshot reloaded = Validation.ReadBack.SnapshotJson.Read<ResultSubjectSnapshot>(System.IO.Path.Combine(root, "subject.json"));
            ResultReferenceModel reference = ResultReference.Build(
                Validation.ReadBack.SnapshotJson.Read<TsdResultSnapshot>(System.IO.Path.Combine(read, "tsd-result.json")),
                Validation.ReadBack.SnapshotJson.Read<Validation.ReadBack.NativeSnapshot>(System.IO.Path.Combine(read, "tbd-snapshot.json")),
                Validation.ReadBack.SnapshotJson.Read<TbdDesignCoverage>(System.IO.Path.Combine(read, "tbd-design-coverage.json")),
                inputs.Rules);

            Assert.That(ResultCompare.Run(reference, reloaded).Verdict, Is.EqualTo("PASS"));
            TestContext.Out.WriteLine(root);
        }

        [TestCase("R2", 1000.0, 1000.0 * (1 + 1e-6), false)]
        [TestCase("R2", 1000.0, 1000.0 + 5e-7, true)]
        [TestCase("R1", 5.073, 5.073 + 1e-11, false)]
        [TestCase("R1", 5.073, 5.073 + 1e-12, true)]
        [TestCase("R0", 1200.0, 1200.0000000000002, false)]
        public void Tolerances_AreTheApprovedClasses(string @class, double reference, double observed, bool pass)
        {
            Assert.That(System.Math.Abs(observed - reference) <= ResultCompare.Tolerance(@class, reference), Is.EqualTo(pass));
        }
    }
}
