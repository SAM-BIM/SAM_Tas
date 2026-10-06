using System;
using System.IO;
using System.Linq;

namespace SAM.Analytical.Tas.DirectT3D.Validation
{
    /// <summary>
    /// Licensed validation of the direct route over the synthetic buildings: what TAS actually builds from them,
    /// read back from the TBD it exports - geometry, and the surface / link types.
    /// </summary>
    public static class SyntheticValidation
    {
        public static int Run(string directory)
        {
            Directory.CreateDirectory(directory);
            Checker check = new Checker();

            Box(check, directory, widths: true);
            Box(check, directory, widths: false);
            BoxWithWindow(check, directory);
            TwoZones(check, directory);
            AdiabaticWall(check, directory);
            Ground(check, directory);
            Stacked(check, directory, upperFirst: false);
            Stacked(check, directory, upperFirst: true);

            check.Write(Path.Combine(directory, "synthetic-results.txt"));
            Console.WriteLine();
            Console.WriteLine("synthetic: passed={0} failed={1}", check.Passed, check.Failed);
            return check.Failed == 0 ? 0 : 1;
        }

        private static DirectRunResult Run(Checker check, string directory, string name, AnalyticalModel model, bool widths)
        {
            DirectRunResult result = DirectRun.Run(model, new ToT3DOptions { UseWidths = widths }, directory, name);
            check.Info("report: " + (result.Report == null ? "(none)" : result.Report.ToString()));
            if (result.Report != null)
            {
                foreach (string line in result.Report.Skipped) check.Info("skipped: " + line);
                foreach (string line in result.Report.Notes) check.Info("note: " + line);
            }

            check.True(result.Converted, name + ": converted");
            check.True(result.Exported, name + ": exported to TBD");
            if (result.Tbd != null)
            {
                foreach (string line in result.Tbd.ToText().Split('\n')) check.Info(line.TrimEnd());
            }

            return result;
        }

        private static int Count(TbdSnapshot tbd, string type)
        {
            return tbd.AllSurfaces().Count(x => x.Type == type);
        }

        private static void Box(Checker check, string directory, bool widths)
        {
            string name = widths ? "box_widths_on" : "box_widths_off";
            check.Section(name + " (5 x 4 x 3 m, walls 0.30 / roof 0.35 / floor 0.40)");

            DirectRunResult result = Run(check, directory, name, SyntheticModels.Box(), widths);
            if (result.Tbd == null) return;
            TbdSnapshot tbd = result.Tbd;

            check.Equal(tbd.Zones.Count, 1, "zones");
            check.Near(tbd.TotalFloorArea, widths ? 17.39 : 20.0, 0.01, "floor area");
            check.Near(tbd.TotalVolume, widths ? 45.649 : 60.0, 0.01, "volume");
            check.Equal(tbd.Zones.Count > 0 ? tbd.Zones[0].Surfaces.Count : 0, 6, "surfaces");
            check.Equal(Count(tbd, "tbdExposed"), 5, "exposed surfaces (4 walls + roof)");
            check.Equal(Count(tbd, "tbdGround"), 1, "ground surfaces (the slab on grade)");
            check.Equal(Count(tbd, "tbdLink"), 0, "linked surfaces");
            check.Equal(Count(tbd, "tbdNullLink"), 0, "null-linked (adiabatic) surfaces");

            Guid spaceGuid = result.Report.ZoneNames.Keys.FirstOrDefault();
            check.Equal(tbd.Zones[0].Description, Query.ZoneDescription(spaceGuid), "zone description carries the SAM space GUID");
        }

        private static void BoxWithWindow(Checker check, string directory)
        {
            check.Section("box_window (one 2 x 1 m window in the south wall, widths OFF)");

            DirectRunResult result = Run(check, directory, "box_window", SyntheticModels.Box(window: true), widths: false);
            if (result.Tbd == null) return;
            TbdSnapshot tbd = result.Tbd;

            check.Equal(result.Report.Openings, 1, "openings imported");
            check.Equal(result.Report.Windows, 1, "window types created");
            TbdSnapshot.Be pane = tbd.BuildingElements.FirstOrDefault(x => x.Name.EndsWith("-pane"));
            TbdSnapshot.Be frame = tbd.BuildingElements.FirstOrDefault(x => x.Name.EndsWith("-frame"));
            check.True(pane != null && frame != null, "TAS made a -pane and a -frame building element for the window type");
            check.Equal(pane?.Name, "Windows: EXT_GLZ -pane", "pane element has the shared-definition name");
            check.Equal(frame?.Name, "Windows: EXT_GLZ -frame", "frame element has the shared-definition name");

            double opening = tbd.AllSurfaces().Where(x => x.Be.StartsWith("Windows: EXT_GLZ")).Sum(x => x.Area);
            check.Near(opening, 2.0, 0.01, "frame + pane area equals the 2 m2 opening polygon");
            double south = tbd.AllSurfaces().Where(x => x.Be == "EXT_WALL" && Math.Abs(x.Orientation - 180) < 1).Sum(x => x.Area);
            check.Near(south, 15.0 - 2.0, 0.01, "south wall (5 x 3 = 15 m2) net of the 2 m2 opening");
        }

