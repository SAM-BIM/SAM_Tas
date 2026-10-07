// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical.Tas;
using SAM.Analytical.Tas.DirectT3D.Validation;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Tas.TM59.Tests
{
    /// <summary>
    /// The load-sensitive three-zone fixture (<see cref="LoadSensitiveModel"/>) that the licensed harness runs through both T3D
    /// routes and simulates. These tests are the COM-free half: they pin that the fixture really has the properties it is meant to
    /// have - so a later edit cannot quietly turn it back into a symmetric model on which the routes agree by accident - and
    /// that the direct route's plan of it is what the licensed comparison then expects.
    /// </summary>
    [TestFixture]
    public class LoadSensitiveModelTests
    {
        private static AnalyticalModel Model()
        {
            return LoadSensitiveModel.Create();
        }

        [Test]
        public void Fixture_HasTheStatedShape()
        {
            AnalyticalModel model = Model();
            AdjacencyCluster cluster = model.AdjacencyCluster;

            Assert.Multiple(() =>
            {
                Assert.That(cluster.GetSpaces().Select(x => x.Name), Is.EqualTo(LoadSensitiveModel.Expected.SpaceNames), "model order A, B, C - it decides which side of each partition is reversed");
                Assert.That(cluster.GetPanels().Count, Is.EqualTo(LoadSensitiveModel.Expected.Panels));
                Assert.That(cluster.GetApertures().Count, Is.EqualTo(LoadSensitiveModel.Expected.Apertures));
                Assert.That(cluster.GetPanels().Count(x => Analytical.Query.Adiabatic(x)), Is.EqualTo(1), "exactly one adiabatic panel");
                Assert.That(cluster.GetApertures().Select(x => x.GetArea()).Distinct().Count(), Is.EqualTo(5).Within(0), "five windows, five different areas");
                Assert.That(cluster.GetApertures().Select(x => x.ApertureConstruction.Name).Distinct().Count(), Is.EqualTo(2), "two glazing types");
            });
        }

        [Test]
        public void Fixture_EveryConstructionLayerAndEveryProfileResolvesInItsLibrary()
        {
            AnalyticalModel model = Model();

            List<string> materials = new List<string>();
            foreach (Construction construction in model.AdjacencyCluster.GetConstructions())
            {
                materials.AddRange(construction.ConstructionLayers.Select(x => x.Name));
            }

            foreach (ApertureConstruction apertureConstruction in model.AdjacencyCluster.GetApertureConstructions())
            {
                materials.AddRange(apertureConstruction.PaneConstructionLayers.Select(x => x.Name));
                materials.AddRange(apertureConstruction.FrameConstructionLayers.Select(x => x.Name));
            }

            foreach (string material in materials.Distinct())
            {
                Assert.That(model.MaterialLibrary.GetObject<SAM.Core.IMaterial>(material), Is.Not.Null, "material '" + material + "' is in the library, or TAS would build an empty layer");
            }

            foreach (Space space in model.AdjacencyCluster.GetSpaces())
            {
                InternalCondition internalCondition = space.InternalCondition;
                Assert.That(internalCondition, Is.Not.Null, space.Name);

                foreach (ProfileType profileType in new[] { ProfileType.Occupancy, ProfileType.EquipmentSensible, ProfileType.Lighting, ProfileType.Infiltration, ProfileType.Heating, ProfileType.Cooling })
                {
                    Assert.That(internalCondition.GetProfile(profileType, model.ProfileLibrary), Is.Not.Null, space.Name + " " + profileType + ": a profile name that does not resolve would drop the property silently");
                }
            }
        }

        [Test]
        public void Fixture_EveryZoneDiffersInEveryLoadDeterminingProperty()
        {
            AnalyticalModel model = Model();
            List<Space> spaces = model.AdjacencyCluster.GetSpaces();

            foreach (InternalConditionParameter parameter in new[]
            {
                InternalConditionParameter.AreaPerPerson,
                InternalConditionParameter.OccupancySensibleGainPerPerson,
                InternalConditionParameter.EquipmentSensibleGainPerArea,
                InternalConditionParameter.LightingGainPerArea,
                InternalConditionParameter.InfiltrationAirChangesPerHour
            })
            {
                List<double> values = spaces.Select(x => { x.InternalCondition.TryGetValue(parameter, out double value); return value; }).ToList();
                Assert.That(values.Distinct().Count(), Is.EqualTo(3), parameter + " must differ in every zone, or a mix-up of zone identities would cancel");
            }

            foreach (ProfileType profileType in new[] { ProfileType.Occupancy, ProfileType.Heating, ProfileType.Cooling })
            {
                List<string> names = spaces.Select(x => x.InternalCondition.GetProfile(profileType, model.ProfileLibrary)).Select(x => string.Join(",", x.GetValues())).ToList();
                Assert.That(names.Distinct().Count(), Is.EqualTo(3), profileType + " profile values must differ in every zone");
            }

            Assert.That(spaces.Count(x => x.InternalCondition.GetProfile(ProfileType.Ventilation, model.ProfileLibrary) != null), Is.EqualTo(2), "two zones are mechanically ventilated, one is not");
        }

        [Test]
        public void Fixture_EveryGuidIsFixed_SoTwoProcessesBuildTheSameModel()
        {
            // The licensed comparison builds the model once per route, in separate processes, and matches objects by GUID.
            AnalyticalModel a = Model();
            AnalyticalModel b = Model();

            Assert.Multiple(() =>
            {
                Assert.That(a.AdjacencyCluster.GetSpaces().Select(x => x.Guid), Is.EqualTo(b.AdjacencyCluster.GetSpaces().Select(x => x.Guid)));
                Assert.That(a.AdjacencyCluster.GetPanels().Select(x => x.Guid), Is.EqualTo(b.AdjacencyCluster.GetPanels().Select(x => x.Guid)));
                Assert.That(a.AdjacencyCluster.GetApertures().Select(x => x.Guid), Is.EqualTo(b.AdjacencyCluster.GetApertures().Select(x => x.Guid)));
                Assert.That(a.AdjacencyCluster.GetPanels().Select(x => x.Guid).Distinct().Count(), Is.EqualTo(LoadSensitiveModel.Expected.Panels), "and they are distinct");
            });
        }

        [TestCase(PanelOrder.Reversed)]
        [TestCase(PanelOrder.Rotated)]
        public void Fixture_AnotherPanelOrder_IsTheSameBuildingWithTheSurfacesCreatedInAnotherOrder(PanelOrder panelOrder)
        {
            AnalyticalModel asListed = LoadSensitiveModel.Create();
            AnalyticalModel reordered = LoadSensitiveModel.Create(false, panelOrder);

            List<System.Guid> before = asListed.AdjacencyCluster.GetPanels().Select(x => x.Guid).ToList();
            List<System.Guid> after = reordered.AdjacencyCluster.GetPanels().Select(x => x.Guid).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(after, Is.Not.EqualTo(before), "the order really is different");
                Assert.That(after, Is.EquivalentTo(before), "the panels are the same ones");
                Assert.That(reordered.AdjacencyCluster.GetApertures().Select(x => x.Guid), Is.EquivalentTo(asListed.AdjacencyCluster.GetApertures().Select(x => x.Guid)));
                Assert.That(reordered.AdjacencyCluster.GetSpaces().Select(x => x.Name), Is.EqualTo(LoadSensitiveModel.Expected.SpaceNames), "the space order - which decides the reversed sides - is untouched");

                // The same relations: every panel still bounds the same spaces.
                foreach (Panel panel in asListed.AdjacencyCluster.GetPanels())
                {
                    Panel other = reordered.AdjacencyCluster.GetObject<Panel>(panel.Guid);
                    Assert.That(reordered.AdjacencyCluster.GetSpaces(other).Select(x => x.Name).OrderBy(x => x), Is.EqualTo(asListed.AdjacencyCluster.GetSpaces(panel).Select(x => x.Name).OrderBy(x => x)), panel.Construction.Name);
                }
            });
        }

        [Test]
        public void Fixture_ReversedModelOrder_ListsTheSpacesTheOtherWayRound_AndNothingElseChanges()
        {
            AnalyticalModel model = LoadSensitiveModel.Create(reverseModelOrder: true);
            T3DImportPlan plan = model.T3DImportPlan();

            Assert.Multiple(() =>
            {
                Assert.That(model.AdjacencyCluster.GetSpaces().Select(x => x.Name), Is.EqualTo(LoadSensitiveModel.Expected.SpaceNames.Reverse()));
                Assert.That(plan.Zones.Select(x => x.Name), Is.EqualTo(LoadSensitiveModel.Expected.SpaceNames.Reverse()));
                Assert.That(model.AdjacencyCluster.GetPanels().Count, Is.EqualTo(LoadSensitiveModel.Expected.Panels));

                // The A-B partition now belongs to B first (B is earlier in the model): the reversed side moves to A.
                Panel partition = model.AdjacencyCluster.GetPanels().Single(x => x.Construction.Name == "PART_AB_ASYM");
                T3DSurfaceSpec surface = plan.Surfaces.Single(x => x.PanelGuid == partition.Guid);
                Assert.That(plan.Zones[surface.Zone].Name, Is.EqualTo(LoadSensitiveModel.SpaceName_Meeting));
                Assert.That(plan.Zones[surface.Zone2].Name, Is.EqualTo(LoadSensitiveModel.SpaceName_Office));
            });
        }

        [Test]
        public void Plan_ThreeZonesInModelOrder_EachCarryingItsSpaceGuidAndTheLevelName()
        {
            AnalyticalModel model = Model();
            T3DImportPlan plan = model.T3DImportPlan();
            List<Space> spaces = model.AdjacencyCluster.GetSpaces();

            Assert.Multiple(() =>
            {
                Assert.That(plan.Zones.Select(x => x.Name), Is.EqualTo(LoadSensitiveModel.Expected.SpaceNames));
                Assert.That(plan.Zones.Select(x => x.SpaceGuid), Is.EqualTo(spaces.Select(x => x.Guid)));
                Assert.That(plan.Zones.Select(x => x.LevelName).Distinct().Single(), Is.EqualTo("Level 0"));
            });
        }

        [Test]
        public void Plan_SurfaceKindsAndOpenings_AreWhatTheFixtureStates()
        {
            T3DImportPlan plan = Model().T3DImportPlan();

            Assert.Multiple(() =>
            {
                Assert.That(plan.Surfaces.Count(x => x.Kind == T3DSurfaceKind.Internal), Is.EqualTo(2), "A-B and A-C");
                Assert.That(plan.Surfaces.Count(x => x.Kind == T3DSurfaceKind.Adiabatic), Is.EqualTo(1), "C east");
                Assert.That(plan.Surfaces.Count(x => x.Kind == T3DSurfaceKind.Ground), Is.EqualTo(3));
                Assert.That(plan.Surfaces.Count(x => x.Kind == T3DSurfaceKind.External), Is.EqualTo(11), "3 roofs + 8 external walls");
                Assert.That(plan.Surfaces.Sum(x => x.Openings.Count), Is.EqualTo(5));
                Assert.That(plan.Windows.Count, Is.EqualTo(5), "one TAS window object per aperture");
                Assert.That(plan.Report.Skipped, Is.Empty, "nothing in the fixture may be skipped");
            });
        }

        [Test]
        public void Plan_TheAsymmetricPartition_BelongsToTheEarlierSpaceInTheModel_WhateverOrderItsRelationsAreStoredIn()
        {
            AnalyticalModel model = Model();
            T3DImportPlan plan = model.T3DImportPlan();

            // The A-B partition's relations are stored B first on purpose.
            Panel partition = model.AdjacencyCluster.GetPanels().Single(x => x.Construction.Name == "PART_AB_ASYM");
            Assert.That(model.AdjacencyCluster.GetSpaces(partition).First().Name, Is.EqualTo(LoadSensitiveModel.SpaceName_Meeting), "the fixture stores the relation order B, A");

            T3DSurfaceSpec surface = plan.Surfaces.Single(x => x.PanelGuid == partition.Guid);
            Assert.Multiple(() =>
            {
                Assert.That(plan.Zones[surface.Zone].Name, Is.EqualTo(LoadSensitiveModel.SpaceName_Office), "first zone = earlier in the model");
                Assert.That(plan.Zones[surface.Zone2].Name, Is.EqualTo(LoadSensitiveModel.SpaceName_Meeting));
            });
        }
    }
}
