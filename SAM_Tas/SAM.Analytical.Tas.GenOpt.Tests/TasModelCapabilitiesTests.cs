// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical.Tas.GenOpt.Tests.Helpers;
using SAM.Core.Optimisation;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Tas.GenOpt.Tests
{
    /// <summary>
    /// PR7b: the "tas-model" engine's capabilities, and how SAM's fixtures validate against them (with the model's
    /// catalogue). The "tas-script" capabilities stay as PR4 left them.
    /// </summary>
    [TestFixture]
    public class TasModelCapabilitiesTests
    {
        private static IOptimisationCapabilities Capabilities => Query.TasModelCapabilities();

        [Test]
        public void Engine_runs_golden_section_hooke_jeeves_and_try_every_option()
        {
            Assert.That(Capabilities.Engine, Is.EqualTo("tas-model"));
            Assert.That(Query.TasModelEngine, Is.EqualTo("tas-model"));
            Assert.That(Capabilities.Algorithms.Select(x => x.Algorithm + " " + x.MinimumVariables + ".." + (x.MaximumVariables?.ToString() ?? "∞")), Is.EqualTo(new[]
            {
                "GoldenSection 1..1",
                "HookeJeeves 1..∞",
                "TryEveryOption 1..1",
            }));
            Assert.That(Capabilities.Senses, Is.EqualTo(new[] { ObjectiveSense.Minimise }));
            Assert.That(Capabilities.VariableTypes, Is.EqualTo(new[] { DesignVariableType.Continuous, DesignVariableType.Discrete }));
            Assert.That(Capabilities.SupportsConstraints, Is.False);
        }

        [Test]
        public void Targets_are_the_pr7a_kinds_plus_the_glazing_choice_and_not_the_g_value()
        {
            Assert.That(Capabilities.Targets.Select(Describe), Is.EqualTo(new[]
            {
                "tbd.internal-condition.heating-setpoint|Zone heating setpoint|Temperature|°C|internalCondition|options=False|max=",
                "tbd.internal-condition.cooling-setpoint|Zone cooling setpoint|Temperature|°C|internalCondition|options=False|max=",
                "tbd.glazing-construction.choice|Glazing system|Unspecified||glazingConstruction|options=True|max=8",
                "tpd.controller.setpoint|Plant controller setpoint|Unspecified||plantRoom,controller|options=False|max=",
            }));
            Assert.That(Capabilities.Targets.Any(x => x.Kind == "tbd.glazing-construction.g-value"), Is.False);
            Assert.That(Capabilities.Targets.SelectMany(x => x.ReferenceKeys).All(x => x.Required), Is.True);
        }

        [Test]
        public void Measures_have_their_units_and_the_overheating_threshold()
        {
            Assert.That(Capabilities.Measures.Select(Describe), Is.EqualTo(new[]
            {
                "tsd.annual-heating-demand|Annual heating demand|Energy|kWh||options=False|max=",
                "tsd.annual-cooling-demand|Annual cooling demand|Energy|kWh||options=False|max=",
                "tsd.overheating-hours|Overheating hours|Time|h||options=False|max=",
                "tpd.annual-energy|Annual plant energy|Energy|kWh||options=False|max=",
                "tpd.annual-cost|Annual plant cost|Currency|GBP||options=False|max=",
                "tpd.annual-co2|Annual plant CO2|Carbon|kgCO2e||options=False|max=",
            }));

            OptimisationBindingParameter threshold = Capabilities.Measures.Single(x => x.Kind == TasModelKind.OverheatingHours).Parameters.Single();
            Assert.That(threshold.Name, Is.EqualTo("threshold"));
            Assert.That(threshold.Default, Is.EqualTo(28));
            Assert.That(threshold.Minimum, Is.EqualTo(20));
            Assert.That(threshold.Maximum, Is.EqualTo(40));
            Assert.That(threshold.Unit, Is.EqualTo("°C"));
        }

        [Test]
        public void Tas_script_capabilities_are_unchanged()
        {
            IOptimisationCapabilities script = Query.TasOptimisationCapabilities();
            Assert.That(script.Engine, Is.EqualTo("tas-script"));
            Assert.That(script.Algorithms.Select(x => x.Algorithm), Is.EqualTo(new[] { OptimisationAlgorithm.GoldenSection, OptimisationAlgorithm.HookeJeeves }));
            Assert.That(script.VariableTypes, Is.EqualTo(new[] { DesignVariableType.Continuous }));
            Assert.That(script.Targets, Is.Empty);
            Assert.That(script.Measures, Is.Empty);
        }

        [Test]
        public void Glazing_choice_fixture_runs_with_a_catalogue_that_offers_its_options()
        {
            OptimisationDefinition definition = TasModelFixtures.Read(TasModelFixtures.FixtureText("glazing-choice.json"));
            OptimisationCatalogue catalogue = new OptimisationCatalogue(
                new[] { new OptimisationCatalogueEntry("Office glazing", new OptimisationTarget(TasModelKind.GlazingChoice, new Dictionary<string, string> { { "glazingConstruction", "Office glazing" } }), options: new[] { "Double low-e", "Triple low-e", "Double solar control" }) },
                new[] { new OptimisationCatalogueEntry("Overheating hours", new OptimisationMeasure(TasModelKind.OverheatingHours)) });

            List<OptimisationDiagnostic> diagnostics = definition.Diagnostics(Capabilities, catalogue);
            Assert.That(diagnostics.IsRunnable(), Is.True, string.Join("\n", diagnostics));
        }

        [Test]
        public void Glazing_choice_fixture_with_an_option_the_model_does_not_offer_is_OPT614()
        {
            OptimisationDefinition definition = TasModelFixtures.Read(TasModelFixtures.FixtureText("glazing-choice.json"));
            OptimisationCatalogue catalogue = new OptimisationCatalogue(
                new[] { new OptimisationCatalogueEntry("Office glazing", new OptimisationTarget(TasModelKind.GlazingChoice, new Dictionary<string, string> { { "glazingConstruction", "Office glazing" } }), options: new[] { "Double low-e", "Triple low-e" }) },
                new[] { new OptimisationCatalogueEntry("Overheating hours", new OptimisationMeasure(TasModelKind.OverheatingHours)) });

            List<OptimisationDiagnostic> diagnostics = definition.Diagnostics(Capabilities, catalogue);
            Assert.That(diagnostics.IsRunnable(), Is.False);
            Assert.That(diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error).Select(x => x.Code), Is.EqualTo(new[] { "OPT614" }));
        }

        [Test]
        public void G_value_fixture_is_not_runnable_because_the_kind_is_not_offered()
        {
            OptimisationDefinition definition = TasModelFixtures.Read(TasModelFixtures.FixtureText("zone-setpoints-glazing-hooke-jeeves.json"));
            List<OptimisationDiagnostic> errors = definition.Diagnostics(Capabilities).Where(x => x.Severity == DiagnosticSeverity.Error).ToList();
            Assert.That(errors.Select(x => x.Code), Is.EqualTo(new[] { "OPT601" }), string.Join("\n", errors));
            Assert.That(errors[0].Message, Does.Contain("g-value"));
        }

        [Test]
        public void Zone_setpoint_fixture_without_the_g_value_runs()
        {
            string text = TasModelFixtures.FixtureText("zone-setpoints-glazing-hooke-jeeves.json");
            OptimisationDefinition definition = TasModelFixtures.Read(text);
            definition.Variables.RemoveAll(x => x.Name == "g-value");
            List<OptimisationDiagnostic> diagnostics = definition.Diagnostics(Capabilities);
            Assert.That(diagnostics.IsRunnable(), Is.True, string.Join("\n", diagnostics));
        }

        [Test]
        public void Bound_systems_demo_fixture_names_plant_room_1_which_the_demo_does_not_have()
        {
            // PR6's fixture says "Plant Room 1"; the Systems Demo's plant room is "Plant Room" (PR7a). SAM is not edited
            // here: against the Demo's catalogue the fixture is OPT609 with a suggestion, and with the Demo's name it runs.
            OptimisationCatalogue catalogue = Query.TasModelCatalogue(TasModelFixtures.Demo());
            string text = TasModelFixtures.FixtureText("systems-demo-bound-golden-section.json");

            List<OptimisationDiagnostic> diagnostics = TasModelFixtures.Read(text).Diagnostics(Capabilities, catalogue);
            OptimisationDiagnostic error = diagnostics.Single(x => x.Severity == DiagnosticSeverity.Error);
            Assert.That(error.Code, Is.EqualTo("OPT609"));
            Assert.That(error.Message + " " + error.Hint, Does.Contain("Plant Room"));

            List<OptimisationDiagnostic> fixedDiagnostics = TasModelFixtures.Read(text.Replace("\"Plant Room 1\"", "\"Plant Room\"")).Diagnostics(Capabilities, catalogue);
            Assert.That(fixedDiagnostics.IsRunnable(), Is.True, string.Join("\n", fixedDiagnostics));
        }

        [Test]
        public void Tas_script_fixtures_are_not_runnable_on_tas_model()
        {
            // The unbound Systems Demo examples belong to "tas-script": every variable and output lacks a binding (OPT608).
            foreach (string fileName in new[] { "systems-demo-golden-section.json", "systems-demo-hooke-jeeves.json" })
            {
                List<OptimisationDiagnostic> diagnostics = TasModelFixtures.Read(TasModelFixtures.FixtureText(fileName)).Diagnostics(Capabilities);
                Assert.That(diagnostics.IsRunnable(), Is.False, fileName);
                Assert.That(diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error).Select(x => x.Code), Does.Contain("OPT608"), fileName);
            }
        }

        [Test]
        public void AI_text_offers_the_choice_and_try_every_option()
        {
            string text = global::SAM.Core.Optimisation.Query.AIExchangeText(null, Capabilities, Query.TasModelCatalogue(TasModelFixtures.SamModel(), TasModelFixtures.Pool()));
            Assert.That(text, Does.Contain("try-every-option"));
            Assert.That(text, Does.Contain("tbd.glazing-construction.choice"));
            Assert.That(text, Does.Not.Contain("g-value\""));
        }

        private static string Describe(OptimisationBindingCapability capability)
        {
            return string.Join("|", capability.Kind, capability.DisplayName, capability.Quantity, capability.Unit, string.Join(",", capability.ReferenceKeys.Select(x => x.Name)), "options=" + capability.AcceptsOptions, "max=" + capability.MaximumOptions);
        }
    }
}
