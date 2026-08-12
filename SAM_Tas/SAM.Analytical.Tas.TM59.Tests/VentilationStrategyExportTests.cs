// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical;
using SAM.Analytical.Enums;
using SAM.Core;
using System;
using System.Collections.Generic;
using System.IO;

namespace SAM.Analytical.Tas.TM59.Tests
{
    /// <summary>
    /// <b>Iteration 0 step 7, export side: the <c>OverheatingScenario</c> is authoritative over the ventilation
    /// system type written into the TM59 XML.</b>
    /// <para>
    /// The two derivations this replaces both live here. <c>Space.ToTM59</c> read the space's
    /// <c>InternalCondition.VentilationSystemTypeName</c> and defaulted to natural ventilation (#1);
    /// <c>AnalyticalModel.ToTM59</c> then let any related <c>VentilationSystem</c> that
    /// <c>IsMechanicalVentilation()</c> override it (#2). They disagreed with each other and with the criterion
    /// the assessment applied, which is how one real run exported "Nat Vent" and "Mech Vent" mixed across three
    /// identical flats.
    /// </para>
    /// <para>
    /// No TAS COM: <c>Building</c> and <c>Zone</c> are XML writers over analytical objects, and nothing here
    /// converts a TBD.
    /// </para>
    /// </summary>
    [TestFixture]
    public class VentilationStrategyExportTests
    {
        private static TM59Manager tM59Manager;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Resources", "SAM_InternalConditionTextMap_TM59.JSON");

            tM59Manager = new TM59Manager(Core.Create.IJSAMObject<TextMap>(File.ReadAllText(path)));
        }

        /// <summary>
        /// <b>An MVRE scenario beats an internal condition saying NV</b> - derivation #1. The control shows the
        /// same model exporting "Nat Vent" through the model-derived overload, so this is a change of answer
        /// and not an agreement.
        /// </summary>
        [Test]
        public void AnMVREScenario_OverridesAnInternalConditionSayingNV()
        {
            AnalyticalModel analyticalModel = Model("NV");

            //Control: the internal condition decides it today.
            Assert.That(SystemTypes(analyticalModel.ToTM59(tM59Manager)), Is.EqualTo(new[] { SystemType.NaturalVentilation }));

            Building building = analyticalModel.ToTM59(tM59Manager, Map(analyticalModel, "MVRE"), out List<string> refusals);

            Assert.That(refusals, Is.Empty);
            Assert.That(SystemTypes(building), Is.EqualTo(new[] { SystemType.MechanicalVentilation }));
        }

        /// <summary>
        /// <b>An NV scenario beats a mechanical <c>VentilationSystem</c> on the model</b> - derivation #2, the
        /// one that used to override everything else.
        /// </summary>
        [Test]
        public void AnNVScenario_OverridesAMechanicalVentilationSystemOnTheModel()
        {
            AnalyticalModel analyticalModel = Model("NV", ventilationSystem: true);

            //Control: the mechanical system overrides the internal condition's "NV" today.
            Assert.That(SystemTypes(analyticalModel.ToTM59(tM59Manager)), Is.EqualTo(new[] { SystemType.MechanicalVentilation }));

            Building building = analyticalModel.ToTM59(tM59Manager, Map(analyticalModel, "NV"), out List<string> refusals);

            Assert.That(refusals, Is.Empty);
            Assert.That(SystemTypes(building), Is.EqualTo(new[] { SystemType.NaturalVentilation }));
        }

        /// <summary>
        /// <b>A refusal loses the whole document, not one room.</b> This XML is configuration for the external
        /// TAS TM59 tool, which cannot be told that a room is missing - it would assess what it was given and
        /// produce a complete-looking answer for an incomplete building. Every space is still visited, so one
        /// unstated dwelling does not hide the others' reasons behind it.
        /// </summary>
        [Test]
        public void AnUnsettledStrategy_RefusesTheWholeExport()
        {
            AnalyticalModel analyticalModel = Model_ThreeFlats();
            List<Space> spaces = analyticalModel.GetSpaces();

            VentilationStrategyMap ventilationStrategyMap = new VentilationStrategyMap();

            //Flat 1 states MVRE, Flat 2's scenario states nothing, Flat 3 is not covered at all.
            ventilationStrategyMap.Add(Scenario("MVRE"), new List<Space> { Find(spaces, "Flat 1 Bedroom 2") });
            ventilationStrategyMap.Add(Scenario(null), new List<Space> { Find(spaces, "Flat 2 Bedroom 2") });

            Building building = analyticalModel.ToTM59(tM59Manager, ventilationStrategyMap, out List<string> refusals);

            Assert.That(building, Is.Null);
            Assert.That(refusals.Count, Is.EqualTo(2));

            //Both are named, and each says the right thing about itself.
            Assert.That(refusals.Exists(x => x.Contains("Flat 2 Bedroom 2") && x.Contains("states no ventilation strategy")), Is.True);
            Assert.That(refusals.Exists(x => x.Contains("Flat 3 Bedroom 2") && x.Contains("No overheating scenario covers")), Is.True);
        }

        /// <summary>
        /// <b>No map means no change.</b> A caller with no scenario gets the old model-derived export, not an
        /// empty one - and no refusals, because nothing was asked of a map that was not supplied.
        /// </summary>
        [Test]
        public void WithoutAMap_TheModelDerivedExportIsUnchanged()
        {
            AnalyticalModel analyticalModel = Model("MVRE");

            Building building = analyticalModel.ToTM59(tM59Manager, null, out List<string> refusals);

            Assert.That(refusals, Is.Empty);
            Assert.That(SystemTypes(building), Is.EqualTo(SystemTypes(analyticalModel.ToTM59(tM59Manager))));
            Assert.That(SystemTypes(building), Is.EqualTo(new[] { SystemType.MechanicalVentilation }));

            //And a null model still refuses rather than throwing, either way in.
            Assert.That(((AnalyticalModel)null).ToTM59(tM59Manager, new VentilationStrategyMap(), out _), Is.Null);
            Assert.That(((AnalyticalModel)null).ToTM59(tM59Manager), Is.Null);
        }

