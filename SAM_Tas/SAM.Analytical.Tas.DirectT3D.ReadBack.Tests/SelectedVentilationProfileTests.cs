// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical.InputEquivalence;
using SAM.Analytical.Tas.DirectT3D.Validation.ReadBack;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace SAM.Analytical.Tas.DirectT3D.ReadBack.Tests
{
    /// <summary>
    /// S08 PR-C review (2026-10-21, P1): an authored Ventilation profile selection that does not resolve (a missing name, or the
    /// name of a profile of another type) was dropped by the expected model, so the zone read as "no delivery selected" and the
    /// approved T-VENT-INACTIVE certified it. The selection is now kept apart from its resolution. Each case runs the full
    /// <see cref="ReadBackCompare.Run"/> path on the committed acceptance fixture, the run's own frozen weather and native
    /// snapshot, with only the SAM input changed (and the frozen-model hash in the manifest updated to match it).
    /// </summary>
    [TestFixture]
    public class SelectedVentilationProfileTests
    {
        private const string Two = "s08-fix-two";
        private static readonly Guid SpaceA = Guid.Parse("5080c000-0000-0000-0000-00000000c0a1");
        private static readonly Guid SpaceB = Guid.Parse("5080c000-0000-0000-0000-00000000c0b1");

        private static string Root(params string[] parts)
        {
            DirectoryInfo directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "Documentation", "S08-PRC-Acceptance-evidence")))
            {
                directory = directory.Parent;
            }

            Assert.That(directory, Is.Not.Null, "repository root not found");
            return Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
        }

        private static void Gunzip(string source, string target)
        {
            using (FileStream input = File.OpenRead(source))
            using (GZipStream gzip = new GZipStream(input, CompressionMode.Decompress))
            using (FileStream output = File.Create(target))
            {
                gzip.CopyTo(output);
            }
        }

        /// <summary>A disposable run folder: the committed fixture changed by <paramref name="change"/>, and the licensed run's weather, snapshot and manifest.</summary>
        private static string Run(Action<AnalyticalModel> change)
        {
            string evidence = Root("Documentation", "S08-PRC-Acceptance-evidence", "runs", Two);
            string run = Path.Combine(Path.GetTempPath(), "s08c-selection", Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(run);

            AnalyticalModel model = Core.Convert.ToSAM<AnalyticalModel>(Root("SAM_Tas", "SAM.Analytical.Tas.DirectT3D.Validation", "ReadBack", "Fixtures", Two + ".json")).Single();
            change?.Invoke(model);
            Core.Convert.ToFile(model, Path.Combine(run, ReadBackFiles.FrozenModel), Core.SAMFileType.Json);
            Gunzip(Path.Combine(evidence, ReadBackFiles.FrozenWeather + ".gz"), Path.Combine(run, ReadBackFiles.FrozenWeather));
            Gunzip(Path.Combine(evidence, ReadBackFiles.Snapshot + ".gz"), Path.Combine(run, ReadBackFiles.Snapshot));

            ExportManifest manifest = SnapshotJson.Read<ExportManifest>(Path.Combine(evidence, ReadBackFiles.ExportManifest));
            manifest.FrozenModelSha256 = manifest.FrozenModelSha256After = SnapshotJson.Sha256(Path.Combine(run, ReadBackFiles.FrozenModel));
            SnapshotJson.Write(manifest, Path.Combine(run, ReadBackFiles.ExportManifest));
            return run;
        }

        private static ReadBackReport Compare(string run, GateRuleSet ruleSet)
        {
            return ReadBackCompare.Run(run, Evidence.Rules, "regression", "test", ruleSet, Path.Combine(run, "out-" + ruleSet));
        }

        // Selects a Ventilation profile for space A by name, as an author would: the raw selection is stored on its condition.
        private static Action<AnalyticalModel> Select(Func<AnalyticalModel, string> name)
        {
            return model =>
            {
                AdjacencyCluster cluster = model.AdjacencyCluster;
                Space space = cluster.GetSpaces().Single(x => x.Guid == SpaceA);
                InternalCondition ic = space.InternalCondition;
                string selection = name(model);
                ic.SetProfileName(ProfileType.Ventilation, selection);
                space.InternalCondition = ic;
                cluster.AddObject(space);

                InternalCondition stored = cluster.GetSpaces().Single(x => x.Guid == SpaceA).InternalCondition;
                Assert.That(stored.GetProfileName(ProfileType.Ventilation), Is.EqualTo(selection), "the authored selection is stored");
                Assert.That(stored.GetProfile(ProfileType.Ventilation, model.ProfileLibrary), Is.Null, "and it does not resolve");
            };
        }

        private static readonly Func<AnalyticalModel, string> Missing = _ => "s08c-missing-ventilation-profile";
        private static readonly Func<AnalyticalModel, string> WrongType = m => m.AdjacencyCluster.GetSpaces().Single(x => x.Guid == SpaceA).InternalCondition.GetProfileName(ProfileType.Heating);

        private static ReportResult Item(ReadBackReport report, string quantityId, Guid space, string key)
        {
            return report.Results.Single(x => x.QuantityId == quantityId && x.ObjectKey == "space:" + space.ToString("D") + ".ic.ticV." + key);
        }

        [Test]
        public void Baseline_TheUnchangedFixture_StillPasses_WithBothZonesInactive()
        {
            ReadBackReport report = Compare(Run(null), GateRuleSet.Approved);

            Assert.That(report.ExitCode, Is.EqualTo(ReadBackExit.Pass), report.Status);
            Assert.That(report.Supporting.Count(x => x.Contains(ReadBackComparison.InactiveVentilationTransformation + " verified")), Is.EqualTo(2));
        }

        private static readonly object[] Selections =
        {
            new object[] { "missing", Missing },
            new object[] { "wrong-type", WrongType }
        };

        [TestCaseSource(nameof(Selections))]
        public void ApprovedRules_ASelectedButUnresolvedVentilationProfile_IsNeverInactive_AndBlocksAsAnInvalidInput(string what, Func<AnalyticalModel, string> name)
        {
            ReadBackReport report = Compare(Run(Select(name)), GateRuleSet.Approved);

            Assert.That(report.ExitCode, Is.Not.EqualTo(ReadBackExit.Pass), what + ": " + report.Status);
            Assert.That(report.ExitCode, Is.EqualTo(ReadBackExit.BlockedTechnical), what + ": " + report.Status + " " + string.Join(" | ", report.StatusReasons));
            foreach (string key in new[] { "hourly", "eflh", "delivered" })
            {
                ReportResult item = Item(report, key == "delivered" ? "8b" : key == "hourly" ? "6.hourly" : "6.eflh", SpaceA, key);
                Assert.That(item.Outcome, Is.EqualTo(EquivalenceOutcome.InputInvalid.ToString()), what + " " + key + ": " + item.Reason);
                Assert.That(item.Reason, Does.Contain("Ventilation profile"), what + " " + key);
                Assert.That(item.Reason, Does.Not.Contain(ReadBackComparison.InactiveVentilationTransformation), what + " " + key);
            }

            Assert.That(report.Supporting.Single(x => x.StartsWith("space:" + SpaceA.ToString("D") + ": " + ReadBackComparison.InactiveVentilationTransformation, StringComparison.Ordinal)), Does.Contain("not applied: SAM selects a Ventilation profile"));
            Assert.That(report.Supporting.Count(x => x.Contains(ReadBackComparison.InactiveVentilationTransformation + " verified")), Is.EqualTo(1), "the unselected zone B keeps its V1 behaviour");
            Assert.That(Item(report, "8b", SpaceB, "delivered").Outcome, Is.EqualTo(EquivalenceOutcome.AcceptedTransformation.ToString()));
        }

        [TestCaseSource(nameof(Selections))]
        public void PreDecisionRules_ASelectedButUnresolvedVentilationProfile_IsAlsoAnInvalidInput(string what, Func<AnalyticalModel, string> name)
        {
            ReadBackReport report = Compare(Run(Select(name)), GateRuleSet.ProvisionalProposed);

            Assert.That(report.ExitCode, Is.EqualTo(ReadBackExit.BlockedTechnical), what + ": " + report.Status);
            Assert.That(Item(report, "8b", SpaceA, "delivered").Outcome, Is.EqualTo(EquivalenceOutcome.InputInvalid.ToString()), what);
        }

        [Test]
        public void ABlankSelection_IsNoSelection_AndStaysInactive()
        {
            Action<AnalyticalModel> blank = model =>
            {
                Space space = model.AdjacencyCluster.GetSpaces().Single(x => x.Guid == SpaceA);
                InternalCondition ic = space.InternalCondition;
                ic.SetProfileName(ProfileType.Ventilation, "   ");
                space.InternalCondition = ic;
                model.AdjacencyCluster.AddObject(space);
            };

            ReadBackReport report = Compare(Run(blank), GateRuleSet.Approved);
            Assert.That(report.ExitCode, Is.EqualTo(ReadBackExit.Pass), report.Status);
        }
    }
}
