// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical;
using SAM.Analytical.Enums;
using SAM.Core;
using SAM.Geometry.Spatial;
using SAM.Weather;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using AnalyticalCreate = SAM.Analytical.Create;
using AnalyticalModify = SAM.Analytical.Modify;
using AnalyticalZone = SAM.Analytical.Zone;
using ApertureTypeDefinition = SAM.Analytical.Tas.ApertureTypeDefinition;
using ApertureTypeProfileMode = SAM.Analytical.Tas.ApertureTypeProfileMode;

namespace SAM.Analytical.Tas.TM59.Tests
{
    /// <summary>
    /// <b>One naturally ventilated dwelling, carried COM-free from the Part O preparation to the three
    /// places the TAS side has to agree with it: the exported ventilation type, the aperture control the
    /// TBD write is given, and the TM59 criterion the assessment applies.</b>
    /// <para>
    /// The case is deliberately the smallest complete one - one flat, one openable window authored
    /// <c>NightClosed</c> over 08-23, one zone stating <c>NV</c> - and it is the same case the licensed
    /// acceptance run uses. Each of the three consumers is already covered in isolation elsewhere
    /// (<see cref="VentilationStrategyExportTests"/>, <see cref="OpeningScheduleResolutionTests"/>,
    /// <see cref="OpeningScheduleDeliveryTests"/>, <see cref="OverheatingCalculatorEquivalenceTests"/>);
    /// what is NOT covered elsewhere, and what this file exists for, is that <b>one</b> model satisfies all
    /// three at once, starting from the model the production preparation actually returns.
    /// </para>
    /// <para>
    /// <b>The dwelling carries a mechanical trace on purpose.</b> Its internal condition states
    /// <c>VentilationSystemTypeName = "MVRE"</c>, which is what a dwelling looks like after being run
    /// through a Part F sizing that is unconditionally System 4 shaped. Every control below shows the
    /// pre-scenario derivation answering "mechanical" for that model, so each assertion is a change of
    /// answer rather than an agreement - the scenario is authoritative, or these tests would pass for the
    /// wrong reason.
    /// </para>
    /// <para>
    /// <b>No TAS COM.</b> <c>Building</c> and <c>Zone</c> are XML writers over analytical objects;
    /// <c>ApertureTypeDefinition</c> is the COM-free resolution of the aperture control that
    /// <c>Modify.SetApertureType</c> writes; <c>TMOverheatingCalculator</c> reads hourly series off a
    /// <c>Space</c>. Nothing here instantiates a coclass or opens a document.
    /// </para>
    /// <para>
    /// Types are fully qualified throughout: this namespace nests under <c>SAM.Analytical.Tas</c> and
    /// <c>SAM.Analytical.Tas.TM59</c>, both of which declare their own <c>Query</c>, <c>Zone</c>,
    /// <c>Modify</c> and <c>Convert</c>, and an unqualified name binds silently to the wrong one.
    /// </para>
    /// </summary>
    [TestFixture]
    public class PartONaturalVentilationWorkflowTests
    {
        private const string ScheduleName = "PartO_DayOpen_08_23";

        private const string Function = "zdwno,0,19.00,21.00,99.00";

        private const string SpaceName = "Flat 1 Bedroom 2";

        private const string ZoneName = "Flat 1";

        /// <summary>What a Part F sized model states, and what the NV scenario has to beat.</summary>
        private const string VentilationSystemTypeName_Model = "MVRE";

        private static readonly string[] DayTypes = { "Weekday", "Saturday", "Sunday" };

        private const string key_ResultantTemperature = "Resultant Temperature";

        private const string key_OccupantSensibleGain = "Occupant Sensible Gain";

        private static TM59Manager tM59Manager;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Resources", "SAM_InternalConditionTextMap_TM59.JSON");

            tM59Manager = new TM59Manager(Core.Create.IJSAMObject<TextMap>(File.ReadAllText(path)));
        }

        // =================================================================================================
        // The preparation this workflow starts from
        // =================================================================================================

