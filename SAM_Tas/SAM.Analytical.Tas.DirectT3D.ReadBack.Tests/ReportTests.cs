// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical.InputEquivalence;
using SAM.Analytical.Tas.DirectT3D.Validation.ReadBack;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using EQ = SAM.Analytical.InputEquivalence.Query;

namespace SAM.Analytical.Tas.DirectT3D.ReadBack.Tests
{
    /// <summary>
    /// The report keeps every series element. A series' GateResult carries its decisive element's metadata only, so the
    /// proposed cases, owner decisions, blockers and outcome counts are tallied over every element - and the gate rank is
    /// not changed by that.
    /// </summary>
    [TestFixture]
    public class ReportTests
    {
        private static QuantityInput Prototype(string quantityId, string key)
        {
            return new QuantityInput { Definition = QuantityCatalog.Get(quantityId), FixtureId = "series", ObjectKey = key, Identity = IdentityStatus.Verified, Alignment = AlignmentStatus.Verified };
        }

        // Each gating state that can sit beside an informational mismatch in one series.
        public static IEnumerable<TestCaseData> GatingStates()
        {
            yield return new TestCaseData(ExpectedValue.FromValue(5), ReaderResult.Absent("gone"), GateStatus.Fail, null).SetName("Absent_ExportDefect");
            yield return new TestCaseData(ExpectedValue.FromValue(5), ReaderResult.Unreadable("COM"), GateStatus.BlockedTechnical, null).SetName("Unreadable_Technical");
            yield return new TestCaseData(ExpectedValue.FromValue(5), ReaderResult.NotANumber(), GateStatus.BlockedOwnerDecision, "G3").SetName("NaN_G3");
            yield return new TestCaseData(ExpectedValue.Unspecified(), ReaderResult.FromValue(0), GateStatus.BlockedOwnerDecision, "G1").SetName("Zero_G1");
            yield return new TestCaseData(ExpectedValue.Invalid("bad input"), ReaderResult.FromValue(5), GateStatus.BlockedTechnical, null).SetName("Invalid_Technical");
        }

        [TestCaseSource(nameof(GatingStates))]
        public void MixedSeries_InformationalBesideAGatingState_BothOrders_KeepsEveryElement(ExpectedValue expected, ReaderResult native, GateStatus gating, string proposed)
        {
            // 9.ground is not gated: its numeric mismatch is informational, every other outcome still gates.
            ExpectedValue informationalExpected = ExpectedValue.FromValue(10);
            ReaderResult informationalNative = ReaderResult.FromValue(11);

            foreach (bool informationalFirst in new[] { true, false })
            {
                List<ExpectedValue> e = informationalFirst ? new List<ExpectedValue> { informationalExpected, expected } : new List<ExpectedValue> { expected, informationalExpected };
                List<ReaderResult> n = informationalFirst ? new List<ReaderResult> { informationalNative, native } : new List<ReaderResult> { native, informationalNative };
                SeriesResult series = EQ.EvaluateSeries(Prototype("9.ground", "weather.groundSeries"), e, n);

                Assert.That(series.GateResult.Status, Is.EqualTo(gating), "the decisive element is the gating one, whatever the order");

                ReadBackReport report = new ReadBackReport();
                ReadBackReport.Tally(report, new[] { series.GateResult }, new[] { new SeriesRecord { QuantityId = "9.ground", ObjectKey = "weather.groundSeries", Result = series } });

                Assert.That(report.InformationalMismatches, Is.EqualTo(1), "the informational element is never dropped");
                Assert.That(report.ElementsByOutcome.Values.Sum(), Is.EqualTo(2));
                Assert.That(report.ElementsByOutcome.ContainsKey(EquivalenceOutcome.OutOfTolerance.ToString()));
                Assert.That(report.Results.Single().Series.ElementCount, Is.EqualTo(2));
                Assert.That(report.NotPass, Has.Count.EqualTo(1));
                if (proposed != null)
                {
                    Assert.That(report.ProposedCases.Keys.Any(x => x.Contains(proposed)));
                }
            }
        }