        private static void TwoZones(Checker check, string directory)
        {
            check.Section("two_zones_partition (A | B sharing one partition, widths OFF)");

            DirectRunResult result = Run(check, directory, "two_zones_partition", SyntheticModels.TwoZones(), widths: false);
            if (result.Tbd == null) return;
            TbdSnapshot tbd = result.Tbd;

            check.Equal(tbd.Zones.Count, 2, "zones");
            check.Equal(result.Report.InternalSurfaces, 1, "the partition is ONE internal surface (AddInternalSurface), not two");
            check.Equal(Count(tbd, "tbdLink"), 2, "tbdLink surfaces: one per zone");
            check.True(tbd.AllSurfaces().Where(x => x.Type == "tbdLink").All(x => x.Linked), "each tbdLink surface has its link");
            check.True(tbd.AllSurfaces().Where(x => x.Type == "tbdLink").All(x => x.Be == "INT_PARTITION"), "the linked surfaces use the partition element");
            check.Near(tbd.TotalFloorArea, 40.0, 0.01, "total floor area");
            check.Near(tbd.TotalVolume, 120.0, 0.01, "total volume");
            check.Equal(Count(tbd, "tbdExposed"), 8, "exposed surfaces (2 x (roof + 3 outer walls))");
            check.Equal(Count(tbd, "tbdGround"), 2, "ground surfaces");
        }

        private static void AdiabaticWall(Checker check, string directory)
        {
            check.Section("adiabatic_wall (A's north wall adiabatic, widths OFF)");

            DirectRunResult result = Run(check, directory, "adiabatic_wall", SyntheticModels.TwoZones(adiabaticNorthWallOfA: true), widths: false);
            if (result.Tbd == null) return;
            TbdSnapshot tbd = result.Tbd;

            check.Equal(result.Report.AdiabaticSurfaces, 1, "adiabatic surfaces imported");
            check.Equal(Count(tbd, "tbdNullLink"), 1, "tbdNullLink surfaces (the adiabatic wall)");
            TbdSnapshot.Surf wall = tbd.AllSurfaces().FirstOrDefault(x => x.Type == "tbdNullLink");
            check.True(wall != null && wall.Zone == "A" && Math.Abs(wall.Orientation) < 1 && Math.Abs(wall.Area - 15.0) < 0.01, "it is A's north wall (orientation 0), 15 m2");
            check.Equal(Count(tbd, "tbdExposed"), 7, "exposed surfaces (8 less the adiabatic wall)");
            check.Equal(Count(tbd, "tbdLink"), 2, "the partition is still linked");
        }

        private static void Ground(Checker check, string directory)
        {
            check.Section("ground (the slab on grade, widths OFF)");

            DirectRunResult result = Run(check, directory, "ground", SyntheticModels.Box(), widths: false);
            if (result.Tbd == null) return;
            TbdSnapshot tbd = result.Tbd;

            TbdSnapshot.Surf slab = tbd.AllSurfaces().FirstOrDefault(x => x.Type == "tbdGround");
            check.True(slab != null && Math.Abs(slab.Area - 20.0) < 0.01 && slab.Be == "GRD_FLOOR", "one tbdGround surface of 20 m2 on GRD_FLOOR");
            check.True(tbd.BuildingElements.Any(x => x.Name == "GRD_FLOOR" && x.Ground), "the GRD_FLOOR building element is flagged ground");
            check.True(tbd.BuildingElements.Where(x => x.Name != "GRD_FLOOR").All(x => !x.Ground), "no other element is");
        }

        private static void Stacked(Checker check, string directory, bool upperFirst)
        {
            string name = upperFirst ? "stacked_upper_first" : "stacked_lower_first";
            check.Section(name + " (horizontal internal floor between two zones, widths OFF)");

            DirectRunResult result = Run(check, directory, name, SyntheticModels.StackedZones(upperFirst), widths: false);
            if (result.Tbd == null) return;
            TbdSnapshot tbd = result.Tbd;

            check.Equal(tbd.Zones.Count, 2, "zones");
            check.Equal(result.Report.InternalSurfaces, 1, "the floor/ceiling is ONE internal surface");
            check.Equal(Count(tbd, "tbdLink"), 2, "tbdLink surfaces: one per zone");
            check.True(tbd.AllSurfaces().Where(x => x.Type == "tbdLink").All(x => x.Linked), "each has its link");

            TbdSnapshot.Zn lower = tbd.Zones.First(x => x.Name == "Lower");
            TbdSnapshot.Zn upper = tbd.Zones.First(x => x.Name == "Upper");
            TbdSnapshot.Surf ceiling = lower.Surfaces.First(x => x.Type == "tbdLink");
            TbdSnapshot.Surf floor = upper.Surfaces.First(x => x.Type == "tbdLink");
            check.Near(ceiling.Inclination, 0, 1, "Lower sees it as a ceiling (inclination 0)");
            check.Near(floor.Inclination, 180, 1, "Upper sees it as a floor (inclination 180)");
            check.Near(ceiling.Area, 20.0, 0.01, "its area (Lower side)");
            check.Near(floor.Area, 20.0, 0.01, "its area (Upper side)");
            check.Near(lower.FloorArea, 20.0, 0.01, "Lower floor area");
            check.Near(upper.FloorArea, 20.0, 0.01, "Upper floor area");
            check.Near(lower.Volume, 60.0, 0.01, "Lower volume");
            check.Near(upper.Volume, 60.0, 0.01, "Upper volume");
        }
    }
}
