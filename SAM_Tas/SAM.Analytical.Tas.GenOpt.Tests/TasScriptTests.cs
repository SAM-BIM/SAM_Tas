// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using SAM.Analytical.Tas.GenOpt.Tests.Helpers;
using SAM.Core.Optimisation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SAM.Analytical.Tas.GenOpt.Tests
{
    /// <summary>
    /// PR7b: the generated TasGenExecute script. One snapshot per chain shape (building only, plant only, glazing choice,
    /// everything) pins every block; each snapshot compiles at C# 7.0 against the Tas interops (TasScriptCompiler), as
    /// TasGenExecute compiles it. Set SAM_TAS_UPDATE_SNAPSHOTS=1 to rewrite the snapshots after a deliberate change.
    /// </summary>
    [TestFixture]
    public class TasScriptTests
    {
        internal const string Setpoints = @"{
  ""schema"": ""sam.optimisation/1"",
  ""name"": ""Office setpoints"",
  ""model"": { ""engine"": ""tas-model"" },
  ""variables"": [
    { ""name"": ""Heating, office"", ""minimum"": 18, ""maximum"": 22, ""start"": 20, ""step"": 1,
      ""target"": { ""kind"": ""tbd.internal-condition.heating-setpoint"", ""reference"": { ""internalCondition"": ""Office Weekday"" } } },
    { ""name"": ""Cooling"", ""minimum"": 22, ""maximum"": 28, ""start"": 24, ""step"": 1,
      ""target"": { ""kind"": ""tbd.internal-condition.cooling-setpoint"", ""reference"": { ""internalCondition"": ""Office Weekday"" } } }
  ],
  ""outputs"": [
    { ""name"": ""Cooling demand"", ""unit"": ""kWh"", ""measure"": { ""kind"": ""tsd.annual-cooling-demand"" } },
    { ""name"": ""Heating demand"", ""unit"": ""kWh"", ""measure"": { ""kind"": ""tsd.annual-heating-demand"" } },
    { ""name"": ""Overheating 25"", ""unit"": ""h"", ""measure"": { ""kind"": ""tsd.overheating-hours"", ""parameters"": { ""threshold"": 25 } } },
    { ""name"": ""Overheating"", ""unit"": ""h"", ""measure"": { ""kind"": ""tsd.overheating-hours"" } }
  ],
  ""objective"": { ""output"": ""Heating demand"", ""sense"": ""minimise"" },
  ""method"": { ""algorithm"": ""hooke-jeeves"" },
  ""stopping"": { ""maximumSimulations"": 50 }
}";

        internal const string Controller = @"{
  ""schema"": ""sam.optimisation/1"",
  ""name"": ""Heat pump controller"",
  ""model"": { ""engine"": ""tas-model"" },
  ""variables"": [
    { ""name"": ""Setpoint"", ""minimum"": -5, ""maximum"": 35, ""start"": 3, ""step"": 1,
      ""target"": { ""kind"": ""tpd.controller.setpoint"", ""reference"": { ""controller"": ""HeatPumpController"", ""plantRoom"": ""Plant Room"" } } }
  ],
  ""outputs"": [
    { ""name"": ""Cost"", ""quantity"": ""currency"", ""unit"": ""GBP"", ""measure"": { ""kind"": ""tpd.annual-cost"" } },
    { ""name"": ""CO2"", ""quantity"": ""carbon"", ""unit"": ""kgCO2e"", ""measure"": { ""kind"": ""tpd.annual-co2"" } },
    { ""name"": ""Energy"", ""unit"": ""kWh"", ""measure"": { ""kind"": ""tpd.annual-energy"" } }
  ],
  ""objective"": { ""output"": ""Cost"", ""sense"": ""minimise"" },
  ""method"": { ""algorithm"": ""golden-section"", ""tolerance"": 0.1 }
}";

        internal const string Glazing = @"{
  ""schema"": ""sam.optimisation/1"",
  ""name"": ""Glazing choice"",
  ""model"": { ""engine"": ""tas-model"" },
  ""variables"": [
    { ""name"": ""Glazing"", ""type"": ""discrete"", ""minimum"": 1, ""maximum"": 3,
      ""target"": { ""kind"": ""tbd.glazing-construction.choice"", ""reference"": { ""glazingConstruction"": ""Windows: SIM_EXT_GLZ -pane"" },
                  ""options"": [ ""Windows: SIM_EXT_GLZ -pane"", ""Double B"", ""Triple low-e"" ] } }
  ],
  ""outputs"": [
    { ""name"": ""Overheating"", ""unit"": ""h"", ""measure"": { ""kind"": ""tsd.overheating-hours"", ""parameters"": { ""threshold"": 28 } } },
    { ""name"": ""Cooling demand"", ""unit"": ""kWh"", ""measure"": { ""kind"": ""tsd.annual-cooling-demand"" } }
  ],
  ""objective"": { ""output"": ""Overheating"", ""sense"": ""minimise"" },
  ""method"": { ""algorithm"": ""try-every-option"" }
}";

        internal const string Everything = @"{
  ""schema"": ""sam.optimisation/1"",
  ""name"": ""Setpoint and controller"",
  ""model"": { ""engine"": ""tas-model"" },
  ""variables"": [
    { ""name"": ""Heating"", ""minimum"": 18, ""maximum"": 22, ""start"": 20, ""step"": 1,
      ""target"": { ""kind"": ""tbd.internal-condition.heating-setpoint"", ""reference"": { ""internalCondition"": ""Office Weekday"" } } },
    { ""name"": ""Controller"", ""minimum"": -5, ""maximum"": 35, ""start"": 3, ""step"": 2,
      ""target"": { ""kind"": ""tpd.controller.setpoint"", ""reference"": { ""controller"": ""HeatPumpController"", ""plantRoom"": ""Plant Room"" } } }
  ],
  ""outputs"": [
    { ""name"": ""Energy"", ""unit"": ""kWh"", ""measure"": { ""kind"": ""tpd.annual-energy"" } },
    { ""name"": ""Heating demand"", ""unit"": ""kWh"", ""measure"": { ""kind"": ""tsd.annual-heating-demand"" } },
    { ""name"": ""Overheating"", ""unit"": ""h"", ""measure"": { ""kind"": ""tsd.overheating-hours"" } }
  ],
  ""objective"": { ""output"": ""Energy"", ""sense"": ""minimise"" },
  ""method"": { ""algorithm"": ""hooke-jeeves"" }
}";

        /// <summary>The three options of the glazing case, as the runner resolves them (pane names given explicitly).</summary>
        internal static Dictionary<string, IReadOnlyList<TasGlazingOption>> GlazingOptions()
        {
            return new Dictionary<string, IReadOnlyList<TasGlazingOption>>
            {
                {
                    "Glazing",
                    new List<TasGlazingOption>
                    {
                        TasModelFixtures.Option(1, TasModelFixtures.SamGlazing, TasModelFixtures.SamGlazing, 0.4001609981060028, 1.2433900833129883, 0.803563117980957),
                        TasModelFixtures.Option(2, "Double B", "Windows: Double B ff1c5e -pane", 0.28368183970451355, 1.6993681192398071, 0.7965357303619385),
                        TasModelFixtures.Option(3, "Triple low-e", "Windows: Triple low-e a5191c -pane", 0.36886733770370483, 0.997646152973175, 0.7281997203826904),
                    }
                },
            };
        }

        public static IEnumerable<TestCaseData> Cases()
        {
            yield return new TestCaseData("setpoints", Setpoints).SetName("Snapshot_building_setpoints_and_two_thresholds");
            yield return new TestCaseData("controller", Controller).SetName("Snapshot_plant_only_controller");
            yield return new TestCaseData("glazing", Glazing).SetName("Snapshot_glazing_choice");
            yield return new TestCaseData("everything", Everything).SetName("Snapshot_building_and_plant");
        }

        internal static string Script(string name, string json)
        {
            OptimisationDefinition definition = TasModelFixtures.Read(json);
            return name == "glazing"
                ? Create.TasScript(definition, TasModelFixtures.SamModel(), GlazingOptions())
                : Create.TasScript(definition, TasModelFixtures.Demo());
        }

        [TestCaseSource(nameof(Cases))]
        public void Generated_script_equals_its_snapshot_and_compiles_as_csharp_7(string name, string json)
        {
            string script = Script(name, json);
            string path = Path.Combine(SnapshotDirectory(), name + ".csx");
            if (Environment.GetEnvironmentVariable("SAM_TAS_UPDATE_SNAPSHOTS") == "1")
            {
                System.IO.File.WriteAllText(path, script);
            }

            Assert.That(System.IO.File.Exists(path), Is.True, "Snapshot missing: " + path);
            Assert.That(script, Is.EqualTo(System.IO.File.ReadAllText(path).Replace("\r\n", "\n")));
            Assert.That(TasScriptCompiler.Errors(script), Is.Empty);
        }

        [TestCaseSource(nameof(Cases))]
        public void Generated_script_is_ascii_and_writes_result_and_every_output(string name, string json)
        {
            string script = Script(name, json);
            Assert.That(script.All(x => x == '\n' || (x >= 0x20 && x <= 0x7E)), Is.True);
            Assert.That(script, Does.Contain("using System.Linq;"));
            Assert.That(script, Does.Not.Contain("#r"));
            Assert.That(script, Does.Not.Contain("TCD."));

            OptimisationDefinition definition = TasModelFixtures.Read(json);
            Assert.That(script, Does.Contain("ScriptOutput.SetValue(\"Result\", "));
            for (int i = 0; i < definition.Outputs.Count; i++)
            {
                Assert.That(script, Does.Contain("ScriptOutput.SetValue(\"Y" + (i + 1) + "\", "));
            }

            for (int i = 0; i < definition.Variables.Count; i++)
            {
                Assert.That(script, Does.Contain("Variable(\"V" + (i + 1) + "\")"));
            }
        }

        [Test]
        public void Chain_rule_selects_the_blocks()
        {
            string setpoints = Script("setpoints", Setpoints);
            Assert.That(setpoints, Does.Contain("tbdDocument.simulate(1, 365, 0, 1, 0, 0, tsdPath, 1, 0);"));
            Assert.That(setpoints, Does.Contain("tsdDocument.openReadOnly(tsdPath);"));
            Assert.That(setpoints, Does.Not.Contain("TPD."));
            Assert.That(setpoints, Does.Contain("double[] thresholds = new double[] { 25, 28 };"));
            Assert.That(setpoints, Does.Contain("ScriptOutput.SetValue(\"Result\", heatingDemand);"));
            Assert.That(setpoints, Does.Contain("ScriptOutput.SetValue(\"Y1\", heatingDemand);"));
            Assert.That(setpoints, Does.Contain("ScriptOutput.SetValue(\"Y2\", coolingDemand);"));
            Assert.That(setpoints, Does.Contain("ScriptOutput.SetValue(\"Y3\", overheating[0]);"));
            Assert.That(setpoints, Does.Contain("ScriptOutput.SetValue(\"Y4\", overheating[1]);"));

            string controller = Script("controller", Controller);
            Assert.That(controller, Does.Not.Contain("tbdDocument"));
            Assert.That(controller, Does.Not.Contain("openReadOnly"));
            Assert.That(controller, Does.Contain("tsdFix.SimulationData.buildingPath = tbdPath;"), "F1: plant without a building simulation re-points the TSD");
            Assert.That(controller.IndexOf("tsdFix.close();", StringComparison.Ordinal), Is.LessThan(controller.IndexOf("FixTSDPath(tsdPath)", StringComparison.Ordinal)));
            Assert.That(controller, Does.Contain("controller.Setpoint = value;"), "written as a double");
            Assert.That(controller, Does.Not.Contain("(float)value;\n    outputs[output + \".Read\"] = controller"));
            Assert.That(controller, Does.Not.Contain("tpdDocument.Save()"));
            Assert.That(controller.IndexOf("resultSet.Dispose();", StringComparison.Ordinal), Is.LessThan(controller.IndexOf("Marshal.FinalReleaseComObject(tpdDocument);", StringComparison.Ordinal)), "F2: results read before the TPD is released");

            string everything = Script("everything", Everything);
            Assert.That(everything, Does.Not.Contain("tsdFix"), "a building simulation already points the TSD at the evaluation's TBD");
            Assert.That(everything.IndexOf("tbdDocument.close();", StringComparison.Ordinal), Is.LessThan(everything.IndexOf("tsdDocument.openReadOnly", StringComparison.Ordinal)));
            Assert.That(everything.IndexOf("tsdDocument.close();", StringComparison.Ordinal), Is.LessThan(everything.IndexOf("tpdDocument.Open(tpdPath);", StringComparison.Ordinal)));
        }

        [Test]
        public void Model_item_names_are_escaped_literals()
        {
            TasModelInventory inventory = new TasModelInventory("a \"b\".tbd", null, null, new[]
            {
                new TasInternalConditionInfo("Büro \"Nord\"\\1 ", null, 1, new TasSetpointProfile(TasSetpointProfileType.Value, 1, 20), null),
            });

            string json = Setpoints.Replace("\"Office Weekday\"", "\"B\\u00fcro \\\"Nord\\\"\\\\1 \"");
            OptimisationDefinition definition = TasModelFixtures.Read(json);
            definition.Variables.RemoveAt(1);
            string script = Create.TasScript(definition, inventory);

            Assert.That(script, Does.Contain("SetSetpoint(tbdBuilding, \"B\\u00fcro \\\"Nord\\\"\\\\1 \", true, Variable(\"V1\"), \"V1\");"));
            Assert.That(script, Does.Contain("[\"a \\\"b\\\".tbd\"]"));
            Assert.That(script, Does.Contain("// V1 = Heating, office: tbd.internal-condition.heating-setpoint { internalCondition: \"B?ro \"Nord\"\\1 \" }"));
            Assert.That(TasScriptCompiler.Errors(script), Is.Empty);
        }

        [Test]
        public void Compiler_check_enforces_csharp_7_and_the_interop_types()
        {
            Assert.That(TasScriptCompiler.Errors("int x = default;"), Has.Some.Contains("CS8107"), "C# 7.1 default literal");
            Assert.That(TasScriptCompiler.Errors("int x = default;", LanguageVersion.CSharp7_1), Is.Empty);
            Assert.That(TasScriptCompiler.Errors("TCD.Document d = null;"), Is.Not.Empty, "TCD is not referenced in TasGenExecute");
            Assert.That(TasScriptCompiler.Errors("var a = new[] { 1 }.Select(x => x);"), Is.Not.Empty, "LINQ needs using System.Linq;");
            Assert.That(TasScriptCompiler.Errors("TBD.TBDDocument d = new TBD.TBDDocument(); ScriptOutput.SetValue(\"Result\", Variables[\"X\"].VariableValue);"), Is.Empty);
        }

        [Test]
        public void Missing_files_or_options_are_refused()
        {
            OptimisationDefinition controller = TasModelFixtures.Read(Controller);
            Assert.That(() => Create.TasScript(controller, TasModelFixtures.SamModel()), Throws.ArgumentException.With.Message.Contains("no TPD"));

            OptimisationDefinition glazing = TasModelFixtures.Read(Glazing);
            Assert.That(() => Create.TasScript(glazing, TasModelFixtures.SamModel()), Throws.ArgumentException.With.Message.Contains("glazing options"));

            Dictionary<string, IReadOnlyList<TasGlazingOption>> detached = GlazingOptions();
            detached["Glazing"] = detached["Glazing"].Select(x => x.Number == 3 ? TasModelFixtures.Option(3, x.Text, null, x.G, x.U, x.Light) : x).ToList();
            Assert.That(() => Create.TasScript(glazing, TasModelFixtures.SamModel(), detached), Throws.ArgumentException.With.Message.Contains("no TBD pane construction"));

            TasModelInventory noTsdNoTbd = new TasModelInventory(null, null, "a.tpd");
            Assert.That(() => Create.TasScript(controller, noTsdNoTbd), Throws.ArgumentException.With.Message.Contains("no TBD"));
        }

        [Test]
        public void Without_a_tsd_the_building_is_simulated_even_without_a_building_target()
        {
            TasModelInventory inventory = new TasModelInventory("a.tbd", null, "a.tpd");
            string script = Create.TasScript(TasModelFixtures.Read(Controller), inventory);
            Assert.That(script, Does.Contain("tbdDocument.simulate("));
            Assert.That(script, Does.Not.Contain("tsdFix"));
            Assert.That(TasScriptCompiler.Errors(script), Is.Empty);
        }

        private static string SnapshotDirectory([CallerFilePath] string sourceFile = null)
        {
            string fromSource = Path.Combine(Path.GetDirectoryName(sourceFile), "Golden", "TasModel");
            return Directory.Exists(fromSource) ? fromSource : Path.Combine(AppContext.BaseDirectory, "Golden", "TasModel");
        }
    }
}
