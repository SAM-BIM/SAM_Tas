using SAM.Core.Tas;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SAM.Analytical.Tas.DirectT3D.Validation
{
    /// <summary>
    /// Evidence for gating the gbXML-era repair steps off the direct route: does re-running each one over a TBD the direct
    /// route produced change anything? A step that changes nothing, on a model that exercises it, is redundant there.
    /// </summary>
    public static class GatingExperiment
    {
        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;

        public static int Run(string[] args)
        {
            // gating <model.sam> <outDir>
            string directory = args[2];
            Checker check = new Checker();

            AnalyticalModel real = SAM.Core.Convert.ToSAM<AnalyticalModel>(args[1])?.FirstOrDefault();

            check.Section("Setting Adiabatic (Modify.UpdateAdiabatic) over a direct TBD - the real model");
            AdiabaticOver(check, directory, "gating_real", real);

            check.Section("Setting Adiabatic over a direct TBD - synthetic: adiabatic wall WITH a window in it");
            AnalyticalModel synthetic = SyntheticModels.AdiabaticWallWithWindow();
            DirectRunResult direct = DirectRun.Run(synthetic, new ToT3DOptions { UseWidths = false }, directory, "gating_adiabatic_window");
            check.True(direct.Tbd != null, "direct run exported");
            if (direct?.Tbd != null)
            {
                foreach (TbdSnapshot.Surf surf in direct.Tbd.AllSurfaces().Where(x => x.Type == "tbdNullLink")) check.Info(string.Format(ci, "direct, as imported: {0} #{1} BE='{2}' area={3:F3}", surf.Zone, surf.Number, surf.Be, surf.Area));
                int apertureNullLinks = direct.Tbd.AllSurfaces().Count(x => x.Type == "tbdNullLink" && x.Be.StartsWith("Windows:"));
                check.Equal(direct.Tbd.AllSurfaces().Count(x => x.Type == "tbdNullLink"), 3, "direct: the adiabatic wall AND its window's pane and frame are null-linked as imported (TAS carries the flag to the openings)");
                check.Equal(apertureNullLinks, 2, "direct: of which aperture surfaces");

                // What the gbXML-era repair would do to that TBD. It aligns SAM geometry to the TBD by the gbXML import's recentring of the
                // footprint on the origin; a direct TBD stays in SAM coordinates, so the shifted adiabatic panel lands on a different wall.
                int after = CountAfterUpdateAdiabatic(direct.Path_TBD, synthetic, out List<string> described);
                foreach (string line in described) check.Info("after Setting Adiabatic: " + line);
                check.True(after > 3, "evidence: Setting Adiabatic over a direct TBD ADDS a false null link (" + after + " vs 3) - it assumes a recentred TBD, so it must stay off the direct route");
            }

            DirectRunResult gbXML = GbXmlRun.Run(synthetic, false, directory, "gating_adiabatic_window_gbxml");
            if (gbXML.Tbd != null)
            {
                int types = gbXML.Tbd.AllSurfaces().Count(x => x.Type == "tbdNullLink");
                check.Info("gbXML route before any repair: tbdNullLink surfaces = " + types + " (aperture ones: " + gbXML.Tbd.AllSurfaces().Count(x => x.Type == "tbdNullLink" && x.Be.StartsWith("Windows:")) + ")");

                // The workflow then runs UpdateAdiabatic over that TBD.
                int before = gbXML.Tbd.AllSurfaces().Count(x => x.Type == "tbdNullLink");
                int after = CountAfterUpdateAdiabatic(gbXML.Path_TBD, synthetic);
                check.Info(string.Format(ci, "gbXML route after Setting Adiabatic: tbdNullLink {0} -> {1} (aperture ones after: see TBD)", before, after));
            }

            check.Section("Where does TAS put the geometry? Panel bounding boxes, SAM vs the TBD of each route (real model)");
            Offsets(check, directory, real, args.Length > 3 ? args[3] : null);

            check.Section("Assigning Adiabatic Constructions over a direct TBD");
            foreach (string path in new[] { Path.Combine(directory, "gating_real.tbd"), Path.Combine(directory, "gating_adiabatic_window.tbd") })
            {
                if (!File.Exists(path)) continue;
                using (SAMTBDDocument sAMTBDDocument = new SAMTBDDocument(path, false))
                {
                    List<Guid> assigned = Modify.AssignAdiabaticConstruction(sAMTBDDocument.TBDDocument, "Adiabatic", new[] { "-unzoned", "-internal", "-exposed" }, false, true);
                    check.Equal(assigned == null ? 0 : assigned.Count, 0, Path.GetFileName(path) + ": building elements it would re-assign (suffix match)");
                }
            }

            check.Write(Path.Combine(directory, "gating-results.txt"));
            Console.WriteLine("gating: passed={0} failed={1}", check.Passed, check.Failed);
            return check.Failed == 0 ? 0 : 1;
        }

        private static void Offsets(Checker check, string directory, AnalyticalModel model, string gbXMLReferenceTbd)
        {
            Func<AdjacencyCluster, string> box = cluster =>
            {
                Geometry.Spatial.BoundingBox3D bb = new Geometry.Spatial.BoundingBox3D(cluster.GetPanels().Where(x => x.PanelType != PanelType.Shade).Select(x => x.GetBoundingBox()).ToList());
                return string.Format(ci, "min=({0:F2},{1:F2},{2:F2}) max=({3:F2},{4:F2},{5:F2}) centroid=({6:F2},{7:F2},{8:F2})", bb.Min.X, bb.Min.Y, bb.Min.Z, bb.Max.X, bb.Max.Y, bb.Max.Z, bb.GetCentroid().X, bb.GetCentroid().Y, bb.GetCentroid().Z);
            };

            check.Info("SAM model   : " + box(model.AdjacencyCluster));

            foreach (string path in new[] { Path.Combine(directory, "gating_real.tbd"), gbXMLReferenceTbd })
            {
                if (!File.Exists(path)) continue;
                using (SAMTBDDocument sAMTBDDocument = new SAMTBDDocument(path, false))
                {
                    AdjacencyCluster cluster = sAMTBDDocument.TBDDocument.Building.ToSAM();
                    check.Info(string.Format("{0,-12}: {1}", path == gbXMLReferenceTbd ? "gbXML TBD" : "direct TBD", box(cluster)));
                }
            }
        }

        private static DirectRunResult AdiabaticOver(Checker check, string directory, string name, AnalyticalModel model)
        {
            DirectRunResult result = DirectRun.Run(model, new ToT3DOptions { UseWidths = false }, directory, name);
            if (result.Tbd == null)
            {
                check.True(false, name + ": direct run exported");
                return null;
            }

            int before = result.Tbd.AllSurfaces().Count(x => x.Type == "tbdNullLink");
            foreach (TbdSnapshot.Surf surf in result.Tbd.AllSurfaces().Where(x => x.Type == "tbdNullLink")) check.Info(string.Format(ci, "before: {0} #{1} BE='{2}' area={3:F3}", surf.Zone, surf.Number, surf.Be, surf.Area));
            int after = CountAfterUpdateAdiabatic(result.Path_TBD, model, out List<string> described);
            foreach (string line in described) check.Info("after : " + line);
            check.Info(string.Format(ci, "{0}: tbdNullLink surfaces {1} -> {2} after running UpdateAdiabatic over the direct TBD", name, before, after));
            check.Equal(after, before, name + ": UpdateAdiabatic changes nothing - the importer already states every adiabatic surface");
            return result;
        }

        // Runs the repair in a read-write session and counts null links, WITHOUT saving: the TBD on disk is not touched.
        private static int CountAfterUpdateAdiabatic(string path_TBD, AnalyticalModel model)
        {
            return CountAfterUpdateAdiabatic(path_TBD, model, out List<string> _);
        }

        private static int CountAfterUpdateAdiabatic(string path_TBD, AnalyticalModel model, out List<string> described)
        {
            described = new List<string>();
            using (SAMTBDDocument sAMTBDDocument = new SAMTBDDocument(path_TBD, false))
            {
                Modify.UpdateAdiabatic(sAMTBDDocument.TBDDocument, model, Core.Tolerance.MacroDistance);

                int count = 0;
                TBD.Building building = sAMTBDDocument.TBDDocument.Building;
                for (int i = 0; ; i++)
                {
                    TBD.zone zone = building.GetZone(i);
                    if (zone == null) break;
                    for (int j = 0; ; j++)
                    {
                        TBD.zoneSurface zoneSurface = zone.GetSurface(j);
                        if (zoneSurface == null) break;
                        if (zoneSurface.type == TBD.SurfaceType.tbdNullLink)
                        {
                            count++;
                            described.Add(string.Format(ci, "{0} #{1} BE='{2}' area={3:F3}", zone.name, zoneSurface.number, zoneSurface.buildingElement?.name, zoneSurface.area));
                        }
                    }
                }

                return count;
            }
        }
    }
}