        /// <summary>
        /// <b>What a strategy means is unchanged.</b> The XML vocabulary has only two values, so the corridor
        /// criterion the assessment distinguishes has no representation here and <c>UV</c> exports as naturally
        /// ventilated - the same <c>Query.IsMechanicalVentilation</c> mapping the old derivations used. Step 7
        /// changed which strategy applies, not what one means.
        /// </summary>
        [TestCase("NV", SystemType.NaturalVentilation)]
        [TestCase("UV", SystemType.NaturalVentilation)]
        [TestCase("MV", SystemType.MechanicalVentilation)]
        [TestCase("MVRE", SystemType.MechanicalVentilation)]
        public void TheStrategyToSystemTypeMapping_IsTheExistingOne(string ventilationStrategy, SystemType systemType_Expected)
        {
            AnalyticalModel analyticalModel = Model("NV");

            Building building = analyticalModel.ToTM59(tM59Manager, Map(analyticalModel, ventilationStrategy), out List<string> refusals);

            Assert.That(refusals, Is.Empty);
            Assert.That(SystemTypes(building), Is.EqualTo(new[] { systemType_Expected }));
        }

        /// <summary>
        /// The exported zone still carries everything it did - the room use, the stamped zone guid, the export
        /// flag - so making the strategy authoritative changed one field of the export and nothing else.
        /// </summary>
        [Test]
        public void OnlyTheSystemType_Changes()
        {
            AnalyticalModel analyticalModel = Model("NV");

            Zone zone_Derived = analyticalModel.ToTM59(tM59Manager).Zones[0];
            Zone zone_Scenario = analyticalModel.ToTM59(tM59Manager, Map(analyticalModel, "MVRE"), out _).Zones[0];

            Assert.That(zone_Scenario.Name, Is.EqualTo(zone_Derived.Name));
            Assert.That(zone_Scenario.Guid, Is.EqualTo(zone_Derived.Guid));
            Assert.That(zone_Scenario.RoomUse, Is.EqualTo(zone_Derived.RoomUse));
            Assert.That(zone_Scenario.Factor, Is.EqualTo(zone_Derived.Factor));
            Assert.That(zone_Scenario.Export, Is.EqualTo(zone_Derived.Export));
            Assert.That(zone_Scenario.WindSpeed, Is.EqualTo(zone_Derived.WindSpeed));

            //Not vacuous - the room really did resolve to a bedroom rather than Undefined.
            Assert.That(zone_Derived.RoomUse, Is.EqualTo(RoomUse.Bedroom));
            Assert.That(zone_Scenario.SystemType, Is.Not.EqualTo(zone_Derived.SystemType));
        }

        // ---------------------------------------------------------------------------------------------
        // Fixture
        // ---------------------------------------------------------------------------------------------

        private static SystemType[] SystemTypes(Building building)
        {
            return building?.Zones?.ConvertAll(x => x.SystemType)?.ToArray();
        }

        private static OverheatingScenario Scenario(string ventilationStrategy)
        {
            SystemTemplate systemTemplate = ventilationStrategy == null ? null : new SystemTemplate(ventilationStrategy, null, null, null, null, null);

            return new OverheatingScenario(PartOAssessmentScope.Dwelling, Guid.NewGuid(), PartOIteration.Undefined, systemTemplate);
        }

        private static VentilationStrategyMap Map(AnalyticalModel analyticalModel, string ventilationStrategy)
        {
            VentilationStrategyMap result = new VentilationStrategyMap();

            result.Add(Scenario(ventilationStrategy), analyticalModel.GetSpaces());

            return result;
        }

        private static Space Find(List<Space> spaces, string name)
        {
            return spaces.Find(x => x.Name == name);
        }

        private static AnalyticalModel Model(string ventilationSystemTypeName, bool ventilationSystem = false)
        {
            AdjacencyCluster adjacencyCluster = new AdjacencyCluster();

            Space space = Space("Flat 1 Bedroom 2", ventilationSystemTypeName);

            adjacencyCluster.AddObject(space);

            if (ventilationSystem)
            {
                VentilationSystem ventilationSystem_Temp = new VentilationSystem("1", new VentilationSystemType("MVRE", "Mechanical Ventilation with Recirculation"));

                adjacencyCluster.AddObject(ventilationSystem_Temp);
                adjacencyCluster.AddRelation(ventilationSystem_Temp, space);
            }

            return new AnalyticalModel("Three Flats", null, null, null, adjacencyCluster);
        }

        /// <summary>Three flats each with a "Bedroom 2", all stating NV in their design data.</summary>
        private static AnalyticalModel Model_ThreeFlats()
        {
            AdjacencyCluster adjacencyCluster = new AdjacencyCluster();

            foreach (string name in new[] { "Flat 1", "Flat 2", "Flat 3" })
            {
                adjacencyCluster.AddObject(Space(name + " Bedroom 2", "NV"));
            }

            return new AnalyticalModel("Three Flats", null, null, null, adjacencyCluster);
        }

        private static Space Space(string name, string ventilationSystemTypeName)
        {
            Space result = new Space(name);

            InternalCondition internalCondition = new InternalCondition(name);

            if (!string.IsNullOrEmpty(ventilationSystemTypeName))
            {
                internalCondition.SetValue(InternalConditionParameter.VentilationSystemTypeName, ventilationSystemTypeName);
            }

            result.InternalCondition = internalCondition;

            return result;
        }
    }
}
