using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SAM.Analytical.Tas.DirectT3D.Validation
{
    /// <summary>
    /// Runs the real <see cref="WorkflowCalculator"/> over a .sam model on either T3D route, into its own output
    /// folder, and keeps what a comparison needs: the TBD, the T3D, the timing CSV, the notes, and the model the
    /// workflow hands back.
    /// </summary>
    public static class WorkflowRun
    {
        public static int Run(string[] args)
        {
            // workflow <model.sam> <outDir> <gbxml|direct> [simulate] [widths] [name=<file stem>]
            string path_Model = args[1];
            string directory = args[2];
            T3DRoute route = string.Equals(args[3], "direct", StringComparison.OrdinalIgnoreCase) ? T3DRoute.Direct : T3DRoute.GbXML;
            bool simulate = args.Contains("simulate");
            bool widths = args.Contains("widths");
            string stem = args.Select(x => x.StartsWith("name=") ? x.Substring(5) : null).FirstOrDefault(x => x != null) ?? "model";

            Directory.CreateDirectory(directory);

            AnalyticalModel model = SAM.Core.Convert.ToSAM<AnalyticalModel>(path_Model)?.FirstOrDefault();
            if (model == null)
            {
                Console.WriteLine("cannot read model " + path_Model);
                return 3;
            }

            string path_TBD = Path.Combine(directory, stem + ".tbd");
            string path_T3D = Path.Combine(directory, stem + ".t3d");
            string path_gbXML = Path.Combine(directory, stem + ".xml");

            foreach (string path in new[] { path_TBD, path_T3D, path_gbXML, Path.ChangeExtension(path_TBD, ".tsd") })
            {
                if (File.Exists(path)) File.Delete(path);
            }

            WorkflowSettings settings = new WorkflowSettings
            {
                Path_TBD = path_TBD,
                T3DRoute = route,
                Simulate = simulate,
                Sizing = true,
                AddIZAMs = true,
                UnmetHours = simulate,
                UpdateZones = true,
                RemoveExistingTBD = true,
                UseWidths = widths,
                SimulateFrom = 1,
                SimulateTo = 365
            };

            if (route == T3DRoute.GbXML)
            {
                gbXMLSerializer.gbXML gbXML = SAM.Analytical.gbXML.Convert.TogbXML(model);
                if (gbXML == null || !SAM.Core.gbXML.Create.gbXML(gbXML, path_gbXML))
                {
                    Console.WriteLine("could not write gbXML");
                    return 3;
                }

                settings.Path_gbXML = path_gbXML;
            }

            WorkflowCalculator calculator = new WorkflowCalculator(settings);
            AnalyticalModel result = calculator.Calculate(model);

            File.WriteAllLines(Path.Combine(directory, stem + ".notes.txt"), calculator.Notes);
            Console.WriteLine("route={0} result={1} notes={2}", route, result == null ? "NULL" : "ok", calculator.Notes.Count);
            foreach (string note in calculator.Notes.Take(40))
            {
                Console.WriteLine("  note: " + (note.Length > 400 ? note.Substring(0, 400) + "..." : note));
            }

            double total = 0;
            foreach (KeyValuePair<string, double> timing in calculator.Timings)
            {
                total += timing.Value;
                Console.WriteLine("  {0,-45} {1,10:F1} ms", timing.Key, timing.Value);
            }

            Console.WriteLine("  {0,-45} {1,10:F1} ms", "TOTAL", total);

            if (result != null)
            {
                SAM.Core.Convert.ToFile(result, Path.Combine(directory, stem + ".result.sam"), SAM.Core.SAMFileType.Json);
            }

            return result == null ? 5 : 0;
        }
    }
}
