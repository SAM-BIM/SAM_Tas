// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Core.Tas;
using System;
using System.IO;

namespace SAM.Analytical.Tas.TM59.Tests
{
    /// <summary>
    /// S01a. TAS writes its error log beside the file it writes, under that file's basename. Measured on the licensed
    /// laptop (2026-10-19): <c>tbddir\alpha.tbd</c> simulated to <c>tsddir\beta.tsd</c> left
    /// <c>tsddir\beta_error_log.txt</c> and nothing beside the TBD. So a separate-output run's log is keyed on the TSD;
    /// an in-place run's stays keyed on its document. Simulated evidence: real files, no TAS.
    /// </summary>
    [TestFixture]
    public class ErrorLogLocationTests
    {
        private string directory;
        private string path_TBD;
        private string path_TSD;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "SAM_S01a_ErrorLogLocation", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(directory, "tbddir"));
            Directory.CreateDirectory(Path.Combine(directory, "tsddir"));
            path_TBD = Path.Combine(directory, "tbddir", "alpha.tbd");
            File.WriteAllBytes(path_TBD, new byte[4096]);
            path_TSD = Path.Combine(directory, "tsddir", "beta.tsd");
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch
            {
                // A leftover temp directory must never fail a test.
            }
        }

        private string Path_TsdLog => Path.Combine(directory, "tsddir", "beta_error_log.txt");

        private string Path_TbdLog => Path.Combine(directory, "tbddir", "alpha_error_log.txt");

        [Test]
        public void E1_SeparateOutput_DifferentlyNamedTSD_KeysTheLogOnTheTSD()
        {
            SimulationEvidence simulationEvidence = new SimulationEvidence(SimulationOutputShape.SeparateOutputFile, path_TBD, path_TSD);

            Assert.That(simulationEvidence.Path_ErrorLog, Is.EqualTo(Path_TsdLog));
            Assert.That(simulationEvidence.Path_ErrorLog, Is.EqualTo(SimulationEvidence.ErrorLogPath(path_TSD)));
        }

        [Test]
        public void E2_TSDKeyedLog_RefusesEvenWithMinusOneAndANonTrivialTSD()
        {
            SimulationEvidence simulationEvidence = new SimulationEvidence(SimulationOutputShape.SeparateOutputFile, path_TBD, path_TSD);
            simulationEvidence.Prepare(DateTime.UtcNow.AddSeconds(-1));

            File.WriteAllBytes(path_TSD, new byte[1034856]);
            File.WriteAllText(Path_TsdLog, "Simulation Failed");

            simulationEvidence.RecordCallReturned((object)-1);
            simulationEvidence.Conclude();

            Assert.Multiple(() =>
            {
                Assert.That(simulationEvidence.Completed, Is.False, "the log TAS actually wrote must be read");
                Assert.That(simulationEvidence.ErrorLogText, Is.EqualTo("Simulation Failed"));
                Assert.That(simulationEvidence.Refusals, Has.Some.StartWith("TAS wrote an error log for this run: " + Path_TsdLog));
                Assert.That(simulationEvidence.Refusals, Has.Some.Contains("(TAS return code: -1)"));
                Assert.That(
                    simulationEvidence.ToDiagnostics(SAM.Core.DiagnosticDetail.Summary).Filter(SAM.Core.DiagnosticDetail.Summary).ConvertAll(x => x.Message),
                    Has.Some.Contains(Path_TsdLog),
                    "the refusal diagnostics name the TSD-keyed log");
            });
        }

        [Test]
        public void E3_StaleTSDKeyedLog_IsClearedBeforeASuccessfulRun()
        {
            File.WriteAllText(Path_TsdLog, "Simulation Failed: an EARLIER run's failure");

            SimulationEvidence simulationEvidence = new SimulationEvidence(SimulationOutputShape.SeparateOutputFile, path_TBD, path_TSD);
            simulationEvidence.Prepare(DateTime.UtcNow.AddSeconds(-1));

            Assert.That(File.Exists(Path_TsdLog), Is.False, "the stale log TAS would be read from is deleted before the run");

            File.WriteAllBytes(path_TSD, new byte[1034856]);
            simulationEvidence.RecordCallReturned((object)-1);
            simulationEvidence.Conclude();

            Assert.That(simulationEvidence.Completed, Is.True, string.Join(" | ", simulationEvidence.Refusals));
        }

        [Test]
        public void E3_ALogBesideTheTBD_IsNotTASsForThisRunAndIsLeftAlone()
        {
            // Not where TAS writes for a separate-output run, so it neither refuses the run nor gets deleted.
            File.WriteAllText(Path_TbdLog, "unrelated");

            SimulationEvidence simulationEvidence = new SimulationEvidence(SimulationOutputShape.SeparateOutputFile, path_TBD, path_TSD);
            simulationEvidence.Prepare(DateTime.UtcNow.AddSeconds(-1));
            File.WriteAllBytes(path_TSD, new byte[1034856]);
            simulationEvidence.RecordCallReturned((object)-1);
            simulationEvidence.Conclude();

            Assert.That(simulationEvidence.Completed, Is.True, string.Join(" | ", simulationEvidence.Refusals));
            Assert.That(File.Exists(Path_TbdLog), Is.True);
        }

        [Test]
        public void E4_InPlaceDocument_KeepsTheDocumentKeyedLog()
        {
            string path_TPD = Path.Combine(directory, "tbddir", "plant.tpd");
            File.WriteAllBytes(path_TPD, new byte[4096]);
            File.SetLastWriteTimeUtc(path_TPD, DateTime.UtcNow.AddHours(-1));
            string path_TpdLog = Path.Combine(directory, "tbddir", "plant_error_log.txt");

            SimulationEvidence ok = new SimulationEvidence(SimulationOutputShape.InPlaceDocument, path_TPD, null);
            Assert.That(ok.Path_ErrorLog, Is.EqualTo(path_TpdLog));

            ok.Prepare(DateTime.UtcNow.AddSeconds(-1));
            File.WriteAllBytes(path_TPD, new byte[8192]);
            ok.RecordCallReturned("Done");
            ok.Conclude();
            ok.RecordResultsReconciled(2, 0, 8759);
            Assert.That(ok.Completed, Is.True, string.Join(" | ", ok.Refusals));

            SimulationEvidence failed = new SimulationEvidence(SimulationOutputShape.InPlaceDocument, path_TPD, null);
            failed.Prepare(DateTime.UtcNow.AddSeconds(-1));
            File.WriteAllBytes(path_TPD, new byte[16384]);
            File.WriteAllText(path_TpdLog, "Simulation Failed");
            failed.RecordCallReturned("Done");
            failed.Conclude();
            failed.RecordResultsReconciled(2, 0, 8759);
            Assert.That(failed.Completed, Is.False, "an in-place run's log beside its document still refuses");
            Assert.That(failed.Refusals, Has.Some.StartWith("TAS wrote an error log for this run: " + path_TpdLog));
        }
    }
}
