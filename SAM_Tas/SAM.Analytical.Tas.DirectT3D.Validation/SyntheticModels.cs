// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Geometry.Spatial;
using System.Collections.Generic;
using System.Linq;

// This file is compiled into BOTH the licensed validation harness and the COM-free unit tests
// (SAM.Analytical.Tas.TM59.Tests links it), so the two always describe the same synthetic buildings. It touches no
// TAS type.
namespace SAM.Analytical.Tas.DirectT3D.Validation
{
    /// <summary>
    /// Small hand-assembled SAM models for the direct SAM -> T3D route. Assembled by hand on purpose (panels from
    /// <c>Create.Panel(construction, panelType, face3D)</c>, relations added explicitly): the convenience factories
    /// reach <c>ActiveSetting</c>, whose cold start is a known trap in a bare console process.
    /// <para>
    /// The reference box is 5 x 4 x 3 m with walls 0.30 m, roof 0.35 m and floor 0.40 m thick - the one the TAS
    /// importer spike measured: widths ON give 17.39 m2 and 45.649 m3, widths OFF 20 m2 and 60 m3.
    /// </para>
    /// </summary>
    public static class SyntheticModels
    {
        public const double Width = 5;
        public const double Depth = 4;
        public const double Height = 3;

        public static Construction WallConstruction { get; } = new Construction(System.Guid.Parse("00000000-0000-0000-0000-0000000000a1"), "EXT_WALL", new[] { new ConstructionLayer("Wall layer", 0.30) });
        public static Construction RoofConstruction { get; } = new Construction(System.Guid.Parse("00000000-0000-0000-0000-0000000000a2"), "EXT_ROOF", new[] { new ConstructionLayer("Roof layer", 0.35) });
        public static Construction FloorConstruction { get; } = new Construction(System.Guid.Parse("00000000-0000-0000-0000-0000000000a3"), "GRD_FLOOR", new[] { new ConstructionLayer("Floor layer", 0.40) });
        public static Construction PartitionConstruction { get; } = new Construction(System.Guid.Parse("00000000-0000-0000-0000-0000000000a4"), "INT_PARTITION", new[] { new ConstructionLayer("Partition layer", 0.10) });
        public static Construction InternalFloorConstruction { get; } = new Construction(System.Guid.Parse("00000000-0000-0000-0000-0000000000a5"), "INT_FLOOR", new[] { new ConstructionLayer("Internal floor layer", 0.25) });
        public static ApertureConstruction GlazingConstruction { get; } = CreateGlazingConstruction();

        // Transparent is stated explicitly: the synthetic material library is empty, so the layers cannot say it,
        // and without it TAS would build an OPAQUE opening (a door) rather than glazing.
        private static ApertureConstruction CreateGlazingConstruction()
        {
            ApertureConstruction result = new ApertureConstruction(System.Guid.Parse("00000000-0000-0000-0000-0000000000b1"), "EXT_GLZ", ApertureType.Window, new[] { new ConstructionLayer("Pane layer", 0.024) }, new[] { new ConstructionLayer("Frame layer", 0.07) });
            result.SetValue(ApertureConstructionParameter.Transparent, true);
            result.SetValue(ApertureConstructionParameter.DefaultFrameWidth, 0.05);
            return result;
        }

        private static Point3D P(double x, double y, double z)
        {
            return new Point3D(x, y, z);
        }

        // A planar quad as a Face3D; the vertex order is the caller's - and deliberately not always counter-clockwise
        // from outside, so the converter's own orientation logic is what is being tested.
        public static Face3D Quad(Point3D p0, Point3D p1, Point3D p2, Point3D p3)
        {
            return new Face3D(new Polygon3D(new[] { p0, p1, p2, p3 }));
        }

        public static Panel Panel(Construction construction, PanelType panelType, Face3D face3D, string name = null, bool adiabatic = false)
        {
            Panel result = Analytical.Create.Panel(construction, panelType, face3D);
            if (adiabatic)
            {
                result.SetValue(Analytical.PanelParameter.Adiabatic, true);
            }

            return result;
        }

