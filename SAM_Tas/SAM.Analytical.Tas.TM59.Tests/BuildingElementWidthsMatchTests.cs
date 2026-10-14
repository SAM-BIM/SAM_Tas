// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical.Tas.DirectT3D.Validation;
using SAM.Geometry.Spatial;
using System;
using System.Collections.Generic;
using System.Linq;
using TasModify = SAM.Analytical.Tas.Modify;
using TasQuery = SAM.Analytical.Tas.Query;
using ZoneSurfaceReference = SAM.Core.Tas.ZoneSurfaceReference;

namespace SAM.Analytical.Tas.TM59.Tests
{
    /// <summary>
    /// <b>Matching the TBD back to SAM when TAS uses building-element widths (UseBEWidths ON).</b>
    /// <para>
    /// With widths ON TAS treats every SAM polygon as its element's centre line and moves each zone's surface half the element
    /// width into the zone. The post-export steps matched TBD surfaces to SAM panels and apertures by a point-in-face test within
    /// 1 mm, so on the house model every one missed: 0 panel and 0 aperture stamps, 0 of 50 aperture parts rebound, 25 of 25
    /// apertures stopped at "gate 3". The correction, each rule pinned here COM-free:
    /// </para>
    /// <list type="number">
    /// <item>an aperture surface is identified by the SAM aperture GUID its element still carries in its name
    /// (<see cref="TasQuery.ApertureByElementName"/>);</item>
    /// <item>an aperture read off the TBD is matched by the pane surface it was read from against the SAM apertures' pane stamps
    /// (<see cref="TasQuery.PaneStampMatch"/>);</item>
    /// <item>an opaque surface the exact test misses may lie off its panel by up to half its element's width, parallel, inside it
    /// once projected (<see cref="TasQuery.OffsetMatch"/>);</item>
    /// <item>a point inside a panel's hole is not on the panel (<see cref="TasQuery.InRangeOutsideHoles"/>) - the stair-void Air
    /// surface was being stamped onto the slab round it, in both width modes.</item>
    /// </list>
    /// <para>
    /// And the OpeningProperties diagnostics: an aperture with none requests no control and is valid, so it is counted in one
    /// summary line instead of one warning each (<see cref="TasQuery.ApertureTypesSummary"/>).
    /// </para>
    /// <para>What TAS builds with these rules is measured on a licensed machine (Documentation/House-T3D-Export-Q4.md).</para>
    /// </summary>
    [TestFixture]
    public class BuildingElementWidthsMatchTests
    {
        private const int BEType_Wall = 2;
        private const int BEType_Glazing = 12;
        private const int BEType_Door = 14;
        private const int BEType_Frame = 15;

        private const double Tolerance = Core.Tolerance.MacroDistance;

        private static readonly ApertureConstruction Glazing = new ApertureConstruction(Guid.Parse("00000000-0000-0000-0000-00000000c001"), "SIM_EXT_GLZ", ApertureType.Window);
        private static readonly ApertureConstruction Door = new ApertureConstruction(Guid.Parse("00000000-0000-0000-0000-00000000c002"), "SIM_INT_SLD", ApertureType.Door);

        private static Point3D P(double x, double y, double z) => new Point3D(x, y, z);

        // A 5 x 3 m wall on the plane y = 0 - the SAM mid-plane of a 0.25 m external wall.
        private static Panel Wall() => SyntheticModels.Panel(SyntheticModels.WallConstruction, PanelType.WallExternal, SyntheticModels.Quad(P(0, 0, 0), P(5, 0, 0), P(5, 0, 3), P(0, 0, 3)));

        private static Aperture Window(double x0) => Analytical.Create.Aperture(Glazing, SyntheticModels.Quad(P(x0, 0, 1), P(x0 + 1, 0, 1), P(x0 + 1, 0, 2), P(x0, 0, 2)));

        private static Dictionary<Guid, Aperture> ByGuid(params Aperture[] apertures) => apertures.ToDictionary(x => x.Guid);

        private static (Panel Slab, Panel Air) StairVoid()
        {
            AdjacencyCluster adjacencyCluster = SyntheticModels.StackedZonesWithStairVoid().AdjacencyCluster;
            return (adjacencyCluster.GetPanels().Find(x => x.PanelType == PanelType.FloorInternal), adjacencyCluster.GetPanels().Find(x => x.PanelType == PanelType.Air));
        }

        // =================================================================================================
        // 1 - aperture surface identity, from the element name
        // =================================================================================================

