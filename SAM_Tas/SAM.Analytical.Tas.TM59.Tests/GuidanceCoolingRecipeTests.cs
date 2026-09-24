// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical.Enums;
using SAM.Analytical.Systems;
using SAM.Analytical.Tas.TPD;
using System;
using System.Linq;

namespace SAM.Analytical.Tas.TM59.Tests
{
    /// <summary>
    /// SAM#123, COM-free: what the manufacturer-guidance grounding resolves from a unit's strategy before it
    /// writes anything to TAS - only the carriers the Stage 11 prototype proved are accepted, and the exchanger
    /// state cells are the stated rules on a grid that puts every threshold on a breakpoint. Fixture values only.
    /// </summary>
    [TestFixture]
    public class GuidanceCoolingRecipeTests
    {
        [Test]
        public void AProvenStrategy_ResolvesToTheRecipe()
        {
            Assert.That(TPD.Modify.TryGetGuidanceRecipe(GuidanceCooling(), out TPD.Modify.GuidanceRecipe recipe, out string refusal), Is.True, refusal);

            Assert.That(recipe.Elevated_Lps, Is.EqualTo(80.0));
            Assert.That(recipe.IntakeOffset_K, Is.EqualTo(14.0).Within(1e-9));
            Assert.That(recipe.DesignSupply_Lps, Is.EqualTo(25.0));
            Assert.That(recipe.DesignExtract_Lps, Is.EqualTo(25.0));
            Assert.That(recipe.CoolingDuty_W, Is.EqualTo(2000.0).Within(1e-9));
            Assert.That(recipe.ExtractFraction, Is.EqualTo(0.8));
        }

        [Test]
        public void TheStateCells_AreTheStatedRules()
        {
            TPD.Modify.TryGetGuidanceRecipe(GuidanceCooling(), out TPD.Modify.GuidanceRecipe recipe, out _);

            //Background: bypass needs extract <= activation, intake > 12, extract > intake and extract > 18.
            Assert.That(recipe.BackgroundEfficiency(14.0, 20.0), Is.EqualTo(0.0));
            Assert.That(recipe.BackgroundEfficiency(12.0, 20.0), Is.EqualTo(0.8));
            Assert.That(recipe.BackgroundEfficiency(14.0, 22.01), Is.EqualTo(0.8));
            Assert.That(recipe.BackgroundEfficiency(14.0, 18.0), Is.EqualTo(0.8));
            Assert.That(recipe.BackgroundEfficiency(-2.0, 17.0), Is.EqualTo(0.8));

            //Cooling: coolth recovery where the intake is warmer than the extract, otherwise bypass.
            Assert.That(recipe.CoolingEfficiency(30.0, 25.0), Is.EqualTo(0.8));
            Assert.That(recipe.CoolingEfficiency(18.0, 25.0), Is.EqualTo(0.0));
        }

        [Test]
        public void EveryThreshold_SitsOnABreakpoint()
        {
            TPD.Modify.TryGetGuidanceRecipe(GuidanceCooling(), out TPD.Modify.GuidanceRecipe recipe, out _);

            foreach (double value in new[] { 12.0, 12.1, 22.0, 30.0 })
            {
                Assert.That(recipe.Intakes_C.Any(x => Math.Abs(x - value) < 1e-9), Is.True, "intake " + value);
            }

            foreach (double value in new[] { 18.0, 18.01, 22.0, 22.01, 30.0 })
            {
                Assert.That(recipe.Extracts_C.Any(x => Math.Abs(x - value) < 1e-9), Is.True, "extract " + value);
            }

            Assert.That(recipe.Intakes_C, Is.Ordered.Ascending);
            Assert.That(recipe.Extracts_C, Is.Ordered.Ascending);
            Assert.That(recipe.Intakes_C.Distinct().Count(), Is.EqualTo(recipe.Intakes_C.Length));
            Assert.That(recipe.Extracts_C.Distinct().Count(), Is.EqualTo(recipe.Extracts_C.Length));
        }

        [Test]
        public void AnExtractSwitchedStrategy_IsRefused()
        {
            MechanicalVentilationGuidanceCooling guidanceCooling = GuidanceCooling(strategy => strategy.CoolingActivationSignal = CoolingActivationSignal.ExtractTemperature);

            Assert.That(TPD.Modify.TryGetGuidanceRecipe(guidanceCooling, out _, out string refusal), Is.False);
            Assert.That(refusal, Does.Contain("room cooling-stat"));
        }

        [Test]
        public void ATableCoolingRule_IsRefused()
        {
            MechanicalVentilationGuidanceCooling guidanceCooling = GuidanceCooling(strategy => strategy.CoolingSupplyTemperatureRule = SupplyTemperatureRule.PerformanceTable());

            Assert.That(TPD.Modify.TryGetGuidanceRecipe(guidanceCooling, out _, out string refusal), Is.False);
            Assert.That(refusal, Does.Contain("intake offset"));
        }