        /// <summary>
        /// The model handed to the export is a dwelling with no continuous mechanical supply or extract on
        /// it, and with the authored opening data intact. Everything below runs on <b>this</b> model, so if
        /// the preparation ever starts inventing an MVHR system again, every test in this file is looking at
        /// the wrong building.
        /// </summary>
        [Test]
        public void ThePreparedNVDwelling_CarriesNoMechanicalAirflowAndKeepsItsOpeningSchedule()
        {
            AnalyticalModel analyticalModel = Prepared();

            foreach (Space space in analyticalModel.GetSpaces())
            {
                Assert.That(space.InternalCondition.TryGetValue(InternalConditionParameter.SupplyAirFlow, out double _), Is.False, "A continuous mechanical supply was written onto a naturally ventilated dwelling.");
                Assert.That(space.InternalCondition.TryGetValue(InternalConditionParameter.ExhaustAirFlow, out double _), Is.False, "A continuous mechanical extract was written onto a naturally ventilated dwelling.");
            }

            //The internal condition keeps its authored NAME on this path - no per-space clone, no "<name> -
            //<space>" rename - which is what lets the TM59 space-application lookup below still resolve it.
            Assert.That(analyticalModel.GetSpaces()[0].InternalCondition.Name, Is.EqualTo("Bedroom"));

            PartOOpeningProperties partOOpeningProperties = PartO(analyticalModel);

            Assert.That(partOOpeningProperties.OpeningRestriction, Is.EqualTo(OpeningRestriction.NightClosed));
            Assert.That(partOOpeningProperties.Schedule?.Name, Is.EqualTo(ScheduleName));
        }

        // =================================================================================================
        // 1. The scenario is authoritative for the exported ventilation type
        // =================================================================================================

        /// <summary>
        /// <b>"NV" exports as Natural Ventilation, over a model that says MVRE.</b> The control is the
        /// model-derived overload, which reads the internal condition and answers Mechanical Ventilation for
        /// the same building - so this is the scenario winning, not the two happening to agree.
        /// </summary>
        [Test]
        public void TheNVScenario_ExportsAsNaturalVentilation_OverAModelStatingMVRE()
        {
            AnalyticalModel analyticalModel = Prepared(out OverheatingScenario overheatingScenario);

            Assert.That(overheatingScenario.VentilationStrategy, Is.EqualTo("NV"));

            //Control: without the scenario, this dwelling exports as mechanical.
            Assert.That(SystemTypes(analyticalModel.ToTM59(tM59Manager)), Is.EqualTo(new[] { SystemType.MechanicalVentilation }));

            Building building = analyticalModel.ToTM59(tM59Manager, Map(analyticalModel, overheatingScenario), out List<string> refusals);

            Assert.That(refusals, Is.Empty);
            Assert.That(building, Is.Not.Null);
            Assert.That(SystemTypes(building), Is.EqualTo(new[] { SystemType.NaturalVentilation }));
        }

        // =================================================================================================
        // 2. The aperture schedule request resolves, and reaches the aperture-definition write path
        // =================================================================================================

        /// <summary>
        /// The opening asks for a schedule, and the source it names is the derived Part O one - the name and
        /// all 24 values the TBD schedule is written from.
        /// </summary>
        [Test]
        public void TheNightClosedAperture_RequestsItsPartOScheduleAndResolvesIt()
        {
            Aperture aperture = SingleAperture(Prepared());

            Assert.That(aperture.TryGetValue(Analytical.ApertureParameter.OpeningProperties, out IOpeningProperties openingProperties), Is.True);

            Assert.That(openingProperties.OpeningScheduleRequests(), Is.EqualTo(new List<bool> { true }));

            bool requested = ((ISingleOpeningProperties)openingProperties).TryGetOpeningScheduleSource(out string name, out int[] values, out string refusal);

            Assert.That(refusal, Is.Null);
            Assert.That(requested, Is.True);
            Assert.That(name, Is.EqualTo(ScheduleName));
            Assert.That(values, Is.EqualTo(ScheduleValues()));
        }

