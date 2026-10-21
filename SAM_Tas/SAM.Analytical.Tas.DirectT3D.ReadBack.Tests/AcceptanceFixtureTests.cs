// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical.Tas.DirectT3D.Validation.ReadBack;
using SAM.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace SAM.Analytical.Tas.DirectT3D.ReadBack.Tests
{
    /// <summary>
    /// S08 PR-C: the committed equivalence fixtures are the files under ReadBack/Fixtures, byte for byte, they are what
    /// the committed factory authors, and they meet the D-FIX criteria and their declared conventions. Offline, no TAS.
    /// </summary>
    public class AcceptanceFixtureTests
    {
        private static string FixturesRoot
        {
            get
            {
                DirectoryInfo directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
                while (directory != null)
                {
                    string candidate = Path.Combine(directory.FullName, "SAM_Tas", "SAM.Analytical.Tas.DirectT3D.Validation", "ReadBack", "Fixtures");
                    if (Directory.Exists(candidate)) return candidate;
                    directory = directory.Parent;
                }

                throw new DirectoryNotFoundException("ReadBack/Fixtures not found above " + TestContext.CurrentContext.TestDirectory);
            }
        }

        private static AnalyticalModel Load(string name)
        {
            return Core.Convert.ToSAM<AnalyticalModel>(Path.Combine(FixturesRoot, name + ".json")).Single();
        }

        private static string Sha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return string.Concat(sha.ComputeHash(stream).Select(x => x.ToString("x2")));
            }
        }

        // Each minted GUID replaced by its order of first appearance: equal text means the same model, authored the same way.
        private static string Canonical(string json)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.Ordinal);
            return Regex.Replace(json, "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", match =>
            {
                string key = match.Value.ToLowerInvariant();
                if (!map.TryGetValue(key, out string token))
                {
                    token = "G" + map.Count;
                    map[key] = token;
                }

                return token;
            });
        }

        [Test]
        public void TheCommittedFixtures_MatchTheirRecordedHashes()
        {
            string[] lines = File.ReadAllLines(Path.Combine(FixturesRoot, "SHA256SUMS.txt")).Where(x => x.Trim().Length > 0).ToArray();
            Assert.That(lines.Select(x => x.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1]), Is.EquivalentTo(AcceptanceFixtures.Names.Select(x => x + ".json")));
            foreach (string line in lines)
            {
                string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                Assert.That(Sha256(Path.Combine(FixturesRoot, parts[1])), Is.EqualTo(parts[0]), parts[1]);
            }
        }

        [TestCaseSource(nameof(Names))]
        public void TheCommittedFixture_IsWhatTheFactoryAuthors(string name)
        {
            string path = Path.Combine(Path.GetTempPath(), "s08c-tests", name + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Core.Convert.ToFile(AcceptanceFixtures.Create(name), path, SAMFileType.Json);

            Assert.That(Canonical(File.ReadAllText(Path.Combine(FixturesRoot, name + ".json"))), Is.EqualTo(Canonical(File.ReadAllText(path))));
        }

        public static IEnumerable<string> Names => AcceptanceFixtures.Names;

        [TestCaseSource(nameof(Names))]
        public void TheFixture_MeetsTheDFixCriteria(string name)
        {
            AnalyticalModel model = Load(name);
            ExpectedModel expected = ReadBackExpected.Build(model, null, name);

            Assert.That(expected.FixtureCriteria, Is.Empty, "D-FIX criteria broken");
            Assert.That(expected.Spaces, Has.Count.EqualTo(name == AcceptanceFixtures.Box ? 1 : 2));
            foreach (ExpectedSpace space in expected.Spaces)
            {
                // D-F9 C: stored = geometric within the 0.5 % of rows 1a and 2.
                Assert.That(space.FloorArea.State, Is.EqualTo(InputEquivalence.SamInputState.Value), space.Name);
                Assert.That(space.ShellVolume.State, Is.EqualTo(InputEquivalence.SamInputState.Value), space.Name);
                Assert.That(System.Math.Abs(space.StoredArea - space.FloorArea.Value) / space.FloorArea.Value, Is.LessThan(0.005), space.Name + " area");
                Assert.That(System.Math.Abs(space.StoredVolume - space.ShellVolume.Value) / space.ShellVolume.Value, Is.LessThan(0.005), space.Name + " volume");

                // Every gated gain, fraction and rate authored (no DOCUMENTED-DEFAULT), and no exhaust.
                foreach (string parameter in new[] { "OccupancySensibleGainPerPerson", "OccupancyLatentGainPerPerson", "LightingGainPerArea", "EquipmentSensibleGainPerArea", "EquipmentLatentGainPerArea", "InfiltrationAirChangesPerHour", "LightingRadiantProportion", "OccupancyRadiantProportion", "EquipmentRadiantProportion" })
                {
                    Assert.That(double.IsNaN(space.Parameters[parameter]), Is.False, space.Name + " " + parameter);
                }

                Assert.That(space.Occupancy, Is.GreaterThan(0), space.Name);
                foreach (string slot in new[] { "ticOSG", "ticLG", "ticESG", "ticELG", "ticI", "ticLL", "ticUL", "ticHLL", "ticHUL" })
                {
                    Assert.That(space.Profiles.ContainsKey(slot), Is.True, space.Name + " " + slot);
                }

                foreach (ExpectedAperture aperture in space.Panels.SelectMany(x => x.Apertures))
                {
                    Assert.That(aperture.HasOpeningProperties, Is.False);
                    Assert.That(aperture.PaneLayers, Has.Count.EqualTo(1), "single pane, no gas layer");
                    Assert.That(aperture.PaneLayers[0].Material, Is.InstanceOf<TransparentMaterial>());
                    Assert.That(aperture.PaneArea.State, Is.EqualTo(InputEquivalence.SamInputState.Value), "T-FRAME split computable");
                }

                foreach (ExpectedPanel panel in space.Panels)
                {
                    Assert.That(panel.Layers.All(x => x.Material != null), Is.True, panel.ConstructionName + ": every layer's material is in the library");
                }
            }

            Assert.That(model.AdjacencyCluster.GetObjects<MechanicalSystem>() ?? new List<MechanicalSystem>(), Is.Empty);
        }

        [TestCaseSource(nameof(Names))]
        public void TheFixture_DeclaresCentreLinePanelsWithPhysicalThickness(string name)
        {
            AnalyticalModel model = Load(name);
            List<Panel> panels = model.AdjacencyCluster.GetPanels();
            Assert.That(panels, Is.Not.Empty);
            foreach (Panel panel in panels)
            {
                Assert.That(panel.TryGetValue(PanelSourceParameter.PhysicalThickness, out double thickness), Is.True, panel.Name);
                Assert.That(thickness, Is.EqualTo(panel.Construction.ConstructionLayers.Sum(x => x.Thickness)).Within(1e-12), panel.Name);
            }

            Assert.That(model.Description, Does.Contain(AcceptanceFixtures.PanelConvention));
        }

        [Test]
        public void TheBox_IsV2_AndTheTwoZoneModel_IsV1InBothZones()
        {
            ExpectedModel box = ReadBackExpected.Build(Load(AcceptanceFixtures.Box), null, AcceptanceFixtures.Box);
            ExpectedSpace boxSpace = box.Spaces.Single();
            Assert.That(boxSpace.Profiles.ContainsKey("ticV"), Is.True, "V2: an explicitly selected Ventilation profile");
            Assert.That(Requirement(boxSpace), Is.True, "V2: a ventilation requirement");

            ExpectedModel two = ReadBackExpected.Build(Load(AcceptanceFixtures.TwoZones), null, AcceptanceFixtures.TwoZones);
            foreach (ExpectedSpace space in two.Spaces)
            {
                Assert.That(space.Profiles.ContainsKey("ticV"), Is.False, space.Name + " V1: no delivery selected");
                Assert.That(Requirement(space), Is.True, space.Name + " V1: a ventilation requirement");
            }
        }

        private static bool Requirement(ExpectedSpace space)
        {
            return new[] { "SupplyAirFlow", "SupplyAirFlowPerArea", "SupplyAirFlowPerPerson", "SupplyAirChangesPerHour" }.Any(x => !double.IsNaN(space.Parameters[x]) && space.Parameters[x] > 0);
        }

        [TestCaseSource(nameof(Names))]
        public void TheFixture_SharesNothingWithTheCalibrationModels(string name)
        {
            AnalyticalModel fixture = Load(name);
            foreach (string calibration in CalibrationModels.Names)
            {
                AnalyticalModel model = CalibrationModels.Create(calibration);
                Assert.That(fixture.MaterialLibrary.GetMaterials().Select(x => x.Name).Intersect(model.MaterialLibrary.GetMaterials().Select(x => x.Name)), Is.Empty, calibration + " materials");
                Assert.That(fixture.AdjacencyCluster.GetPanels().Select(x => x.Construction.Name).Intersect(model.AdjacencyCluster.GetPanels().Select(x => x.Construction.Name)), Is.Empty, calibration + " constructions");
                Assert.That(fixture.AdjacencyCluster.GetSpaces().Select(x => x.Guid).Intersect(model.AdjacencyCluster.GetSpaces().Select(x => x.Guid)), Is.Empty, calibration + " spaces");
                Assert.That(fixture.AdjacencyCluster.GetSpaces().Select(x => x.InternalCondition.Name).Intersect(model.AdjacencyCluster.GetSpaces().Select(x => x.InternalCondition.Name)), Is.Empty, calibration + " conditions");
            }
        }
    }
}