        [Test]
        public void AnElevatedAirflowOutsideTheStatedOffsets_IsRefused()
        {
            MechanicalVentilationGuidanceCooling guidanceCooling = GuidanceCooling(strategy => strategy.CoolingSupplyTemperatureRule = SupplyTemperatureRule.IntakeOffset(new[] { 90.0, 100.0 }, new[] { 14.0, 13.0 }));

            Assert.That(TPD.Modify.TryGetGuidanceRecipe(guidanceCooling, out _, out string refusal), Is.False);
            Assert.That(refusal, Does.Contain("no intake offset"));
        }

        [Test]
        public void NoStatedCapacity_IsRefused()
        {
            MechanicalVentilationGuidanceCooling guidanceCooling = GuidanceCooling(null, withCapacity: false);

            Assert.That(TPD.Modify.TryGetGuidanceRecipe(guidanceCooling, out _, out string refusal), Is.False);
            Assert.That(refusal, Does.Contain("capacity"));
        }

        private static MechanicalVentilationGuidanceCooling GuidanceCooling(Action<VentilationUnitOperatingStrategy> edit = null, bool withCapacity = true)
        {
            VentilationUnitOperatingStrategy strategy = new VentilationUnitOperatingStrategy
            {
                Source = "Test Fixture, manufacturer modelling guidance, v.1 - not a real product",
                CoolingActivationTemperature_C = 22.0,
                CoolingActivationSignal = CoolingActivationSignal.RoomTemperature,
                MinimumCoolingActivationTemperature_C = 22.0,
                MaximumCoolingActivationTemperature_C = 25.0,
                BypassMinimumIntakeTemperature_C = 12.0,
                BypassMinimumExtractTemperature_C = 18.0,
                ElevatedAirFlow_Lps = 80.0,
                MinimumElevatedAirFlow_Lps = 70.0,
                MaximumElevatedAirFlow_Lps = 90.0,
                SummerBypassSupplyTemperatureRule = SupplyTemperatureRule.OutdoorAir(),
                HeatCoolthRecoverySupplyTemperatureRule = SupplyTemperatureRule.LinearBlend(0.8),
                CoolingSupplyTemperatureRule = SupplyTemperatureRule.IntakeOffset(new[] { 70.0, 80.0, 90.0 }, new[] { 15.0, 14.0, 13.0 }),
            };

            edit?.Invoke(strategy);

            VentilationUnitPerformanceTable table = new VentilationUnitPerformanceTable(
                new[]
                {
                    new VentilationUnitPerformanceAxis(VentilationUnitPerformanceAxis.Name_ExternalDryBulbTemperature, "degC", new double[] { 29, 34 }),
                    new VentilationUnitPerformanceAxis(VentilationUnitPerformanceAxis.Name_EnteringDryBulbTemperature, "degC", new double[] { 23, 26 }),
                    new VentilationUnitPerformanceAxis(VentilationUnitPerformanceAxis.Name_AirFlowRate, "l/s", new double[] { 50, 120 }),
                },
                withCapacity
                    ? new[]
                    {
                        new VentilationUnitPerformanceOutput(VentilationUnitPerformanceOutput.Name_SupplyAirTemperature, "degC", new double[] { 15, 16, 17, 18, 16, 17, 18, 19 }),
                        new VentilationUnitPerformanceOutput(VentilationUnitPerformanceOutput.Name_CombinedCoolingCapacity, "kW", new double[] { 1.0, 1.5, 1.2, 1.7, 1.4, 2.0, 1.3, 1.9 }),
                    }
                    : new[]
                    {
                        new VentilationUnitPerformanceOutput(VentilationUnitPerformanceOutput.Name_SupplyAirTemperature, "degC", new double[] { 15, 16, 17, 18, 16, 17, 18, 19 }),
                    });

            MechanicalVentilationGuidanceSettings settings = new MechanicalVentilationGuidanceSettings
            {
                OperatingStrategy = strategy,
                SupplyAirTemperatureTable = table,
                SourceIdentifier = "Test Fixture",
            };

            Guid guid_Stat = Guid.NewGuid();

            return new MechanicalVentilationGuidanceCooling(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                guid_Stat,
                new[]
                {
                    new MechanicalVentilationGuidanceRoom(guid_Stat, Guid.NewGuid(), 13.0, 0.0, 41.6, 0.0),
                    new MechanicalVentilationGuidanceRoom(Guid.NewGuid(), Guid.NewGuid(), 12.0, 0.0, 38.4, 0.0),
                    new MechanicalVentilationGuidanceRoom(Guid.NewGuid(), Guid.NewGuid(), 0.0, 15.0, 0.0, 48.0),
                    new MechanicalVentilationGuidanceRoom(Guid.NewGuid(), Guid.NewGuid(), 0.0, 10.0, 0.0, 32.0),
                },
                settings);
        }
    }
}
