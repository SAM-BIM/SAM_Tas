// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SAM.Analytical.Benchmark;
using SAM.Core;

namespace SAM.Analytical.Tas.Benchmark.Tests
{
    /// <summary>
    /// S01a, A9. When the TAS workflow refuses a run (it returns no model), the producer keeps the workflow's Error
    /// diagnostics - the TAS return code and error log - as B1a warnings, says so in one note instead of the
    /// missing-TSD note (a refused run leaves a stub TSD), and still writes a valid Failure document with exit 5.
    /// </summary>
    [TestFixture]
    public class WorkflowFailureTests
    {
        private const string Refusal = "TAS refused the simulation: TBDDocument.simulate returned 0. TAS return code: 0; TAS error log: C:\\run\\box_error_log.txt.";

        private static List<Diagnostic> RefusedRunDiagnostics()
        {
            return new List<Diagnostic>
            {
                new Diagnostic(DiagnosticSeverity.Info, DiagnosticDetail.Summary, "Workflow", "This run started from the canonical TBD.", "TAS-WF-002", 0),
                new Diagnostic(DiagnosticSeverity.Error, DiagnosticDetail.Summary, "Simulating Model", Refusal, "TAS-SIM-001", 1),
                new Diagnostic(DiagnosticSeverity.Error, DiagnosticDetail.Summary, "Simulating Model", "TAS wrote an error log for this run: C:\\run\\box_error_log.txt\r\nSimulation Failed", "TAS-SIM-001", 2),
                new Diagnostic(DiagnosticSeverity.Warning, DiagnosticDetail.Summary, "Workflow", "An issue.", "TAS-WF-003", 3),
            };
        }

        private static TasBenchmarkContext FailedContext()
        {
            TasBenchmarkContext context = BenchmarkFixture.GoldenContext();
            context.State = RunState.Failure;
            context.ModelResult = null;
            return context;
        }

        [Test]
        public void A9_AddWorkflowFailure_KeepsEveryErrorAsAWarning_WithItsCode()
        {
            TasBenchmarkContext context = FailedContext();

            Producer.AddWorkflowFailure(context, RefusedRunDiagnostics());

            Assert.Multiple(() =>
            {
                Assert.That(context.Warnings, Has.Count.EqualTo(2), "Errors only: the Info and Warning records are not failure messages");
                Assert.That(context.Warnings[0], Is.EqualTo("TAS-SIM-001: " + Refusal));
                Assert.That(context.Warnings[1], Does.StartWith("TAS-SIM-001: TAS wrote an error log for this run"));
                Assert.That(context.Notes, Has.Count.EqualTo(1));
                Assert.That(context.Notes[0], Does.Contain("refused the run"));
                Assert.That(context.Notes[0], Does.Not.Contain("No TSD was produced"), "a refused run leaves a stub TSD: the missing-TSD note would mislead");
            });
        }

        [Test]
        public void A9_AddWorkflowFailure_WithNoErrors_StillSaysWhatHappened()
        {
            TasBenchmarkContext context = FailedContext();

            Producer.AddWorkflowFailure(context, null);

            Assert.That(context.Warnings, Is.Empty);
            Assert.That(context.Notes, Is.EqualTo(new[] { "The TAS workflow returned no model and reported no error; measurements are unavailable." }));
        }

        [Test]
        public void A9_RefusedRun_IsAValidFailureDocument_WithExitCode5()
        {
            TasBenchmarkContext context = FailedContext();
            Producer.AddWorkflowFailure(context, RefusedRunDiagnostics());

            AnalyticalModel model = Producer.StripPreviousResults(BenchmarkFixture.SingleSpaceModel());
            BenchmarkDocument document = model.ToBenchmark(context);

            string outputPath = Path.Combine(Path.GetTempPath(), "SAM_S01a_Benchmark_" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                int exitCode = Producer.Emit(document, outputPath, runSucceeded: false, new StringWriter());

                Assert.Multiple(() =>
                {
                    Assert.That(exitCode, Is.EqualTo((int)BenchmarkExitCode.ProducerFailure));
                    Assert.That(document.Provenance.State, Is.EqualTo(RunState.Failure));
                    Assert.That(document.Provenance.Warnings, Has.Some.Contains("TBDDocument.simulate returned 0"));
                    Assert.That(document.Provenance.Warnings, Has.Some.Contains("box_error_log.txt"));
                    Assert.That(File.Exists(outputPath), Is.True, "a valid Failure document is still written");
                });
            }
            finally
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }
    }
}
