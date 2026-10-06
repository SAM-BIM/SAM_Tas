using SAM.Core.Tas;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SAM.Analytical.Tas.DirectT3D.Validation
{
    /// <summary>
    /// Does the direct route's per-surface, per-opening COM traffic become a bottleneck at the size of a real building? A grid of
    /// zones - a few hundred, a few thousand panels - converted and exported, timed in its parts, and compared with the gbXML route's
    /// conversion of the very same model.
    /// </summary>
    public static class ScaleExperiment
    {
        public static int Run(string[] args)
        {
            // scale <outDir> <nx> <ny> [gbxml]
            string directory = args[1];
            int nx = int.Parse(args[2]);
            int ny = int.Parse(args[3]);
            bool gbXML = args.Contains("gbxml");
            Directory.CreateDirectory(directory);

            Stopwatch stopwatch = Stopwatch.StartNew();
            AnalyticalModel model = SyntheticModels.Grid(nx, ny);
            double ms_Build = stopwatch.Elapsed.TotalMilliseconds;

            stopwatch.Restart();
            T3DImportPlan plan = model.T3DImportPlan(new ToT3DOptions());
            double ms_Plan = stopwatch.Elapsed.TotalMilliseconds;

            Console.WriteLine("model {0}x{1}: build {2:F0} ms; plan {3:F0} ms - {4}", nx, ny, ms_Build, ms_Plan, plan.Report);
            foreach (string line in plan.Report.Skipped.Take(5)) Console.WriteLine("  skipped: " + line);

            // The COM replay and the export, in separate stopwatches, in one TAS session.
            string path_T3D = Path.Combine(directory, string.Format("grid{0}x{1}.t3d", nx, ny));
            string path_TBD = Path.ChangeExtension(path_T3D, ".tbd");
            if (File.Exists(path_TBD)) File.Delete(path_TBD);

            double ms_Open, ms_Replay, ms_Save, ms_Export;
            using (SAMT3DDocument sAMT3DDocument = new SAMT3DDocument())
            {
                stopwatch.Restart();
                TAS3D.T3DDocument t3DDocument = sAMT3DDocument.T3DDocument;
                ms_Open = stopwatch.Elapsed.TotalMilliseconds;

                stopwatch.Restart();
                bool converted = plan.ToT3D(t3DDocument, new ToT3DOptions());
                ms_Replay = stopwatch.Elapsed.TotalMilliseconds;

                stopwatch.Restart();
                t3DDocument.Save(path_T3D);
                ms_Save = stopwatch.Elapsed.TotalMilliseconds;

                stopwatch.Restart();
                bool exported = converted && Convert.ToTBD(t3DDocument, path_TBD, 1, 365, 15, true, false);
                ms_Export = stopwatch.Elapsed.TotalMilliseconds;

                Console.WriteLine("direct: open {0:F0} ms; COM replay {1:F0} ms ({2} zones, {3} elements, {4} windows, {5} surfaces, {6} openings -> {7:F2} ms per surface/opening call); save {8:F0} ms; T3D->TBD export {9:F0} ms; ok={10}/{11}",
                    ms_Open, ms_Replay, plan.Report.Zones, plan.Report.Elements, plan.Report.Windows, plan.Report.Surfaces, plan.Report.Openings,
                    ms_Replay / Math.Max(1, plan.Report.Surfaces + plan.Report.Openings), ms_Save, ms_Export, converted, exported);
            }

            if (File.Exists(path_TBD))
            {
                TbdSnapshot tbd = TbdSnapshot.Read(path_TBD);
                Console.WriteLine("TBD: zones={0} floor={1:F1} volume={2:F1} surfaces={3} BEs={4}", tbd.Zones.Count, tbd.TotalFloorArea, tbd.TotalVolume, tbd.AllSurfaces().Count(), tbd.BuildingElements.Count);
            }

            if (gbXML)
            {
                stopwatch.Restart();
                DirectRunResult gb = GbXmlRun.Run(model, false, directory, string.Format("grid{0}x{1}_gbxml", nx, ny));
                Console.WriteLine("gbXML route (gbXML write + TAS import + UpdateT3D + export): {0:F0} ms; ok={1}", stopwatch.Elapsed.TotalMilliseconds, gb.Exported);
                if (gb.Tbd != null) Console.WriteLine("TBD: zones={0} floor={1:F1} volume={2:F1} surfaces={3} BEs={4}", gb.Tbd.Zones.Count, gb.Tbd.TotalFloorArea, gb.Tbd.TotalVolume, gb.Tbd.AllSurfaces().Count(), gb.Tbd.BuildingElements.Count);
            }

            return 0;
        }
    }
}
