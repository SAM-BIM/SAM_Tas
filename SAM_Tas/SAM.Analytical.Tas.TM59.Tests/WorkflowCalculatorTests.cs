// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;

namespace SAM.Analytical.Tas.TM59.Tests
{
    /// <summary>
    /// <b>Each <see cref="WorkflowCalculator.Calculate"/> call owns a fresh <see cref="WorkflowCalculator.Notes"/>
    /// state.</b>
    /// <para>
    /// A calculator instance can be re-run. The notes list is cleared at the very top of
    /// <see cref="WorkflowCalculator.Calculate"/>, ahead of the validation gate that rejects a null model or
    /// null settings: previously the clear sat behind that gate, so a rejected run returned null with the
    /// PREVIOUS run's notes still visible - a caller reading <see cref="WorkflowCalculator.Notes"/> after a
    /// refused run would have reported notes that belonged to an earlier calculation.
    /// </para>
    /// <para>
    /// No TAS COM: the invalid-input paths under test return before anything touches a TBD type, and the
    /// prior run's notes are simulated by writing the private list directly - the only way to populate it
    /// without a licensed TAS.
    /// </para>
    /// </summary>
    [TestFixture]
    public class WorkflowCalculatorTests
    {
        /// <summary>Stands in for a completed previous run: one leftover note in the private list.</summary>
        private static void SimulatePreviousRun(WorkflowCalculator workflowCalculator)
        {
            FieldInfo fieldInfo = typeof(WorkflowCalculator).GetField("notes", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(fieldInfo, Is.Not.Null, "the private notes list this test fills in place of a real previous run");

            ((List<string>)fieldInfo.GetValue(workflowCalculator)).Add("A note left over from a previous run.");
        }

        [Test]
        public void Calculate_NullModel_AfterAPreviousRun_ExposesNoStaleNotes()
        {
            WorkflowCalculator workflowCalculator = new WorkflowCalculator();
            SimulatePreviousRun(workflowCalculator);
            Assert.That(workflowCalculator.Notes, Has.Count.EqualTo(1), "control: the previous run's note is visible before the rejected run");

            Assert.That(workflowCalculator.Calculate(null), Is.Null);

            Assert.That(workflowCalculator.Notes, Is.Empty, "a rejected run must not expose the previous run's notes");
        }

        [Test]
        public void Calculate_NullSettings_AfterAPreviousRun_ExposesNoStaleNotes()
        {
            WorkflowCalculator workflowCalculator = new WorkflowCalculator();
            SimulatePreviousRun(workflowCalculator);
            Assert.That(workflowCalculator.Notes, Has.Count.EqualTo(1), "control: the previous run's note is visible before the rejected run");

            AnalyticalModel analyticalModel = new AnalyticalModel("Rejected Run", null, null, null, new AdjacencyCluster());
            Assert.That(workflowCalculator.Calculate(analyticalModel), Is.Null, "no WorkflowSettings, so the run is rejected");

            Assert.That(workflowCalculator.Notes, Is.Empty, "a rejected run must not expose the previous run's notes");
        }

        [Test]
        public void Calculate_RejectedOnAFreshInstance_StillExposesNoNotes()
        {
            WorkflowCalculator workflowCalculator = new WorkflowCalculator();

            Assert.That(workflowCalculator.Calculate(null), Is.Null);
            Assert.That(workflowCalculator.Notes, Is.Empty);
        }

        // ------------------------------------------------------------------ S01a: a refused simulation

        private static Core.Tas.SimulationEvidence RefusedEvidence(string directory)
        {
            string path_TBD = System.IO.Path.Combine(directory, "run.tbd");
            System.IO.File.WriteAllBytes(path_TBD, new byte[4096]);
            string path_TSD = System.IO.Path.Combine(directory, "run.tsd");

            Core.Tas.SimulationEvidence simulationEvidence = new Core.Tas.SimulationEvidence(Core.Tas.SimulationOutputShape.SeparateOutputFile, path_TBD, path_TSD);
            simulationEvidence.Prepare(System.DateTime.UtcNow.AddSeconds(-1));
            System.IO.File.WriteAllBytes(path_TSD, new byte[22]);
            System.IO.File.WriteAllText(Core.Tas.SimulationEvidence.ErrorLogPath(path_TBD), "Simulation Failed");
            simulationEvidence.RecordCallReturned((object)0);
            simulationEvidence.Conclude();
            return simulationEvidence;
        }

        [Test]
        public void A8_ARefusedSimulation_ReachesNotesAndDiagnostics_AsTASSIM001Errors()
        {
            string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_S01a_Workflow", System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            try
            {
                Core.Tas.SimulationEvidence simulationEvidence = RefusedEvidence(directory);
                Assert.That(simulationEvidence.Completed, Is.False, "fixture: a refused run");

                WorkflowCalculator workflowCalculator = new WorkflowCalculator();
                MethodInfo methodInfo = typeof(WorkflowCalculator).GetMethod("AddSimulationRefusals", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(methodInfo, Is.Not.Null, "the one place a refused simulation is written to the workflow's diagnostics");
                methodInfo.Invoke(workflowCalculator, new object[] { simulationEvidence });

                List<Core.Diagnostic> diagnostics = new List<Core.Diagnostic>(workflowCalculator.GetDiagnostics(Core.DiagnosticDetail.Summary));
                string path_ErrorLog = Core.Tas.SimulationEvidence.ErrorLogPath(System.IO.Path.Combine(directory, "run.tbd"));

                Assert.Multiple(() =>
                {
                    Assert.That(diagnostics, Has.Count.EqualTo(simulationEvidence.Refusals.Count), "one Error per refusal");
                    Assert.That(diagnostics.ConvertAll(x => x.Severity), Is.All.EqualTo(Core.DiagnosticSeverity.Error));
                    Assert.That(diagnostics.ConvertAll(x => x.Code), Is.All.EqualTo(Core.Tas.TasDiagnosticCodes.SimulationRefusal));
                    Assert.That(diagnostics.ConvertAll(x => x.Message), Has.Some.Contains("TAS return code: 0"));
                    Assert.That(diagnostics.ConvertAll(x => x.Message), Has.Some.Contains(path_ErrorLog));
                    Assert.That(workflowCalculator.Notes, Is.EqualTo(simulationEvidence.Refusals), "the legacy text is the refusals verbatim");
                });
            }
            finally
            {
                try { System.IO.Directory.Delete(directory, true); } catch { }
            }
        }

        [Test]
        public void Calculate_ResetsTheLastRunsSimulationEvidence()
        {
            WorkflowCalculator workflowCalculator = new WorkflowCalculator();
            PropertyInfo propertyInfo = typeof(WorkflowCalculator).GetProperty("SimulationEvidence");
            Assert.That(propertyInfo, Is.Not.Null);

            propertyInfo.SetValue(workflowCalculator, new Core.Tas.SimulationEvidence(Core.Tas.SimulationOutputShape.SeparateOutputFile, "a.tbd", "a.tsd"));
            Assert.That(workflowCalculator.SimulationEvidence, Is.Not.Null, "control: a previous run's evidence");

            Assert.That(workflowCalculator.Calculate(null), Is.Null);
            Assert.That(workflowCalculator.SimulationEvidence, Is.Null, "a run that did not simulate must not expose a previous run's evidence");
        }
    }
}
