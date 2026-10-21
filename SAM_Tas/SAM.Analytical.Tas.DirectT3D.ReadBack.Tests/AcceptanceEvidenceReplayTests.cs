// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical.InputEquivalence;
using SAM.Analytical.Tas.DirectT3D.Validation.ReadBack;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;

namespace SAM.Analytical.Tas.DirectT3D.ReadBack.Tests
{
    /// <summary>
    /// S08 PR-C: the licensed acceptance (Documentation/S08-PRC-Acceptance-evidence) replayed offline. The committed native
    /// snapshots of the two fixture runs are compared again with the committed fixture and the run's own frozen weather,
    /// under the committed rules, and must reproduce the committed verdicts exactly.
    /// </summary>
    [TestFixture]
    public class AcceptanceEvidenceReplayTests
    {
        private static string EvidenceRoot
        {
            get
            {
                DirectoryInfo directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
                while (directory != null)
                {
                    string candidate = Path.Combine(directory.FullName, "Documentation", "S08-PRC-Acceptance-evidence");
                    if (Directory.Exists(candidate)) return candidate;
                    directory = directory.Parent;
                }

                throw new DirectoryNotFoundException("Documentation/S08-PRC-Acceptance-evidence not found above " + TestContext.CurrentContext.TestDirectory);
            }
        }

        private static string FixturePath(string name)
        {
            return Path.Combine(EvidenceRoot, "..", "..", "SAM_Tas", "SAM.Analytical.Tas.DirectT3D.Validation", "ReadBack", "Fixtures", name + ".json");
        }

        private static string Gunzip(string path)
        {
            string target = Path.Combine(Path.GetTempPath(), "s08c-replay", Guid.NewGuid().ToString("N").Substring(0, 8) + "-" + Path.GetFileNameWithoutExtension(path));
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            using (FileStream input = File.OpenRead(path))
            using (GZipStream gzip = new GZipStream(input, CompressionMode.Decompress))
            using (FileStream output = File.Create(target))
            {
                gzip.CopyTo(output);
            }

            return target;
        }

        private static string Sha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return string.Concat(sha.ComputeHash(stream).Select(x => x.ToString("x2")));
            }
        }

        private static ReadBackReport Replay(string fixture, GateRuleSet ruleSet, out ExportManifest manifest)
        {
            string run = Path.Combine(EvidenceRoot, "runs", fixture);
            manifest = SnapshotJson.Read<ExportManifest>(Path.Combine(run, ReadBackFiles.ExportManifest));
            string model = FixturePath(fixture);
            string weather = Gunzip(Path.Combine(run, ReadBackFiles.FrozenWeather + ".gz"));

            // The inputs are exactly the frozen files the licensed export ran on.
            Assert.That(Sha256(model), Is.EqualTo(manifest.FrozenModelSha256), "the committed fixture is the frozen model");
            Assert.That(Sha256(weather), Is.EqualTo(manifest.FrozenWeatherSha256), "the committed weather is the frozen weather");

            ExpectedModel expected = ReadBackExpected.Build(Core.Convert.ToSAM<AnalyticalModel>(model).Single(), Core.Convert.ToSAM<Weather.WeatherData>(weather).Single(), fixture);
            NativeSnapshot snapshot = SnapshotJson.Read<NativeSnapshot>(Gunzip(Path.Combine(run, ReadBackFiles.Snapshot + ".gz")));
            return ReadBackReport.Create(ReadBackComparison.Run(expected, snapshot, CalibrationRules.Load(Evidence.Rules), ruleSet), "replay", null);
        }

        private static ReadBackReport Committed(string fixture, string set)
        {
            return SnapshotJson.Read<ReadBackReport>(Gunzip(Path.Combine(EvidenceRoot, "runs", fixture, "compare-" + set, "report.json.gz")));
        }

        [TestCase("s08-fix-box", GateRuleSet.Approved, "approved", ReadBackExit.Pass)]
        [TestCase("s08-fix-two", GateRuleSet.Approved, "approved", ReadBackExit.Pass)]
        [TestCase("s08-fix-box", GateRuleSet.ProvisionalProposed, "provisional", ReadBackExit.Pass)]
        [TestCase("s08-fix-two", GateRuleSet.ProvisionalProposed, "provisional", ReadBackExit.BlockedOwner)]
        public void TheLicensedVerdict_ReplaysOffline_ItemByItem(string fixture, GateRuleSet ruleSet, string set, int exit)
        {
            ReadBackReport replay = Replay(fixture, ruleSet, out _);
            ReadBackReport committed = Committed(fixture, set);

            Assert.That(replay.ExitCode, Is.EqualTo(exit), replay.Status);
            Assert.That(committed.ExitCode, Is.EqualTo(exit), committed.Status);
            Assert.That(replay.Coverage, Is.EqualTo("Complete"));
            Assert.That(replay.Results.Select(x => x.QuantityId + "|" + x.Outcome + "|" + x.Status).OrderBy(x => x), Is.EqualTo(committed.Results.Select(x => x.QuantityId + "|" + x.Outcome + "|" + x.Status).OrderBy(x => x)));
            Assert.That(replay.ElementsByOutcome, Is.EquivalentTo(committed.ElementsByOutcome));
        }

        [Test]
        public void TheTwoZoneVerdict_DependsOnlyOnTheInactiveVentilationTransformation()
        {
            ReadBackReport approved = Replay("s08-fix-two", GateRuleSet.Approved, out _);
            ReadBackReport provisional = Replay("s08-fix-two", GateRuleSet.ProvisionalProposed, out _);

            var changed = approved.Results.Zip(provisional.Results, (a, p) => new { a, p }).Where(x => x.a.Outcome != x.p.Outcome).ToList();
            Assert.That(changed.Select(x => x.a.QuantityId).Distinct(), Is.EquivalentTo(new[] { "6.hourly", "6.eflh", "8b" }));
            Assert.That(changed.All(x => x.a.ObjectKey.Contains(".ic.ticV.") && x.a.Outcome == "AcceptedTransformation" && x.a.Reason.Contains(ReadBackComparison.InactiveVentilationTransformation)), Is.True);
            Assert.That(changed, Has.Count.EqualTo(6));
        }
    }
}
