// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Core.Tas;
using System;
using System.IO;

namespace SAM.Analytical.Tas.TM59.Tests
{
    /// <summary>
    /// What the string <c>ITPD.Simulate</c> returns is allowed to decide.
    /// <para>
    /// Four returns were measured on licensed TAS, each accompanying a run that produced nothing. But
    /// <b>no successful run has been observed through that route</b>, so what TAS returns on success is
    /// not established - it could be null, empty, or a status. Treating every non-empty answer as an
    /// error would be an assumption, and would turn every good run into a refusal if TAS ever says
    /// something on success.
    /// </para>
    /// <para>
    /// So this fixture pins the conservative contract: a <b>measured</b> failure refuses; anything else is
    /// preserved verbatim and decides nothing, leaving the complete <c>ZoneTemperature</c> reconciliation
    /// as the decisive gate.
    /// </para>
    /// </summary>
    [TestFixture]
    public class SimulationDiagnosticTests
    {
        [TestCase("Plant room has no components")]
        [TestCase("Plant Room Has Errors")]
        [TestCase("Failed to open the TSD file")]
        [TestCase("Sizing Flow Failed")]
        public void MeasuredFailures_AreClassifiedAsFailures(string returned)
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    SimulationDiagnostic.Classify(returned),
                    Is.EqualTo(SimulationDiagnosticKind.KnownFailure),
                    "Every one of these was observed on licensed TAS accompanying a run that produced nothing.");

                Assert.That(SimulationDiagnostic.IsFailure(returned), Is.True);
            });
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void NoAnswer_IsSilentAndNotAFailure(string returned)
        {
            Assert.Multiple(() =>
            {
                Assert.That(SimulationDiagnostic.Classify(returned), Is.EqualTo(SimulationDiagnosticKind.Silent));
                Assert.That(SimulationDiagnostic.IsFailure(returned), Is.False);
            });
        }

        [TestCase("Success")]
        [TestCase("Simulation complete")]
        [TestCase("8760 hours simulated")]
        public void UnrecognisedAnswer_IsNotTreatedAsAFailure(string returned)
        {
            // This is the whole point. If TAS answers with a status on success, a non-empty test would
            // refuse every good run. Until a successful return is actually measured, an unrecognised
            // answer must decide nothing.
            Assert.Multiple(() =>
            {
                Assert.That(
                    SimulationDiagnostic.Classify(returned),
                    Is.EqualTo(SimulationDiagnosticKind.Unrecognised));

                Assert.That(
                    SimulationDiagnostic.IsFailure(returned),
                    Is.False,
                    "An unmeasured answer must not be assumed to be an error.");
            });
        }

        [Test]
        public void Classification_IsCaseInsensitive()
        {
            // TAS's own casing varies between diagnostics - "Plant room has no components" against
            // "Plant Room Has Errors" - so matching must not depend on it.
            Assert.That(SimulationDiagnostic.IsFailure("PLANT ROOM HAS ERRORS"), Is.True);
            Assert.That(SimulationDiagnostic.IsFailure("plant room has no components"), Is.True);
        }

        // ---------------------------------------------------------------- evidence integration

        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "SAM_PR2_Diag", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
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
            }
        }

        private SimulationEvidence InPlaceRun()
        {
            string path_TPD = Path.Combine(directory, "m.tpd");
            File.WriteAllBytes(path_TPD, new byte[4096]);

            SimulationEvidence simulationEvidence = new SimulationEvidence(
                SimulationOutputShape.InPlaceDocument, path_TPD, null);

            simulationEvidence.Prepare(DateTime.UtcNow.AddSeconds(-1));
            File.WriteAllBytes(path_TPD, new byte[8192]);

            return simulationEvidence;
        }

        [Test]
        public void Evidence_PreservesTheRawStringWhateverItSays()
        {
            SimulationEvidence simulationEvidence = InPlaceRun();

            simulationEvidence.RecordCallReturned("Sizing Flow Failed");

            Assert.That(
                simulationEvidence.NativeDiagnostic,
                Is.EqualTo("Sizing Flow Failed"),
                "The raw native string is evidence and must be kept verbatim.");
        }

        [Test]
        public void Evidence_MeasuredFailure_Refuses()
        {
            SimulationEvidence simulationEvidence = InPlaceRun();

            bool mayProceed = simulationEvidence.RecordCallReturned("Plant Room Has Errors");

            Assert.Multiple(() =>
            {
                Assert.That(mayProceed, Is.False);
                Assert.That(simulationEvidence.Completed, Is.False);
                Assert.That(simulationEvidence.Refusals, Has.Some.Contains("Plant Room Has Errors"));
            });
        }

        [Test]
        public void Evidence_UnrecognisedAnswer_StillNeedsTheResultsToReconcile()
        {
            SimulationEvidence simulationEvidence = InPlaceRun();

            bool mayProceed = simulationEvidence.RecordCallReturned("Simulation complete");
            simulationEvidence.Conclude();

            Assert.Multiple(() =>
            {
                Assert.That(mayProceed, Is.True, "An unmeasured answer does not refuse...");
                Assert.That(
                    simulationEvidence.Completed,
                    Is.False,
                    "...but it does not pass the run either: the reconciliation is the decisive gate.");
                Assert.That(simulationEvidence.NativeDiagnosticKind, Is.EqualTo(SimulationDiagnosticKind.Unrecognised));
                Assert.That(simulationEvidence.Notes, Has.Some.Contains("neither success nor failure"));
            });

            simulationEvidence.RecordResultsReconciled(2, 0, 23);

            Assert.That(
                simulationEvidence.Completed,
                Is.True,
                "Once the results reconcile, the run is evidenced.");
        }

        [Test]
        public void Evidence_SilentAnswer_StillNeedsTheResultsToReconcile()
        {
            SimulationEvidence simulationEvidence = InPlaceRun();

            simulationEvidence.RecordCallReturned((string)null);
            simulationEvidence.Conclude();

            Assert.That(
                simulationEvidence.Completed,
                Is.False,
                "Silence is not success either - a saved TPD proves nothing.");

            simulationEvidence.RecordResultsReconciled(2, 0, 23);

            Assert.That(simulationEvidence.Completed, Is.True);
        }
    }
}