        [Test]
        public void NonDecisiveGCases_AreStillReported_AndDoNotChangeTheRank()
        {
            foreach (bool failFirst in new[] { true, false })
            {
                List<ExpectedValue> e = new List<ExpectedValue> { ExpectedValue.FromValue(1), ExpectedValue.Unspecified(), ExpectedValue.FromValue(3) };
                List<ReaderResult> n = new List<ReaderResult> { ReaderResult.FromValue(2), ReaderResult.FromValue(0), ReaderResult.NotANumber() };
                if (!failFirst)
                {
                    e.Reverse();
                    n.Reverse();
                }

                SeriesResult series = EQ.EvaluateSeries(Prototype("9", "weather.mixed"), e, n);
                Assert.That(series.GateResult.Status, Is.EqualTo(GateStatus.Fail));
                Assert.That(series.GateResult.Proposed, Is.Null, "the decisive FAIL carries no proposal of its own");

                ReadBackReport report = new ReadBackReport();
                ReadBackReport.Tally(report, new[] { series.GateResult }, new[] { new SeriesRecord { QuantityId = "9", ObjectKey = "weather.mixed", Result = series } });
                Assert.That(report.ProposedCases.Keys.Count(x => x.Contains("G1")), Is.EqualTo(1));
                Assert.That(report.ProposedCases.Keys.Count(x => x.Contains("G3")), Is.EqualTo(1));
                Assert.That(report.OwnerDecisions.Keys, Does.Contain("OD-G1").And.Contain("OD-G3"));
                Assert.That(report.Results.Single().Series.ProposedCases, Has.Count.EqualTo(2));
                Assert.That(report.Results.Single().Status, Is.EqualTo(GateStatus.Fail.ToString()));
            }
        }

        [Test]
        public void SerializedReport_RetainsNonDecisiveGCases_AndEveryNotPassElement()
        {
            string run = Evidence.Unpack("calib-two");
            string output = Path.Combine(run, "out");
            ReadBackReport written = ReadBackCompare.Run(run, Evidence.Rules, "calib-two", "test", GateRuleSet.Interim, output);
            ReadBackReport read = SnapshotJson.Read<ReadBackReport>(Path.Combine(output, ReadBackCompare.ReportFile));

            Assert.That(read.Status, Is.EqualTo(written.Status));
            Assert.That(read.ProposedCases.Keys.Any(x => x.Contains("G1")));
            Assert.That(read.OwnerDecisions.Keys, Does.Contain("OD-G1"));
            // Two zones, each with no SAM ventilation profile and a present, zero native ticV: 8b and the schedule rows 6.hourly
            // and 6.eflh are all the proposed G1 (BLOCKED, owner decision), never absence.
            Assert.That(read.NotPass, Has.Count.EqualTo(6));
            Assert.That(read.NotPass.All(x => x.StartsWith(GateStatus.BlockedOwnerDecision.ToString(), StringComparison.Ordinal) && x.Contains(".ic.ticV.")), string.Join(" | ", read.NotPass));
            Assert.That(read.NotPass.Count(x => x.Contains(" 8b ")), Is.EqualTo(2));
            Assert.That(read.NotPass.Count(x => x.Contains(" 6.hourly ")), Is.EqualTo(2));
            Assert.That(read.NotPass.Count(x => x.Contains(" 6.eflh ")), Is.EqualTo(2));
            Assert.That(read.Results.Where(x => x.Series != null).All(x => x.Series.ElementCount > 0));

            // Every element of every series is in the elements file, one row each.
            int expectedRows = read.Results.Where(x => x.Series != null).Sum(x => x.Series.ElementCount);
            int rows;
            using (StreamReader reader = new StreamReader(new GZipStream(File.OpenRead(Path.Combine(output, ReadBackCompare.ElementsFile)), CompressionMode.Decompress)))
            {
                rows = -1;
                while (reader.ReadLine() != null) rows++;
            }

            Assert.That(rows, Is.EqualTo(expectedRows));
        }

