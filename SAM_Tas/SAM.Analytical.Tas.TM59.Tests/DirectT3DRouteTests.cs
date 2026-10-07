// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical.Tas;
using SAM.Analytical.Tas.DirectT3D.Validation;
using SAM.Geometry.Spatial;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace SAM.Analytical.Tas.TM59.Tests
{
    /// <summary>
    /// <b>The direct SAM -> T3D route, everything that can be decided without a TAS licence.</b>
    /// <para>
    /// The route is two halves on purpose. <see cref="Tas.Query.T3DImportPlan"/> decides every zone, element, window
    /// type, surface, opening and shade from the SAM model alone; <c>Convert.ToT3D</c> replays that plan into TAS. These
    /// tests pin the first half and the plumbing around it (the workflow setting, the geometry helper, the zone
    /// identity). What TAS then builds from the plan - areas, volumes, link types - is measured on a licensed machine by
    /// <c>SAM.Analytical.Tas.DirectT3D.Validation</c> and recorded in DIRECT_T3D_ROUTE.md.
    /// </para>
    /// <para>
    /// The synthetic buildings are the harness's own (<c>SyntheticModels.cs</c> is compiled into both), so what is
    /// asserted here about a box is what the licensed run then builds in TAS.
    /// </para>
    /// </summary>
    [TestFixture]
    public class DirectT3DRouteTests
    {
        // ------------------------------------------------------------------------------------------------------
        // The workflow setting: GbXML is the default and only an explicit "Direct" changes anything.
        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void WorkflowSettings_DefaultRoute_IsGbXML()
        {
            Assert.That(new WorkflowSettings().T3DRoute, Is.EqualTo(T3DRoute.GbXML), "a new settings object must be the established route");
            Assert.That(new WorkflowSettings(new WorkflowSettings()).T3DRoute, Is.EqualTo(T3DRoute.GbXML), "and so must a copy of one");
            Assert.That(default(T3DRoute), Is.EqualTo(T3DRoute.GbXML), "the enum's zero value is the gbXML route, so an uninitialised field can never mean Direct");
        }

        [Test]
        public void WorkflowSettings_ASettingsFileWrittenBeforeTheSettingExisted_ReadsAsGbXML()
        {
            // Exactly what a pre-existing serialized WorkflowSettings looks like: every old key, no T3DRoute.
            JsonObject legacy = new JsonObject
            {
                ["_type"] = "SAM.Analytical.Tas.WorkflowSettings",
                ["Path_TBD"] = @"C:\x\model.tbd",
                ["Path_gbXML"] = @"C:\x\model.xml",
                ["UnmetHours"] = true,
                ["Simulate"] = true,
                ["Sizing"] = true,
                ["UpdateZones"] = true,
                ["UseWidths"] = true,
                ["AddIZAMs"] = true,
                ["SimulateFrom"] = 1,
                ["SimulateTo"] = 365,
            };

            WorkflowSettings workflowSettings = new WorkflowSettings(legacy);

            Assert.Multiple(() =>
            {
                Assert.That(workflowSettings.T3DRoute, Is.EqualTo(T3DRoute.GbXML));
                Assert.That(workflowSettings.Path_gbXML, Is.EqualTo(@"C:\x\model.xml"), "everything else it said is still read");
                Assert.That(workflowSettings.UseWidths, Is.True);
                Assert.That(workflowSettings.SimulateTo, Is.EqualTo(365));
            });
        }

        [TestCase(T3DRoute.GbXML)]
        [TestCase(T3DRoute.Direct)]
        public void WorkflowSettings_Route_RoundTripsThroughJson(T3DRoute route)
        {
            WorkflowSettings source = new WorkflowSettings { T3DRoute = route, Path_TBD = @"C:\x\model.tbd" };

            JsonObject jsonObject = source.ToJsonObject();
            WorkflowSettings readBack = new WorkflowSettings(jsonObject);

            Assert.Multiple(() =>
            {
                Assert.That(jsonObject["T3DRoute"]?.GetValue<string>(), Is.EqualTo(route.ToString()), "written by name, so the file says what it means");
                Assert.That(readBack.T3DRoute, Is.EqualTo(route));
                Assert.That(new WorkflowSettings(readBack).T3DRoute, Is.EqualTo(route), "and the copy constructor carries it");
            });
        }

        [Test]
        public void WorkflowSettings_Route_OnlyAnExplicitDirectSelectsDirect()
        {
            Func<JsonNode, T3DRoute> read = node => new WorkflowSettings(new JsonObject { ["T3DRoute"] = node }).T3DRoute;

            Assert.Multiple(() =>
            {
                Assert.That(read(JsonValue.Create("Direct")), Is.EqualTo(T3DRoute.Direct));
                Assert.That(read(JsonValue.Create(" direct ")), Is.EqualTo(T3DRoute.Direct), "the name is read case-insensitively and trimmed");
                Assert.That(read(JsonValue.Create(1)), Is.EqualTo(T3DRoute.Direct), "and so is its number");

                Assert.That(read(JsonValue.Create("GbXML")), Is.EqualTo(T3DRoute.GbXML));
                Assert.That(read(null), Is.EqualTo(T3DRoute.GbXML), "an explicit null");
                Assert.That(read(JsonValue.Create("")), Is.EqualTo(T3DRoute.GbXML));
                Assert.That(read(JsonValue.Create("Directly")), Is.EqualTo(T3DRoute.GbXML), "text that merely resembles it");
                Assert.That(read(JsonValue.Create("Idf")), Is.EqualTo(T3DRoute.GbXML), "a route that does not exist");
                Assert.That(read(JsonValue.Create(0)), Is.EqualTo(T3DRoute.GbXML));
                Assert.That(read(JsonValue.Create(2)), Is.EqualTo(T3DRoute.GbXML), "a number that is not Direct's");
                Assert.That(read(JsonValue.Create(true)), Is.EqualTo(T3DRoute.GbXML), "a value of the wrong type");
                Assert.That(read(new JsonObject()), Is.EqualTo(T3DRoute.GbXML), "a structure where a name should be");
            });
        }

        [Test]
        public void WorkflowSettings_TheSettingDoesNotDisturbAnyOther()
        {
            WorkflowSettings source = new WorkflowSettings { Path_gbXML = "a.xml", UseWidths = true, RemoveIZAMs = true, Simulate = false };
            WorkflowSettings direct = new WorkflowSettings(source) { T3DRoute = T3DRoute.Direct };

            Assert.Multiple(() =>
            {
                Assert.That(new WorkflowSettings(direct.ToJsonObject()).Path_gbXML, Is.EqualTo("a.xml"), "Direct ignores Path_gbXML but does not erase it");
                Assert.That(new WorkflowSettings(direct.ToJsonObject()).UseWidths, Is.True);
                Assert.That(new WorkflowSettings(direct.ToJsonObject()).RemoveIZAMs, Is.True);
                Assert.That(new WorkflowSettings(direct.ToJsonObject()).Simulate, Is.False);
            });
        }

        [Test]
        public void Calculate_DirectRouteWithACanonicalTBD_IsRefusedBeforeAnythingRuns()
        {
            // The direct route converts the geometry; a canonical TBD says it is already converted. Contradictory, like gbXML + canonical.
            WorkflowCalculator workflowCalculator = new WorkflowCalculator(new WorkflowSettings
            {
                T3DRoute = T3DRoute.Direct,
                Path_TBD = @"C:\does-not-exist\model.tbd",
                Path_TBD_Canonical = @"C:\does-not-exist\canonical.tbd",
            });

            AnalyticalModel result = workflowCalculator.Calculate(new AnalyticalModel(Guid.NewGuid(), "model"));

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Null);
                Assert.That(workflowCalculator.Notes.Any(x => x.Contains("T3DRoute.Direct") && x.Contains("canonical")), Is.True, string.Join(" | ", workflowCalculator.Notes));
            });
        }

        // ------------------------------------------------------------------------------------------------------
        // The geometry helper: one place a SAM polygon becomes a TAS polygon.
        // ------------------------------------------------------------------------------------------------------

        private static Point3D P(double x, double y, double z)
        {
            return new Point3D(x, y, z);
        }

        [Test]
        public void TasPolygon_DropsTheRepeatedClosingVertex()
        {
            List<Point3D> polygon = new[] { P(0, 0, 0), P(4, 0, 0), P(4, 3, 0), P(0, 3, 0), P(0, 0, 0) }.TasPolygon();

            Assert.That(polygon, Has.Count.EqualTo(4), "TAS takes an open loop");
        }

        [Test]
        public void TasPolygon_MergesNearDuplicateVertices()
        {
            List<Point3D> polygon = new[] { P(0, 0, 0), P(0.0002, 0, 0), P(4, 0, 0), P(4, 3, 0), P(4, 3.0004, 0), P(0, 3, 0) }.TasPolygon(0.001);

            Assert.That(polygon, Has.Count.EqualTo(4));
        }

        [Test]
        public void TasPolygon_RemovesCollinearVertices_AndIsRepeatableToAFixedPoint()
        {
            // Three vertices along the bottom edge, and one on the right edge.
            List<Point3D> polygon = new[] { P(0, 0, 0), P(1, 0, 0), P(2, 0, 0), P(4, 0, 0), P(4, 1.5, 0), P(4, 3, 0), P(0, 3, 0) }.TasPolygon();

            Assert.Multiple(() =>
            {
                Assert.That(polygon, Has.Count.EqualTo(4));
                Assert.That(polygon.NewellNormal().Length / 2, Is.EqualTo(12).Within(1e-9), "the polygon is the same 4 x 3 rectangle");
            });
        }

        [Test]
        public void TasPolygon_Tolerance_DecidesWhatIsCollinear()
        {
            Point3D[] bowed = { P(0, 0, 0), P(2, 0.004, 0), P(4, 0, 0), P(4, 3, 0), P(0, 3, 0) };

            Assert.Multiple(() =>
            {
                Assert.That(bowed.TasPolygon(0.001), Has.Count.EqualTo(5), "4 mm off the line is a real vertex at a 1 mm tolerance");
                Assert.That(bowed.TasPolygon(0.01), Has.Count.EqualTo(4), "and noise at 1 cm");
            });
        }

        [Test]
        public void TasPolygon_ADegeneratePolygonIsNotASurface()
        {
            Assert.Multiple(() =>
            {
                Assert.That(new[] { P(0, 0, 0), P(1, 0, 0) }.TasPolygon(), Is.Null, "two points");
                Assert.That(new[] { P(0, 0, 0), P(1, 0, 0), P(2, 0, 0) }.TasPolygon(), Is.Null, "three collinear points");
                Assert.That(new[] { P(0, 0, 0), P(1, 0, 0), P(0, 0, 0), P(1, 0, 0) }.TasPolygon(), Is.Null, "a loop that doubles back on itself");
                Assert.That(((IEnumerable<Point3D>)null).TasPolygon(), Is.Null);
                Assert.That(new Point3D[] { null, P(0, 0, 0), null }.TasPolygon(), Is.Null, "nulls are skipped, not thrown on");
            });
        }

        [Test]
        public void TasPolygon_OrientsToTheRequiredNormal()
        {
            Point3D[] counterClockwiseFromAbove = { P(0, 0, 0), P(4, 0, 0), P(4, 3, 0), P(0, 3, 0) };

            List<Point3D> up = counterClockwiseFromAbove.TasPolygon(new Vector3D(0, 0, 1), 0.001, out bool reversed_Up);
            List<Point3D> down = counterClockwiseFromAbove.TasPolygon(new Vector3D(0, 0, -1), 0.001, out bool reversed_Down);

            Assert.Multiple(() =>
            {
                Assert.That(reversed_Up, Is.False);
                Assert.That(up.NewellNormal().Z, Is.GreaterThan(0));
                Assert.That(reversed_Down, Is.True);
                Assert.That(down.NewellNormal().Z, Is.LessThan(0), "reversed to point down");
                Assert.That(down.NewellNormal().Length, Is.EqualTo(up.NewellNormal().Length).Within(1e-9), "and still the same polygon");
            });
        }

        [Test]
        public void TasPolygon_Orientation_IsRightForAVerticalAndASlopedPanel()
        {
            Point3D[] wall = { P(0, 0, 0), P(4, 0, 0), P(4, 0, 3), P(0, 0, 3) };               // in the XZ plane; right-hand normal is -Y
            Point3D[] slope = { P(0, 0, 0), P(4, 0, 0), P(4, 3, 1.5), P(0, 3, 1.5) };          // tilted about X; right-hand normal points up and toward -Y (0, -0.45, 0.9)

            Vector3D outwardOfWall = new Vector3D(0, 1, 0);
            Vector3D outwardOfSlope = new Vector3D(0, 0.4, -1);                                  // an underside: down and toward +Y

            List<Point3D> wall_Reoriented = wall.TasPolygon(outwardOfWall, 0.001, out bool wallReversed);
            List<Point3D> slope_Reoriented = slope.TasPolygon(outwardOfSlope, 0.001, out bool slopeReversed);

            Assert.Multiple(() =>
            {
                Assert.That(wallReversed, Is.True, "this wall was wound the other way round from the way TAS wants");
                Assert.That(wall_Reoriented.NewellNormal().Y, Is.GreaterThan(0));
                Assert.That(slopeReversed, Is.True, "so was this slope");
                Assert.That(slope_Reoriented.NewellNormal().Z, Is.LessThan(0));
                Assert.That(slope_Reoriented.NewellNormal().Y, Is.GreaterThan(0), "it keeps its tilt: only its winding changed");
                Assert.That(slope.TasPolygon(new Vector3D(0, -0.4, 1), 0.001, out bool upReversed).NewellNormal().Z, Is.GreaterThan(0), "and the same slope read as a top surface is left as it is");
                Assert.That(upReversed, Is.False);
            });
        }

        [Test]
        public void ToTasCoordinates_IsThreeRowsByNColumns_XYZ()
        {
            // The layout is not a free choice: double[n,3] is accepted by TAS and silently builds half the area.
            double[,] coordinates = new List<Point3D> { P(1, 2, 3), P(4, 5, 6), P(7, 8, 9) }.ToTasCoordinates();

            Assert.Multiple(() =>
            {
                Assert.That(coordinates.GetLength(0), Is.EqualTo(3), "rows: X, Y, Z");
                Assert.That(coordinates.GetLength(1), Is.EqualTo(3), "columns: one per vertex");
                Assert.That(new[] { coordinates[0, 0], coordinates[1, 0], coordinates[2, 0] }, Is.EqualTo(new[] { 1.0, 2.0, 3.0 }));
                Assert.That(new[] { coordinates[0, 2], coordinates[1, 2], coordinates[2, 2] }, Is.EqualTo(new[] { 7.0, 8.0, 9.0 }));
            });

            double[,] fourPoints = new List<Point3D> { P(0, 0, 0), P(1, 0, 0), P(1, 1, 0), P(0, 1, 0) }.ToTasCoordinates();
            Assert.That(fourPoints.GetLength(0), Is.EqualTo(3), "still 3 rows with 4 vertices - not the n x 3 that would pass for a 3-vertex polygon");
            Assert.That(((IList<Point3D>)null).ToTasCoordinates(), Is.Null);
        }

        [Test]
        public void ProjectedOnPlane_SnapsAndReportsTheLargestMove()
        {
            List<Point3D> projected = new[] { P(1, 0.01, 1), P(2, -0.02, 1), P(2, 0, 2) }.ProjectedOnPlane(P(0, 0, 0), new Vector3D(0, 1, 0), out double maxDistance);

            Assert.Multiple(() =>
            {
                Assert.That(maxDistance, Is.EqualTo(0.02).Within(1e-12));
                Assert.That(projected.All(x => Math.Abs(x.Y) < 1e-12), Is.True, "every vertex is on the plane");
                Assert.That(projected[0].X, Is.EqualTo(1).Within(1e-12), "moved along the normal only");
            });
        }

        // ------------------------------------------------------------------------------------------------------
        // The zone identity: the SAM space GUID in the zone description.
        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void ZoneDescription_CarriesTheSpaceGuid_AndReadsBack()
        {
            Guid spaceGuid = Guid.NewGuid();

            string description = spaceGuid.ZoneDescription();

            Assert.Multiple(() =>
            {
                Assert.That(description.TryGetSpaceGuid(out Guid readBack), Is.True);
                Assert.That(readBack, Is.EqualTo(spaceGuid));
                Assert.That(("[Id]=12; [LevelName]=Level 0; " + description + "; free text").TryGetSpaceGuid(out Guid amongOthers), Is.True, "it is one segment among the others");
                Assert.That(amongOthers, Is.EqualTo(spaceGuid));
            });
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("[Id]=12; [LevelName]=Level 0")]
        [TestCase("[SpaceGuid]=not-a-guid")]
        [TestCase("SpaceGuid=75b28088-2415-44c0-bea8-3179b3475df0")]
        public void ZoneDescription_WithoutAValidMarker_NamesNoSpace(string description)
        {
            Assert.That(description.TryGetSpaceGuid(out Guid guid), Is.False, "every gbXML-route zone, and any zone a person wrote, resolves by the old identities");
            Assert.That(guid, Is.EqualTo(Guid.Empty));
        }

        [Test]
        public void ZoneDescription_SurvivesTheComposerThatUpdateZoneUses()
        {
            // Modify.UpdateZone rewrites the zone description through SAMZoneMetadata.Compose. The marker must come through it intact.
            Guid spaceGuid = Guid.NewGuid();

            string composed = SAMZoneMetadata.Compose(spaceGuid.ZoneDescription(), "1234", "Level 02", new SAMZoneMetadata { SupplyAirFlow = 0.03 });

            Assert.Multiple(() =>
            {
                Assert.That(composed, Does.Contain("[Id]=1234"));
                Assert.That(composed, Does.Contain("[LevelName]=Level 02"));
                Assert.That(composed.TryGetSpaceGuid(out Guid readBack), Is.True, composed);
                Assert.That(readBack, Is.EqualTo(spaceGuid));
            });

            // And it survives being composed again over its own output (a second UpdateZone pass).
            string again = SAMZoneMetadata.Compose(composed, null, null, new SAMZoneMetadata { SupplyAirFlow = 0.03 });
            Assert.That(again.TryGetSpaceGuid(out Guid readBackAgain), Is.True, again);
            Assert.That(readBackAgain, Is.EqualTo(spaceGuid));
        }

        [Test]
        public void ResolvedZone_TheDescriptionIdentityOutranksAStaleStampAndAName()
        {
            Guid spaceGuid = Guid.NewGuid();
            IReadOnlyDictionary<Guid, string> bySpaceGuid = new Dictionary<Guid, string> { [spaceGuid] = "the zone made for this space" };
            IReadOnlyDictionary<string, string> byGuid = new Dictionary<string, string> { ["stale-stamp"] = "some other zone" };
            IReadOnlyDictionary<string, string> byName = new Dictionary<string, string> { ["Studio"] = "a zone that merely shares the name" };

            Assert.Multiple(() =>
            {
                Assert.That(Tas.Query.ResolvedZone(spaceGuid, bySpaceGuid, "stale-stamp", "Studio", byGuid, byName), Is.EqualTo("the zone made for this space"));
                Assert.That(Tas.Query.ResolvedZone(Guid.NewGuid(), bySpaceGuid, "stale-stamp", "Studio", byGuid, byName), Is.EqualTo("some other zone"), "a space the description does not name falls through to the stamp, exactly as before");
                Assert.That(Tas.Query.ResolvedZone(Guid.NewGuid(), bySpaceGuid, null, "Studio", byGuid, byName), Is.EqualTo("a zone that merely shares the name"), "and then to the name");
                Assert.That(Tas.Query.ResolvedZone(Guid.NewGuid(), bySpaceGuid, null, "Nobody", byGuid, byName), Is.Null, "and no match is still a refusal");
                Assert.That(Tas.Query.ResolvedZone(spaceGuid, new Dictionary<Guid, string>(), null, "Studio", byGuid, byName), Is.EqualTo("a zone that merely shares the name"), "an empty description index is the gbXML route, unchanged");
                Assert.That(Tas.Query.ResolvedZone(spaceGuid, null, null, "Studio", byGuid, byName), Is.EqualTo("a zone that merely shares the name"), "so is a missing one");
            });
        }

        // ------------------------------------------------------------------------------------------------------
        // Which side of an internal wall is reversed: SAM's convention, from the stamps UpdateIds leaves.
        // ------------------------------------------------------------------------------------------------------

        private const string ZoneGuid_A = "{AAAAAAAA-0000-4000-8000-00000000000A}";
        private const string ZoneGuid_B = "{BBBBBBBB-0000-4000-8000-00000000000B}";

        // The two-zone building with its spaces stamped with zone GUIDs, and its partition stamped with the two surfaces TAS made for it.
        private static AdjacencyCluster StampedTwoZones(string zoneGuid_Of_Stamp1, string zoneGuid_Of_Stamp2, bool secondStamp, out Panel partition, out List<Space> spaces_OfPartition, bool partitionRelatedToBFirst = false)
        {
            AnalyticalModel model = SyntheticModels.TwoZones(false, partitionRelatedToBFirst);
            AdjacencyCluster cluster = model.AdjacencyCluster;

            foreach (Space space in cluster.GetSpaces())
            {
                space.SetValue(Tas.SpaceParameter.ZoneGuid, space.Name == "A" ? ZoneGuid_A : ZoneGuid_B);
                cluster.AddObject(space);
            }

            partition = cluster.GetPanels().Single(x => cluster.GetSpaces(x).Count == 2);
            partition.SetValue(Tas.PanelParameter.ZoneSurfaceReference_1, new Core.Tas.ZoneSurfaceReference(4, zoneGuid_Of_Stamp1));
            if (secondStamp)
            {
                partition.SetValue(Tas.PanelParameter.ZoneSurfaceReference_2, new Core.Tas.ZoneSurfaceReference(1, zoneGuid_Of_Stamp2));
            }

            cluster.AddObject(partition);

            spaces_OfPartition = cluster.GetSpaces(partition);
            return cluster;
        }

        [Test]
        public void InternalSurfaceReversals_TheSecondSpacesSurfaceIsReversed_WhicheverStampHoldsIt()
        {
            // Stamp 1 on A's surface, stamp 2 on B's.
            AdjacencyCluster cluster = StampedTwoZones(ZoneGuid_A, ZoneGuid_B, true, out Panel _, out List<Space> spaces);
            string first = spaces[0].Name;

            Dictionary<ZoneSurfaceKey, bool> reversals = cluster.InternalSurfaceReversals();

            Assert.Multiple(() =>
            {
                Assert.That(reversals, Has.Count.EqualTo(2));
                Assert.That(reversals[Tas.Query.ZoneSurfaceKey(ZoneGuid_A, 4)], Is.EqualTo(first != "A"), "A's surface is reversed exactly when A is the panel's SECOND space");
                Assert.That(reversals[Tas.Query.ZoneSurfaceKey(ZoneGuid_B, 1)], Is.EqualTo(first != "B"));
                Assert.That(reversals.Values.Count(x => x), Is.EqualTo(1), "exactly one side of the pair is reversed");
            });

            // The same wall, stamped the other way round: stamp 1 on B's surface, stamp 2 on A's. The answer follows the zone, not the slot.
            AdjacencyCluster crossed = StampedTwoZones(ZoneGuid_B, ZoneGuid_A, true, out Panel _, out List<Space> spaces_Crossed);
            Dictionary<ZoneSurfaceKey, bool> reversals_Crossed = crossed.InternalSurfaceReversals();

            Assert.Multiple(() =>
            {
                Assert.That(reversals_Crossed[Tas.Query.ZoneSurfaceKey(ZoneGuid_B, 4)], Is.EqualTo(spaces_Crossed[0].Name != "B"));
                Assert.That(reversals_Crossed[Tas.Query.ZoneSurfaceKey(ZoneGuid_A, 1)], Is.EqualTo(spaces_Crossed[0].Name != "A"));
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InternalSurfaceReversals_TheLaterSpaceInTheModelIsReversed_HoweverThePanelsRelationsAreStored(bool partitionRelatedToBFirst)
        {
            // A is the earlier space in the model in both cases; only the order of the partition's own relations differs. Modify.Update
            // reverses the surface of the space it reaches later in adjacencyCluster.GetSpaces(), and the gbXML exporter sorts a surface's
            // AdjacentSpaceIds by the same model index - so the answer must follow the model's order, never the relation order.
            AdjacencyCluster cluster = StampedTwoZones(ZoneGuid_A, ZoneGuid_B, true, out Panel _, out List<Space> spaces_OfPartition, partitionRelatedToBFirst);

            Assert.That(cluster.GetSpaces().Select(x => x.Name), Is.EqualTo(new[] { "A", "B" }), "the model lists A first");
            Assert.That(spaces_OfPartition[0].Name, Is.EqualTo(partitionRelatedToBFirst ? "B" : "A"), "the fixture really stores the partition's relations in the order asked for");

            Dictionary<ZoneSurfaceKey, bool> reversals = cluster.InternalSurfaceReversals();

            Assert.Multiple(() =>
            {
                Assert.That(reversals, Has.Count.EqualTo(2));
                Assert.That(reversals[Tas.Query.ZoneSurfaceKey(ZoneGuid_A, 4)], Is.False, "A, the earlier space in the model, sees the layers as listed");
                Assert.That(reversals[Tas.Query.ZoneSurfaceKey(ZoneGuid_B, 1)], Is.True, "B, the later space in the model, sees them reversed");
            });
        }

        [Test]
        public void InternalSurfaceReversals_ZoneGuidSpelling_DoesNotMatter()
        {
            // TAS reports a zone GUID braced and upper-case; a stamp may have been written bare and lower-case.
            AdjacencyCluster cluster = StampedTwoZones(ZoneGuid_A.Trim('{', '}').ToLowerInvariant(), ZoneGuid_B.ToLowerInvariant(), true, out Panel _, out List<Space> _);

            Assert.That(cluster.InternalSurfaceReversals(), Has.Count.EqualTo(2));
        }

        [Test]
        public void InternalSurfaceReversals_NeverGuesses()
        {
            Assert.Multiple(() =>
            {
                Assert.That(StampedTwoZones(ZoneGuid_A, null, false, out Panel _, out List<Space> _).InternalSurfaceReversals(), Is.Empty, "only one side stamped");
                Assert.That(StampedTwoZones(ZoneGuid_A, ZoneGuid_A, true, out Panel _, out List<Space> _).InternalSurfaceReversals(), Is.Empty, "both stamps on the same zone");
                Assert.That(StampedTwoZones(ZoneGuid_A, "{CCCCCCCC-0000-4000-8000-00000000000C}", true, out Panel _, out List<Space> _).InternalSurfaceReversals(), Is.Empty, "a stamp naming a zone neither space resolved to");
                Assert.That(StampedTwoZones(null, ZoneGuid_B, true, out Panel _, out List<Space> _).InternalSurfaceReversals(), Is.Empty, "a stamp with no zone");
                Assert.That(SyntheticModels.TwoZones().AdjacencyCluster.InternalSurfaceReversals(), Is.Empty, "a model UpdateIds has not stamped");
                Assert.That(((AdjacencyCluster)null).InternalSurfaceReversals(), Is.Empty);
            });
        }

        [Test]
        public void InternalSurfaceReversals_LeavesFloorsAndCeilingsToTas()
        {
            AnalyticalModel model = SyntheticModels.StackedZones();
            AdjacencyCluster cluster = model.AdjacencyCluster;
            foreach (Space space in cluster.GetSpaces())
            {
                space.SetValue(Tas.SpaceParameter.ZoneGuid, space.Name == "Lower" ? ZoneGuid_A : ZoneGuid_B);
                cluster.AddObject(space);
            }

            Panel slab = cluster.GetPanels().Single(x => cluster.GetSpaces(x).Count == 2);
            slab.SetValue(Tas.PanelParameter.ZoneSurfaceReference_1, new Core.Tas.ZoneSurfaceReference(3, ZoneGuid_A));
            slab.SetValue(Tas.PanelParameter.ZoneSurfaceReference_2, new Core.Tas.ZoneSurfaceReference(2, ZoneGuid_B));
            cluster.AddObject(slab);

            Assert.That(cluster.InternalSurfaceReversals(), Is.Empty, "TAS assigns a horizontal surface's reversed side from the geometry (measured identical to the gbXML route); SAM's rule is not imposed on it");
        }

        // ------------------------------------------------------------------------------------------------------
        // The import plan.
        // ------------------------------------------------------------------------------------------------------

        private static Vector3D Normal(T3DSurfaceSpec surface)
        {
            return Points(surface.Coordinates).NewellNormal();
        }

        private static List<Point3D> Points(double[,] coordinates)
        {
            List<Point3D> result = new List<Point3D>();
            for (int i = 0; i < coordinates.GetLength(1); i++)
            {
                result.Add(new Point3D(coordinates[0, i], coordinates[1, i], coordinates[2, i]));
            }

            return result;
        }

        private static Vector3D Unit(Vector3D vector3D)
        {
            return vector3D.Unit;
        }

        [Test]
        public void Plan_Box_OneZoneSixSurfacesThreeElements()
        {
            T3DImportPlan plan = SyntheticModels.Box().T3DImportPlan();

            Assert.Multiple(() =>
            {
                Assert.That(plan.Zones, Has.Count.EqualTo(1));
                Assert.That(plan.Zones[0].Name, Is.EqualTo("Box"));
                Assert.That(plan.Elements.Select(x => x.Name), Is.EquivalentTo(new[] { "EXT_WALL", "EXT_ROOF", "GRD_FLOOR" }), "one element per construction, named after it");
                Assert.That(plan.Surfaces, Has.Count.EqualTo(6));
                Assert.That(plan.Surfaces.Count(x => x.Kind == T3DSurfaceKind.External), Is.EqualTo(5));
                Assert.That(plan.Surfaces.Count(x => x.Kind == T3DSurfaceKind.Ground), Is.EqualTo(1));
                Assert.That(plan.Report.Skipped, Is.Empty);
                Assert.That(plan.Windows, Is.Empty);
                Assert.That(plan.Report.ToString(), Does.Contain("zones=1").And.Contain("surfaces=6"));
            });
        }

        [Test]
        public void Plan_Elements_CarryWidthGroundAndBEType()
        {
            T3DImportPlan plan = SyntheticModels.Box().T3DImportPlan();
            T3DElementSpec wall = plan.Elements.Single(x => x.Name == "EXT_WALL");
            T3DElementSpec roof = plan.Elements.Single(x => x.Name == "EXT_ROOF");
            T3DElementSpec floor = plan.Elements.Single(x => x.Name == "GRD_FLOOR");

            Assert.Multiple(() =>
            {
                Assert.That(wall.Width, Is.EqualTo(0.30).Within(1e-9), "the construction's thickness");
                Assert.That(roof.Width, Is.EqualTo(0.35).Within(1e-9));
                Assert.That(floor.Width, Is.EqualTo(0.40).Within(1e-9));
                Assert.That(floor.Ground, Is.True, "a slab on grade is a ground element");
                Assert.That(wall.Ground, Is.False);
                Assert.That(roof.Ground, Is.False);
                Assert.That(wall.BEType, Is.EqualTo(Tas.Query.BEType("External Wall")));
                Assert.That(roof.BEType, Is.EqualTo(Tas.Query.BEType("Roof")));
                Assert.That(floor.BEType, Is.EqualTo(Tas.Query.BEType("Slab on Grade")));
                Assert.That(floor.ZoneFloorArea, Is.True, "a floor counts toward the zone floor area");
                Assert.That(wall.Description, Is.EqualTo(SyntheticModels.WallConstruction.Guid.ToString("D")), "the construction GUID rides on the element");
            });
        }

        [Test]
        public void Plan_EveryPolygon_IsAnOpenThreeByNLoopWhoseNormalPointsOutOfItsZone()
        {
            T3DImportPlan plan = SyntheticModels.Box().T3DImportPlan();

            Assert.Multiple(() =>
            {
                foreach (T3DSurfaceSpec surface in plan.Surfaces)
                {
                    Assert.That(surface.Coordinates.GetLength(0), Is.EqualTo(3), surface.PanelName + ": rows");
                    Assert.That(surface.Coordinates.GetLength(1), Is.EqualTo(4), surface.PanelName + ": a quad, not repeated at the end");
                }

                // The box is 0..5 x 0..4 x 0..3; its centre (2.5, 2, 1.5). Every normal must point away from it - whatever way the fixture wound the polygon.
                Point3D centre = P(2.5, 2, 1.5);
                foreach (T3DSurfaceSpec surface in plan.Surfaces)
                {
                    List<Point3D> points = Points(surface.Coordinates);
                    Point3D onSurface = points[0];
                    Vector3D fromCentre = new Vector3D(centre, onSurface);
                    Vector3D normal = Normal(surface);
                    double dot = fromCentre.X * normal.X + fromCentre.Y * normal.Y + fromCentre.Z * normal.Z;
                    Assert.That(dot, Is.GreaterThan(0), surface.PanelName + " (" + surface.Kind + ") points inward");
                }
            });
        }

        [Test]
        public void Plan_PolygonAreas_AreTheSamePanelsAreas()
        {
            AnalyticalModel model = SyntheticModels.Box();
            T3DImportPlan plan = model.T3DImportPlan();

            double[] expected = model.AdjacencyCluster.GetPanels().Select(x => x.GetArea()).OrderBy(x => x).ToArray();
            double[] actual = plan.Surfaces.Select(x => Normal(x).Length / 2).OrderBy(x => x).ToArray();

            Assert.That(actual, Is.EqualTo(expected).Within(1e-9), "cleaning the polygons does not change them");
        }

        [Test]
        public void Plan_APanelBetweenTwoSpaces_IsOneInternalSurface_NormalFromTheFirstIntoTheSecond()
        {
            T3DImportPlan plan = SyntheticModels.TwoZones().T3DImportPlan();

            T3DSurfaceSpec partition = plan.Surfaces.Single(x => x.Kind == T3DSurfaceKind.Internal);

            Assert.Multiple(() =>
            {
                Assert.That(plan.Surfaces.Count(x => x.PanelName == null || partition.PanelGuid == x.PanelGuid), Is.EqualTo(1), "ONE surface for the panel, not one per side");
                Assert.That(partition.Zone2, Is.GreaterThanOrEqualTo(0));
                Assert.That(partition.Zone, Is.Not.EqualTo(partition.Zone2));
                Assert.That(plan.Report.InternalSurfaces, Is.EqualTo(1));

                string first = plan.Zones[partition.Zone].Name;
                Vector3D normal = Unit(Normal(partition));
                Assert.That(normal.X, Is.EqualTo(first == "A" ? 1 : -1).Within(1e-9), "the normal points from the first zone (" + first + ") toward the second");
                Assert.That(plan.Elements.Single(x => x.Key == partition.ElementKey).Name, Is.EqualTo("INT_PARTITION"));
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Plan_AHorizontalInternalFloor_IsOneInternalSurface_NormalPointsOutOfTheFirstZone(bool upperFirst)
        {
            T3DImportPlan plan = SyntheticModels.StackedZones(upperFirst).T3DImportPlan();

            T3DSurfaceSpec slab = plan.Surfaces.Single(x => x.Kind == T3DSurfaceKind.Internal);
            string first = plan.Zones[slab.Zone].Name;
            Vector3D normal = Unit(Normal(slab));

            Assert.Multiple(() =>
            {
                Assert.That(first, Is.EqualTo(upperFirst ? "Upper" : "Lower"));
                Assert.That(Math.Abs(normal.Z), Is.EqualTo(1).Within(1e-9), "horizontal");
                Assert.That(normal.Z, Is.EqualTo(first == "Lower" ? 1 : -1).Within(1e-9), "up out of the lower zone, down out of the upper one");
                Assert.That(plan.Surfaces.Count(x => x.Kind == T3DSurfaceKind.Ground), Is.EqualTo(1));
                Assert.That(plan.Surfaces.Count(x => x.Kind == T3DSurfaceKind.External), Is.EqualTo(9), "8 walls + the roof");
            });
        }

        [Test]
        public void Plan_AnAdiabaticPanel_IsImportedAdiabatic()
        {
            T3DImportPlan plan = SyntheticModels.TwoZones(adiabaticNorthWallOfA: true).T3DImportPlan();

            T3DSurfaceSpec wall = plan.Surfaces.Single(x => x.Kind == T3DSurfaceKind.Adiabatic);

            Assert.Multiple(() =>
            {
                Assert.That(plan.Zones[wall.Zone].Name, Is.EqualTo("A"));
                Assert.That(plan.Report.AdiabaticSurfaces, Is.EqualTo(1));
                Assert.That(Unit(Normal(wall)).Y, Is.EqualTo(1).Within(1e-9), "a north wall faces +Y");
            });
        }

        [Test]
        public void Plan_AnAdiabaticPartitionBetweenTwoSpaces_IsOneNullLinkedSurfacePerZone()
        {
            // The gbXML route's UpdateAdiabatic null-links BOTH sides of an adiabatic panel that separates two spaces; a single
            // AddInternalSurface would instead link them and let heat flow.
            AnalyticalModel model = SyntheticModels.TwoZones();
            AdjacencyCluster cluster = model.AdjacencyCluster;
            Panel partition = cluster.GetPanels().Single(x => cluster.GetSpaces(x).Count == 2);
            partition.SetValue(Analytical.PanelParameter.Adiabatic, true);
            cluster.AddObject(partition);

            T3DImportPlan plan = new AnalyticalModel(model, cluster).T3DImportPlan();

            List<T3DSurfaceSpec> sides = plan.Surfaces.Where(x => x.Kind == T3DSurfaceKind.InternalAdiabaticSide).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(sides, Has.Count.EqualTo(2));
                Assert.That(sides.Select(x => x.Zone).Distinct().Count(), Is.EqualTo(2), "one per zone");
                Assert.That(plan.Surfaces.Any(x => x.Kind == T3DSurfaceKind.Internal), Is.False, "and no linked surface");
                Assert.That(Unit(Normal(sides[0])).X * Unit(Normal(sides[1])).X, Is.EqualTo(-1).Within(1e-9), "each normal points out of its own zone");
                Assert.That(plan.Report.AdiabaticSurfaces, Is.EqualTo(2));
            });
        }

        [Test]
        public void Plan_OpeningsFollowTheirHostImmediately_OnTheHostPlane_WoundTheSameWay()
        {
            AnalyticalModel model = SyntheticModels.Box(3);
            T3DImportPlan plan = model.T3DImportPlan();

            T3DSurfaceSpec host = plan.Surfaces.Single(x => x.Openings.Count != 0);

            Assert.Multiple(() =>
            {
                Assert.That(host.Openings, Has.Count.EqualTo(3), "AddOpening attaches to the surface added last, so the plan carries each host with its openings");
                Assert.That(plan.Report.Openings, Is.EqualTo(3));

                Vector3D hostNormal = Unit(Normal(host));
                foreach (T3DOpeningSpec opening in host.Openings)
                {
                    List<Point3D> points = Points(opening.Coordinates);
                    Assert.That(points.All(x => Math.Abs(x.Y) < 1e-9), Is.True, "on the host's plane (the south wall, y = 0)");
                    Vector3D normal = Unit(points.NewellNormal());
                    Assert.That(normal.X * hostNormal.X + normal.Y * hostNormal.Y + normal.Z * hostNormal.Z, Is.EqualTo(1).Within(1e-9), "the same winding as the host");
                    Assert.That(points.NewellNormal().Length / 2, Is.EqualTo(1).Within(1e-9), "the 1 m2 polygon sets the opening's size");
                }
            });
        }

        [Test]
        public void Plan_Windows_AreOnePerAperture_ByDefault_NamedAfterTheAperture()
        {
            AnalyticalModel model = SyntheticModels.Box(3);
            T3DImportPlan plan = model.T3DImportPlan();
            List<Aperture> apertures = model.AdjacencyCluster.GetApertures();

            Assert.Multiple(() =>
            {
                Assert.That(plan.Windows, Has.Count.EqualTo(3), "TAS folds the openings of one window object on one host into ONE zone surface, so each aperture gets its own");
                Assert.That(plan.Windows.Select(x => x.Name).Distinct().Count(), Is.EqualTo(3));
                foreach (Aperture aperture in apertures)
                {
                    T3DWindowSpec window = plan.Windows.Single(x => x.Description == aperture.Guid.ToString("D"));
                    Assert.That(window.Name, Is.EqualTo("Windows: EXT_GLZ " + aperture.Guid.ToString("D") + " "), "the instance name the existing aperture steps decode - with the trailing space TAS appends -pane/-frame to");
                    Assert.That(Tas.Query.UniqueNameDecomposition(window.Name.Trim() + " -pane", out string prefix, out string name, out Guid? guid, out int id), Is.True);
                    Assert.That(guid, Is.EqualTo(aperture.Guid), "UniqueNameDecomposition recovers the aperture from the building element name TAS will build");
                    Assert.That(window.Transparent, Is.True);
                    Assert.That(window.FramePercent, Is.EqualTo(aperture.GetFrameFactor() * 100).Within(1e-9), "the aperture's own frame percentage, not a rounded shared one");
                }
            });
        }

        [Test]
        public void Plan_Windows_SharedWindowTypes_IsOnePerApertureConstruction_NamedLikeASharedDefinition()
        {
            T3DImportPlan plan = SyntheticModels.Box(3).T3DImportPlan(new ToT3DOptions { SharedWindowTypes = true });

            Assert.Multiple(() =>
            {
                Assert.That(plan.Windows, Has.Count.EqualTo(1));
                Assert.That(plan.Windows[0].Name, Is.EqualTo("Windows: EXT_GLZ "), "TAS will name its elements 'Windows: EXT_GLZ -pane' / '-frame' - the shared-definition names Query.BuildingElementName makes");
                Assert.That(plan.Surfaces.Single(x => x.Openings.Count != 0).Openings.Select(x => x.WindowKey).Distinct().Count(), Is.EqualTo(1));
            });
        }

        [Test]
        public void Plan_AShadePanel_IsAShade_AndOnlyWhenAskedFor()
        {
            AnalyticalModel model = SyntheticModels.BoxWithShade();

            T3DImportPlan withShades = model.T3DImportPlan();
            T3DImportPlan withoutShades = model.T3DImportPlan(new ToT3DOptions { ImportShades = false });

            Assert.Multiple(() =>
            {
                Assert.That(withShades.Shades, Has.Count.EqualTo(1));
                Assert.That(withShades.Surfaces, Has.Count.EqualTo(6), "a shade is not a zone surface");
                Assert.That(withShades.Report.Shades, Is.EqualTo(1));
                Assert.That(withShades.Report.ShadesImported, Is.EqualTo(1));
                Assert.That(withShades.Shades[0].Coordinates.GetLength(0), Is.EqualTo(3));

                Assert.That(withoutShades.Shades, Is.Empty);
                Assert.That(withoutShades.Report.Shades, Is.EqualTo(1), "still counted");
                Assert.That(withoutShades.Report.ShadesImported, Is.EqualTo(0));
                Assert.That(withoutShades.Report.Skipped.Any(x => x.Contains("Shade panel")), Is.True, "and named - never silently lost");
            });
        }

        [Test]
        public void Plan_AnApertureOnAShade_IsReported_NotSilentlyLost()
        {
            AnalyticalModel model = SyntheticModels.BoxWithShade();
            AdjacencyCluster cluster = model.AdjacencyCluster;

            Panel canopy = cluster.GetPanels().Single(x => x.PanelType == PanelType.Shade);
            Aperture aperture = Analytical.Create.Aperture(SyntheticModels.GlazingConstruction, SyntheticModels.Quad(new Point3D(1, -1.5, 3), new Point3D(2, -1.5, 3), new Point3D(2, -0.5, 3), new Point3D(1, -0.5, 3)));
            Assert.That(aperture, Is.Not.Null);
            Assert.That(canopy.AddAperture(aperture), Is.True);
            cluster.AddObject(canopy);

            T3DImportPlan plan = new AnalyticalModel(model, cluster).T3DImportPlan();

            Assert.Multiple(() =>
            {
                Assert.That(plan.Shades, Has.Count.EqualTo(1), "the shade itself is still imported");
                Assert.That(plan.Report.Skipped.Count(x => x.Contains(aperture.Guid.ToString()) && x.Contains("shade")), Is.EqualTo(1), "its aperture, which AddShadeSurface cannot carry, is named in the report");
            });
        }

        [Test]
        public void StoreyName_IsTheLevelEveryZoneNames_AndNeverAGuess()
        {
            Assert.Multiple(() =>
            {
                Assert.That(new[] { "Level 0", "Level 0 ", " Level 0" }.StoreyName(), Is.EqualTo("Level 0"), "every zone names the same level");
                Assert.That(new[] { "Level 0", "Level 1" }.StoreyName(), Is.Null, "zones that disagree");
                Assert.That(new[] { "Level 0", null }.StoreyName(), Is.Null, "a zone with no level");
                Assert.That(new[] { "Level 0", "  " }.StoreyName(), Is.Null, "a zone with a blank level");
                Assert.That(new string[0].StoreyName(), Is.Null, "a storey with no zones");
                Assert.That(((IEnumerable<string>)null).StoreyName(), Is.Null);
                Assert.That(new[] { "Level 0", "level 0" }.StoreyName(), Is.Null, "level names are compared exactly");
            });
        }

        [Test]
        public void Plan_Zones_CarryTheSpacesLevelName()
        {
            AnalyticalModel model = SyntheticModels.TwoZones();
            AdjacencyCluster cluster = model.AdjacencyCluster;
            foreach (Space space in cluster.GetSpaces())
            {
                if (space.Name == "A")
                {
                    space.SetValue(Analytical.SpaceParameter.LevelName, "Level 0");
                    cluster.AddObject(space);
                }
            }

            T3DImportPlan plan = new AnalyticalModel(model, cluster).T3DImportPlan();

            Assert.Multiple(() =>
            {
                Assert.That(plan.Zones.Single(x => x.Name == "A").LevelName, Is.EqualTo("Level 0"));
                Assert.That(plan.Zones.Single(x => x.Name == "B").LevelName, Is.Null, "a space with no level name gives none");
            });
        }

        [Test]
        public void Plan_ElementPerPanel_GivesEverySurfaceItsOwnElement()
        {
            T3DImportPlan plan = SyntheticModels.Box().T3DImportPlan(new ToT3DOptions { ElementPerPanel = true });

            Assert.Multiple(() =>
            {
                Assert.That(plan.Elements, Has.Count.EqualTo(6));
                Assert.That(plan.Surfaces.Select(x => x.ElementKey).Distinct().Count(), Is.EqualTo(6));
                Assert.That(plan.Elements.Select(x => x.Name).Distinct().Count(), Is.EqualTo(6), "names stay unique");
                Assert.That(plan.Elements.Count(x => x.Ground), Is.EqualTo(1));
            });
        }

        [Test]
        public void Plan_OneConstructionUsedOnAndOffTheGround_BecomesTwoElements_SoGroundIsNeverWrong()
        {
            // The same construction on the slab on grade and on an internal floor: one element would make the internal floor a ground surface.
            AnalyticalModel model = SyntheticModels.StackedZones();
            AdjacencyCluster cluster = model.AdjacencyCluster;
            Space lower = cluster.GetSpaces().Single(x => x.Name == "Lower");
            Panel groundFloor = cluster.GetPanels(lower).Single(x => x.PanelType == PanelType.SlabOnGrade);
            Panel regrounded = Analytical.Create.Panel(groundFloor, SyntheticModels.InternalFloorConstruction);
            cluster.AddObject(regrounded);

            T3DImportPlan plan = new AnalyticalModel(model, cluster).T3DImportPlan();

            List<T3DElementSpec> elements = plan.Elements.Where(x => x.Description == SyntheticModels.InternalFloorConstruction.Guid.ToString("D")).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(elements, Has.Count.EqualTo(2));
                Assert.That(elements.Count(x => x.Ground), Is.EqualTo(1));
                Assert.That(elements.Select(x => x.Name).Distinct().Count(), Is.EqualTo(2), "told apart by name, not silently merged");
                Assert.That(plan.Report.Notes.Any(x => x.Contains("already taken")), Is.True, "and said so");
            });
        }

        [Test]
        public void Plan_ZonesCarryTheSpaceGuid_AndTheMappingIsInTheReport()
        {
            AnalyticalModel model = SyntheticModels.TwoZones();
            T3DImportPlan plan = model.T3DImportPlan();

            Assert.Multiple(() =>
            {
                foreach (Space space in model.AdjacencyCluster.GetSpaces())
                {
                    T3DZoneSpec zone = plan.Zones.Single(x => x.SpaceGuid == space.Guid);
                    Assert.That(zone.Name, Is.EqualTo(space.Name));
                    Assert.That(zone.Description, Is.EqualTo(space.Guid.ZoneDescription()));
                    Assert.That(zone.Description.TryGetSpaceGuid(out Guid guid) && guid == space.Guid, Is.True);
                    Assert.That(plan.Report.ZoneNames[space.Guid], Is.EqualTo(space.Name));
                    Assert.That(zone.External, Is.False);
                }
            });
        }

        [Test]
        public void Plan_ASpaceWithNoPanels_IsNotAZone()
        {
            AnalyticalModel model = SyntheticModels.Box();
            AdjacencyCluster cluster = model.AdjacencyCluster;
            cluster.AddObject(new Space("Orphan", P(50, 50, 1)));

            T3DImportPlan plan = new AnalyticalModel(model, cluster).T3DImportPlan();

            Assert.That(plan.Zones.Select(x => x.Name), Is.EquivalentTo(new[] { "Box" }), "a zone with no surfaces would only upset TAS");
        }

        [Test]
        public void Plan_APanelBoundingMoreThanTwoSpaces_IsReportedNotImported()
        {
            AnalyticalModel model = SyntheticModels.TwoZones();
            AdjacencyCluster cluster = model.AdjacencyCluster;
            Space third = new Space("C", P(7.5, 2, 1.5));
            cluster.AddObject(third);
            Panel partition = cluster.GetPanels().Single(x => cluster.GetSpaces(x).Count == 2);
            cluster.AddRelation(third, partition);

            T3DImportPlan plan = new AnalyticalModel(model, cluster).T3DImportPlan();

            Assert.Multiple(() =>
            {
                Assert.That(plan.Surfaces.Any(x => x.PanelGuid == partition.Guid), Is.False);
                Assert.That(plan.Report.Skipped.Any(x => x.Contains("bounds 3 spaces")), Is.True, string.Join(" | ", plan.Report.Skipped));
            });
        }

        [Test]
        public void Plan_AnOpenShell_FallsBackToThePanelsNormal_AndSaysSo()
        {
            // Remove the roof: the shell is open, so "out of the space" cannot be established from it.
            AnalyticalModel model = SyntheticModels.Box();
            AdjacencyCluster cluster = model.AdjacencyCluster;
            Panel roof = cluster.GetPanels().Single(x => x.PanelType == PanelType.Roof);
            cluster.RemoveObject<Panel>(roof.Guid);

            T3DImportPlan plan = new AnalyticalModel(model, cluster).T3DImportPlan();

            Assert.Multiple(() =>
            {
                Assert.That(plan.Surfaces, Has.Count.EqualTo(5), "the surfaces that remain are still imported");
                Assert.That(plan.Report.Notes.Count(x => x.Contains("shell is not closed")), Is.EqualTo(5), string.Join(" | ", plan.Report.Notes));
            });
        }

        [Test]
        public void Plan_APanelWithAHole_ImportsItsOuterLoop_AndReportsTheHole()
        {
            // AddSurface takes one outer loop; a hole must be reported, not silently ignored.
            Plane plane = Plane.WorldXY;
            Geometry.Planar.IClosed2D outer = new Geometry.Planar.Polygon2D(new[] { new Geometry.Planar.Point2D(0, 0), new Geometry.Planar.Point2D(5, 0), new Geometry.Planar.Point2D(5, 4), new Geometry.Planar.Point2D(0, 4) });
            Geometry.Planar.IClosed2D hole = new Geometry.Planar.Polygon2D(new[] { new Geometry.Planar.Point2D(1, 1), new Geometry.Planar.Point2D(2, 1), new Geometry.Planar.Point2D(2, 2), new Geometry.Planar.Point2D(1, 2) });
            Face3D face3D = plane.Convert(Geometry.Planar.Create.Face2D(outer, new[] { hole }));

            List<Point3D> polygon = face3D.TasPolygon(new Vector3D(0, 0, 1), 0.001, out bool _, out int holes);

            Assert.Multiple(() =>
            {
                Assert.That(holes, Is.EqualTo(1));
                Assert.That(polygon, Has.Count.EqualTo(4), "the outer loop only");
                Assert.That(polygon.NewellNormal().Length / 2, Is.EqualTo(20).Within(1e-9));
            });
        }

        [Test]
        public void Plan_AGridOfZones_CountsAddUp_AndTheDoorAndRooflightGetTheirOwnTypes()
        {
            // 10 x 10 zones: 100 slabs and 100 roofs; 9 x 10 + 10 x 9 = 180 shared partitions, each ONE surface; 4 x 10 = 40 perimeter walls,
            // two windows in each of the 20 that face north or south.
            T3DImportPlan plan = SyntheticModels.Grid(10, 10).T3DImportPlan();

            Assert.Multiple(() =>
            {
                Assert.That(plan.Zones, Has.Count.EqualTo(100));
                Assert.That(plan.Report.GroundSurfaces, Is.EqualTo(100));
                Assert.That(plan.Report.InternalSurfaces, Is.EqualTo(180), "9 x 10 + 10 x 9 shared partitions, each ONE surface");
                Assert.That(plan.Report.ExternalSurfaces, Is.EqualTo(140), "100 roofs + 40 perimeter walls");
                Assert.That(plan.Report.Surfaces, Is.EqualTo(420));
                Assert.That(plan.Report.Openings, Is.EqualTo(40), "two windows in each of the 20 north / south perimeter walls");
                Assert.That(plan.Report.Skipped, Is.Empty);
            });
        }

        [Test]
        public void Plan_ADoorAndARooflight_AreTypedByTheirHost()
        {
            T3DImportPlan plan = SyntheticModels.BoxWithDoorAndRooflight().T3DImportPlan();

            T3DWindowSpec door = plan.Windows.Single(x => x.Name.StartsWith("Doors: EXT_DOOR"));
            T3DWindowSpec rooflight = plan.Windows.Single(x => x.Name.StartsWith("Windows: EXT_GLZ"));

            Assert.Multiple(() =>
            {
                Assert.That(door.OpeningType, Is.EqualTo(2), "AddWindow openingType 2 is a door");
                Assert.That(door.PositionType, Is.EqualTo(2));
                Assert.That(door.Transparent, Is.False, "an opaque door");
                Assert.That(rooflight.OpeningType, Is.EqualTo(1), "an opening in a roof is a rooflight");
                Assert.That(rooflight.PositionType, Is.EqualTo(1));
                Assert.That(Points(plan.Surfaces.Single(x => x.Openings.Any(o => o.WindowKey == rooflight.Key)).Openings.Single().Coordinates).All(x => Math.Abs(x.Z - 3) < 1e-9), Is.True, "on the roof plane");
            });
        }

        [TestCase(0.0, 0.5, TestName = "Plan_NorthAngle_Zero_IsTheHalfDegreeTasNeeds")]
        [TestCase(90.0, 90.0)]
        [TestCase(-30.0, 330.0, TestName = "Plan_NorthAngle_Negative_IsTheSameDirectionNotAClampToHalfADegree")]
        [TestCase(390.0, 30.0)]
        public void Plan_NorthAngle_IsDegreesInZeroTo360(double degrees, double expected)
        {
            AnalyticalModel model = SyntheticModels.Box();
            model.SetValue(Analytical.AnalyticalModelParameter.NorthAngle, degrees * Math.PI / 180);

            Assert.That(model.T3DImportPlan().NorthAngle, Is.EqualTo(expected).Within(0.05));
        }

        [Test]
        public void Plan_AnOpenFirstShell_UsesTheClosedSecondShellToOrientTheSurface()
        {
            // A's roof removed (open shell); B is closed. The partition's outward side for A is the opposite of B's.
            AnalyticalModel model = SyntheticModels.TwoZones();
            AdjacencyCluster cluster = model.AdjacencyCluster;
            Space a = cluster.GetSpaces().Single(x => x.Name == "A");
            Panel roofOfA = cluster.GetPanels(a).Single(x => x.PanelType == PanelType.Roof);
            cluster.RemoveObject<Panel>(roofOfA.Guid);

            T3DImportPlan plan = new AnalyticalModel(model, cluster).T3DImportPlan();

            T3DSurfaceSpec partition = plan.Surfaces.Single(x => x.Kind == T3DSurfaceKind.Internal);
            string first = plan.Zones[partition.Zone].Name;
            Vector3D normal = Unit(Normal(partition));
            Assert.Multiple(() =>
            {
                // A is the first zone (its shell is open), so the verified answer came from B's shell.
                Assert.That(first, Is.EqualTo("A"));
                Assert.That(normal.X, Is.EqualTo(1).Within(1e-9), "out of A, toward B");
                Assert.That(plan.Report.Notes.Any(x => x.Contains("shell is not closed") && x.Contains(partition.PanelGuid.ToString())), Is.False, "no unverified-orientation caveat for the partition");
            });
        }

        [Test]
        public void Plan_NullModelOrCluster_IsNull()
        {
            Assert.Multiple(() =>
            {
                Assert.That(((AnalyticalModel)null).T3DImportPlan(), Is.Null);
                Assert.That(new AnalyticalModel(Guid.NewGuid(), "empty").T3DImportPlan(), Is.Null);
            });
        }

        [Test]
        public void ToT3DOptions_Defaults_AreTheEvidenceBasedOnes()
        {
            ToT3DOptions options = new ToT3DOptions();

            Assert.Multiple(() =>
            {
                Assert.That(options.UseWidths, Is.False, "same default as WorkflowSettings.UseWidths; SAM polygons are room boundaries, not centre lines (measured)");
                Assert.That(options.SharedWindowTypes, Is.False, "TAS folds shared-window openings on one host into one surface (measured)");
                Assert.That(options.ElementPerPanel, Is.False);
                Assert.That(options.ImportShades, Is.True, "AddShadeSurface reproduces the gbXML route's shading hour for hour (measured)");
                Assert.That(options.ZoneSetName, Is.EqualTo("SAM"));
            });
        }
    }
}
