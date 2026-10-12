// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical.Tas.GenOpt.Tests.Helpers;
using SAM.Core.Optimisation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Tas.GenOpt.Tests
{
    /// <summary>
    /// PR7b-2 D1 (owner decision option (a), 2026-10-12): zone setpoints and glazing are optimised only on a TBD whose
    /// zones Tas sizes in every simulation. Measured on copies: Energy Simulation with Sizing on leaves tbdNoSizing with
    /// fixed capacities (kept by every evaluation, sized again by the next Energy Simulation: ModelA heating +82%, glazing
    /// +14%); Sizing off leaves tbdSizing and the Systems Demo has tbdDesignSizingOnly, both sized again by every simulation.
    /// The guard is at target discovery (catalogue), definition validation (runner, OPT617), execution (the generated
    /// script checks the TBD itself) and Apply (the inventory, then the TBD itself). Plant controllers are not affected.
    /// </summary>
    [TestFixture]
    public class SizingGuardTests
    {
        private const string HeatingOnly = @"{
  ""schema"": ""sam.optimisation/1"",
  ""name"": ""Cell 1 heating"",
  ""model"": { ""engine"": ""tas-model"" },
  ""variables"": [
    { ""name"": ""Cell 1 heating"", ""minimum"": 18, ""maximum"": 24, ""start"": 21, ""step"": 1,
      ""target"": { ""kind"": ""tbd.internal-condition.heating-setpoint"", ""reference"": { ""internalCondition"": ""Cell 1"" } } }
  ],
  ""outputs"": [ { ""name"": ""Heating demand"", ""unit"": ""kWh"", ""measure"": { ""kind"": ""tsd.annual-heating-demand"" } } ],
  ""objective"": { ""output"": ""Heating demand"", ""sense"": ""minimise"" },
  ""method"": { ""algorithm"": ""golden-section"" }
}";

        private static TasModelInventory With(TasModelInventory inventory, params (int Heating, int Cooling)[] zones)
        {
            inventory.ZoneSizing = zones.Select((x, i) => new TasZoneSizing("Zone " + (i + 1), x.Heating, 1000, x.Cooling, 1000)).ToList();
            return inventory;
        }

        // ---- the detector

        [Test]
        public void The_sizing_constants_are_the_measured_Tas_values()
        {
            Assert.That(TasZoneSizing.NoSizing, Is.EqualTo(0));
            Assert.That(TasZoneSizing.DesignSizingOnly, Is.EqualTo(1));
            Assert.That(TasZoneSizing.Sizing, Is.EqualTo(2));
            Assert.That(Query.SizingTypeText(3), Is.EqualTo("tbdMixed"));
            Assert.That(Query.SizingTypeText(4), Is.EqualTo("tbdLoadPerArea"));
            Assert.That(Query.SizingTypeText(9), Is.EqualTo("sizing type 9"));
        }

        [TestCase(1, 1, TasCapacitySizing.SizedEverySimulation)]
        [TestCase(2, 2, TasCapacitySizing.SizedEverySimulation)]
        [TestCase(1, 2, TasCapacitySizing.SizedEverySimulation)]
        [TestCase(0, 0, TasCapacitySizing.Fixed)]
        [TestCase(0, 2, TasCapacitySizing.Fixed)]
        [TestCase(1, 0, TasCapacitySizing.Fixed)]
        [TestCase(3, 1, TasCapacitySizing.Unrecognised)]
        [TestCase(1, 4, TasCapacitySizing.Unrecognised)]
        [TestCase(-1, 1, TasCapacitySizing.Unrecognised)]
        [TestCase(99, 99, TasCapacitySizing.Unrecognised)]
        public void One_zone_decides_by_its_heating_and_cooling_sizing(int heating, int cooling, TasCapacitySizing expected)
        {
            Assert.That(Query.CapacitySizing(new[] { new TasZoneSizing("Z", heating, 100, cooling, 100) }), Is.EqualTo(expected));
        }

        [Test]
        public void A_fixed_capacity_of_zero_is_fixed_too()
        {
            // SAM_Tas' system sizing method writes tbdNoSizing with 0; a later Energy Simulation may size it differently.
            Assert.That(Query.CapacitySizing(new[] { new TasZoneSizing("Z", 0, 0, 0, 0) }), Is.EqualTo(TasCapacitySizing.Fixed));
        }

        [Test]
        public void One_fixed_zone_among_sized_ones_fixes_the_building_and_fixed_wins_over_unrecognised()
        {
            Assert.That(Query.CapacitySizing(new[] { new TasZoneSizing("A", 1, 1, 1, 1), new TasZoneSizing("B", 2, 1, 2, 1), new TasZoneSizing("C", 0, 5, 1, 1) }), Is.EqualTo(TasCapacitySizing.Fixed));
            Assert.That(Query.CapacitySizing(new[] { new TasZoneSizing("A", 3, 1, 3, 1), new TasZoneSizing("B", 0, 5, 0, 5) }), Is.EqualTo(TasCapacitySizing.Fixed));
        }

        [Test]
        public void Not_read_or_no_zone_is_unknown()
        {
            Assert.That(Query.CapacitySizing(null), Is.EqualTo(TasCapacitySizing.Unknown));
            Assert.That(Query.CapacitySizing(new TasZoneSizing[0]), Is.EqualTo(TasCapacitySizing.Unknown));
            Assert.That(new TasModelInventory("a.tbd", null, null).CapacitySizing, Is.EqualTo(TasCapacitySizing.Unknown));
        }

        [Test]
        public void The_measured_fixtures_have_the_measured_states()
        {
            Assert.That(TasModelFixtures.ModelASizingOn().CapacitySizing, Is.EqualTo(TasCapacitySizing.Fixed));
            Assert.That(TasModelFixtures.ModelASizingOff().CapacitySizing, Is.EqualTo(TasCapacitySizing.SizedEverySimulation));
            Assert.That(TasModelFixtures.Demo().CapacitySizing, Is.EqualTo(TasCapacitySizing.SizedEverySimulation));
        }

        // ---- why, in words

        [Test]
        public void The_refusal_names_the_zones_the_cause_the_controllers_and_the_Sizing_off_limitation()
        {
            string refusal = Query.TasModelSizingRefusal(TasModelFixtures.ModelASizingOn());
            Assert.That(refusal, Does.StartWith("Zone setpoints and glazing cannot be optimised on this model: its TBD holds zone heating/cooling capacities fixed by Energy Simulation's Sizing (2 zones: “Cell 1”, “Cell 2”)."));
            Assert.That(refusal, Does.Contain("the next Energy Simulation sizes them again for the changed design, so the optimised result would not hold."));
            Assert.That(refusal, Does.Contain("Plant controllers can still be optimised."));
            Assert.That(refusal, Does.EndWith("run Energy Simulation with Sizing off first: Tas then sizes the plant within every simulation, without a design-day limit, which is a different design problem."));
        }

        [Test]
        public void Unknown_and_unrecognised_sizing_are_refused_in_their_own_words()
        {
            Assert.That(Query.TasModelSizingRefusal(new TasModelInventory("a.tbd", null, null)), Does.Contain("the sizing of its TBD zones is not known (not read)"));
            Assert.That(Query.TasModelSizingRefusal(With(new TasModelInventory("a.tbd", null, null))), Does.Contain("(the TBD has no zone)"));
            Assert.That(Query.TasModelSizingRefusal(With(new TasModelInventory("a.tbd", null, null), (1, 1), (3, 1))), Does.Contain("the sizing of zone “Zone 2” (tbdMixed) has not been measured"));
        }

        [Test]
        public void Zones_sized_in_every_simulation_give_no_refusal()
        {
            Assert.That(Query.TasModelSizingRefusal(TasModelFixtures.Demo()), Is.Null);
            Assert.That(Query.TasModelSizingRefusal(TasModelFixtures.ModelASizingOff()), Is.Null);
        }

        [Test]
        public void Many_fixed_zones_are_listed_as_three_and_a_count()
        {
            TasModelInventory inventory = With(new TasModelInventory("a.tbd", null, null), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0));
            Assert.That(Query.TasModelSizingRefusal(inventory), Does.Contain("(5 zones: “Zone 1”, “Zone 2”, “Zone 3” and 2 more)"));
        }

        // ---- target discovery

        [Test]
        public void Sizing_on_offers_no_setpoint_or_glazing_but_still_every_measure()
        {
            OptimisationCatalogue on = Query.TasModelCatalogue(TasModelFixtures.ModelASizingOn(), TasModelFixtures.Pool());
            Assert.That(on.Variables, Is.Empty);
            Assert.That(on.Outputs.Select(x => x.Name), Is.EqualTo(new[] { "Annual heating demand", "Annual cooling demand", "Overheating hours" }));
        }

        [Test]
        public void The_catalogue_says_why_in_its_source_which_the_AI_text_shows()
        {
            Assert.That(Query.TasModelCatalogue(TasModelFixtures.ModelASizingOn()).Source, Is.EqualTo("read from the Tas model. " + Query.TasModelSizingRefusal(TasModelFixtures.ModelASizingOn())));
            Assert.That(Query.TasModelCatalogue(TasModelFixtures.ModelASizingOff()).Source, Is.EqualTo("read from the Tas model"));
            Assert.That(Query.TasModelCatalogue(TasModelFixtures.Demo()).Source, Is.EqualTo("read from the Tas model"));
            Assert.That(Query.TasModelCatalogue(new TasModelInventory(null, "a.tsd", null)).Source, Is.EqualTo("read from the Tas model"), "no TBD, nothing to refuse");
        }

        [Test]
        public void Sizing_off_offers_the_setpoints_and_the_glazing()
        {
            OptimisationCatalogue off = Query.TasModelCatalogue(TasModelFixtures.ModelASizingOff(), TasModelFixtures.Pool());
            Assert.That(off.Variables.Select(x => x.Name), Is.SupersetOf(new[] { "Cell 1 heating setpoint", "Cell 1 cooling setpoint", "Cell 2 heating setpoint", "Cell 2 cooling setpoint" }));
            Assert.That(off.Variables.Count(x => x.Target.Kind == TasModelKind.GlazingChoice), Is.EqualTo(1));
        }

        [Test]
        public void The_Systems_Demo_keeps_its_setpoints_and_controllers()
        {
            OptimisationCatalogue demo = Query.TasModelCatalogue(TasModelFixtures.Demo());
            Assert.That(demo.Variables.Select(x => x.Target.Kind).Distinct(), Is.EquivalentTo(new[] { TasModelKind.HeatingSetpoint, TasModelKind.CoolingSetpoint, TasModelKind.ControllerSetpoint }));
        }

        [TestCase(0)]
        [TestCase(3)]
        [TestCase(-1)]
        public void With_fixed_or_unmeasured_sizing_only_the_plant_controllers_are_offered(int sizing)
        {
            TasModelInventory inventory = With(TasModelFixtures.Demo(), (1, 1), (sizing, 1));
            OptimisationCatalogue catalogue = Query.TasModelCatalogue(inventory);
            Assert.That(catalogue.Variables.Select(x => x.Name), Is.EqualTo(new[] { "HeatPumpController setpoint (Plant Room)", "Controller 2 setpoint (Plant Room)" }));
            Assert.That(catalogue.Outputs.Select(x => x.Measure.Kind), Does.Contain(TasModelKind.AnnualPlantEnergy));
        }

        [Test]
        public void Without_the_zones_sizing_only_the_plant_controllers_are_offered()
        {
            TasModelInventory inventory = TasModelFixtures.Demo();
            inventory.ZoneSizing = null;
            Assert.That(Query.TasModelCatalogue(inventory).Variables.All(x => x.Target.Kind == TasModelKind.ControllerSetpoint), Is.True);
        }

        // ---- definition validation (a direct caller of the engine)

        private static TasModelRunner Runner(TestFolder folder, string json, TasModelInventory inventory)
        {
            return new TasModelRunner(TasModelFixtures.Read(json), new TasModelRunSettings(folder.Folder("project")) { Inventory = inventory, GlazingPool = TasModelFixtures.Pool() });
        }

        [Test]
        public void A_setpoint_target_on_a_sized_model_is_refused_as_OPT617_before_the_catalogue_check()
        {
            using (TestFolder folder = new TestFolder())
            {
                TasOptimisationDefinitionException exception = Assert.Throws<TasOptimisationDefinitionException>(() => Runner(folder, HeatingOnly, TasModelFixtures.ModelASizingOn()));
                OptimisationDiagnostic diagnostic = exception.Diagnostics.Single();
                Assert.That(diagnostic.Code, Is.EqualTo(TasModelRunner.SizingRefusalCode).And.EqualTo("OPT617"));
                Assert.That(diagnostic.Path, Is.EqualTo("variables[0].target"));
                Assert.That(diagnostic.Message, Does.StartWith("“Cell 1 heating” changes the TBD. Zone setpoints and glazing cannot be optimised on this model"));
                Assert.That(diagnostic.Hint, Does.Contain("Sizing off"));
            }
        }

        [Test]
        public void The_same_definition_runs_on_the_model_simulated_with_Sizing_off()
        {
            using (TestFolder folder = new TestFolder())
            {
                TasModelRunner runner = Runner(folder, HeatingOnly, TasModelFixtures.ModelASizingOff());
                Assert.That(runner.ScriptText, Does.Contain("RefuseFixedCapacities(tbdBuilding);"));
            }
        }

        [Test]
        public void A_glazing_target_on_a_sized_model_is_refused()
        {
            using (TestFolder folder = new TestFolder())
            {
                TasModelInventory inventory = TasModelFixtures.SamModel();
                inventory.ZoneSizing = new[] { new TasZoneSizing("Kitchen", TasZoneSizing.NoSizing, 1500, TasZoneSizing.NoSizing, 2500) };
                TasOptimisationDefinitionException exception = Assert.Throws<TasOptimisationDefinitionException>(() => Runner(folder, TasScriptTests.Glazing, inventory));
                Assert.That(exception.Diagnostics.Select(x => x.Code + " " + x.Path), Is.EqualTo(new[] { "OPT617 variables[0].target" }));
            }
        }

        [Test]
        public void A_mixed_definition_is_refused_for_its_TBD_variable_only()
        {
            using (TestFolder folder = new TestFolder())
            {
                TasModelInventory inventory = With(TasModelFixtures.Demo(), (0, 0));
                TasOptimisationDefinitionException exception = Assert.Throws<TasOptimisationDefinitionException>(() => Runner(folder, TasScriptTests.Everything, inventory));
                Assert.That(exception.Diagnostics.Select(x => x.Code + " " + x.Path), Is.EqualTo(new[] { "OPT617 variables[0].target" }), "the controller (variables[1]) is not refused");
            }
        }

        [Test]
        public void A_controller_only_definition_runs_on_a_sized_model_and_its_script_has_no_TBD_check()
        {
            using (TestFolder folder = new TestFolder())
            {
                TasModelRunner runner = Runner(folder, TasScriptTests.Controller, With(TasModelFixtures.Demo(), (0, 0)));
                Assert.That(runner.Definition.Variables.Single().Target.Kind, Is.EqualTo(TasModelKind.ControllerSetpoint));
                Assert.That(runner.ScriptText, Does.Not.Contain("RefuseFixedCapacities"));
            }
        }

        [Test]
        public void An_inventory_without_the_zones_sizing_refuses_TBD_targets()
        {
            using (TestFolder folder = new TestFolder())
            {
                TasModelInventory inventory = TasModelFixtures.Demo();
                inventory.ZoneSizing = null;
                TasOptimisationDefinitionException exception = Assert.Throws<TasOptimisationDefinitionException>(() => Runner(folder, TasScriptTests.Setpoints, inventory));
                Assert.That(exception.Diagnostics.Select(x => x.Code + " " + x.Path), Is.EqualTo(new[] { "OPT617 variables[0].target", "OPT617 variables[1].target" }));
                Assert.That(exception.Diagnostics[0].Message, Does.Contain("is not known (not read)"));
            }
        }

        // ---- execution: the generated script checks the TBD itself

        [Test]
        public void Every_script_with_a_TBD_target_checks_the_TBD_before_its_first_edit()
        {
            foreach ((string name, string json) in new[] { ("setpoints", TasScriptTests.Setpoints), ("glazing", TasScriptTests.Glazing), ("everything", TasScriptTests.Everything) })
            {
                string script = TasScriptTests.Script(name, json);
                int check = script.IndexOf("RefuseFixedCapacities(tbdBuilding);", StringComparison.Ordinal);
                int edit = new[] { script.IndexOf("SetSetpoint(tbdBuilding", StringComparison.Ordinal), script.IndexOf("SetGlazing(tbdBuilding", StringComparison.Ordinal) }.Where(x => x >= 0).Min();
                Assert.That(check, Is.GreaterThan(0).And.LessThan(edit), name);
            }

            Assert.That(TasScriptTests.Script("controller", TasScriptTests.Controller), Does.Not.Contain("RefuseFixedCapacities"));
        }

        /// <summary>The script's own check, run on stand-in zones: it decides exactly as <see cref="Query.CapacitySizing"/>.</summary>
        [TestCase(1, 1, true)]
        [TestCase(2, 2, true)]
        [TestCase(1, 2, true)]
        [TestCase(0, 1, false)]
        [TestCase(1, 0, false)]
        [TestCase(3, 1, false)]
        [TestCase(1, 4, false)]
        public void The_scripts_check_accepts_only_zones_sized_in_every_simulation(int heating, int cooling, bool accepted)
        {
            string script = TasScriptTests.Script("setpoints", TasScriptTests.Setpoints);
            TestDelegate run = () => StandInTbdProbe.Run(script, "RefuseFixedCapacities", new[] { (1, 1), (heating, cooling) });
            if (accepted)
            {
                Assert.That(run, Throws.Nothing);
            }
            else
            {
                Assert.That(run, Throws.Exception.With.Message.EqualTo("Zone setpoints and glazing are not changed: the TBD's zone capacities are not sized in every simulation (\"Zone 2\" (heating " + heating + ", cooling " + cooling + "); 0 = fixed by Energy Simulation's Sizing), so the result would not hold after the next Energy Simulation."));
            }

            Assert.That(Query.CapacitySizing(new[] { new TasZoneSizing("A", 1, 1, 1, 1), new TasZoneSizing("B", heating, 1, cooling, 1) }) == TasCapacitySizing.SizedEverySimulation, Is.EqualTo(accepted), "the script and the engine decide alike");
        }

        [Test]
        public void The_scripts_check_refuses_a_TBD_without_zones_and_lists_three_zones_and_a_count()
        {
            string script = TasScriptTests.Script("glazing", TasScriptTests.Glazing);
            Assert.That(() => StandInTbdProbe.Run(script, "RefuseFixedCapacities", new (int, int)[0]), Throws.Exception.With.Message.EqualTo("The TBD has no zone, so its sizing cannot be checked; zone setpoints and glazing are not changed."));
            Assert.That(() => StandInTbdProbe.Run(script, "RefuseFixedCapacities", Enumerable.Repeat((0, 0), 5)), Throws.Exception.With.Message.Contains("\"Zone 1\" (heating 0, cooling 0), \"Zone 2\" (heating 0, cooling 0), \"Zone 3\" (heating 0, cooling 0) and 2 more"));
        }
    }
}