        /// <summary>
        /// <b>The aperture control the TBD write is given.</b> <c>Query.ApertureTypeDefinition</c> is the
        /// COM-free half of <c>Modify.SetApertureType</c> - the same resolution, lifted out - so what it
        /// returns is what a <c>TBD.ApertureType</c> and its profile are written from: the function as the
        /// base curve, the Part O schedule beside it as the availability multiplier, and the authored
        /// opening factor.
        /// </summary>
        [Test]
        public void TheNightClosedAperture_ResolvesTheApertureControlTheTBDWriteIsGiven()
        {
            ISingleOpeningProperties singleOpeningProperties = PartO(Prepared());

            ApertureTypeDefinition apertureTypeDefinition = singleOpeningProperties.ApertureTypeDefinition(DayTypes, out string name_Schedule, out string refusal);

            Assert.That(refusal, Is.Null);
            Assert.That(apertureTypeDefinition, Is.Not.Null);

            //A function AND a schedule: the function is the base curve, and the schedule stays on as the
            //availability multiplier. This is the shape the licensed acceptance reads back out of the TBD.
            Assert.That(apertureTypeDefinition.Mode, Is.EqualTo(ApertureTypeProfileMode.Function));
            Assert.That(apertureTypeDefinition.Function, Is.EqualTo(Function));

            Assert.That(name_Schedule, Is.EqualTo(ScheduleName));
            Assert.That(apertureTypeDefinition.HasSchedule, Is.True);
            Assert.That(apertureTypeDefinition.ScheduleValues, Is.EqualTo(ScheduleValues()));

            Assert.That(apertureTypeDefinition.Factor, Is.EqualTo(0.75f));

            //Carried through rather than re-derived: PartOOpeningProperties computes its own discharge
            //coefficient from the opening's height, width and maximum opening angle, and the definition takes
            //whatever that is. Pinning a literal here would be pinning that formula, which is a different test.
            Assert.That(apertureTypeDefinition.DischargeCoefficient, Is.EqualTo(System.Convert.ToSingle(singleOpeningProperties.GetDischargeCoefficient())));
        }

        /// <summary>
        /// The schedule is part of the control's IDENTITY, not decoration on it: the same window without the
        /// night closure is a different aperture type, so a TBD cannot quietly share one control between a
        /// restricted and an unrestricted opening.
        /// </summary>
        [Test]
        public void TheNightClosedControl_IsNotTheSameControlAsAnUnrestrictedOne()
        {
            ApertureTypeDefinition apertureTypeDefinition = PartO(Prepared()).ApertureTypeDefinition(DayTypes, out string _);

            PartOOpeningProperties partOOpeningProperties_Unrestricted = new PartOOpeningProperties(1.2, 1.0, 30.0, OpeningRestriction.Unrestricted) { Factor = 0.75 };
            partOOpeningProperties_Unrestricted.SetValue(OpeningPropertiesParameter.Function, Function);

            ApertureTypeDefinition apertureTypeDefinition_Unrestricted = partOOpeningProperties_Unrestricted.ApertureTypeDefinition(DayTypes, out string _);

            Assert.That(apertureTypeDefinition_Unrestricted.HasSchedule, Is.False);
            Assert.That(apertureTypeDefinition, Is.Not.EqualTo(apertureTypeDefinition_Unrestricted));
        }

        /// <summary>
        /// <b>Delivered, not merely requested.</b> The undelivered-request report is what turns a schedule
        /// that silently failed to reach the TBD into a refusal, so the delivered case must report nothing
        /// and the failed case must name the opening.
        /// </summary>
        [Test]
        public void TheRequestedSchedule_IsReportedOnlyWhenItFailsToReachTheTBD()
        {
            Assert.That(SingleAperture(Prepared()).TryGetValue(Analytical.ApertureParameter.OpeningProperties, out IOpeningProperties openingProperties), Is.True);

            Assert.That(openingProperties.UndeliveredOpeningScheduleRequests(new List<bool> { true }), Is.Empty);
            Assert.That(openingProperties.UndeliveredOpeningScheduleRequests(new List<bool> { false }), Is.EqualTo(new List<int> { 0 }));
        }

        // =================================================================================================
        // 3. TM59 selects the natural-ventilation assessment route
        // =================================================================================================

