// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Microsoft.Win32;
using NUnit.Framework;
using SAM.Analytical.Tas.GenOpt.Tests.Helpers;
using SAM.Math;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SAM.Analytical.Tas.GenOpt.Tests
{
    /// <summary>
    /// End to end through real child processes: GenOptDocument.RunNative, then the SAM.Math kernel, then
    /// TasGenExecuteObjectiveEvaluator, then the stub TasGenExecute (Gate T protocol). COM-free and Tas-free.
    /// </summary>
    [TestFixture]
    public class NativeRunTests
    {
        /// <summary>A 2-D Hooke-Jeeves document (the e1-hj-quad-2d set-up) whose script is a stub spec.</summary>
        private static GenOptDocument Document(string workspace, string script, Algorithm algorithm = null, int maxIte = 2000, IEnumerable<string> outputs = null)
        {
            GenOptDocument document = new GenOptDocument(workspace);
            document.AddParameter(new NumberParameter { Name = "x1", Initial = 0.9, Min = -1, Max = 1, Step = 0.05 });
            document.AddParameter(new NumberParameter { Name = "x2", Initial = 0.9, Min = -1, Max = 1, Step = 0.05 });
            document.Algorithm = algorithm ?? new GPSHookeJeevesAlgorithm();
            document.OptimizationSettings.MaxIterations = maxIte;
            foreach (string output in outputs ?? new[] { "Result" })
            {
                document.AddObjective(output);
            }

            document.AddScript(script);
            return document;
        }

        private static string Quadratic(IDictionary<string, string> modes = null, IDictionary<string, int> sleepMs = null, IEnumerable<string> outputs = null, string format = "R")
            => TestFolder.StubScript(center: new[] { 0.1, -0.37 }, weight: new[] { 1.0, 2.0 }, modes: modes, sleepMs: sleepMs, outputs: outputs, format: format);

        private static NativeGenOptRun Run(TestFolder folder, GenOptDocument document, IProgress<OptimisationProgress> progress = null, CancellationToken cancellationToken = default)
            => document.RunNative(folder.Folder("runs"), TestFolder.StubExecutable, progress, cancellationToken);

        private static string[] EvaluationFolders(NativeGenOptRun run) => Directory.GetDirectories(run.Workspace.EvaluationsDirectory).Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal).ToArray();

        // ------------------------------------------------------------------ process contract

        [Test]
        public void EveryEvaluation_GetsAFreshFolder_TheProjectAsArgs0_AndItAsCwd()
        {
            using (TestFolder folder = new TestFolder())
            {
                NativeGenOptRun run = Run(folder, Document(folder.Folder("ws"), Quadratic(), maxIte: 6));

                string[] evaluations = EvaluationFolders(run);
                Assert.That(evaluations, Is.EqualTo(Enumerable.Range(1, run.Result.Simulations).Select(i => i.ToString("D4")).ToArray()));
                foreach (string name in evaluations)
                {
                    string directory = Path.Combine(run.Workspace.EvaluationsDirectory, name);
                    string[] invocation = System.IO.File.ReadAllLines(Path.Combine(directory, "stub-invocation.txt"));
                    Assert.That(invocation, Is.EqualTo(new[] { "argc=1", "arg=" + run.Workspace.ProjectDirectory, "cwd=" + directory }));
                    Assert.That(System.IO.File.Exists(Path.Combine(directory, "Variables.txt")), Is.True);
                    Assert.That(System.IO.File.Exists(Path.Combine(directory, "Output.txt")), Is.True);
                }

                Assert.That(Directory.GetFiles(run.Workspace.ProjectDirectory).Select(Path.GetFileName), Is.EqualTo(new[] { "Script.txt" }));
            }
        }

        [Test]
        public void VariablesTxt_HoldsTheKernelCoordinates()
        {
            using (TestFolder folder = new TestFolder())
            {
                NativeGenOptRun run = Run(folder, Document(folder.Folder("ws"), Quadratic(), maxIte: 3));

                OptimisationTraceEntry first = run.Result.Entries[0];
                string variables = System.IO.File.ReadAllText(Path.Combine(run.Workspace.EvaluationsDirectory, "0001", "Variables.txt"));
                Assert.That(first.Coordinates, Is.EqualTo(new[] { 0.9, 0.9 }));
                Assert.That(variables, Is.EqualTo("x1,0.9,-1,1,0.05,System.Double\nx2,0.9,-1,1,0.05,System.Double"));
            }
        }

        /// <summary>No Java, cmd.exe or Tas Manager registry: the stub is the only process, and HKCU\SOFTWARE\EDSL\TasManager is unchanged.</summary>
        [Test]
        public void NativeRun_DoesNotTouchTheTasManagerRegistry()
        {
            string Snapshot()
            {
                if (!OperatingSystem.IsWindows())
                {
                    return string.Empty;
                }

                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\EDSL\TasManager"))
                {
                    if (key == null)
                    {
                        return "<none>";
                    }

                    return string.Join("|", key.GetSubKeyNames().OrderBy(n => n).Select(n =>
                    {
                        using (RegistryKey sub = key.OpenSubKey(n))
                        {
                            return n + "=" + (sub?.GetValue("Path") as string);
                        }
                    }));
                }
            }

            using (TestFolder folder = new TestFolder())
            {
                string before = Snapshot();

                NativeGenOptRun run = Run(folder, Document(folder.Folder("ws"), Quadratic(), maxIte: 3));

                Assert.That(run.Result.Simulations, Is.EqualTo(3));
                Assert.That(Snapshot(), Is.EqualTo(before));
            }
        }

        // ------------------------------------------------------------------ failure classification through a real process

        /// <summary>A failure in the first evaluation stops the run without a retry (kernel rule), so each class is isolated.</summary>
        [TestCase("errorFile", "injected script exception")]
        [TestCase("compileError", "CS1002")]
        [TestCase("exitCode", "E0434352")]
        [TestCase("missingResult", "no 'Result::' line")]
        [TestCase("malformedResult", "not a number")]
        [TestCase("commaDecimal", "comma")]
        public void FailureClass_IsReportedAsAnEvaluationFailure(string mode, string message)
        {
            using (TestFolder folder = new TestFolder())
            {
                NativeGenOptRun run = Run(folder, Document(folder.Folder("ws"), Quadratic(new Dictionary<string, string> { ["1"] = mode })));

                Assert.That(run.Result.Outcome, Is.EqualTo(OptimisationOutcome.EvaluationFailed));
                Assert.That(run.Result.FailedSimulation, Is.EqualTo(1));
                Assert.That(run.Result.FailureMessage, Does.Contain(message));
                Assert.That(run.Result.Retries, Is.EqualTo(0));
                Assert.That(EvaluationFolders(run), Is.EqualTo(new[] { "0001" }));
            }
        }

        [Test]
        public void AppendedOutput_TheLastResultLineWins()
        {
            using (TestFolder folder = new TestFolder())
            {
                NativeGenOptRun reference = Run(folder, Document(folder.Folder("ws1"), Quadratic(), maxIte: 4));
                NativeGenOptRun appended = Run(folder, Document(folder.Folder("ws2"), Quadratic(new Dictionary<string, string> { ["1"] = "appendTwice", ["2"] = "appendTwice" }), maxIte: 4));

                Assert.That(appended.Result.Entries.Select(e => e.Objective), Is.EqualTo(reference.Result.Entries.Select(e => e.Objective)));
            }
        }

        [Test]
        public void MultipleOutputs_AreRecorded_OnlyTheFirstIsMinimised()
        {
            using (TestFolder folder = new TestFolder())
            {
                string[] outputs = { "Result", "Twice", "Thrice" };
                NativeGenOptRun single = Run(folder, Document(folder.Folder("ws1"), Quadratic(), maxIte: 40));
                NativeGenOptRun multiple = Run(folder, Document(folder.Folder("ws2"), Quadratic(outputs: outputs), outputs: outputs, maxIte: 40));

                Assert.That(multiple.ObjectiveNames, Is.EqualTo(outputs));
                Assert.That(multiple.Result.Entries.Select(e => e.Coordinates.ToArray()), Is.EqualTo(single.Result.Entries.Select(e => e.Coordinates.ToArray())));
                foreach (OptimisationTraceEntry entry in multiple.Result.Entries)
                {
                    double sum = entry.Coordinates[0] + entry.Coordinates[1];
                    Assert.That(entry.Outputs.Count, Is.EqualTo(3));
                    Assert.That(entry.Outputs[1], Is.EqualTo(1 * sum));
                    Assert.That(entry.Outputs[2], Is.EqualTo(2 * sum));
                }
            }
        }

        // ------------------------------------------------------------------ retry-once-then-stop (owned by the kernel)

        [Test]
        public void FailureOnce_IsRetriedOnce_InAFreshFolder_WithTheSameSimulationNumber()
        {
            using (TestFolder folder = new TestFolder())
            {
                NativeGenOptRun reference = Run(folder, Document(folder.Folder("ws1"), Quadratic(), maxIte: 8));
                NativeGenOptRun run = Run(folder, Document(folder.Folder("ws2"), Quadratic(new Dictionary<string, string> { ["4a1"] = "errorFile" }), maxIte: 8));

                Assert.That(run.Result.Retries, Is.EqualTo(1));
                Assert.That(EvaluationFolders(run), Does.Contain("0004").And.Contain("0004-retry"));
                Assert.That(System.IO.File.ReadAllText(Path.Combine(run.Workspace.EvaluationsDirectory, "0004", "Error.txt")), Does.Contain("injected"));
                Assert.That(System.IO.File.Exists(Path.Combine(run.Workspace.EvaluationsDirectory, "0004-retry", "Error.txt")), Is.False);
                Assert.That(run.Result.Entries.Select(e => e.Objective), Is.EqualTo(reference.Result.Entries.Select(e => e.Objective)));
                Assert.That(run.Result.Simulations, Is.EqualTo(reference.Result.Simulations));
            }
        }

        [Test]
        public void FailureTwice_StopsTheRun_NoFurtherEvaluation()
        {
            using (TestFolder folder = new TestFolder())
            {
                NativeGenOptRun run = Run(folder, Document(folder.Folder("ws"), Quadratic(new Dictionary<string, string> { ["4"] = "errorFile" })));

                Assert.That(run.Result.Outcome, Is.EqualTo(OptimisationOutcome.EvaluationFailed));
                Assert.That(run.Result.Retries, Is.EqualTo(1));
                Assert.That(run.Result.FailedSimulation, Is.EqualTo(4));
                Assert.That(run.Result.Minimum, Is.Null);
                Assert.That(EvaluationFolders(run), Is.EqualTo(new[] { "0001", "0002", "0003", "0004", "0004-retry" }));
            }
        }

        // ------------------------------------------------------------------ cooperative cancellation

        /// <summary>
        /// Cancelling while TasGenExecute runs does not kill it. The running evaluation finishes normally (it writes its
        /// Output.txt and the stub's completion marker), and no further evaluation starts.
        /// </summary>
        [Test]
        public void Cancellation_LetsTheRunningEvaluationFinish_AndStartsNoOther()
        {
            using (TestFolder folder = new TestFolder())
            using (CancellationTokenSource source = new CancellationTokenSource())
            {
                GenOptDocument document = Document(folder.Folder("ws"), Quadratic(sleepMs: new Dictionary<string, int> { ["2"] = 2500 }));
                string runs = folder.Folder("runs");

                Task<NativeGenOptRun> task = Task.Run(() => document.RunNative(runs, TestFolder.StubExecutable, null, source.Token));

                // Cancel only once evaluation 2's process is running (it records its invocation first, then sleeps).
                DateTime deadline = DateTime.UtcNow.AddSeconds(30);
                while (!Directory.EnumerateFiles(runs, "stub-invocation.txt", SearchOption.AllDirectories).Any(p => Path.GetFileName(Path.GetDirectoryName(p)) == "0002"))
                {
                    Assert.That(DateTime.UtcNow, Is.LessThan(deadline), "evaluation 2 never started");
                    Thread.Sleep(20);
                }

                string evaluation2 = Path.GetDirectoryName(Directory.EnumerateFiles(runs, "stub-invocation.txt", SearchOption.AllDirectories).First(p => Path.GetFileName(Path.GetDirectoryName(p)) == "0002"));
                Assert.That(System.IO.File.Exists(Path.Combine(evaluation2, "stub-finished.txt")), Is.False, "cancel must happen while the process runs");
                source.Cancel();

                NativeGenOptRun run = task.Result;

                Assert.That(run.Result.Outcome, Is.EqualTo(OptimisationOutcome.Cancelled));
                Assert.That(System.IO.File.Exists(Path.Combine(evaluation2, "stub-finished.txt")), Is.True, "the running evaluation was not allowed to finish");
                Assert.That(System.IO.File.ReadAllText(Path.Combine(evaluation2, "Output.txt")), Does.Contain("Result::"));
                Assert.That(EvaluationFolders(run), Is.EqualTo(new[] { "0001", "0002" }));
                // Kernel contract (PR2): simulation 3 is numbered before the cancellation check, so it is counted
                // although it never started (no 0003 folder, no process).
                Assert.That(run.Result.Simulations, Is.EqualTo(3));
                Assert.That(run.Result.Entries.Any(e => e.Simulation == 2), Is.True);
                Assert.That(run.Result.Minimum, Is.Null);
            }
        }

        [Test]
        public void Cancellation_BeforeTheRun_StartsNoEvaluation()
        {
            using (TestFolder folder = new TestFolder())
            using (CancellationTokenSource source = new CancellationTokenSource())
            {
                source.Cancel();

                NativeGenOptRun run = Run(folder, Document(folder.Folder("ws"), Quadratic()), null, source.Token);

                Assert.That(run.Result.Outcome, Is.EqualTo(OptimisationOutcome.Cancelled));
                Assert.That(EvaluationFolders(run), Is.Empty);
            }
        }

        // ------------------------------------------------------------------ progress, sequencing, refusals

        private sealed class ListProgress : IProgress<OptimisationProgress>
        {
            public List<OptimisationProgress> Items { get; } = new List<OptimisationProgress>();

            public void Report(OptimisationProgress value) => Items.Add(value);
        }

        [Test]
        public void Progress_IsForwardedFromTheKernel()
        {
            using (TestFolder folder = new TestFolder())
            {
                ListProgress progress = new ListProgress();

                NativeGenOptRun run = Run(folder, Document(folder.Folder("ws"), Quadratic(), maxIte: 10), progress);

                Assert.That(progress.Items.Count, Is.EqualTo(run.Result.Entries.Count + run.Result.MainIterations.Count));
            }
        }

        /// <summary>UnitsOfExecution never makes the native run parallel: the trace is identical for 0, 1 and 8.</summary>
        [Test]
        public void UnitsOfExecution_DoesNotChangeTheSequentialRun()
        {
            using (TestFolder folder = new TestFolder())
            {
                List<string> traces = new List<string>();
                foreach (int units in new[] { 0, 1, 8 })
                {
                    GenOptDocument document = new GenOptDocument(folder.Folder("ws" + units));
                    document.AddParameter(new NumberParameter { Name = "x1", Initial = 3, Min = -5, Max = 35, Step = 1 });
                    document.Algorithm = new GoldenSectionAlgorithm { AbsDiffFunction = 0.1 };
                    document.OptimizationSettings.UnitsOfExecution = units;
                    document.AddObjective("Result");
                    document.AddScript(TestFolder.StubScript(center: new[] { 10.0 }));
                    NativeGenOptRun run = Run(folder, document);
                    traces.Add(string.Join(";", run.Result.Entries.Select(e => e.Simulation + ":" + e.Coordinates[0].ToString("R"))));
                }

                Assert.That(traces.Distinct().Count(), Is.EqualTo(1));
            }
        }

        [Test]
        public void RefusedConfiguration_CreatesNothingAndStartsNoProcess()
        {
            using (TestFolder folder = new TestFolder())
            {
                string runs = folder.Folder("runs");
                GenOptDocument document = Document(folder.Folder("ws"), Quadratic(), algorithm: new GPSCoordinateSearchAlgorithm { MeshSizeDivider = 2, MeshSizeExponentIncrement = 1, NumberOfStepReduction = 4 });

                Assert.Throws<GenOptCompatibilityException>(() => document.RunNative(runs, TestFolder.StubExecutable));
                Assert.Throws<NotSupportedException>(() => Document(folder.Path, Quadratic(), algorithm: new NelderMeadONeillcsAlgorithm()).RunNative(runs, TestFolder.StubExecutable));
                Assert.Throws<FileNotFoundException>(() => Document(folder.Path, Quadratic()).RunNative(runs, folder.Combine("missing.exe")));
                Assert.That(Directory.GetFileSystemEntries(runs), Is.Empty);
            }
        }
    }
}
