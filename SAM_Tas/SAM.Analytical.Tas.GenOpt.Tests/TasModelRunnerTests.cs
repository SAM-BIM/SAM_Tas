// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical.Tas.GenOpt.Tests.Helpers;
using SAM.Core.Optimisation;
using SAM.Math;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SAM.Analytical.Tas.GenOpt.Tests
{
    /// <summary>
    /// PR7b: a "tas-model" definition mapped onto the SAM.Math kernel, and run end to end through the stub TasGenExecute
    /// (a real child process; Script.txt is the generated script, the stub's behaviour comes from its environment).
    /// </summary>
    [TestFixture]
    public class TasModelRunnerTests
    {
        private const string StubSpecVariable = "SAM_TAS_GENOPT_STUB_SPEC";

        [Test]
        public void Choice_maps_to_try_every_option_over_its_options()
        {
            TasModelKernel kernel = TasModelFixtures.Read(TasScriptTests.Glazing).ToSAM_TasModelKernel();

            Assert.That(kernel.Optimiser, Is.TypeOf<TryEveryOption>());
            Assert.That(kernel.Optimiser.MaximumSimulations, Is.EqualTo(2000));
            OptimisationParameter parameter = kernel.Problem.Parameters.Single();
            Assert.That(new[] { parameter.Name, parameter.Initial.ToString(), parameter.Minimum.ToString(), parameter.Maximum.ToString(), parameter.Step.ToString() }, Is.EqualTo(new[] { "V1", "1", "1", "3", "1" }));
            Assert.That(kernel.Problem.OutputCount, Is.EqualTo(2));
            Assert.That(kernel.Objectives.Select(x => x.Delimiter), Is.EqualTo(new[] { "Y1::", "Y2::" }));
            Assert.That(kernel.VariableNames, Is.EqualTo(new[] { "Glazing" }));
            Assert.That(kernel.OutputNames, Is.EqualTo(new[] { "Overheating", "Cooling demand" }));
        }

        [Test]
        public void Golden_section_and_hooke_jeeves_map_as_the_tas_script_route()
        {
            TasModelKernel golden = TasModelFixtures.Read(TasScriptTests.Controller).ToSAM_TasModelKernel();
            Assert.That(golden.Optimiser, Is.TypeOf<GoldenSection>());
            Assert.That(((GoldenSection)golden.Optimiser).AbsoluteDifference, Is.EqualTo(0.1));
            OptimisationParameter setpoint = golden.Problem.Parameters.Single();
            Assert.That(new[] { setpoint.Initial, setpoint.Minimum, setpoint.Maximum, setpoint.Step }, Is.EqualTo(new double[] { 3, -5, 35, 1 }));

            // The same definition through PR4's tas-script mapping gives the same optimiser settings and parameter.
            OptimisationDefinition definition = TasModelFixtures.Read(TasScriptTests.Controller);
            definition.Model.Engine = "tas-script";
            foreach (DesignVariable designVariable in definition.Variables)
            {
                designVariable.Target = null;
            }

            foreach (OptimisationOutput output in definition.Outputs)
            {
                output.Measure = null;
            }

            using (TestFolder folder = new TestFolder())
            {
                GenOptDocument document = definition.ToGenOptDocument(folder.Path, "// script");
                NumberParameter parameter = document.CommandFile.Parameters.Cast<NumberParameter>().Single();
                Assert.That(new[] { parameter.Initial, parameter.Min, parameter.Max, parameter.Step }, Is.EqualTo(new[] { setpoint.Initial, setpoint.Minimum, setpoint.Maximum, setpoint.Step }));
                GoldenSection fromScript = (GoldenSection)document.CommandFile.Algorithm.ToSAM_Optimiser(document.CommandFile.OptimizationSettings, 1);
                Assert.That(fromScript.AbsoluteDifference, Is.EqualTo(((GoldenSection)golden.Optimiser).AbsoluteDifference));
                Assert.That(fromScript.MaximumSimulations, Is.EqualTo(golden.Optimiser.MaximumSimulations));
            }

            TasModelKernel hookeJeeves = TasModelFixtures.Read(TasScriptTests.Setpoints).ToSAM_TasModelKernel();
            Assert.That(hookeJeeves.Optimiser, Is.TypeOf<HookeJeeves>());
            Assert.That(hookeJeeves.Optimiser.MaximumSimulations, Is.EqualTo(50));
            Assert.That(hookeJeeves.Problem.Parameters.Select(x => x.Name), Is.EqualTo(new[] { "V1", "V2" }));
            Assert.That(hookeJeeves.VariableNames, Is.EqualTo(new[] { "Heating, office", "Cooling" }));
            Assert.That(hookeJeeves.OutputNames, Is.EqualTo(new[] { "Heating demand", "Cooling demand", "Overheating 25", "Overheating" }));
        }

        [Test]
        public void A_definition_that_cannot_run_throws_before_anything_runs()
        {
            OptimisationDefinition definition = TasModelFixtures.Read(TasModelFixtures.FixtureText("zone-setpoints-glazing-hooke-jeeves.json"));
            TasOptimisationDefinitionException exception = Assert.Throws<TasOptimisationDefinitionException>(() => definition.ToSAM_TasModelKernel());
            Assert.That(exception.Diagnostics.Any(x => x.Code == "OPT601"), Is.True);

            using (TestFolder folder = new TestFolder())
            {
                TasModelRunSettings settings = new TasModelRunSettings(folder.Path, folder.Combine("runs"), TestFolder.StubExecutable) { Inventory = TasModelFixtures.Demo() };
                string text = TasModelFixtures.FixtureText("systems-demo-bound-golden-section.json");
                TasOptimisationDefinitionException notInModel = Assert.Throws<TasOptimisationDefinitionException>(() => new TasModelRunner(TasModelFixtures.Read(text), settings));
                Assert.That(notInModel.Diagnostics.Single(x => x.Severity == DiagnosticSeverity.Error).Code, Is.EqualTo("OPT609"));
                Assert.That(Directory.Exists(folder.Combine("runs")), Is.False);
            }
        }

        [Test]
        public void Glazing_choice_runs_every_option_end_to_end()
        {
            using (TestFolder folder = new TestFolder())
            using (new StubSpec("{\"kind\":\"quadratic\",\"center\":[3],\"offset\":10,\"outputs\":[\"Y1\",\"Y2\"]}"))
            {
                string project = Project(folder, "000000_SAM_AnalyticalModel.tbd", "000000_SAM_AnalyticalModel.tsd");
                List<TasGlazingSystem> pool = TasModelFixtures.Pool(attach: true);
                pool.Add(TasModelFixtures.Attach(TasModelFixtures.System("Double D", "My glazing systems", 0.31, 1.3, 0.75, "aaaaaaaa-0000-0000-0000-0000000d0d0d")));
                TasModelRunSettings settings = new TasModelRunSettings(project, folder.Combine("runs"), TestFolder.StubExecutable) { Inventory = TasModelFixtures.SamModel(), GlazingPool = pool };

                string json = TasScriptTests.Glazing.Replace("[ \"Windows: SIM_EXT_GLZ -pane\", \"Double B\", \"Triple low-e\" ]", "[ \"Windows: SIM_EXT_GLZ -pane\", \"Triple low-e\", \"Double D\" ]");
                TasModelRunner runner = new TasModelRunner(TasModelFixtures.Read(json), settings);
                List<string> written = new List<string>();
                runner.GlazingWriter = (tbd, options) => written.AddRange(options.Select(x => Path.GetFileName(tbd) + " <- " + x.Text));

                Assert.That(runner.GlazingOptions["Glazing"].Select(x => x.Text), Is.EqualTo(new[] { TasModelFixtures.SamGlazing, "Triple low-e", "Double D" }));
                NativeGenOptRun run = runner.Run();

                Assert.That(written, Is.EqualTo(new[] { "000000_SAM_AnalyticalModel.tbd <- Triple low-e", "000000_SAM_AnalyticalModel.tbd <- Double D" }));
                Assert.That(run.Result.Outcome, Is.EqualTo(OptimisationOutcome.Success));
                List<OptimisationTraceEntry> options = run.Result.Entries.Where(x => x.Event == OptimisationEvent.OptionEvaluated).ToList();
                Assert.That(options.Select(x => x.Coordinates[0]), Is.EqualTo(new double[] { 1, 2, 3 }));
                Assert.That(options.Select(x => x.Simulation), Is.EqualTo(new[] { 1, 2, 3 }));
                Assert.That(options.Select(x => x.Objective), Is.EqualTo(new double[] { 14, 11, 10 }));
                Assert.That(options.Select(x => x.Outputs[1]), Is.EqualTo(new double[] { 1, 2, 3 }), "Y2 = the stub's coordinate sum");
                Assert.That(run.Result.Minimum.Coordinates[0], Is.EqualTo(3));
                Assert.That(new NativeGenOptOutcome(run, false).BestEntry.Coordinates[0], Is.EqualTo(3));
                Assert.That(run.ParameterNames, Is.EqualTo(new[] { "Glazing" }));
                Assert.That(run.ObjectiveNames, Is.EqualTo(new[] { "Overheating", "Cooling demand" }));
                Assert.That(System.IO.File.ReadAllText(Path.Combine(run.Workspace.ProjectDirectory, "Script.txt")), Is.EqualTo(runner.ScriptText));
                Assert.That(System.IO.File.ReadAllText(Path.Combine(run.Workspace.EvaluationsDirectory, "0002", "Variables.txt")), Is.EqualTo("V1,2,1,3,1,System.Double"));
                Assert.That(runner.ScriptText, Does.Contain("\"Windows: Triple low-e a5191c -pane\""));
            }
        }

        [Test]
        public void Controller_setpoint_runs_golden_section_end_to_end_and_test_one_simulation()
        {
            using (TestFolder folder = new TestFolder())
            using (new StubSpec("{\"kind\":\"quadratic\",\"center\":[4.5],\"offset\":7000,\"outputs\":[\"Y1\",\"Y2\",\"Y3\"]}"))
            {
                string project = Project(folder, "Systems Training.tbd", "Systems Training.tsd", "Systems Training.tpd");
                TasModelRunSettings settings = new TasModelRunSettings(project, folder.Combine("runs"), TestFolder.StubExecutable) { Inventory = TasModelFixtures.Demo() };
                TasModelRunner runner = new TasModelRunner(TasModelFixtures.Read(TasScriptTests.Controller), settings);
                runner.GlazingWriter = (tbd, options) => Assert.Fail("no glazing to write");

                TasModelTest test = runner.Test();
                Assert.That(test.Evaluation.Succeeded, Is.True, test.Evaluation.Message);
                Assert.That(test.Coordinates, Is.EqualTo(new double[] { 3 }));
                Assert.That(test.Evaluation.Outputs[0], Is.EqualTo(7002.25));
                Assert.That(System.IO.File.ReadAllText(Path.Combine(test.EvaluationDirectory, "Variables.txt")), Is.EqualTo("V1,3,-5,35,1,System.Double"));

                NativeGenOptRun run = runner.Run();
                NativeGenOptOutcome outcome = new NativeGenOptOutcome(run, false);
                Assert.That(outcome.Successful, Is.True);
                Assert.That(outcome.BestEntry.Coordinates[0], Is.EqualTo(4.5).Within(0.5));
                Assert.That(run.Result.Entries.Count, Is.GreaterThan(5));
            }
        }

        [Test]
        public void A_failing_evaluation_stops_the_choice_with_no_best_option()
        {
            using (TestFolder folder = new TestFolder())
            using (new StubSpec("{\"kind\":\"quadratic\",\"outputs\":[\"Y1\",\"Y2\"],\"modes\":{\"2\":\"errorFile\"}}"))
            {
                string project = Project(folder, "000000_SAM_AnalyticalModel.tbd", "000000_SAM_AnalyticalModel.tsd");
                List<TasGlazingSystem> pool = TasModelFixtures.Pool(attach: true);
                pool.Add(TasModelFixtures.Attach(TasModelFixtures.System("Double D", "My glazing systems", 0.31, 1.3, 0.75, "aaaaaaaa-0000-0000-0000-0000000d0d0d")));
                TasModelRunSettings settings = new TasModelRunSettings(project, folder.Combine("runs"), TestFolder.StubExecutable) { Inventory = TasModelFixtures.SamModel(), GlazingPool = pool };
                string json = TasScriptTests.Glazing.Replace("[ \"Windows: SIM_EXT_GLZ -pane\", \"Double B\", \"Triple low-e\" ]", "[ \"Windows: SIM_EXT_GLZ -pane\", \"Triple low-e\", \"Double D\" ]");
                TasModelRunner runner = new TasModelRunner(TasModelFixtures.Read(json), settings) { GlazingWriter = (tbd, options) => { } };

                NativeGenOptRun run = runner.Run();
                Assert.That(run.Result.Outcome, Is.EqualTo(OptimisationOutcome.EvaluationFailed));
                Assert.That(new NativeGenOptOutcome(run, false).BestEntry, Is.Null);
                Assert.That(Directory.Exists(Path.Combine(run.Workspace.EvaluationsDirectory, "0002-retry")), Is.True);
            }
        }

        /// <summary>A project folder with empty Tas files (the stub never opens them).</summary>
        private static string Project(TestFolder folder, params string[] files)
        {
            string project = folder.Folder("project");
            foreach (string file in files)
            {
                System.IO.File.WriteAllText(Path.Combine(project, file), string.Empty);
            }

            return project;
        }

        /// <summary>Sets the stub's spec for the generated (C#) Script.txt while the test runs.</summary>
        private sealed class StubSpec : IDisposable
        {
            public StubSpec(string json)
            {
                Environment.SetEnvironmentVariable(StubSpecVariable, json);
            }

            public void Dispose()
            {
                Environment.SetEnvironmentVariable(StubSpecVariable, null);
            }
        }
    }
}
