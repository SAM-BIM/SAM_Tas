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
            Assert.That(recipe.CoolingExtractFraction, Is.EqualTo(0.8576).Within(1e-9));
            Assert.That(recipe.CoilNetDrop_K, Is.EqualTo(8.245).Within(1e-9));
            Assert.That(recipe.MinimumSupply_C, Is.EqualTo(13.0));
            Assert.That(recipe.DesignSupply_Lps, Is.EqualTo(25.0));
            Assert.That(recipe.DesignExtract_Lps, Is.EqualTo(25.0));
            Assert.That(recipe.CoolingDuty_W, Is.EqualTo(2000.0).Within(1e-9));
            Assert.That(recipe.ExtractFraction, Is.EqualTo(0.8));
        }

        [Test]
        public void TheStateCells_AreTheStatedRules()
        {
            TPD.Modify.TryGetGuidanceRecipe(GuidanceCooling(), out TPD.Modify.GuidanceRecipe recipe, out _);

            //Background: the unit's own bypass - intake > 12, extract > intake and extract > 19, independent of the
            //cooling-stat (a warm extract with the stat satisfied still bypasses) - else the background fraction.
            Assert.That(recipe.BackgroundEfficiency(14.0, 20.0), Is.EqualTo(0.0));
            Assert.That(recipe.BackgroundEfficiency(12.0, 20.0), Is.EqualTo(0.8));
            Assert.That(recipe.BackgroundEfficiency(14.0, 22.01), Is.EqualTo(0.0));
            Assert.That(recipe.BackgroundEfficiency(14.0, 19.0), Is.EqualTo(0.8));
            Assert.That(recipe.BackgroundEfficiency(14.0, 19.01), Is.EqualTo(0.0));
            Assert.That(recipe.BackgroundEfficiency(-2.0, 17.0), Is.EqualTo(0.8));

            //Cooling: the same bypass decision, otherwise heat/coolth recovery at the elevated-airflow fraction.
            Assert.That(recipe.CoolingEfficiency(30.0, 25.0), Is.EqualTo(0.8576).Within(1e-9));
            Assert.That(recipe.CoolingEfficiency(18.0, 25.0), Is.EqualTo(0.0));
            Assert.That(recipe.CoolingEfficiency(10.0, 25.0), Is.EqualTo(0.8576).Within(1e-9));
        }

        [Test]
        public void TheSupplyLaw_IsTheNetDropFloored_WithTheKinkOnTheGrid()
        {
            TPD.Modify.TryGetGuidanceRecipe(GuidanceCooling(), out TPD.Modify.GuidanceRecipe recipe, out _);

            Assert.That(recipe.SupplyLaw_C(30.0), Is.EqualTo(30.0 - 8.245).Within(1e-9));
            Assert.That(recipe.SupplyLaw_C(13.0 + 8.245), Is.EqualTo(13.0).Within(1e-9));
            Assert.That(recipe.SupplyLaw_C(13.0 + 8.245 + 0.1), Is.EqualTo(13.1).Within(1e-9));
            Assert.That(recipe.SupplyLaw_C(15.0), Is.EqualTo(13.0));
            Assert.That(recipe.SupplyLawEntering_C, Is.EqualTo(new[] { -50.0, 13.0 + 8.245, 60.0 }));
        }

        [Test]
        public void NoStatedMinimum_IsAStraightLineTable()
        {
            MechanicalVentilationGuidanceCooling guidanceCooling = GuidanceCooling(strategy => strategy.CoolingSupplyTemperatureRule = CoolingRule(double.NaN));

            Assert.That(TPD.Modify.TryGetGuidanceRecipe(guidanceCooling, out TPD.Modify.GuidanceRecipe recipe, out string refusal), Is.True, refusal);
            Assert.That(recipe.SupplyLawEntering_C, Is.EqualTo(new[] { -50.0, 60.0 }));
            Assert.That(recipe.SupplyLaw_C(15.0), Is.EqualTo(15.0 - 8.245).Within(1e-9));
        }

        [Test]
        public void EveryThreshold_SitsOnABreakpoint()
        {
            TPD.Modify.TryGetGuidanceRecipe(GuidanceCooling(), out TPD.Modify.GuidanceRecipe recipe, out _);

            foreach (double value in new[] { 12.0, 12.1, 22.0, 30.0 })
            {
                Assert.That(recipe.Intakes_C.Any(x => Math.Abs(x - value) < 1e-9), Is.True, "intake " + value);
            }

            foreach (double value in new[] { 19.0, 19.01, 22.0, 30.0 })
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
            Assert.That(refusal, Does.Contain("exchanger then coil"));
        }

        [Test]
        public void TheSupersededIntakeOffsetRule_IsRefused()
        {
            MechanicalVentilationGuidanceCooling guidanceCooling = GuidanceCooling(strategy => strategy.CoolingSupplyTemperatureRule = SupplyTemperatureRule.IntakeOffset(new[] { 70.0, 80.0, 90.0 }, new[] { 15.0, 14.0, 13.0 }));

            Assert.That(TPD.Modify.TryGetGuidanceRecipe(guidanceCooling, out _, out string refusal), Is.False);
            Assert.That(refusal, Does.Contain("exchanger then coil"));
        }

        [Test]
        public void AnElevatedAirflowOutsideTheStatedFigures_IsRefused()
        {
            MechanicalVentilationGuidanceCooling guidanceCooling = GuidanceCooling(strategy =>
            {
                strategy.CoolingSupplyTemperatureRule = SupplyTemperatureRule.ExchangerThenCoil(new[] { 90.0, 100.0 }, new[] { 0.85, 0.84 }, new[] { 8.5, 8.2 }, new[] { 0.6, 0.8 }, 13.0);
            });

            Assert.That(TPD.Modify.TryGetGuidanceRecipe(guidanceCooling, out _, out string refusal), Is.False);
            Assert.That(refusal, Does.Contain("no exchanger and coil figures"));
        }

        [Test]
        public void NoStatedCapacity_IsRefused()
        {
            MechanicalVentilationGuidanceCooling guidanceCooling = GuidanceCooling(null, withCapacity: false);

            Assert.That(TPD.Modify.TryGetGuidanceRecipe(guidanceCooling, out _, out string refusal), Is.False);
            Assert.That(refusal, Does.Contain("capacity"));
        }

        private static SupplyTemperatureRule CoolingRule(double minimum_C)
        {
            return SupplyTemperatureRule.ExchangerThenCoil(new[] { 60.0, 80.0, 100.0, 120.0 }, new[] { 0.8796, 0.8576, 0.8356, 0.8136 }, new[] { 9.265, 8.745, 8.225, 7.705 }, new[] { 0.3, 0.5, 0.8, 1.1 }, minimum_C);
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
                BypassMinimumExtractTemperature_C = 19.0,
                ElevatedAirFlow_Lps = 80.0,
                MinimumElevatedAirFlow_Lps = 60.0,
                MaximumElevatedAirFlow_Lps = 120.0,
                SummerBypassSupplyTemperatureRule = SupplyTemperatureRule.OutdoorAir(),
                HeatCoolthRecoverySupplyTemperatureRule = SupplyTemperatureRule.LinearBlend(0.8),
                CoolingSupplyTemperatureRule = CoolingRule(13.0),
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