        [Test]
        public void ApertureByElementName_PaneElementNamingTheAperture_ReturnsItAsThePane()
        {
            Aperture window = Window(1);
            Aperture other = Window(3);

            Aperture result = TasQuery.ApertureByElementName(string.Format("Windows: SIM_EXT_GLZ {0} -pane", window.Guid), BEType_Glazing, ByGuid(window, other), out AperturePart aperturePart);

            Assert.That(result?.Guid, Is.EqualTo(window.Guid));
            Assert.That(aperturePart, Is.EqualTo(AperturePart.Pane));
        }

        [Test]
        public void ApertureByElementName_FrameElement_ReturnsTheFrame()
        {
            Aperture window = Window(1);

            Aperture result = TasQuery.ApertureByElementName(string.Format("Windows: SIM_EXT_GLZ {0} -frame", window.Guid), BEType_Frame, ByGuid(window), out AperturePart aperturePart);

            Assert.That(result?.Guid, Is.EqualTo(window.Guid));
            Assert.That(aperturePart, Is.EqualTo(AperturePart.Frame));
        }

        [Test]
        public void ApertureByElementName_DoorTypedLeaf_ReturnsThePane()
        {
            //TAS types a door leaf DOORELEMENT (14); it is the opening's pane, as the import reads it.
            Aperture door = Analytical.Create.Aperture(Door, SyntheticModels.Quad(P(1, 0, 0), P(2, 0, 0), P(2, 0, 2), P(1, 0, 2)));

            Aperture result = TasQuery.ApertureByElementName(string.Format("Doors: SIM_INT_SLD {0} -pane", door.Guid), BEType_Door, ByGuid(door), out AperturePart aperturePart);

            Assert.That(result?.Guid, Is.EqualTo(door.Guid));
            Assert.That(aperturePart, Is.EqualTo(AperturePart.Pane));
        }

        [Test]
        public void ApertureByElementName_SharedDefinitionNameWithNoGuid_ReturnsNull()
        {
            //After Reusing Aperture Definitions an element is named after its definition - nothing to decode, so geometry decides.
            Aperture window = Window(1);

            Assert.That(TasQuery.ApertureByElementName("Windows: SIM_EXT_GLZ -pane", BEType_Glazing, ByGuid(window), out AperturePart aperturePart), Is.Null);
            Assert.That(aperturePart, Is.EqualTo(AperturePart.Undefined));
        }

        [Test]
        public void ApertureByElementName_GuidOfAnApertureNotInThisSpace_ReturnsNull()
        {
            Aperture window = Window(1);

            Assert.That(TasQuery.ApertureByElementName(string.Format("Windows: SIM_EXT_GLZ {0} -pane", Guid.NewGuid()), BEType_Glazing, ByGuid(window), out AperturePart _), Is.Null);
        }

        [Test]
        public void ApertureByElementName_OpaqueElement_ReturnsNull()
        {
            //An opaque element is never an aperture surface, whatever its name carries.
            Aperture window = Window(1);

            Assert.That(TasQuery.ApertureByElementName(string.Format("SIM_EXT_SLD {0}", window.Guid), BEType_Wall, ByGuid(window), out AperturePart _), Is.Null);
        }

        // =================================================================================================
        // 2 - TBD aperture to SAM aperture, by the pane surface stamps
        // =================================================================================================

        private static Aperture Stamped(Aperture aperture, AperturePart aperturePart, params ZoneSurfaceReference[] zoneSurfaceReferences)
        {
            aperture.SetApertureZoneSurfaceReferences(aperturePart, zoneSurfaceReferences.ToList(), out string _);
            return aperture;
        }

        [Test]
        public void PaneStampMatch_TbdPaneSurfaceStampedOnASamAperture_ReturnsThatAperture()
        {
            string zone = Guid.NewGuid().ToString("B");
            Aperture sam_1 = Stamped(Window(1), AperturePart.Pane, new ZoneSurfaceReference(3, zone));
            Aperture sam_2 = Stamped(Window(3), AperturePart.Pane, new ZoneSurfaceReference(8, zone));

            //Geometry deliberately elsewhere: the TBD aperture is offset by half a wall, as with widths ON.
            Aperture tbd = Stamped(Analytical.Create.Aperture(Glazing, SyntheticModels.Quad(P(3, 0.125, 1), P(4, 0.125, 1), P(4, 0.125, 2), P(3, 0.125, 2))), AperturePart.Pane, new ZoneSurfaceReference(8, zone));

            Assert.That(TasQuery.PaneStampMatch(tbd, new[] { sam_1, sam_2 })?.Guid, Is.EqualTo(sam_2.Guid));
        }