        /// <summary>The six faces of a box, normals deliberately mixed (floor and some walls wound inward).</summary>
        public static List<Panel> BoxPanels(double x0, double x1, double y0, double y1, double z0, double z1, Construction floor, PanelType floorType, Construction roof, PanelType roofType, Construction wall, PanelType wallType, bool floorOutward = true)
        {
            List<Panel> result = new List<Panel>();

            // floor: outward is -Z. Wound counter-clockwise from below when floorOutward, else the other way.
            Face3D face_Floor = floorOutward
                ? Quad(P(x0, y0, z0), P(x0, y1, z0), P(x1, y1, z0), P(x1, y0, z0))
                : Quad(P(x0, y0, z0), P(x1, y0, z0), P(x1, y1, z0), P(x0, y1, z0));
            result.Add(Panel(floor, floorType, face_Floor));

            // roof: outward +Z, wound the "wrong" (clockwise from above) way on purpose
            result.Add(Panel(roof, roofType, Quad(P(x0, y0, z1), P(x0, y1, z1), P(x1, y1, z1), P(x1, y0, z1))));

            // south (y0), east (x1), north (y1), west (x0)
            result.Add(Panel(wall, wallType, Quad(P(x0, y0, z0), P(x1, y0, z0), P(x1, y0, z1), P(x0, y0, z1))));
            result.Add(Panel(wall, wallType, Quad(P(x1, y0, z0), P(x1, y1, z0), P(x1, y1, z1), P(x1, y0, z1))));
            result.Add(Panel(wall, wallType, Quad(P(x1, y1, z1), P(x0, y1, z1), P(x0, y1, z0), P(x1, y1, z0))));
            result.Add(Panel(wall, wallType, Quad(P(x0, y1, z0), P(x0, y0, z0), P(x0, y0, z1), P(x0, y1, z1))));

            return result;
        }

        private static AdjacencyCluster Cluster(IEnumerable<Space> spaces, IEnumerable<Panel> panels, params KeyValuePair<Space, Panel>[] relations)
        {
            AdjacencyCluster result = new AdjacencyCluster();
            foreach (Space space in spaces)
            {
                result.AddObject(space);
            }

            foreach (Panel panel in panels)
            {
                result.AddObject(panel);
            }

            foreach (KeyValuePair<Space, Panel> relation in relations)
            {
                result.AddRelation(relation.Key, relation.Value);
            }

            return result;
        }

        // One material per construction layer: the gbXML export reads the library, and with it the pane material is what
        // makes the glazing transparent.
        private static SAM.Core.MaterialLibrary Materials()
        {
            SAM.Core.MaterialLibrary result = new SAM.Core.MaterialLibrary("Materials");
            foreach (string name in new[] { "Wall layer", "Roof layer", "Floor layer", "Partition layer", "Internal floor layer", "Frame layer" })
            {
                result.Add(new SAM.Core.OpaqueMaterial(name, "Synthetic", name, name, 1.0, 1000, 1800));
            }

            result.Add(new SAM.Core.TransparentMaterial("Pane layer", "Synthetic", "Pane layer", "Pane layer", 1.0, 840, 2500));
            return result;
        }

        private static AnalyticalModel Model(string name, AdjacencyCluster adjacencyCluster)
        {
            return new AnalyticalModel(name, null, null, null, adjacencyCluster, Materials(), new ProfileLibrary("Profiles"));
        }

        /// <summary>One box zone, the reference box. With <paramref name="window"/> a 2 x 1 m window in the south wall.</summary>
        public static AnalyticalModel Box(bool window = false)
        {
            return Box(window ? 1 : 0);
        }

        /// <summary>
        /// The reference box with <paramref name="windows"/> separate 1 x 1 m windows side by side on the south wall, all of
        /// the one aperture construction - the case that shows how TAS groups openings into TBD surfaces.
        /// </summary>
        public static AnalyticalModel Box(int windows)
        {
            Space space = new Space("Box", P(2.5, 2, 1.5));

            List<Panel> panels = BoxPanels(0, Width, 0, Depth, 0, Height, FloorConstruction, PanelType.SlabOnGrade, RoofConstruction, PanelType.Roof, WallConstruction, PanelType.WallExternal);

            if (windows == 1)
            {
                Panel south = panels[2];
                south.AddAperture(Analytical.Create.Aperture(GlazingConstruction, Quad(P(1.5, 0, 1), P(3.5, 0, 1), P(3.5, 0, 2), P(1.5, 0, 2))));
            }
            else
            {
                for (int i = 0; i < windows; i++)
                {
                    double x = 0.5 + i * 1.5;
                    panels[2].AddAperture(Analytical.Create.Aperture(GlazingConstruction, Quad(P(x, 0, 1), P(x + 1, 0, 1), P(x + 1, 0, 2), P(x, 0, 2))));
                }
            }

            AdjacencyCluster adjacencyCluster = new AdjacencyCluster();
            adjacencyCluster.AddObject(space);
            foreach (Panel panel in panels)
            {
                adjacencyCluster.AddObject(panel);
                adjacencyCluster.AddRelation(space, panel);
            }

            return Model("Box", adjacencyCluster);
        }

