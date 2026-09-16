// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical.Systems;
using SAM.Analytical.Tas.TPD;
using SAM.Core;
using SAM.Core.Systems;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Tas.TM59.Tests
{
    /// <summary>
    /// PR5B (SAM#111), COM-free: the explicit route with a SAM_Systems recirculation cooling branch in the graph.
    /// The branch is left out of the ventilation intent by identity - the ventilation intent is exactly B0's -
    /// the duty carriers never copy a branch damper, a SAM-named table reaches TAS as the published grid, and the
    /// hourly evidence reduction refuses every behaviour the branch was not built to have. Fixture values only.
    /// </summary>
    [TestFixture]
    public class RecirculationCoolingTests
    {
        private static VentilationUnitPerformanceTable Table()
        {
            return new VentilationUnitPerformanceTable(
                new[]
                {
                    new VentilationUnitPerformanceAxis(VentilationUnitPerformanceAxis.Name_ExternalDryBulbTemperature, "degC", new double[] { 20, 30 }),
                    new VentilationUnitPerformanceAxis(VentilationUnitPerformanceAxis.Name_EnteringDryBulbTemperature, "degC", new double[] { 22, 26 }),
                    new VentilationUnitPerformanceAxis(VentilationUnitPerformanceAxis.Name_AirFlowRate, "l/s", new double[] { 40, 80, 120 }),
                },
                new[] { new VentilationUnitPerformanceOutput(VentilationUnitPerformanceOutput.Name_SupplyAirTemperature, "degC", new double[] { 14, 15, 16, 16, 17, 18, 15, 16, 17, 17, 18, 19 }) });
        }

        private static MechanicalVentilationCoolingSettings Settings()
        {
            return new MechanicalVentilationCoolingSettings
            {
                SupplyAirTemperatureTable = Table(),
                FlowFractionByControlTemperature = new FlowFractionControlCurve(new double[] { 21, 25 }, new double[] { 0.4, 1.0 }),
                MaximumOperatingAirFlow_Lps = 100,
                CoolingEnableTemperature_C = 21,
            };
        }

        private static MechanicalVentilationMaterialisation Materialise_B4(AdjacencyCluster adjacencyCluster, AirHandlingUnit airHandlingUnit)
        {
            MechanicalVentilationMaterialisation result = adjacencyCluster.MechanicalVentilation(
                SystemVentilationFixture.Template(),
                new MechanicalVentilationSettings { CoolingSettings = new Dictionary<Guid, MechanicalVentilationCoolingSettings> { { airHandlingUnit.Guid, Settings() } } });

            Assert.That(result.IsMaterialised, Is.True, string.Join(" | ", result.Refusals));
            Assert.That(result.RecirculationCoolings, Has.Count.EqualTo(1));

            return result;
        }

        private static SystemVentilationConversionContext Context(MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation, AdjacencyCluster adjacencyCluster, IEnumerable<MechanicalVentilationRecirculationCooling> recirculationCoolings)
        {
            return TPD.Create.SystemVentilationConversionContext(
                mechanicalVentilationMaterialisation.SystemEnergyCentre,
                mechanicalVentilationMaterialisation.Bindings,
                SystemVentilationFixture.ZoneReferences(adjacencyCluster),
                SystemVentilationFanHeatGainPolicy.ClearToZero,
                recirculationCoolings);
        }

        // =====================================================================================================
        // The intent
        // =====================================================================================================

        [Test]
        public void TheBranch_IsLeftOutOfTheVentilationIntent_WhichIsExactlyB0s()
        {
            AdjacencyCluster adjacencyCluster = SystemVentilationFixture.Dwelling(out AirHandlingUnit airHandlingUnit);

            MechanicalVentilationMaterialisation b0 = SystemVentilationFixture.Materialise(adjacencyCluster);
            MechanicalVentilationMaterialisation b4 = Materialise_B4(adjacencyCluster, airHandlingUnit);

            SystemVentilationConversionContext context_B0 = Context(b0, adjacencyCluster, null);
            SystemVentilationConversionContext context_B4 = Context(b4, adjacencyCluster, b4.RecirculationCoolings);

            Assert.That(context_B4.Refusals, Is.Empty);
            Assert.That(context_B4.RoomIntents.Count, Is.EqualTo(context_B0.RoomIntents.Count));

            foreach (SystemVentilationConnectionType systemVentilationConnectionType in new[] { SystemVentilationConnectionType.Supply, SystemVentilationConnectionType.Extract, SystemVentilationConnectionType.Transfer })
            {
                Assert.That(context_B4.Count(systemVentilationConnectionType), Is.EqualTo(context_B0.Count(systemVentilationConnectionType)), systemVentilationConnectionType.ToString());
            }

            Assert.That(
                context_B4.LegIntents.Select(x => x.DesignFlowRate_Lps).OrderBy(x => x),
                Is.EqualTo(context_B0.LegIntents.Select(x => x.DesignFlowRate_Lps).OrderBy(x => x)));

            MechanicalVentilationRecirculationCooling recirculationCooling = b4.RecirculationCoolings[0];
            foreach (Guid guid in recirculationCooling.Guids_Connection)
            {
                Assert.That(context_B4.IsRecirculationConnection(guid), Is.True);
                Assert.That(context_B4.LegIntent(guid), Is.Null);
            }

            foreach (Guid guid in recirculationCooling.Guids_Component)
            {
                Assert.That(context_B4.IsRecirculationComponent(guid), Is.True);
            }

            Assert.That(context_B4.RecirculationCooling(recirculationCooling.Guid_AirSystem), Is.Not.Null);
        }

        [Test]
        public void AGraphWithABranch_ThatDoesNotDeclareIt_IsRefused()
        {
            AdjacencyCluster adjacencyCluster = SystemVentilationFixture.Dwelling(out AirHandlingUnit airHandlingUnit);
            MechanicalVentilationMaterialisation b4 = Materialise_B4(adjacencyCluster, airHandlingUnit);

            //The branch's room connections would read as second supply and extract legs of every room.
            Assert.That(Context(b4, adjacencyCluster, null).Refusals, Is.Not.Empty);
        }

        [Test]
        public void ABranchOutsideItsUnitsOwnAirSystem_IsRefused()
        {
            AdjacencyCluster adjacencyCluster = SystemVentilationFixture.Dwelling(out AirHandlingUnit airHandlingUnit);
            MechanicalVentilationMaterialisation b4 = Materialise_B4(adjacencyCluster, airHandlingUnit);

            MechanicalVentilationRecirculationCooling real = b4.RecirculationCoolings[0];
            MechanicalVentilationRecirculationCooling misplaced = new MechanicalVentilationRecirculationCooling(
                real.Guid_AirHandlingUnit, Guid.NewGuid(), real.Guid_DXCoil, real.Guid_Fan, real.Guid_Fan_Source, real.Guid_Connection_DXCoilToFan, real.Rooms, real.Settings);

            SystemVentilationConversionContext context = Context(b4, adjacencyCluster, new[] { misplaced });

            Assert.That(context.Refusals, Has.Some.Contains("not that unit's own air system"));
        }

        [Test]
        public void TheDutyCarriers_AreExactlyB0s_AndNeverACopyOfARecirculationDamper()
        {
            AdjacencyCluster adjacencyCluster = SystemVentilationFixture.Dwelling(out AirHandlingUnit airHandlingUnit);

            MechanicalVentilationMaterialisation b0 = SystemVentilationFixture.Materialise(adjacencyCluster);
            MechanicalVentilationMaterialisation b4 = Materialise_B4(adjacencyCluster, airHandlingUnit);

            SystemVentilationConversionContext context_B0 = Context(b0, adjacencyCluster, null);
            SystemVentilationConversionContext context_B4 = Context(b4, adjacencyCluster, b4.RecirculationCoolings);

            Assert.That(TPD.Modify.MaterialiseVentilationDutyCarriers(new SystemEnergyCentre(b0.SystemEnergyCentre), context_B0), Is.True);

            SystemEnergyCentre working = new SystemEnergyCentre(b4.SystemEnergyCentre);
            Assert.That(TPD.Modify.MaterialiseVentilationDutyCarriers(working, context_B4), Is.True, string.Join(" | ", context_B4.Refusals));

            List<SystemVentilationLegIntent> carriers_B0 = context_B0.LegIntents.Where(x => x.Guid_DutyCarrier != Guid.Empty).ToList();
            List<SystemVentilationLegIntent> carriers_B4 = context_B4.LegIntents.Where(x => x.Guid_DutyCarrier != Guid.Empty).ToList();

            Assert.That(carriers_B4.Count, Is.EqualTo(carriers_B0.Count));
            Assert.That(carriers_B4.Select(x => x.DesignFlowRate_Lps).OrderBy(x => x), Is.EqualTo(carriers_B0.Select(x => x.DesignFlowRate_Lps).OrderBy(x => x)));

            SystemPlantRoom systemPlantRoom = working.GetSystemPlantRooms()[0];
            Dictionary<Guid, DisplaySystemDamper> dampers = systemPlantRoom.GetSystemComponents<DisplaySystemDamper>().ToDictionary(x => x.Guid);

            foreach (SystemVentilationLegIntent systemVentilationLegIntent in carriers_B4)
            {
                Assert.That(context_B4.IsRecirculationComponent(systemVentilationLegIntent.Guid_DutyCarrier), Is.False);

                DisplaySystemDamper carrier = dampers[systemVentilationLegIntent.Guid_DutyCarrier];
                Assert.That(carrier.Name, Does.Not.Contain("Recirculation"));
                Assert.That(carrier.DesignFlowRate.Value, Is.EqualTo(systemVentilationLegIntent.DesignFlowRate_Lps));
            }
        }

        // =====================================================================================================
        // The table
        // =====================================================================================================

        [Test]
        public void ASamNamedTable_ReachesTasAsThePublishedGrid_AndReadsBackIdentical()
        {
            TableModifier tableModifier = Table().SupplyAirTemperatureModifier(out double _);
            Assert.That(tableModifier.Headers.Take(3), Is.EqualTo(new[] { "ODB", "EDB", "EFlow" }));

            FakeTpdProfileData profile = new FakeTpdProfileData();
            Assert.That(TPD.Modify.AddModifier(profile, tableModifier, null), Is.True);

            FakeTpdTableModifier table = profile.Table;
            Assert.That(table.GetVariable(1), Is.EqualTo(global::TPD.tpdProfileDataVariableType.tpdProfileDataVariableODB));
            Assert.That(table.GetVariable(2), Is.EqualTo(global::TPD.tpdProfileDataVariableType.tpdProfileDataVariableEDB));
            Assert.That(table.GetVariable(3), Is.EqualTo(global::TPD.tpdProfileDataVariableType.tpdProfileDataVariableEFlow));
            Assert.That(table.Extrapolate, Is.Zero);
            Assert.That(table.Multiplier, Is.EqualTo(global::TPD.tpdProfileDataModifierMultiplier.tpdProfileDataModifierEqual));
            Assert.That(new[] { table.GetAxisSize(1), table.GetAxisSize(2), table.GetAxisSize(3) }, Is.EqualTo(new[] { 2, 2, 3 }));
            Assert.That(table.GetDataValue(2, 2, 3), Is.EqualTo(19.0));

            TableModifier readBack = TPD.Convert.ToSAM(table) as TableModifier;
            Assert.That(TPD.Modify.TableRefusal(tableModifier, readBack), Is.Null);
        }

        [Test]
        public void TheReadBackComparison_FindsAChangedCell_AndExtrapolation()
        {
            TableModifier tableModifier = Table().SupplyAirTemperatureModifier(out double _);

            FakeTpdProfileData profile = new FakeTpdProfileData();
            TPD.Modify.AddModifier(profile, tableModifier, null);
            profile.Table.SetDataValue(1, 1, 1, 14.3);
            Assert.That(TPD.Modify.TableRefusal(tableModifier, TPD.Convert.ToSAM(profile.Table) as TableModifier), Does.Contain("cell"));

            FakeTpdProfileData profile_Extrapolating = new FakeTpdProfileData();
            TPD.Modify.AddModifier(profile_Extrapolating, tableModifier, null);
            profile_Extrapolating.Table.Extrapolate = -1;
            Assert.That(TPD.Modify.TableRefusal(tableModifier, TPD.Convert.ToSAM(profile_Extrapolating.Table) as TableModifier), Does.Contain("extrapolation"));
        }

        // =====================================================================================================
        // The hourly evidence
        // =====================================================================================================

        private static MechanicalVentilationRecirculationCooling Branch()
        {
            return new MechanicalVentilationRecirculationCooling(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, Settings());
        }

        private static double Published(double odb, double edb, double q)
        {
            return Table().Value(VentilationUnitPerformanceOutput.Name_SupplyAirTemperature, new[] { odb, edb, q }, Enums.PerformanceDomainPolicy.ClampToDomain);
        }

        /// <summary>Hour 0 below the gate and idle at the law's floor; hour 1 above it, cooled to the published value at the full law.</summary>
        private static RecirculationCoolingResult Reduce(Action<double[], double[], double[], double[], double[]> defect = null)
        {
            double[] odb = { 10, 30 };
            double[] tMix = { 18, 25 };
            double[] q = { 40, 100 };
            double[] tOut = { 18, Published(30, 25, 100) };
            double[] deviation = { 0.01, 0.02 };

            defect?.Invoke(odb, tMix, q, tOut, deviation);

            return TPD.Create.RecirculationCoolingResult(Branch(), 4000, odb, tMix, q, tOut, deviation);
        }

        [Test]
        public void TheBuiltBehaviour_IsValid_AndCounted()
        {
            RecirculationCoolingResult result = Reduce();

            Assert.That(result.Refusals, Is.Empty);
            Assert.That(result.Count_Cooling, Is.EqualTo(1));
            Assert.That(result.Count_BelowGate, Is.EqualTo(1));
            Assert.That(result.Count_Heating, Is.Zero);
            Assert.That(result.Count_GateViolation, Is.Zero);
            Assert.That(result.Count_OffLaw, Is.Zero);
            Assert.That(result.OperatingAirFlowMinimum_Lps, Is.EqualTo(40));
            Assert.That(result.OperatingAirFlowMaximum_Lps, Is.EqualTo(100));
            Assert.That(result.MaximumTableError_K, Is.LessThan(1e-9));
            Assert.That(result.StartHour, Is.EqualTo(4000));
            Assert.That(result.Cooling_kWh, Is.EqualTo(100 / 1000.0 * TPD.Create.RecirculationCoolingRhoCp * (25 - Published(30, 25, 100)) / 1000.0).Within(1e-9));
        }

        [TestCase("heating", "heated")]
        [TestCase("gate", "below the")]
        [TestCase("below-range", "outside")]
        [TestCase("above-range", "outside")]
        [TestCase("table", "published table")]
        [TestCase("ventilation", "disturbed the ventilation")]
        [TestCase("nan", "not finite")]
        [TestCase("short", "complete hourly series")]
        public void EveryBehaviourTheBranchWasNotBuiltToHave_IsRefused(string defect, string expected)
        {
            RecirculationCoolingResult result = defect == "short"
                ? TPD.Create.RecirculationCoolingResult(Branch(), 0, new double[] { 10 }, new double[] { 18, 25 }, new double[] { 40, 100 }, new double[] { 18, 18 }, null)
                : Reduce((odb, tMix, q, tOut, deviation) =>
                {
                    switch (defect)
                    {
                        case "heating": tOut[0] = 18.5; break;
                        case "gate": tOut[0] = 17.5; break;
                        case "below-range": q[0] = 30; break;
                        case "above-range": q[1] = 100.5; break;
                        case "table": tOut[1] += 0.2; break;
                        case "ventilation": deviation[1] = 0.1; break;
                        case "nan": tMix[1] = double.NaN; break;
                    }
                });

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Refusals, Has.Some.Contains(expected));
        }

        [Test]
        public void AnHourOffTheIdealLaw_IsCounted_NotRefused()
        {
            //60 l/s at a 25 C mixed return, where the law asks for 100 l/s: inside the range, off the law.
            RecirculationCoolingResult result = Reduce((odb, tMix, q, tOut, deviation) =>
            {
                q[1] = 60;
                tOut[1] = Published(30, 25, 60);
            });

            Assert.That(result.Refusals, Is.Empty);
            Assert.That(result.Count_OffLaw, Is.EqualTo(1));
        }

        /// <summary>
        /// SAM#111 real-project acceptance (2026-09-15): the declared control cannot command a flow outside
        /// its own range, so a small excursion is the native solver's within-hour ramp, not the design's
        /// behaviour. It is reported AT the range and counted, never refused.
        /// </summary>
        [TestCase(0.05, TestName = "Clamp_JustAboveTheCeiling")]
        [TestCase(0.050278, TestName = "Clamp_TheExcursionTheRealProjectMeasured")]
        [TestCase(0.1, TestName = "Clamp_ExactlyAtTheBound")]
        public void AFlowJustOutsideTheRange_IsReportedAtTheRange_AndCounted_NotRefused(double excursion)
        {
            RecirculationCoolingResult result = Reduce((odb, tMix, q, tOut, deviation) => q[1] = 100 + excursion);

            Assert.That(result.Refusals, Is.Empty);
            Assert.That(result.Count_OutOfRange, Is.Zero);
            Assert.That(result.Count_Clamped, Is.EqualTo(1));
            Assert.That(result.MaximumClampedExcursion_Lps, Is.EqualTo(excursion).Within(1e-12));

            //Reported AT the ceiling - the whole point of the change is what the reader is told.
            Assert.That(result.OperatingAirFlowMaximum_Lps, Is.EqualTo(100));
        }

        [Test]
        public void AFlowJustBelowTheMinimum_IsReportedAtTheMinimum()
        {
            //The range has two ends and the same argument holds at both: the law's lowest flow fraction
            //cannot command less than the minimum either.
            RecirculationCoolingResult result = Reduce((odb, tMix, q, tOut, deviation) => q[0] = 40 - 0.05);

            Assert.That(result.Refusals, Is.Empty);
            Assert.That(result.Count_Clamped, Is.EqualTo(1));
            Assert.That(result.OperatingAirFlowMinimum_Lps, Is.EqualTo(40));
        }

        /// <summary>
        /// The bound itself. Beyond <c>RecirculationCoolingClamp_Lps</c> the flow is left exactly as TAS
        /// answered it and still refuses - a solver genuinely running the branch outside its envelope is
        /// never clamped into silence. This is the assertion that stops the constant being raised quietly.
        /// </summary>
        [Test]
        public void AFlowFurtherOutsideTheRangeThanTheClamp_StillRefuses()
        {
            Assert.That(TPD.Create.RecirculationCoolingClamp_Lps, Is.EqualTo(0.1),
                "The clamp is a measured bound (SAM#111, largest excursion 0.0503 l/s of 26 280 hours), not a derived one. "
                + "Raising it needs a fresh licensed measurement and this test's reason updated with it.");

            RecirculationCoolingResult result = Reduce((odb, tMix, q, tOut, deviation) => q[1] = 100 + 0.1 + 1e-6);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Refusals, Has.Some.Contains("outside"));
            Assert.That(result.Count_OutOfRange, Is.EqualTo(1));
            Assert.That(result.Count_Clamped, Is.Zero);
        }

        [Test]
        public void AClampedHour_IsJudgedAndCharged_AtTheClampedFlow()
        {
            //Not cosmetic: the clamp runs before the hour's duty, range, law and table coordinates are
            //taken, so a clamped hour is charged at the flow the control could actually have commanded.
            RecirculationCoolingResult result = Reduce((odb, tMix, q, tOut, deviation) => q[1] = 100 + 0.05);

            Assert.That(result.Count_Clamped, Is.EqualTo(1));
            Assert.That(result.Cooling_kWh, Is.EqualTo(
                100 / 1000.0 * TPD.Create.RecirculationCoolingRhoCp * (25 - Published(30, 25, 100)) / 1000.0).Within(1e-9));

            //and identical to the unclamped run, which is what "reported at the range" has to mean
            Assert.That(result.Cooling_kWh, Is.EqualTo(Reduce().Cooling_kWh).Within(1e-12));
            Assert.That(result.Count_OffLaw, Is.Zero);
            Assert.That(result.MaximumTableError_K, Is.LessThan(1e-9));
        }

        [Test]
        public void NoExcursion_ClampsNothing()
        {
            RecirculationCoolingResult result = Reduce();

            Assert.That(result.Count_Clamped, Is.Zero);
            Assert.That(result.MaximumClampedExcursion_Lps, Is.Zero);
        }
    }
}