        [Test]
        public void PaneStampMatch_InternalDoorStampedFromTheOtherZone_ReturnsTheDoor()
        {
            //An internal door has a pane surface in each zone; the TBD side may have been read from either.
            string zoneA = Guid.NewGuid().ToString("B");
            string zoneB = Guid.NewGuid().ToString("B");
            Aperture door = Stamped(Analytical.Create.Aperture(Door, SyntheticModels.Quad(P(1, 0, 0), P(2, 0, 0), P(2, 0, 2), P(1, 0, 2))), AperturePart.Pane, new ZoneSurfaceReference(3, zoneA), new ZoneSurfaceReference(14, zoneB));

            Aperture tbd = Stamped(Window(1), AperturePart.Pane, new ZoneSurfaceReference(14, zoneB));

            Assert.That(TasQuery.PaneStampMatch(tbd, new[] { door })?.Guid, Is.EqualTo(door.Guid));
        }

        [Test]
        public void PaneStampMatch_SameNumberInAnotherZone_ReturnsNull()
        {
            Aperture sam = Stamped(Window(1), AperturePart.Pane, new ZoneSurfaceReference(3, Guid.NewGuid().ToString("B")));
            Aperture tbd = Stamped(Window(1), AperturePart.Pane, new ZoneSurfaceReference(3, Guid.NewGuid().ToString("B")));

            Assert.That(TasQuery.PaneStampMatch(tbd, new[] { sam }), Is.Null);
        }

        [Test]
        public void PaneStampMatch_OnlyAFrameStampNamesTheSurface_ReturnsNull()
        {
            string zone = Guid.NewGuid().ToString("B");
            Aperture sam = Stamped(Window(1), AperturePart.Frame, new ZoneSurfaceReference(3, zone));
            Aperture tbd = Stamped(Window(1), AperturePart.Pane, new ZoneSurfaceReference(3, zone));

            Assert.That(TasQuery.PaneStampMatch(tbd, new[] { sam }), Is.Null);
        }

        [Test]
        public void PaneStampMatch_UnstampedSamApertures_ReturnsNull()
        {
            //Nothing stamped - the caller then falls back to geometry, exactly as before.
            Aperture tbd = Stamped(Window(1), AperturePart.Pane, new ZoneSurfaceReference(3, Guid.NewGuid().ToString("B")));

            Assert.That(TasQuery.PaneStampMatch(tbd, new[] { Window(1), Window(3) }), Is.Null);
        }

        // =================================================================================================
        // 3 - an opaque surface offset by half its element's width
        // =================================================================================================

        [Test]
        public void OffsetMatch_SurfaceHalfAWallWidthIntoTheZone_MatchesTheWall()
        {
            //House model: 0.25 m external wall, its zone surface 0.125 m in from the SAM mid-plane.
            Panel wall = Wall();

            Panel result = TasQuery.OffsetMatch(wall, P(2.5, 0.125, 1.5), new Vector3D(0, -1, 0), 0.125 + Tolerance, Tolerance, out double distance);

            Assert.That(result?.Guid, Is.EqualTo(wall.Guid));
            Assert.That(distance, Is.EqualTo(0.125).Within(1e-9));
        }

        [Test]
        public void OffsetMatch_FurtherThanHalfTheWidth_ReturnsNull()
        {
            Assert.That(TasQuery.OffsetMatch(Wall(), P(2.5, 0.2, 1.5), new Vector3D(0, 1, 0), 0.125 + Tolerance, Tolerance, out double _), Is.Null);
        }

        [Test]
        public void OffsetMatch_NotParallel_ReturnsNull()
        {
            //A floor surface near the foot of a wall is within reach of the wall's plane but is not on it.
            Assert.That(TasQuery.OffsetMatch(Wall(), P(2.5, 0.1, 0.1), new Vector3D(0, 0, 1), 0.125 + Tolerance, Tolerance, out double _), Is.Null);
        }

        [Test]
        public void OffsetMatch_ProjectedOutsideThePanel_ReturnsNull()
        {
            Assert.That(TasQuery.OffsetMatch(Wall(), P(6, 0.125, 1.5), new Vector3D(0, 1, 0), 0.125 + Tolerance, Tolerance, out double _), Is.Null);
        }

        [Test]
        public void OffsetMatch_ProjectedIntoAHole_ReturnsNullForTheSlab_AndTheAirPanelForTheVoid()
        {
            //Stair void: slab at z = 3, 0.20 m, hole x 1-2, y 1-3 filled by an Air panel. The ceiling below sits 0.1 m down.
            (Panel slab, Panel air) = StairVoid();

            Assert.That(TasQuery.OffsetMatch(slab, P(1.5, 2, 2.9), new Vector3D(0, 0, 1), 0.1 + Tolerance, Tolerance, out double _), Is.Null);
            Assert.That(TasQuery.OffsetMatch(air, P(1.5, 2, 2.9), new Vector3D(0, 0, 1), 0.1 + Tolerance, Tolerance, out double _)?.Guid, Is.EqualTo(air.Guid));
            Assert.That(TasQuery.OffsetMatch(slab, P(3.5, 2, 2.9), new Vector3D(0, 0, 1), 0.1 + Tolerance, Tolerance, out double _)?.Guid, Is.EqualTo(slab.Guid));
        }