        /// <summary>
        /// <b>The NV route, selected by the scenario.</b> The dwelling is a bedroom, so the natural
        /// ventilation BEDROOM criterion is the one that applies. The control is the same calculation with
        /// no map, where the model's own "MVRE" picks the mechanical criterion - which is the wrong answer
        /// this whole chain exists to stop.
        /// </summary>
        [Test]
        public void TheNVScenario_SelectsTheNaturalVentilationTM59Route()
        {
            AnalyticalModel analyticalModel = Prepared(out OverheatingScenario overheatingScenario);

            //Control: no scenario, and the model's own data picks the mechanical criterion.
            List<TM59ExtendedResult> results_Derived = Calculator(analyticalModel, null).Calculate_TM59(analyticalModel.GetSpaces());

            Assert.That(results_Derived.Count, Is.EqualTo(1));
            Assert.That(results_Derived[0], Is.InstanceOf<TM59MechanicalVentilationExtendedResult>());

            List<TM59ExtendedResult> results = Calculator(analyticalModel, Map(analyticalModel, overheatingScenario)).Calculate_TM59(analyticalModel.GetSpaces());

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0], Is.InstanceOf<TM59NaturalVentilationBedroomExtendedResult>());

            //And it is the natural-ventilation branch, not merely a type that inherits from something shared:
            //the extended natural result is a separate inheritance branch from TM59NaturalVentilationResult.
            Assert.That(results[0], Is.Not.InstanceOf<TM59MechanicalVentilationExtendedResult>());
            Assert.That(results[0], Is.Not.InstanceOf<TM59CorridorExtendedResult>());
        }

        // =================================================================================================
        // Fixture
        // =================================================================================================

        /// <summary>
        /// The one NV dwelling, run through the PRODUCTION Part O preparation - the same
        /// <c>Modify.PreparePartOIteration</c> the Grasshopper component calls - so what every test above
        /// reads is the model the workflow really hands to the TAS side.
        /// </summary>
        private static AnalyticalModel Prepared()
        {
            return Prepared(out OverheatingScenario _);
        }

        private static AnalyticalModel Prepared(out OverheatingScenario overheatingScenario)
        {
            AnalyticalModel analyticalModel = Model();

            List<AnalyticalZone> zones = analyticalModel.GetZones();

            Assert.That(zones.Count, Is.EqualTo(1));

            Dictionary<System.Guid, string> dictionary_VentilationStrategy = new Dictionary<System.Guid, string> { { zones[0].Guid, "NV" } };

            PartOIterationPreparation partOIterationPreparation = AnalyticalModify.PreparePartOIteration(analyticalModel, PartOIteration.BasePassive, null, dictionary_VentilationStrategy);

            Assert.That(partOIterationPreparation.Refusal, Is.Null);
            Assert.That(partOIterationPreparation.AirflowApplication, Is.EqualTo(PartOPartFAirflowApplication.SkipNaturalVentilation));
            Assert.That(partOIterationPreparation.Successful, Is.True);
            Assert.That(partOIterationPreparation.OverheatingScenarios.Count, Is.EqualTo(1));

            overheatingScenario = partOIterationPreparation.OverheatingScenarios[0];

            return partOIterationPreparation.AnalyticalModel;
        }

        /// <summary>
        /// One flat: a bedroom with the hourly series a TSD conversion leaves behind, a zone to state the
        /// scenario over, and one openable window authored <c>NightClosed</c> over 08-23.
        /// </summary>
        private static AnalyticalModel Model()
        {
            AdjacencyCluster adjacencyCluster = new AdjacencyCluster();

            //Named "Bedroom" so the shipped TM59 TextMap resolves the Sleeping application, which is what
            //selects the bedroom variant of whichever criterion applies.
            InternalCondition internalCondition = new InternalCondition("Bedroom");

            //The mechanical trace a Part F sized model carries, and the thing the NV scenario has to beat.
            internalCondition.SetValue(InternalConditionParameter.VentilationSystemTypeName, VentilationSystemTypeName_Model);

            Space space = new Space(SpaceName) { InternalCondition = internalCondition };

            //Exactly how Analytical.Tas.Convert.ToSAM(TSD.ZoneData, ...) stores an hourly series.
            ParameterSet parameterSet = new ParameterSet("SAM.Analytical.Tas.dll");
            parameterSet.Add(key_ResultantTemperature, Series(new double[] { 21.0, 24.5, 27.5, 29.0 }));
            parameterSet.Add(key_OccupantSensibleGain, Series(new double[] { 0, 80.0, 80.0, 0 }));

            space.Add(parameterSet);

            adjacencyCluster.AddObject(space);
            adjacencyCluster.AddObject(new AnalyticalZone(ZoneName));

            Panel panel = AnalyticalCreate.Panel(new Construction(System.Guid.NewGuid(), "Wall"), PanelType.Wall, WallFace());

            Aperture aperture = AnalyticalCreate.Aperture(new ApertureConstruction(System.Guid.NewGuid(), "Window", ApertureType.Window), ApertureFace());

            //Exactly what SAMAnalytical.AddOpeningPropertiesByPartO authors for restriction_ = NightClosed
            //with the default 08-23 window.
            PartOOpeningProperties partOOpeningProperties = new PartOOpeningProperties(1.2, 1.0, 30.0, OpeningRestriction.NightClosed, 8, 23) { Factor = 0.75 };
            partOOpeningProperties.SetValue(OpeningPropertiesParameter.Function, Function);

            aperture.AddSingleOpeningProperties(partOOpeningProperties);
            panel.AddAperture(aperture);

            adjacencyCluster.AddObject(panel);

            AnalyticalModel result = new AnalyticalModel("Part O NV Dwelling", null, null, null, adjacencyCluster);

            //Qualified: unqualified AnalyticalModelParameter binds to SAM.Analytical.Tas's own enum here.
            result.SetValue(Analytical.AnalyticalModelParameter.WeatherData, new WeatherData("Test", "Test", 51.5, -0.1, 0, WeatherYear()));

            return result;
        }

        /// <summary>The TAS-side calculator, reading the TAS legacy series keys the fixture stores.</summary>
        private static TMOverheatingCalculator Calculator(AnalyticalModel analyticalModel, VentilationStrategyMap ventilationStrategyMap)
        {
            return new TMOverheatingCalculator(analyticalModel)
            {
                TextMap = TM59TextMap(),
                ResultantTemperatureSeriesKey = key_ResultantTemperature,
                OccupancySensibleGainSeriesKey = key_OccupantSensibleGain,
                VentilationStrategyMap = ventilationStrategyMap,
            };
        }

        private static TextMap TM59TextMap()
        {
            TextMap result = Core.Create.TextMap("TM59");

            result.Add("Sleeping", "Bedroom");

            return result;
        }

        /// <summary>The scenario, mapped onto the spaces it governs - the whole dwelling.</summary>
        private static VentilationStrategyMap Map(AnalyticalModel analyticalModel, OverheatingScenario overheatingScenario)
        {
            VentilationStrategyMap result = new VentilationStrategyMap();

            result.Add(overheatingScenario, analyticalModel.GetSpaces());

            return result;
        }

        private static SystemType[] SystemTypes(Building building)
        {
            return building?.Zones?.ConvertAll(x => x.SystemType)?.ToArray();
        }

        /// <summary>Open 08:00-22:59, shut 23:00-07:59 - "000000001111111111111110".</summary>
        private static int[] ScheduleValues()
        {
            int[] result = new int[24];
            for (int hour = 0; hour < 24; hour++)
            {
                result[hour] = hour >= 8 && hour < 23 ? 1 : 0;
            }

            return result;
        }

        private static PartOOpeningProperties PartO(AnalyticalModel analyticalModel)
        {
            Assert.That(SingleAperture(analyticalModel).TryGetValue(Analytical.ApertureParameter.OpeningProperties, out IOpeningProperties openingProperties), Is.True);

            return (PartOOpeningProperties)openingProperties;
        }

        private static Aperture SingleAperture(AnalyticalModel analyticalModel)
        {
            List<Aperture> apertures = analyticalModel.AdjacencyCluster.GetApertures();

            Assert.That(apertures, Is.Not.Null);
            Assert.That(apertures.Count, Is.EqualTo(1));

            return apertures[0];
        }

        private static Face3D WallFace()
        {
            return new Face3D(new Geometry.Spatial.Polygon3D(new Point3D[] { new Point3D(0, 0, 0), new Point3D(10, 0, 0), new Point3D(10, 10, 0), new Point3D(0, 10, 0) }));
        }

        private static Face3D ApertureFace()
        {
            return new Face3D(new Geometry.Spatial.Polygon3D(new Point3D[] { new Point3D(1, 1, 0), new Point3D(3, 1, 0), new Point3D(3, 3, 0), new Point3D(1, 3, 0) }));
        }

        private static WeatherYear WeatherYear()
        {
            WeatherYear result = new WeatherYear(2018);

            for (int day = 0; day < 365; day++)
            {
                for (int hour = 0; hour < 24; hour++)
                {
                    result.Add(day, hour, new Dictionary<string, double> { { WeatherDataType.DryBulbTemperature.ToString(), 20.0 } });
                }
            }

            return result;
        }

        private static JsonArray Series(IEnumerable<double> values)
        {
            JsonArray result = new JsonArray();

            foreach (double value in values)
            {
                result.Add(value);
            }

            return result;
        }
    }
}