        [Test]
        public void ExitCodes_ZeroOnlyForPass_AndInfrastructureIsDistinct()
        {
            Assert.That(ReadBackExit.Of(GateStatus.Pass), Is.EqualTo(0));
            List<int> others = new[] { GateStatus.Fail, GateStatus.BlockedTechnical, GateStatus.BlockedOwnerDecision, GateStatus.NotRun, GateStatus.NotPassAcceptedLimitation }.Select(ReadBackExit.Of).ToList();
            Assert.That(others, Has.None.EqualTo(0));
            Assert.That(others.Distinct().Count(), Is.EqualTo(others.Count));
            Assert.That(others, Has.None.EqualTo(ReadBackExit.Infrastructure));
            Assert.That(ReadBackReport.InfrastructureFailure("x", new[] { "licence" }).ExitCode, Is.EqualTo(ReadBackExit.Infrastructure));
        }

        [Test]
        public void Preconditions_AReaderInTheExportProcess_OrAChangedInput_IsInfrastructure()
        {
            string run = Evidence.Unpack("calib-box");
            ExportManifest manifest = SnapshotJson.Read<ExportManifest>(Path.Combine(run, ReadBackFiles.ExportManifest));
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            string model = Path.Combine(run, ReadBackFiles.FrozenModel), weather = Path.Combine(run, ReadBackFiles.FrozenWeather);
            Assert.That(ReadBackCompare.Checks(manifest, snapshot, model, weather), Is.Empty);

            snapshot.Reader.ProcessId = manifest.ProcessId;
            Assert.That(ReadBackCompare.Checks(manifest, snapshot, model, weather).Any(x => x.Contains("fresh process")));

            snapshot = Evidence.Snapshot(run);
            snapshot.Reader.TbdSha256Before = "0000";
            Assert.That(ReadBackCompare.Checks(manifest, snapshot, model, weather).Any(x => x.Contains("TBD the reader opened")));

            File.SetAttributes(model, FileAttributes.Normal);
            File.AppendAllText(model, " ");
            ReadBackReport report = ReadBackCompare.Run(run, Evidence.Rules, "changed", "test", GateRuleSet.Interim, Path.Combine(run, "out"));
            Assert.That(report.ExitCode, Is.EqualTo(ReadBackExit.Infrastructure));
            Assert.That(report.Infrastructure.Any(x => x.Contains("frozen model")));
        }

        [Test]
        public void LeapYearWeather_8784Hours_IsRefusedAsUnsupported_NotTruncated()
        {
            Assert.That(ReadBackCompare.EpwRefusal(8760), Is.Null);
            Assert.That(ReadBackCompare.EpwRefusal(8784), Does.StartWith("UNSUPPORTED-MAPPING"));
            Assert.That(ReadBackCompare.EpwRefusal(8000), Does.StartWith("INPUT-INVALID"));

            string path = Path.Combine(Path.GetTempPath(), "s08b-leap-" + Guid.NewGuid().ToString("N") + ".epw");
            File.WriteAllLines(path, Enumerable.Range(0, 8).Select(x => "HEADER " + x).Concat(Enumerable.Range(0, 8784).Select(x => "2024,1,1,1,0,data")));
            Assert.That(ReadBackCompare.EpwHourCount(path), Is.EqualTo(8784));
        }

        [Test]
        public void SnapshotDiff_IgnoresMintedGuidsAndReaderData_AndFindsAChangedField()
        {
            string run = Evidence.Unpack("calib-box");
            string a = Path.Combine(run, ReadBackFiles.Snapshot), b = Path.Combine(run, "changed.json");
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            snapshot.Reader.ProcessId = -1;
            snapshot.Zones[0].Guid = "{minted}";
            SnapshotJson.Write(snapshot, b);
            Assert.That(SnapshotDiff.Compare(a, b, "same").DifferenceCount, Is.EqualTo(0));

            snapshot.Zones[0].Volume = NativeField.Of(61);
            SnapshotJson.Write(snapshot, b);
            SnapshotDiffResult diff = SnapshotDiff.Compare(a, b, "changed");
            Assert.That(diff.Differences.Select(x => x.Path), Does.Contain("$.Zones[0].Volume.Value"));
        }
    }
}