        // =================================================================================================
        // 4 - a point in a hole is not on the holed panel
        // =================================================================================================

        [Test]
        public void InRangeOutsideHoles_PointInTheHole_IsNotOnTheSlab()
        {
            (Panel slab, Panel air) = StairVoid();

            Assert.That(slab.GetFace3D(false).InRange(P(1.5, 2, 3), Tolerance), Is.True, "Face3D.InRange ignores holes - the defect");
            Assert.That(TasQuery.InRangeOutsideHoles(slab.GetFace3D(false), P(1.5, 2, 3), Tolerance), Is.False);
            Assert.That(TasQuery.InRangeOutsideHoles(air.GetFace3D(false), P(1.5, 2, 3), Tolerance), Is.True);
        }

        [Test]
        public void InRangeOutsideHoles_SolidPartAndHoleEdge_AreOnTheSlab()
        {
            (Panel slab, Panel _) = StairVoid();
            Face3D face3D = slab.GetFace3D(false);

            Assert.That(TasQuery.InRangeOutsideHoles(face3D, P(3.5, 2, 3), Tolerance), Is.True);
            Assert.That(TasQuery.InRangeOutsideHoles(face3D, P(1, 2, 3), Tolerance), Is.True);
            Assert.That(TasQuery.InRangeOutsideHoles(face3D, P(3.5, 2, 3.2), Tolerance), Is.False);
        }

        // =================================================================================================
        // 5 - OpeningProperties are optional: the Updating Aperture Types summary
        // =================================================================================================

        private static List<string> Issues(List<string> notes) => notes.FindAll(x => x.StartsWith(TasModify.NotePrefix_Issue, StringComparison.Ordinal));

        [Test]
        public void ApertureTypesSummary_FixedWindowsAndDoorsWithoutOpeningProperties_OneInformationalLineNoWarnings()
        {
            //House model: 16 fixed windows and 9 doors, none with OpeningProperties. Was 25 ISSUE lines (20 listed + a count).
            List<string> notes = TasQuery.ApertureTypesSummary(25, 25, 25, 0, 0, 0, new Dictionary<string, int>(), new Dictionary<string, int>());

            Assert.That(notes, Has.Count.EqualTo(1));
            Assert.That(Issues(notes), Is.Empty);
            Assert.That(notes[0], Does.Contain("25 TBD apertures seen, 25 matched").And.Contain("25 with no OpeningProperties"));
        }

        [Test]
        public void ApertureTypesSummary_OperableWindowWithOpeningPropertiesWritten_NoWarnings()
        {
            //Load-sensitive fixture: 2 operable windows written, 1 schedule requested and read back, 3 fixed.
            List<string> notes = TasQuery.ApertureTypesSummary(5, 5, 3, 2, 1, 1, new Dictionary<string, int>(), new Dictionary<string, int>());

            Assert.That(Issues(notes), Is.Empty);
            Assert.That(notes[0], Does.Contain("2 updated"));
        }

        [Test]
        public void ApertureTypesSummary_RequestedControlRefused_StaysAWarning()
        {
            //An explicitly requested control that could not be written is still an issue, with its gate and count.
            List<string> notes = TasQuery.ApertureTypesSummary(3, 3, 2, 0, 1, 0, new Dictionary<string, int> { { "6 aperture type write", 1 } }, new Dictionary<string, int> { { "6 aperture type write", 1 } });

            List<string> issues = Issues(notes);
            Assert.That(issues, Has.Count.EqualTo(2));
            Assert.That(issues[0], Does.Contain("1 aperture(s) stopped at gate 6 aperture type write"));
            Assert.That(issues[1], Does.Contain("1 of 1 requested availability schedules did NOT"));
        }

        [Test]
        public void ApertureTypesSummary_UnmatchedApertures_StayAWarning()
        {
            //What widths ON produced before the fix: every aperture stopped at gate 3. Still reported, still counted past the cap.
            List<string> notes = TasQuery.ApertureTypesSummary(25, 0, 0, 0, 0, 0, new Dictionary<string, int> { { "3 no SAM aperture", 25 } }, new Dictionary<string, int> { { "3 no SAM aperture", 20 } });

            Assert.That(Issues(notes), Has.Count.EqualTo(1));
            Assert.That(Issues(notes)[0], Does.Contain("25 aperture(s) stopped at gate 3").And.Contain("the remaining 5 are counted only"));
        }
    }
}