        /// <summary>
        /// Two 5 x 4 x 3 m zones side by side (A: x 0..5, B: x 5..10) separated by one shared partition at x = 5.
        /// With <paramref name="adiabaticNorthWallOfA"/> A's north wall is adiabatic.
        /// </summary>
        public static AnalyticalModel TwoZones(bool adiabaticNorthWallOfA = false, bool partitionRelatedToBFirst = false)
        {
            Space spaceA = new Space("A", P(2.5, 2, 1.5));
            Space spaceB = new Space("B", P(7.5, 2, 1.5));

            List<Panel> panels_A = BoxPanels(0, 5, 0, 4, 0, 3, FloorConstruction, PanelType.SlabOnGrade, RoofConstruction, PanelType.Roof, WallConstruction, PanelType.WallExternal);
            List<Panel> panels_B = BoxPanels(5, 10, 0, 4, 0, 3, FloorConstruction, PanelType.SlabOnGrade, RoofConstruction, PanelType.Roof, WallConstruction, PanelType.WallExternal);

            // A's east wall (x = 5) and B's west wall (x = 5) are one shared partition panel instead.
            Panel partition = Panel(PartitionConstruction, PanelType.WallInternal, Quad(P(5, 0, 0), P(5, 4, 0), P(5, 4, 3), P(5, 0, 3)));
            panels_A.RemoveAt(3); // east of A
            panels_B.RemoveAt(5); // west of B

            if (adiabaticNorthWallOfA)
            {
                panels_A[3] = Panel(WallConstruction, PanelType.WallExternal, Quad(P(5, 4, 3), P(0, 4, 3), P(0, 4, 0), P(5, 4, 0)), adiabatic: true);
            }

            AdjacencyCluster adjacencyCluster = new AdjacencyCluster();
            adjacencyCluster.AddObject(spaceA);
            adjacencyCluster.AddObject(spaceB);
            foreach (Panel panel in panels_A)
            {
                adjacencyCluster.AddObject(panel);
                adjacencyCluster.AddRelation(spaceA, panel);
            }

            foreach (Panel panel in panels_B)
            {
                adjacencyCluster.AddObject(panel);
                adjacencyCluster.AddRelation(spaceB, panel);
            }

            adjacencyCluster.AddObject(partition);
            if (partitionRelatedToBFirst)
            {
                // The model still lists A before B; only the partition's own relations are stored B-first.
                adjacencyCluster.AddRelation(spaceB, partition);
                adjacencyCluster.AddRelation(spaceA, partition);
            }
            else
            {
                adjacencyCluster.AddRelation(spaceA, partition);
                adjacencyCluster.AddRelation(spaceB, partition);
            }

            return Model("TwoZones", adjacencyCluster);
        }

        public static ApertureConstruction DoorConstruction { get; } = CreateDoorConstruction();

        // An opaque door: no transparent material and no Transparent parameter, so TAS builds a door (BEType 14), not glazing.
        private static ApertureConstruction CreateDoorConstruction()
        {
            return new ApertureConstruction(System.Guid.Parse("00000000-0000-0000-0000-0000000000b2"), "EXT_DOOR", ApertureType.Door, new[] { new ConstructionLayer("Partition layer", 0.04) });
        }

        /// <summary>
        /// The reference box with a 1 x 2 m door in the south wall and a 1 x 1 m rooflight in the roof - the two openings whose
        /// TAS position type is not the window default.
        /// </summary>
        public static AnalyticalModel BoxWithDoorAndRooflight()
        {
            AnalyticalModel model = Box();
            AdjacencyCluster adjacencyCluster = model.AdjacencyCluster;

            Panel south = adjacencyCluster.GetPanels().First(x => x.PanelType == PanelType.WallExternal && System.Math.Abs(x.GetBoundingBox().Max.Y) < 1e-9);
            south.AddAperture(Analytical.Create.Aperture(DoorConstruction, Quad(P(1, 0, 0), P(2, 0, 0), P(2, 0, 2), P(1, 0, 2))));
            adjacencyCluster.AddObject(south);

            Panel roof = adjacencyCluster.GetPanels().First(x => x.PanelType == PanelType.Roof);
            roof.AddAperture(Analytical.Create.Aperture(GlazingConstruction, Quad(P(2, 1, 3), P(3, 1, 3), P(3, 2, 3), P(2, 2, 3))));
            adjacencyCluster.AddObject(roof);

            return new AnalyticalModel(model, adjacencyCluster);
        }

