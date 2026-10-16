// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Core.Optimisation;
using System;
using System.Collections.Generic;
using System.Linq;
using Diagnostic = SAM.Core.Diagnostic;
using DiagnosticDetail = SAM.Core.DiagnosticDetail;
using Diagnostics = SAM.Core.Diagnostics;
using OptimisationSeverity = SAM.Core.Optimisation.DiagnosticSeverity;
using Severity = SAM.Core.DiagnosticSeverity;

namespace SAM.Analytical.Tas.GenOpt.Tests
{
    /// <summary>
    /// Unified Diagnostics, phase 4B (TAS): <c>Query.ToDiagnostic</c> / <c>Query.ToDiagnostics</c> over the <see cref="OptimisationDiagnostic"/> findings the
    /// Tas optimisation checks raise. The producer states its severity, so it is mapped one to one; no message is ever read.
    /// </summary>
    [TestFixture]
    public class DiagnosticsAdapterTests
    {
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
        public void EverySeverity_IsMappedOneToOne_WithCodePathAndLegacyText()
        {
            OptimisationDiagnostic info = new OptimisationDiagnostic(OptimisationSeverity.Info, "OPT999", "$.a", "An information.");
            OptimisationDiagnostic warning = new OptimisationDiagnostic(OptimisationSeverity.Warning, "OPT501", "$.variables[0].name", "A warning.", "Check the name.");
            OptimisationDiagnostic error = new OptimisationDiagnostic(OptimisationSeverity.Error, "OPT214", "$.variables[0].maximum", "An error.", null, 9, 7);

            Diagnostic dInfo = info.ToDiagnostic(0);
            Diagnostic dWarning = warning.ToDiagnostic(1);
            Diagnostic dError = error.ToDiagnostic(2);

            Assert.That((dInfo.Severity, dInfo.Detail, dInfo.Code, dInfo.Subject, dInfo.Message, dInfo.SourceIndex), Is.EqualTo((Severity.Info, DiagnosticDetail.Standard, "OPT999", "$.a", "An information.", (int?)0)));
            Assert.That((dWarning.Severity, dWarning.Code, dWarning.Subject, dWarning.Message, dWarning.SourceIndex), Is.EqualTo((Severity.Warning, "OPT501", "$.variables[0].name", "A warning. Check the name.", (int?)1)));
            Assert.That((dError.Severity, dError.Code, dError.Subject, dError.Message), Is.EqualTo((Severity.Error, "OPT214", "$.variables[0].maximum (line 9, column 7)", "An error.")));

            Assert.That(info.ToString(), Is.EqualTo("Info OPT999 at $.a: An information."), "the legacy text of the finding is unchanged");
            Assert.That(error.ToString(), Is.EqualTo("Error OPT214 at $.variables[0].maximum (line 9, column 7): An error."));
        }

        [Test]
        public void AnUndefinedSeverity_IsAnError_NeverInformation()
        {
            OptimisationDiagnostic diagnostic = new OptimisationDiagnostic((OptimisationSeverity)99, "OPT000", "$", "Unknown severity.");

            Assert.That(diagnostic.ToDiagnostic().Severity, Is.EqualTo(Severity.Error));
        }

        [Test]
        public void TheRealScriptCheck_KeepsItsWarnings_AtEveryDetailLevel_InOrder()
        {
            string script = "Variables[\"Setpoint\"]; ScriptOutput.SetValue(\"Result\", r); ScriptOutput.SetValue(\"Cost\", c);";
            List<OptimisationDiagnostic> findings = Definition().TasScriptDiagnostics(script);
            Assert.That(findings.Select(x => x.Code).ToList(), Is.EqualTo(new[] { "OPT502", "OPT502" }), "fixture: two warnings from the real producer");
            List<string> legacy = findings.Select(x => x.ToString()).ToList();

            foreach (DiagnosticDetail detail in Enum.GetValues(typeof(DiagnosticDetail)))
            {
                Diagnostics diagnostics = findings.ToDiagnostics(detail);

                Assert.That(diagnostics.Select(x => x.Severity).ToList(), Is.EqualTo(new[] { Severity.Warning, Severity.Warning }), detail.ToString());
                Assert.That(diagnostics.Select(x => x.Subject).ToList(), Is.EqualTo(new[] { "$.outputs[1].name", "$.outputs[2].name" }));
                Assert.That(diagnostics.Select(x => x.SourceIndex).ToList(), Is.EqualTo(new int?[] { 0, 1 }.ToList()));
                Assert.That(diagnostics.First().Message, Does.StartWith("The Tas script never writes output \"cost\"."));
            }

            Assert.That(findings.Select(x => x.ToString()).ToList(), Is.EqualTo(legacy), "the adapter does not change the findings");
        }

