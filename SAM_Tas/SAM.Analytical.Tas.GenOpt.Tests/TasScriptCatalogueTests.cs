// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical.Tas.GenOpt.Tests.Helpers;
using SAM.Core.Optimisation;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SAM.Analytical.Tas.GenOpt.Tests
{
    /// <summary>
    /// PR4: the literal scan of a Tas script for the names it reads (<c>Variables["…"]</c>) and writes
    /// (<c>ScriptOutput.SetValue("…", …)</c>), and the L6 warnings built on it.
    /// </summary>
    [TestFixture]
    public class TasScriptCatalogueTests
    {
        private static string[] Variables(string scriptText) => Query.TasScriptCatalogue(scriptText).Variables.Select(x => x.Name).ToArray();

        private static string[] Outputs(string scriptText) => Query.TasScriptCatalogue(scriptText).Outputs.Select(x => x.Name).ToArray();

        /// <summary>The SAM-owned daylight example shipped with SAM_Tas (files\resources\Analytical\Tas\GenOpt).</summary>
        private static string DaylightScript()
        {
            DirectoryInfo directory = new DirectoryInfo(Path.GetDirectoryName(GoldenTrace.Directory_Golden));
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "SAM_Tas", "files")))
            {
                directory = directory.Parent;
            }

            Assert.That(directory, Is.Not.Null, "SAM_Tas checkout not found");
            return System.IO.File.ReadAllText(Path.Combine(directory.FullName, "SAM_Tas", "files", "resources", "Analytical", "Tas", "GenOpt", "Script.txt"));
        }

        [Test]
        public void DaylightExample_ReadsNorthAngle_WritesDaylightFactorAndResult()
        {
            OptimisationCatalogue catalogue = Query.TasScriptCatalogue(DaylightScript());

            Assert.That(catalogue.Variables.Select(x => x.Name), Is.EqualTo(new[] { "NorthAngle" }));
            Assert.That(catalogue.Outputs.Select(x => x.Name), Is.EqualTo(new[] { "DaylightFactor", "Result" }));
            Assert.That(catalogue.Source, Is.EqualTo(Query.TasScriptCatalogueSource));
            Assert.That(catalogue.Variables.Concat(catalogue.Outputs).All(x => x.Unit == null && x.Quantity == OptimisationQuantity.Unspecified), Is.True);
        }

        [Test]
        public void SystemsDemoShape_IsFound()
        {
            string script = string.Join("\r\n",
                "double dSetpoint = Variables[\"Setpoint\"].VariableValue;",
                "controller.Setpoint = dSetpoint;",
                "// Retrieve the Annual Cost (GBP)",
                "ScriptOutput.SetValue(\"Result\", dTotal);",
                "ScriptOutput.SetValue(\"Cost\", dCost);",
                "ScriptOutput.SetValue(\"CO2\", dCO2);");

            Assert.That(Variables(script), Is.EqualTo(new[] { "Setpoint" }));
            Assert.That(Outputs(script), Is.EqualTo(new[] { "Result", "Cost", "CO2" }));
        }

        [Test]
        public void Names_AreListedOnce_InFirstAppearanceOrder()
        {
            string script = "Variables[\"B\"]; Variables[\"A\"]; Variables[\"B\"]; ScriptOutput.SetValue(\"Y\", 1); ScriptOutput.SetValue(\"X\", 2); ScriptOutput.SetValue(\"Y\", 3);";

            Assert.That(Variables(script), Is.EqualTo(new[] { "B", "A" }));
            Assert.That(Outputs(script), Is.EqualTo(new[] { "Y", "X" }));
        }

        [Test]
        public void Whitespace_InsideTheCall_IsAllowed()
        {
            string script = "Variables [ \"A\" ].VariableValue; ScriptOutput . SetValue (\r\n  \"Y\" , v);";

            Assert.That(Variables(script), Is.EqualTo(new[] { "A" }));
            Assert.That(Outputs(script), Is.EqualTo(new[] { "Y" }));
        }

        [Test]
        public void Comments_AreIgnored_StringsAreNot()
        {
            string script = string.Join("\n",
                "// Variables[\"LineComment\"]",
                "/* ScriptOutput.SetValue(\"BlockComment\", 1);",
                "   Variables[\"BlockComment\"] */",
                "string url = \"http://example\"; Variables[\"AfterUrl\"];",
                "string verbatim = @\"C:\\path // not a comment \"\"quoted\"\"\"; ScriptOutput.SetValue(\"AfterVerbatim\", 1);",
                "char c = '\"'; Variables[\"AfterChar\"];");

            Assert.That(Variables(script), Is.EqualTo(new[] { "AfterUrl", "AfterChar" }));
            Assert.That(Outputs(script), Is.EqualTo(new[] { "AfterVerbatim" }));
        }

        [Test]
        public void NonLiteralNames_AndOtherCalls_AreNotFound()
        {
            string script = "Variables[name].VariableValue; Variables[\"\"]; MyVariables[\"X\"]; MyScriptOutput.SetValue(\"W\", 1); Output.SetValue(\"Z\", 1);";

            Assert.That(Variables(script), Is.Empty);
            Assert.That(Outputs(script), Is.Empty);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("no names here")]
        public void EmptyScript_GivesAnEmptyCatalogue(string scriptText)
        {
            Assert.That(Query.TasScriptCatalogue(scriptText).IsEmpty, Is.True);
        }

        // ------------------------------------------------------------------ L6 warnings

        private static OptimisationDefinition Definition()
        {
            return new OptimisationDefinition
            {
                Model = new OptimisationModel(Query.TasOptimisationEngine),
                Variables = new List<DesignVariable> { new DesignVariable("Setpoint", -5, 35, 3, 1) },
                Outputs = new List<OptimisationOutput> { new OptimisationOutput("Result"), new OptimisationOutput("cost"), new OptimisationOutput("Energy") },
                Objective = new OptimisationObjective("Result", ObjectiveSense.Minimise),
                Method = new GoldenSectionMethod { Tolerance = 0.1 },
                Stopping = new StoppingCriteria(10),
            };
        }

        [Test]
        public void ScriptDiagnostics_WarnForNamesTheScriptDoesNotUse()
        {
            string script = "Variables[\"Setpoint\"]; ScriptOutput.SetValue(\"Result\", r); ScriptOutput.SetValue(\"Cost\", c);";

            List<OptimisationDiagnostic> diagnostics = Definition().TasScriptDiagnostics(script);

            Assert.That(diagnostics.Select(x => (x.Severity, x.Code, x.Path)), Is.EqualTo(new[]
            {
                (DiagnosticSeverity.Warning, "OPT502", "$.outputs[1].name"),
                (DiagnosticSeverity.Warning, "OPT502", "$.outputs[2].name"),
            }));
            Assert.That(diagnostics[0].Message, Is.EqualTo("The Tas script never writes output \"cost\"."));
            Assert.That(diagnostics[0].Hint, Does.EndWith("names are case-sensitive and the script uses \"Cost\"."));
            Assert.That(diagnostics[1].Hint, Does.EndWith("the script uses \"Result\", \"Cost\"."));
            Assert.That(diagnostics.IsRunnable(), Is.True, "warnings never block a run");
        }

        [Test]
        public void ScriptDiagnostics_WarnForAVariableTheScriptDoesNotRead()
        {
            List<OptimisationDiagnostic> diagnostics = Definition().TasScriptDiagnostics("ScriptOutput.SetValue(\"Result\", r);");

            OptimisationDiagnostic diagnostic = diagnostics.Single(x => x.Code == "OPT501");
            Assert.That(diagnostic.Path, Is.EqualTo("$.variables[0].name"));
            Assert.That(diagnostic.Message, Is.EqualTo("The Tas script never reads design variable \"Setpoint\"."));
            Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Warning));
        }

        [Test]
        public void ScriptDiagnostics_NoneForAMatchingScript_OrANullDefinition()
        {
            string script = "Variables[\"Setpoint\"]; ScriptOutput.SetValue(\"Result\", r); ScriptOutput.SetValue(\"cost\", c); ScriptOutput.SetValue(\"Energy\", e);";

            Assert.That(Definition().TasScriptDiagnostics(script), Is.Empty);
            Assert.That(((OptimisationDefinition)null).TasScriptDiagnostics(script), Is.Empty);
        }
    }
}
