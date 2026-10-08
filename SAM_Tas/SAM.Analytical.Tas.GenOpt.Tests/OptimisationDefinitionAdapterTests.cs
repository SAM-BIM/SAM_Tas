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
    /// PR4: the SAM.Core.Optimisation definition (schema "sam.optimisation/1") run on Tas through the accepted native
    /// route. Parity anchor: for both Systems Demo examples, the document built from the definition equals the one the
    /// SAM_UI form built in PR5 (same GenOpt objects, same objective order) and runs the same evaluations with the same
    /// Variables.txt bytes.
    /// </summary>
    [TestFixture]
    public class OptimisationDefinitionAdapterTests
    {
        private const string GoldenSection = "systems-demo-golden-section.json";
        private const string HookeJeeves = "systems-demo-hooke-jeeves.json";

        /// <summary>The SAM.Core.Optimisation fixtures, read from the sibling SAM checkout (as the golden traces are).</summary>
        private static string Directory_Fixtures => Path.Combine(Path.GetDirectoryName(GoldenTrace.Directory_Golden), "Optimisation");

        private static OptimisationDefinition Fixture(string fileName)
        {
            OptimisationDefinition result = global::SAM.Core.Optimisation.Create.OptimisationDefinition(System.IO.File.ReadAllText(Path.Combine(Directory_Fixtures, fileName)), out List<OptimisationDiagnostic> diagnostics);
            Assert.That(result, Is.Not.Null, string.Join("\n", diagnostics));
            return result;
        }

        /// <summary>
        /// The document the SAM_UI form built for an example in PR5 (TasOptimisationInput.Load, TryGetDefinition,
        /// TasOptimisationDefinition.ToGenOptDocument): SAM_Tas default algorithm and settings objects with the example's
        /// values, the script, then Result, Cost, CO2 (objective first), then the Setpoint parameter.
        /// </summary>
        private static GenOptDocument FormDocument(string fileName, string directory, string scriptText)
        {
            Algorithm algorithm;
            NumberParameter numberParameter;
            if (fileName == HookeJeeves)
            {
                algorithm = new GPSHookeJeevesAlgorithm();
                numberParameter = new NumberParameter() { Name = "Setpoint", Initial = 10, Min = -5, Max = 35, Step = 2 };
            }
            else
            {
                algorithm = new GoldenSectionAlgorithm() { AbsDiffFunction = 0.1 };
                numberParameter = new NumberParameter() { Name = "Setpoint", Initial = 3, Min = -5, Max = 35, Step = 1 };
            }

            GenOptDocument result = new GenOptDocument(directory)
            {
                Algorithm = algorithm,
                OptimizationSettings = new OptimizationSettings() { MaxIterations = 2000, MaxEqualResults = 100 },
            };

            result.AddScript(scriptText);
            foreach (string name in new[] { "Result", "Cost", "CO2" })
            {
                result.AddObjective(new Objective(name));
            }

            result.AddParameter(numberParameter);
            return result;
        }

        private static List<NumberParameter> Parameters(GenOptDocument genOptDocument) => genOptDocument.CommandFile.Parameters.Cast<NumberParameter>().ToList();

        private static List<string> ObjectiveNames(GenOptDocument genOptDocument) => Convert.Objectives(ObjectiveFunctionLocation(genOptDocument)).ConvertAll(x => x.Name);

        private static ObjectiveFunctionLocation ObjectiveFunctionLocation(GenOptDocument genOptDocument)
        {
            // The document keeps its ObjectiveFunctionLocation private; the GenOpt-format OutputFile reads the same one.
            System.Reflection.FieldInfo fieldInfo = typeof(GenOptDocument).GetField("objectiveFunctionLocation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return (ObjectiveFunctionLocation)fieldInfo.GetValue(genOptDocument);
        }

        private static long Bits(double value) => BitConverter.DoubleToInt64Bits(value);

        private static void AssertSameParameters(GenOptDocument expected, GenOptDocument actual)
        {
            List<NumberParameter> parameters_Expected = Parameters(expected);
            List<NumberParameter> parameters_Actual = Parameters(actual);
            Assert.That(parameters_Actual.Count, Is.EqualTo(parameters_Expected.Count));
            for (int i = 0; i < parameters_Expected.Count; i++)
            {
                Assert.That(parameters_Actual[i].Name, Is.EqualTo(parameters_Expected[i].Name));
                Assert.That(Bits(parameters_Actual[i].Initial), Is.EqualTo(Bits(parameters_Expected[i].Initial)), "Initial");
                Assert.That(Bits(parameters_Actual[i].Min), Is.EqualTo(Bits(parameters_Expected[i].Min)), "Min");
                Assert.That(Bits(parameters_Actual[i].Max), Is.EqualTo(Bits(parameters_Expected[i].Max)), "Max");
                Assert.That(Bits(parameters_Actual[i].Step), Is.EqualTo(Bits(parameters_Expected[i].Step)), "Step");
            }
        }

        private static OptimisationDefinition Changed(string fileName, Action<OptimisationDefinition> change)
        {
            OptimisationDefinition result = Fixture(fileName);
            change(result);
            return result;
        }

        private static TasOptimisationDefinitionException Refused(OptimisationDefinition optimisationDefinition, string code)
        {
            TasOptimisationDefinitionException exception = Assert.Throws<TasOptimisationDefinitionException>(() => optimisationDefinition.ToGenOptDocument(@"C:\no-such-folder", "script"));
            Assert.That(exception.Diagnostics.Select(x => x.Code), Does.Contain(code));
            Assert.That(exception.Message, Does.StartWith("The optimisation definition cannot run with the Tas engine: "));
            return exception;
        }

        // ------------------------------------------------------------------ capabilities

        [Test]
        public void Capabilities_AreWhatTheNativeRouteRuns()
        {
            IOptimisationCapabilities capabilities = Query.TasOptimisationCapabilities();

            Assert.That(capabilities.Engine, Is.EqualTo("tas-script"));
            Assert.That(capabilities.Engine, Is.EqualTo(Query.TasOptimisationEngine));
            Assert.That(capabilities.Algorithms.Select(x => (x.Algorithm, x.MinimumVariables, x.MaximumVariables)), Is.EqualTo(new (OptimisationAlgorithm, int, int?)[]
            {
                (OptimisationAlgorithm.GoldenSection, 1, 1),
                (OptimisationAlgorithm.HookeJeeves, 1, null),
            }));
            Assert.That(capabilities.Senses, Is.EqualTo(new[] { ObjectiveSense.Minimise }));
            Assert.That(capabilities.VariableTypes, Is.EqualTo(new[] { DesignVariableType.Continuous }));
            Assert.That(capabilities.SupportsConstraints, Is.False);
            Assert.That(Query.TasOptimisationCapabilities(), Is.SameAs(capabilities));
        }

        [TestCase(GoldenSection)]
        [TestCase(HookeJeeves)]
        public void Fixtures_AreRunnableWithTas(string fileName)
        {
            List<OptimisationDiagnostic> diagnostics = Fixture(fileName).Diagnostics(Query.TasOptimisationCapabilities());

            Assert.That(diagnostics.IsRunnable(), Is.True, string.Join("\n", diagnostics));
        }

        // ------------------------------------------------------------------ parity with the PR5 form path

        [TestCase(GoldenSection)]
        [TestCase(HookeJeeves)]
        public void Document_EqualsTheFormDocument(string fileName)
        {
            GenOptDocument expected = FormDocument(fileName, @"C:\ws", "script text");
            GenOptDocument actual = Fixture(fileName).ToGenOptDocument(@"C:\ws", "script text");

            Assert.That(actual.Directory, Is.EqualTo(expected.Directory));
            Assert.That(actual.Algorithm.GetType(), Is.EqualTo(expected.Algorithm.GetType()));
            Assert.That(actual.Algorithm.Text, Is.EqualTo(expected.Algorithm.Text));
            Assert.That(actual.OptimizationSettings.Text, Is.EqualTo(expected.OptimizationSettings.Text));
            Assert.That(actual.OptimizationSettings.MaxEqualResults, Is.EqualTo(new OptimizationSettings().MaxEqualResults));
            Assert.That(actual.CommandFile.Text, Is.EqualTo(expected.CommandFile.Text));
            Assert.That(actual.OutputFile.Text, Is.EqualTo(expected.OutputFile.Text));
            Assert.That(actual.ParameterFile.Text, Is.EqualTo(expected.ParameterFile.Text));
            Assert.That(actual.ScriptFile.Text, Is.EqualTo(expected.ScriptFile.Text));
            Assert.That(ObjectiveNames(actual), Is.EqualTo(new[] { "Result", "Cost", "CO2" }));
            AssertSameParameters(expected, actual);
        }

        [TestCase(GoldenSection)]
        [TestCase(HookeJeeves)]
        public void Run_EqualsTheFormRun_EvaluationForEvaluation(string fileName)
        {
            // Setpoint optimum near 4.97 (the licensed golden-section optimum); Cost and CO2 are recorded.
            string script = TestFolder.StubScript(center: new[] { 4.968943799848584 }, weight: new[] { 1.0 }, outputs: new[] { "Result", "Cost", "CO2" });

            using (TestFolder folder = new TestFolder())
            {
                string workspace = folder.Folder("ws");
                NativeGenOptRun run_Expected = FormDocument(fileName, workspace, script).RunNative(folder.Folder("runs-form"), TestFolder.StubExecutable);
                NativeGenOptRun run_Actual = Fixture(fileName).ToGenOptDocument(workspace, script).RunNative(folder.Folder("runs-definition"), TestFolder.StubExecutable);

                Assert.That(run_Actual.Result.Outcome, Is.EqualTo(run_Expected.Result.Outcome));
                Assert.That(run_Actual.Result.Simulations, Is.EqualTo(run_Expected.Result.Simulations));
                Assert.That(run_Actual.Result.Simulations, Is.GreaterThan(3));
                Assert.That(run_Actual.ParameterNames, Is.EqualTo(run_Expected.ParameterNames));
                Assert.That(run_Actual.ObjectiveNames, Is.EqualTo(new[] { "Result", "Cost", "CO2" }));
                Assert.That(run_Actual.ObjectiveNames, Is.EqualTo(run_Expected.ObjectiveNames));

                Assert.That(run_Actual.Result.Entries.Count, Is.EqualTo(run_Expected.Result.Entries.Count));
                for (int i = 0; i < run_Expected.Result.Entries.Count; i++)
                {
                    OptimisationTraceEntry expected = run_Expected.Result.Entries[i];
                    OptimisationTraceEntry actual = run_Actual.Result.Entries[i];
                    Assert.That((actual.Simulation, actual.MainIteration, actual.SubIteration, actual.Event), Is.EqualTo((expected.Simulation, expected.MainIteration, expected.SubIteration, expected.Event)), "entry " + i);
                    Assert.That(actual.Coordinates.Select(Bits), Is.EqualTo(expected.Coordinates.Select(Bits)), "entry " + i);
                    Assert.That(actual.Outputs.Select(Bits), Is.EqualTo(expected.Outputs.Select(Bits)), "entry " + i);
                }

                // Hooke-Jeeves reports a best point; golden section reports its final interval instead.
                Assert.That(run_Actual.Result.Minimum?.Coordinates.Select(Bits), Is.EqualTo(run_Expected.Result.Minimum?.Coordinates.Select(Bits)));
                Assert.That(run_Actual.Result.Interval == null, Is.EqualTo(run_Expected.Result.Interval == null));
                if (run_Expected.Result.Interval != null)
                {
                    Assert.That((Bits(run_Actual.Result.Interval.Lower), Bits(run_Actual.Result.Interval.Upper)), Is.EqualTo((Bits(run_Expected.Result.Interval.Lower), Bits(run_Expected.Result.Interval.Upper))));
                }

                string[] folders_Expected = Directory.GetDirectories(run_Expected.Workspace.EvaluationsDirectory).Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal).ToArray();
                string[] folders_Actual = Directory.GetDirectories(run_Actual.Workspace.EvaluationsDirectory).Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal).ToArray();
                Assert.That(folders_Actual, Is.EqualTo(folders_Expected));
                foreach (string name in folders_Expected)
                {
                    byte[] bytes_Expected = System.IO.File.ReadAllBytes(Path.Combine(run_Expected.Workspace.EvaluationsDirectory, name, TasGenExecuteObjectiveEvaluator.VariablesFileName));
                    byte[] bytes_Actual = System.IO.File.ReadAllBytes(Path.Combine(run_Actual.Workspace.EvaluationsDirectory, name, TasGenExecuteObjectiveEvaluator.VariablesFileName));
                    Assert.That(bytes_Actual, Is.EqualTo(bytes_Expected), "Variables.txt of evaluation " + name);
                }

                Assert.That(System.IO.File.ReadAllBytes(Path.Combine(run_Actual.Workspace.ProjectDirectory, "Script.txt")), Is.EqualTo(System.IO.File.ReadAllBytes(Path.Combine(run_Expected.Workspace.ProjectDirectory, "Script.txt"))));
            }
        }

        // ------------------------------------------------------------------ mapping rules

        [Test]
        public void Objective_GoesFirst_RecordedOutputsKeepTheirOrder()
        {
            OptimisationDefinition optimisationDefinition = Changed(GoldenSection, x => x.Objective.Output = "CO2");

            Assert.That(ObjectiveNames(optimisationDefinition.ToGenOptDocument(@"C:\ws", "script")), Is.EqualTo(new[] { "CO2", "Result", "Cost" }));
        }

        [Test]
        public void GoldenSection_StartAndStep_PassThrough_WhenGiven()
        {
            OptimisationDefinition optimisationDefinition = Changed(GoldenSection, x => { x.Variables[0].Start = 4.968943799848584; x.Variables[0].Step = 0.1; });

            NumberParameter numberParameter = Parameters(optimisationDefinition.ToGenOptDocument(@"C:\ws", "script")).Single();
            Assert.That(Bits(numberParameter.Initial), Is.EqualTo(Bits(4.968943799848584)));
            Assert.That(Bits(numberParameter.Step), Is.EqualTo(Bits(0.1)));
        }

        [Test]
        public void GoldenSection_WithoutStartAndStep_UsesTheMinimumAndZero_AsTheFormDid()
        {
            OptimisationDefinition optimisationDefinition = Changed(GoldenSection, x => { x.Variables[0].Start = null; x.Variables[0].Step = null; });

            NumberParameter numberParameter = Parameters(optimisationDefinition.ToGenOptDocument(@"C:\ws", "script")).Single();
            Assert.That(numberParameter.Initial, Is.EqualTo(-5));
            Assert.That(Bits(numberParameter.Step), Is.EqualTo(Bits(0.0)));
        }

        [Test]
        public void GoldenSection_Tolerance_IsAbsDiffFunction_AndDefaultsToSAMTas()
        {
            GoldenSectionAlgorithm given = (GoldenSectionAlgorithm)Changed(GoldenSection, x => ((GoldenSectionMethod)x.Method).Tolerance = 0.025).ToGenOptDocument(@"C:\ws", "s").Algorithm;
            GoldenSectionAlgorithm omitted = (GoldenSectionAlgorithm)Changed(GoldenSection, x => ((GoldenSectionMethod)x.Method).Tolerance = null).ToGenOptDocument(@"C:\ws", "s").Algorithm;

            Assert.That(given.AbsDiffFunction, Is.EqualTo(0.025));
            Assert.That(omitted.AbsDiffFunction, Is.EqualTo(new GoldenSectionAlgorithm().AbsDiffFunction));
        }

        [Test]
        public void HookeJeeves_Settings_MapToTheMeshKeywords()
        {
            OptimisationDefinition optimisationDefinition = Changed(HookeJeeves, x => x.Method = new HookeJeevesMethod { StepReductionFactor = 3, InitialStepExponent = 1, StepExponentIncrement = 2, StepReductions = 5 });

            GPSHookeJeevesAlgorithm algorithm = (GPSHookeJeevesAlgorithm)optimisationDefinition.ToGenOptDocument(@"C:\ws", "s").Algorithm;
            Assert.That((algorithm.MeshSizeDivider, algorithm.InitialMeshSizeExponent, algorithm.MeshSizeExponentIncrement, algorithm.NumberOfStepReduction), Is.EqualTo((3.0, 1.0, 2.0, 5.0)));
        }

        [Test]
        public void HookeJeeves_OmittedSettings_KeepTheSAMTasDefaults()
        {
            OptimisationDefinition optimisationDefinition = Changed(HookeJeeves, x => x.Method = new HookeJeevesMethod());

            GPSHookeJeevesAlgorithm algorithm = (GPSHookeJeevesAlgorithm)optimisationDefinition.ToGenOptDocument(@"C:\ws", "s").Algorithm;
            Assert.That(algorithm.Text, Is.EqualTo(new GPSHookeJeevesAlgorithm().Text));
        }

        [Test]
        public void MaximumSimulations_IsMaxIte_AndOtherSettingsKeepTheirDefaults()
        {
            OptimizationSettings given = Changed(GoldenSection, x => x.Stopping = new StoppingCriteria(25)).ToGenOptDocument(@"C:\ws", "s").OptimizationSettings;
            OptimizationSettings omitted = Changed(GoldenSection, x => x.Stopping = null).ToGenOptDocument(@"C:\ws", "s").OptimizationSettings;
            OptimizationSettings defaults = new OptimizationSettings();

            Assert.That(given.MaxIterations, Is.EqualTo(25));
            Assert.That((given.MaxEqualResults, given.WriteStepNumber, given.UnitsOfExecution), Is.EqualTo((defaults.MaxEqualResults, defaults.WriteStepNumber, defaults.UnitsOfExecution)));
            Assert.That(omitted.Text, Is.EqualTo(defaults.Text));
        }

        [Test]
        public void Definition_IsNotChanged_AndDocumentsAreIndependent()
        {
            OptimisationDefinition optimisationDefinition = Fixture(HookeJeeves);
            string json = optimisationDefinition.ToJson();

            GenOptDocument first = optimisationDefinition.ToGenOptDocument(@"C:\ws", "s");
            Parameters(first)[0].Min = 99;
            GenOptDocument second = optimisationDefinition.ToGenOptDocument(@"C:\ws", "s");

            Assert.That(optimisationDefinition.ToJson(), Is.EqualTo(json));
            Assert.That(Parameters(second)[0].Min, Is.EqualTo(-5));
        }

        [Test]
        public void TwoVariables_HookeJeeves_KeepDefinitionOrder()
        {
            OptimisationDefinition optimisationDefinition = Changed(HookeJeeves, x => x.Variables.Add(new DesignVariable("Flow", 0.5, 2.5, 1, 0.25)));

            List<NumberParameter> parameters = Parameters(optimisationDefinition.ToGenOptDocument(@"C:\ws", "s"));
            Assert.That(parameters.Select(x => x.Name), Is.EqualTo(new[] { "Setpoint", "Flow" }));
            Assert.That((parameters[1].Initial, parameters[1].Min, parameters[1].Max, parameters[1].Step), Is.EqualTo((1.0, 0.5, 2.5, 0.25)));
        }

        // ------------------------------------------------------------------ refusals (nothing is built)

        [Test]
        public void Maximise_IsRefused() => Refused(Changed(GoldenSection, x => x.Objective.Sense = ObjectiveSense.Maximise), "OPT413");

        [Test]
        public void Constraint_IsRefused() => Refused(Changed(GoldenSection, x => x.Constraints = new List<OptimisationConstraint> { new OptimisationConstraint { Output = "Cost", AtMost = 1000 } }), "OPT414");

        [Test]
        public void IntegerVariable_IsRefused() => Refused(Changed(HookeJeeves, x => x.Variables[0].Type = DesignVariableType.Integer), "OPT415");

        [Test]
        public void GoldenSection_WithTwoVariables_IsRefused() => Refused(Changed(GoldenSection, x => x.Variables.Add(new DesignVariable("Flow", 0, 1))), "OPT412");

        [Test]
        public void OtherEngine_IsRefused() => Refused(Changed(GoldenSection, x => x.Model.Engine = "energyplus"), "OPT410");

        [Test]
        public void NonFiniteBound_IsRefused() => Refused(Changed(GoldenSection, x => x.Variables[0].Maximum = double.PositiveInfinity), "OPT214");

        [Test]
        public void HookeJeeves_WithoutStart_IsRefused() => Refused(Changed(HookeJeeves, x => x.Variables[0].Start = null), "OPT406");

        [Test]
        public void Refusal_IsAGenOptCompatibilityException_WithEveryDiagnostic()
        {
            TasOptimisationDefinitionException exception = Refused(Changed(GoldenSection, x => { x.Objective.Sense = ObjectiveSense.Maximise; x.Variables[0].Minimum = 40; }), "OPT413");

            Assert.That(exception, Is.InstanceOf<GenOptCompatibilityException>());
            Assert.That(exception.Diagnostics.Count(x => x.Severity == DiagnosticSeverity.Error), Is.GreaterThanOrEqualTo(2));
            foreach (OptimisationDiagnostic diagnostic in exception.Diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error))
            {
                Assert.That(exception.Message, Does.Contain(diagnostic.Message));
            }
        }

        [Test]
        public void NullDefinition_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ((OptimisationDefinition)null).ToGenOptDocument(@"C:\ws", "s"));
            Assert.Throws<ArgumentNullException>(() => ((OptimisationDefinition)null).ToGenOptDocument(new TasExecutionSettings()));
        }

        // ------------------------------------------------------------------ local execution settings

        [Test]
        public void ExecutionSettings_ReadTheScript_AndUseTheProjectFolder()
        {
            using (TestFolder folder = new TestFolder())
            {
                string scriptPath = folder.Combine("Script.txt");
                System.IO.File.WriteAllText(scriptPath, "Variables[\"Setpoint\"]\r\nScriptOutput.SetValue(\"Result\", 1);");
                TasExecutionSettings tasExecutionSettings = new TasExecutionSettings(folder.Folder("ws"), scriptPath, folder.Combine("runs"), @"C:\stub.exe");

                GenOptDocument genOptDocument = Fixture(GoldenSection).ToGenOptDocument(tasExecutionSettings);

                Assert.That(genOptDocument.Directory, Is.EqualTo(tasExecutionSettings.ProjectFolder));
                Assert.That(genOptDocument.ScriptFile.Text, Is.EqualTo(FormDocument(GoldenSection, tasExecutionSettings.ProjectFolder, System.IO.File.ReadAllText(scriptPath)).ScriptFile.Text));

                TasExecutionSettings copy = new TasExecutionSettings(tasExecutionSettings);
                Assert.That((copy.ProjectFolder, copy.ScriptPath, copy.RunsFolder, copy.TasGenExecutePath), Is.EqualTo((tasExecutionSettings.ProjectFolder, tasExecutionSettings.ScriptPath, tasExecutionSettings.RunsFolder, tasExecutionSettings.TasGenExecutePath)));
            }
        }

        [Test]
        public void ExecutionSettings_MissingScript_Throws()
        {
            Assert.Throws<FileNotFoundException>(() => Fixture(GoldenSection).ToGenOptDocument(new TasExecutionSettings(@"C:\ws", @"C:\no-such-folder\Script.txt")));
            Assert.Throws<FileNotFoundException>(() => Fixture(GoldenSection).ToGenOptDocument(new TasExecutionSettings(@"C:\ws", null)));
            Assert.Throws<ArgumentNullException>(() => Fixture(GoldenSection).ToGenOptDocument((TasExecutionSettings)null));
        }
    }
}