        [Test]
        public void TheRealRefusal_ItsErrorsStayErrors()
        {
            OptimisationDefinition definition = Definition();
            definition.Variables[0].Maximum = double.PositiveInfinity;

            TasOptimisationDefinitionException exception = Assert.Throws<TasOptimisationDefinitionException>(() => definition.ToGenOptDocument(@"C:\no-such-folder", "script"));
            Diagnostics diagnostics = exception.Diagnostics.ToDiagnostics(DiagnosticDetail.Summary);

            Assert.That(diagnostics.HasErrors, Is.True);
            Assert.That(diagnostics.Where(x => x.Severity == Severity.Error).Select(x => x.Code).ToList(),
                Is.EqualTo(exception.Diagnostics.Where(x => x.Severity == OptimisationSeverity.Error).Select(x => x.Code).ToList()), "no error is dropped or renamed");
            Assert.That(diagnostics.Select(x => x.Code).ToList(), Does.Contain("OPT214"));
        }

        [Test]
        public void DetailLevels_OnlyNarrowInformation_AndKeepEveryWarningAndError()
        {
            List<OptimisationDiagnostic> findings = new List<OptimisationDiagnostic>
            {
                new OptimisationDiagnostic(OptimisationSeverity.Error, "OPT1", "$", "e"),
                new OptimisationDiagnostic(OptimisationSeverity.Info, "OPT2", "$", "i"),
                new OptimisationDiagnostic(OptimisationSeverity.Warning, "OPT3", "$", "w"),
            };

            string Key(Diagnostic d) => d.Severity + "|" + d.Code + "|" + d.SourceIndex;
            List<string> summary = findings.ToDiagnostics(DiagnosticDetail.Summary).Select(Key).ToList();
            List<string> standard = findings.ToDiagnostics(DiagnosticDetail.Standard).Select(Key).ToList();
            List<string> detailed = findings.ToDiagnostics(DiagnosticDetail.Detailed).Select(Key).ToList();

            Assert.That(summary, Is.EqualTo(new[] { "Error|OPT1|0", "Warning|OPT3|2" }), "Summary: the information is narrowed, the warning and error are kept");
            Assert.That(standard, Is.EqualTo(new[] { "Error|OPT1|0", "Info|OPT2|1", "Warning|OPT3|2" }));
            Assert.That(detailed, Is.EqualTo(standard));
            Assert.That(summary.All(standard.Contains) && standard.All(detailed.Contains), Is.True);
        }

        [Test]
        public void NullAndEmptyInput_AreHandled()
        {
            Assert.That(((OptimisationDiagnostic)null).ToDiagnostic(), Is.Null);
            Assert.That(((IEnumerable<OptimisationDiagnostic>)null).ToDiagnostics().Count, Is.EqualTo(0));
            Assert.That(new List<OptimisationDiagnostic>().ToDiagnostics().Count, Is.EqualTo(0));

            Diagnostics diagnostics = new List<OptimisationDiagnostic> { null, new OptimisationDiagnostic(OptimisationSeverity.Error, "OPT1", "$", null) }.ToDiagnostics();
            Assert.That(diagnostics.Single().SourceIndex, Is.EqualTo(1), "a null entry is skipped but counted");
            Assert.That(diagnostics.Single().Message, Is.EqualTo(string.Empty));
        }
    }
}
