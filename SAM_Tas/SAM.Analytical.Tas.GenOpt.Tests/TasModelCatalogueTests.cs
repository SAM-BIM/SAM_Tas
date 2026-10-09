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
    /// PR7b: the "tas-model" catalogue built from an inventory (no Tas), and the glazing options of PR7a-2 answer 4 with
    /// the owner-approved defaults.
    /// </summary>
    [TestFixture]
    public class TasModelCatalogueTests
    {
        [Test]
        public void Demo_catalogue_lists_setpoints_per_internal_condition_controllers_and_every_measure()
        {
            OptimisationCatalogue catalogue = Query.TasModelCatalogue(TasModelFixtures.Demo());

            Assert.That(catalogue.HasBindings, Is.True);
            Assert.That(catalogue.Variables.Select(Line), Is.EqualTo(new[]
            {
                "Office Weekday heating setpoint|tbd.internal-condition.heating-setpoint|internalCondition=Office Weekday|20|16..24|°C",
                "Office Weekday cooling setpoint|tbd.internal-condition.cooling-setpoint|internalCondition=Office Weekday|24|21..28|°C",
                "Office Weekend heating setpoint|tbd.internal-condition.heating-setpoint|internalCondition=Office Weekend|12|12..24|°C",
                "Steady State Heating heating setpoint|tbd.internal-condition.heating-setpoint|internalCondition=Steady State Heating |21|16..24|°C",
                "HeatPumpController setpoint (Plant Room)|tpd.controller.setpoint|controller=HeatPumpController,plantRoom=Plant Room|3|..|°C",
                "Controller 2 setpoint (Plant Room)|tpd.controller.setpoint|controller=Controller 2,plantRoom=Plant Room|0.5|..|",
            }));

            Assert.That(catalogue.Outputs.Select(Line), Is.EqualTo(new[]
            {
                "Annual heating demand|tsd.annual-heating-demand||15227.8406637096|..|kWh",
                "Annual cooling demand|tsd.annual-cooling-demand||2978.53252598965|..|kWh",
                "Overheating hours|tsd.overheating-hours|||..|h",
                "Annual plant energy|tpd.annual-energy||27387.79|..|kWh",
                "Annual plant cost|tpd.annual-cost||7363.17|..|GBP",
                "Annual plant CO2|tpd.annual-co2||3932.89|..|kgCO2e",
            }));

            Assert.That(catalogue.Outputs.Single(x => x.Measure.Kind == TasModelKind.OverheatingHours).Measure.Parameters["threshold"], Is.EqualTo(28));
        }

        [Test]
        public void The_exact_internal_condition_name_is_kept_with_its_trailing_space()
        {
            OptimisationCatalogueEntry entry = Query.TasModelCatalogue(TasModelFixtures.Demo()).Variables.Single(x => x.Name.StartsWith("Steady"));
            Assert.That(entry.Target.Reference["internalCondition"], Is.EqualTo("Steady State Heating "));
        }

        [Test]
        public void Heating_maximum_stays_below_the_cooling_setpoint()
        {
            TasModelInventory inventory = new TasModelInventory("a.tbd", null, null, new[]
            {
                new TasInternalConditionInfo("Lab", null, 1, new TasSetpointProfile(TasSetpointProfileType.Value, 1, 19), new TasSetpointProfile(TasSetpointProfileType.Value, 1, 22)),
            });

            OptimisationCatalogueEntry heating = Query.TasModelCatalogue(inventory).Variables.Single(x => x.Target.Kind == TasModelKind.HeatingSetpoint);
            Assert.That(heating.Minimum, Is.EqualTo(16));
            Assert.That(heating.Maximum, Is.EqualTo(22));
        }

        [Test]
        public void Sam_model_skips_design_day_no_heating_no_cooling_factor_and_unsupported_profiles()
        {
            OptimisationCatalogue catalogue = Query.TasModelCatalogue(TasModelFixtures.SamModel());

            Assert.That(catalogue.Variables.Select(x => x.Name), Is.EqualTo(new[] { "Kitchen heating setpoint", "Kitchen cooling setpoint" }));
            Assert.That(catalogue.Variables[0].Description, Does.Contain("24-hour profile; the 12 hours at the setpoint change"));
            Assert.That(catalogue.Variables[0].Description, Does.Contain("Residential Kitchen - Kitchen"));
        }

        [Test]
        public void Without_a_tpd_there_is_no_plant_target_or_measure()
        {
            OptimisationCatalogue catalogue = Query.TasModelCatalogue(TasModelFixtures.SamModel());
            Assert.That(catalogue.Variables.Any(x => x.Target.Kind == TasModelKind.ControllerSetpoint), Is.False);
            Assert.That(catalogue.Outputs.Select(x => x.Measure.Kind), Is.EqualTo(new[] { TasModelKind.AnnualHeatingDemand, TasModelKind.AnnualCoolingDemand, TasModelKind.OverheatingHours }));
        }

        [Test]
        public void Cost_is_offered_only_in_pounds()
        {
            TasModelInventory inventory = TasModelFixtures.Demo();
            inventory.TpdCostUnit = "€";
            Assert.That(Query.TasModelCatalogue(inventory).Outputs.Any(x => x.Measure.Kind == TasModelKind.AnnualPlantCost), Is.False);
            inventory.TpdCostUnit = null;
            Assert.That(Query.TasModelCatalogue(inventory).Outputs.Any(x => x.Measure.Kind == TasModelKind.AnnualPlantCost), Is.False);
        }

        [Test]
        public void A_tsd_alone_offers_building_measures_but_no_target_and_no_plant()
        {
            TasModelInventory inventory = new TasModelInventory(null, "a.tsd", "a.tpd", TasModelFixtures.Demo().InternalConditions, null, TasModelFixtures.Demo().PlantRooms);
            OptimisationCatalogue catalogue = Query.TasModelCatalogue(inventory);
            Assert.That(catalogue.Variables, Is.Empty);
            Assert.That(catalogue.Outputs.Select(x => x.Measure.Kind), Is.EqualTo(new[] { TasModelKind.AnnualHeatingDemand, TasModelKind.AnnualCoolingDemand, TasModelKind.OverheatingHours }));
        }

        [Test]
        public void Glazing_choice_is_listed_only_with_a_pool_and_carries_every_option_in_its_description()
        {
            Assert.That(Query.TasModelCatalogue(TasModelFixtures.SamModel()).Variables.Any(x => x.Target.Kind == TasModelKind.GlazingChoice), Is.False);

            OptimisationCatalogueEntry entry = Query.TasModelCatalogue(TasModelFixtures.SamModel(), TasModelFixtures.Pool()).Variables.Single(x => x.Target.Kind == TasModelKind.GlazingChoice);
            Assert.That(entry.Name, Is.EqualTo("Glazing system (Windows: SIM_EXT_GLZ -pane)"));
            Assert.That(entry.Target.Reference["glazingConstruction"], Is.EqualTo(TasModelFixtures.SamGlazing));
            Assert.That(entry.Options, Is.EqualTo(new[] { TasModelFixtures.SamGlazing, "Triple low-e" }));
            Assert.That(entry.Value, Is.EqualTo(1));
            Assert.That(entry.Minimum, Is.EqualTo(1));
            Assert.That(entry.Maximum, Is.EqualTo(2));
            Assert.That(entry.Description, Does.Contain("2 building elements"));
            Assert.That(entry.Description, Does.Contain("1 Windows: SIM_EXT_GLZ -pane: g 0.400, Ug 1.24, light 0.804 (Model (current))"));
            Assert.That(entry.Description, Does.Contain("2 Triple low-e: g 0.369, Ug 1.00, light 0.728 (My glazing systems)"));
        }

        [Test]
        public void Glazing_options_follow_the_owner_defaults()
        {
            // SAM model glazing: g 0.400, U 1.243, light 0.804 -> Ug <= 1.543, light >= 0.704.
            List<TasGlazingOption> options = Query.TasGlazingOptions(TasModelFixtures.SamModel().GlazingConstructions[0], TasModelFixtures.Pool());

            Assert.That(options.Select(x => x.Number + " " + x.Text), Is.EqualTo(new[]
            {
                "1 Windows: SIM_EXT_GLZ -pane",
                "2 Triple low-e",
            }));
            Assert.That(options[0].IsCurrent, Is.True);
            Assert.That(options[0].PaneConstruction, Is.EqualTo(TasModelFixtures.SamGlazing));
            Assert.That(options[1].System.Guid.ToString(), Does.EndWith("a5191c"));
            Assert.That(options[1].PaneConstruction, Is.Null, "no aperture construction attached: cannot be written");
        }

        [Test]
        public void Glazing_option_filter_rules_one_by_one()
        {
            TasGlazingConstructionInfo current = new TasGlazingConstructionInfo("Current", new[] { "E1" }, 0.4, 1.2, 0.8);
            List<TasGlazingSystem> pool = new List<TasGlazingSystem>
            {
                TasModelFixtures.System("Same as current", "Model", 0.4004, 1.2004, 0.8004, "00000000-0000-0000-0000-000000000001"),
                TasModelFixtures.System("U at the limit", "Library", 0.30, 1.5, 0.8, "00000000-0000-0000-0000-000000000002"),
                TasModelFixtures.System("U above the limit", "Library", 0.31, 1.5011, 0.8, "00000000-0000-0000-0000-000000000003"),
                TasModelFixtures.System("Light at the limit", "Library", 0.32, 1.0, 0.7, "00000000-0000-0000-0000-000000000004"),
                TasModelFixtures.System("Light below the limit", "Library", 0.33, 1.0, 0.6989, "00000000-0000-0000-0000-000000000005"),
                TasModelFixtures.System("Opaque", "Library", 0.34, 1.0, 0.8, "00000000-0000-0000-0000-000000000006", transparent: false),
                TasModelFixtures.System("Door", "Library", 0.35, 1.0, 0.8, "00000000-0000-0000-0000-000000000007", apertureType: "Door"),
                TasModelFixtures.System("Duplicate of U at the limit", "User", 0.3001, 1.4999, 0.8001, "00000000-0000-0000-0000-000000000008"),
                TasModelFixtures.System("No values", "User", double.NaN, 1.0, 0.8, "00000000-0000-0000-0000-000000000009"),
                TasModelFixtures.System("High g", "User", 0.6, 1.0, 0.8, "00000000-0000-0000-0000-00000000000a"),
            };

            Assert.That(Query.TasGlazingOptions(current, pool).Select(x => x.Text), Is.EqualTo(new[] { "Current", "U at the limit", "Light at the limit", "High g" }));

            TasGlazingFilter range = new TasGlazingFilter { MinimumG = 0.31, MaximumG = 0.59 };
            Assert.That(Query.TasGlazingOptions(current, pool, range).Select(x => x.Text), Is.EqualTo(new[] { "Current", "Light at the limit" }));

            TasGlazingFilter strict = new TasGlazingFilter { UgAllowance = 0, LightAllowance = 0 };
            Assert.That(Query.TasGlazingOptions(current, pool, strict).Select(x => x.Text), Is.EqualTo(new[] { "Current", "High g" }));
        }

        [Test]
        public void At_most_eight_options_spread_over_the_g_order()
        {
            TasGlazingConstructionInfo current = new TasGlazingConstructionInfo("Current", new[] { "E1" }, 0.5, 1.0, 0.7);
            List<TasGlazingSystem> pool = Enumerable.Range(0, 20).Select(i => TasModelFixtures.System("S" + i.ToString("00"), "User", 0.1 + i * 0.03, 1.0, 0.7, "00000000-0000-0000-0000-0000000001" + i.ToString("00"))).ToList();

            List<TasGlazingOption> options = Query.TasGlazingOptions(current, pool);
            Assert.That(options.Count, Is.EqualTo(8));
            Assert.That(options.Select(x => x.Text), Is.EqualTo(new[] { "Current", "S00", "S03", "S06", "S10", "S13", "S16", "S19" }));
            Assert.That(options.Skip(1).Select(x => x.G), Is.Ordered);
            Assert.That(options.Select(x => x.Number), Is.EqualTo(Enumerable.Range(1, 8)));

            Assert.That(Query.TasGlazingOptions(current, pool, new TasGlazingFilter { MaximumOptions = 3 }).Select(x => x.Text), Is.EqualTo(new[] { "Current", "S00", "S19" }));
            Assert.That(Query.TasGlazingOptions(current, pool, new TasGlazingFilter { MaximumOptions = 1 }).Select(x => x.Text), Is.EqualTo(new[] { "Current" }));
        }

        [Test]
        public void Shared_names_get_the_short_id()
        {
            TasGlazingConstructionInfo current = new TasGlazingConstructionInfo("SIM_EXT_GLZ", new[] { "E1" }, 0.4, 1.2, 0.8);
            List<TasGlazingSystem> pool = new List<TasGlazingSystem>
            {
                TasModelFixtures.System("SIM_EXT_GLZ", "Library", 0.41, 1.3, 0.8, "00000000-0000-0000-0000-000000b9d885"),
                TasModelFixtures.System("Twin", "Library", 0.42, 1.3, 0.8, "00000000-0000-0000-0000-000000aaaaaa"),
                TasModelFixtures.System("Twin", "User", 0.43, 1.3, 0.8, "00000000-0000-0000-0000-000000bbbbbb"),
                TasModelFixtures.System("Door twin", "Library", 0.44, 1.3, 0.8, "00000000-0000-0000-0000-000000cccccc"),
                TasModelFixtures.System("Door twin", "Library", 0.45, 1.3, 0.8, "00000000-0000-0000-0000-000000dddddd", apertureType: "Door"),
                TasModelFixtures.System("Opaque twin", "Library", 0.46, 1.3, 0.8, "00000000-0000-0000-0000-000000eeeeee"),
                TasModelFixtures.System("Opaque twin", "Library", 0.47, 1.3, 0.8, "00000000-0000-0000-0000-000000ffffff", transparent: false),
            };

            Assert.That(Query.TasGlazingOptions(current, pool).Select(x => x.Text), Is.EqualTo(new[] { "SIM_EXT_GLZ", "SIM_EXT_GLZ b9d885", "Twin aaaaaa", "Twin bbbbbb", "Door twin", "Opaque twin" }));
        }

        private static string Line(OptimisationCatalogueEntry entry)
        {
            OptimisationBinding binding = (OptimisationBinding)entry.Target ?? entry.Measure;
            string reference = string.Join(",", binding.Reference.OrderBy(x => x.Key, System.StringComparer.Ordinal).Select(x => x.Key + "=" + x.Value));
            return string.Join("|", entry.Name, binding.Kind, reference, entry.Value?.ToString(System.Globalization.CultureInfo.InvariantCulture), entry.Minimum?.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".." + entry.Maximum?.ToString(System.Globalization.CultureInfo.InvariantCulture), entry.Unit);
        }
    }
}