        /// <summary>
        /// A single storey of <paramref name="nx"/> x <paramref name="ny"/> zones, each 5 x 4 x 3 m, side by side - one shared
        /// partition between neighbours, a roof and a slab on grade each, and (with <paramref name="windows"/>) two windows in every
        /// outer wall. The scale case: a TM59-sized building is a few hundred zones and a few thousand panels.
        /// </summary>
        public static AnalyticalModel Grid(int nx, int ny, bool windows = true)
        {
            AdjacencyCluster adjacencyCluster = new AdjacencyCluster();
            Space[,] spaces = new Space[nx, ny];
            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < ny; j++)
                {
                    spaces[i, j] = new Space(string.Format("Z{0}_{1}", i, j), P(i * 5 + 2.5, j * 4 + 2, 1.5));
                    adjacencyCluster.AddObject(spaces[i, j]);
                }
            }

            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < ny; j++)
                {
                    double x0 = i * 5, x1 = x0 + 5, y0 = j * 4, y1 = y0 + 4;
                    Space space = spaces[i, j];

                    Panel floor = Panel(FloorConstruction, PanelType.SlabOnGrade, Quad(P(x0, y0, 0), P(x0, y1, 0), P(x1, y1, 0), P(x1, y0, 0)));
                    Panel roof = Panel(RoofConstruction, PanelType.Roof, Quad(P(x0, y0, 3), P(x1, y0, 3), P(x1, y1, 3), P(x0, y1, 3)));
                    foreach (Panel panel in new[] { floor, roof })
                    {
                        adjacencyCluster.AddObject(panel);
                        adjacencyCluster.AddRelation(space, panel);
                    }

                    // South (y0) and west (x0) walls; the north and east walls belong to the neighbour when there is one.
                    for (int side = 0; side < 4; side++)
                    {
                        // 0 south, 1 east, 2 north, 3 west
                        int ni = i + (side == 1 ? 1 : side == 3 ? -1 : 0);
                        int nj = j + (side == 2 ? 1 : side == 0 ? -1 : 0);
                        bool neighbour = ni >= 0 && ni < nx && nj >= 0 && nj < ny;
                        if (neighbour && (side == 1 || side == 2))
                        {
                            continue; // built by the neighbour's west / south side
                        }

                        Face3D face = side == 0 ? Quad(P(x0, y0, 0), P(x1, y0, 0), P(x1, y0, 3), P(x0, y0, 3))
                            : side == 1 ? Quad(P(x1, y0, 0), P(x1, y1, 0), P(x1, y1, 3), P(x1, y0, 3))
                            : side == 2 ? Quad(P(x1, y1, 0), P(x0, y1, 0), P(x0, y1, 3), P(x1, y1, 3))
                            : Quad(P(x0, y1, 0), P(x0, y0, 0), P(x0, y0, 3), P(x0, y1, 3));

                        Panel wall = Panel(neighbour ? PartitionConstruction : WallConstruction, neighbour ? PanelType.WallInternal : PanelType.WallExternal, face);
                        if (!neighbour && windows && (side == 0 || side == 2))
                        {
                            double y = side == 0 ? y0 : y1;
                            wall.AddAperture(Analytical.Create.Aperture(GlazingConstruction, Quad(P(x0 + 0.5, y, 1), P(x0 + 2, y, 1), P(x0 + 2, y, 2.2), P(x0 + 0.5, y, 2.2))));
                            wall.AddAperture(Analytical.Create.Aperture(GlazingConstruction, Quad(P(x0 + 2.8, y, 1), P(x0 + 4.3, y, 1), P(x0 + 4.3, y, 2.2), P(x0 + 2.8, y, 2.2))));
                        }

                        adjacencyCluster.AddObject(wall);
                        adjacencyCluster.AddRelation(space, wall);
                        if (neighbour)
                        {
                            adjacencyCluster.AddRelation(spaces[ni, nj], wall);
                        }
                    }
                }
            }

            return Model(string.Format("Grid{0}x{1}", nx, ny), adjacencyCluster);
        }

        /// <summary>
        /// The two-zone building with A's north wall adiabatic AND a 1 x 1 m window in it: what becomes of an opening in an
        /// adiabatic wall decides whether the repair that re-derives adiabatic surfaces from geometry is redundant.
        /// </summary>
        public static AnalyticalModel AdiabaticWallWithWindow()
        {
            AnalyticalModel model = TwoZones(adiabaticNorthWallOfA: true);
            AdjacencyCluster adjacencyCluster = model.AdjacencyCluster;
            Space spaceA = adjacencyCluster.GetSpaces().First(x => x.Name == "A");
            Panel north = adjacencyCluster.GetPanels(spaceA).First(x => Analytical.Query.Adiabatic(x));
            north.AddAperture(Analytical.Create.Aperture(GlazingConstruction, Quad(P(2, 4, 1), P(3, 4, 1), P(3, 4, 2), P(2, 4, 2))));
            adjacencyCluster.AddObject(north);
            return new AnalyticalModel(model, adjacencyCluster);
        }

        /// <summary>
        /// Two zones one above the other (Lower: z 0..3, Upper: z 3..6), 5 x 4 m, sharing one horizontal internal
        /// floor at z = 3. <paramref name="upperFirst"/> lists the upper space first, so the shared panel's
        /// first zone is the one above it.
        /// </summary>
        public static AnalyticalModel StackedZones(bool upperFirst = false)
        {
            Space lower = new Space("Lower", P(2.5, 2, 1.5));
            Space upper = new Space("Upper", P(2.5, 2, 4.5));

            List<Panel> panels_Lower = BoxPanels(0, 5, 0, 4, 0, 3, FloorConstruction, PanelType.SlabOnGrade, InternalFloorConstruction, PanelType.FloorInternal, WallConstruction, PanelType.WallExternal);
            List<Panel> panels_Upper = BoxPanels(0, 5, 0, 4, 3, 6, InternalFloorConstruction, PanelType.FloorInternal, RoofConstruction, PanelType.Roof, WallConstruction, PanelType.WallExternal);

            Panel slab = Panel(InternalFloorConstruction, PanelType.FloorInternal, Quad(P(0, 0, 3), P(5, 0, 3), P(5, 4, 3), P(0, 4, 3)));
            panels_Lower.RemoveAt(1); // lower ceiling
            panels_Upper.RemoveAt(0); // upper floor

            AdjacencyCluster adjacencyCluster = new AdjacencyCluster();
            if (upperFirst)
            {
                adjacencyCluster.AddObject(upper);
                adjacencyCluster.AddObject(lower);
            }
            else
            {
                adjacencyCluster.AddObject(lower);
                adjacencyCluster.AddObject(upper);
            }

            foreach (Panel panel in panels_Lower)
            {
                adjacencyCluster.AddObject(panel);
                adjacencyCluster.AddRelation(lower, panel);
            }

            foreach (Panel panel in panels_Upper)
            {
                adjacencyCluster.AddObject(panel);
                adjacencyCluster.AddRelation(upper, panel);
            }

            adjacencyCluster.AddObject(slab);
            if (upperFirst)
            {
                adjacencyCluster.AddRelation(upper, slab);
                adjacencyCluster.AddRelation(lower, slab);
            }
            else
            {
                adjacencyCluster.AddRelation(lower, slab);
                adjacencyCluster.AddRelation(upper, slab);
            }

            return Model("StackedZones", adjacencyCluster);
        }

        /// <summary>
        /// The reference box with a 2 m deep horizontal canopy 1 m above the south window, as a free-standing
        /// <c>PanelType.Shade</c> panel that bounds no space. The window faces the sun; the canopy is the only thing
        /// that can shade it.
        /// </summary>
        public static AnalyticalModel BoxWithShade(bool shade = true, double canopyDepth = 2.0, double canopyZ = 3.0)
        {
            AnalyticalModel model = Box(window: true);
            if (!shade)
            {
                return model;
            }

            AdjacencyCluster adjacencyCluster = model.AdjacencyCluster;
            Panel canopy = Panel(WallConstruction, PanelType.Shade, Quad(P(0, -canopyDepth, canopyZ), P(Width, -canopyDepth, canopyZ), P(Width, 0, canopyZ), P(0, 0, canopyZ)));
            adjacencyCluster.AddObject(canopy);

            return new AnalyticalModel(model, adjacencyCluster);
        }
    }
}
