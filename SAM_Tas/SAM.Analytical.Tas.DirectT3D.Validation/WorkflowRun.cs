// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

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
            // workflow <model.sam|loadsensitive[-reversed][-shuffled|-rotated]> <outDir> <gbxml|direct> [simulate] [widths] [name=<file stem>] [weather=<model.sam or .twd> [weathername=<text>]]
            string path_Model = args[1];
            string directory = args[2];
            T3DRoute route = string.Equals(args[3], "direct", StringComparison.OrdinalIgnoreCase) ? T3DRoute.Direct : T3DRoute.GbXML;
            bool simulate = args.Contains("simulate");
            bool widths = args.Contains("widths");
            string stem = args.Select(x => x.StartsWith("name=") ? x.Substring(5) : null).FirstOrDefault(x => x != null) ?? "model";

            Directory.CreateDirectory(directory);

            // "loadsensitive" is the synthetic three-zone fixture (LoadSensitiveModel); anything else is a .sam file.
            bool synthetic = path_Model.StartsWith("loadsensitive", StringComparison.OrdinalIgnoreCase);
            AnalyticalModel model = synthetic ? LoadSensitiveModel.Create(path_Model.IndexOf("-reversed", StringComparison.OrdinalIgnoreCase) >= 0, path_Model.IndexOf("-shuffled", StringComparison.OrdinalIgnoreCase) >= 0 ? PanelOrder.Reversed : path_Model.IndexOf("-rotated", StringComparison.OrdinalIgnoreCase) >= 0 ? PanelOrder.Rotated : PanelOrder.AsListed) : SAM.Core.Convert.ToSAM<AnalyticalModel>(path_Model)?.FirstOrDefault();
            if (model == null)
            {
                Console.WriteLine("cannot read model " + path_Model);
                return 3;
            }

            // The synthetic model carries no weather of its own. Take it from a real model, so the run has the same sun and
            // temperatures the real-model comparison had - and stamp it on the model, which is where the workflow looks.
            string path_Weather = args.Select(x => x.StartsWith("weather=") ? x.Substring(8) : null).FirstOrDefault(x => x != null);
            string weatherName = args.Select(x => x.StartsWith("weathername=") ? x.Substring(12) : null).FirstOrDefault(x => x != null);
            SAM.Weather.WeatherData weatherData = null;
            if (path_Weather != null)
            {
                if (path_Weather.EndsWith(".twd", StringComparison.OrdinalIgnoreCase))
                {
                    // A TAS weather library: the first year whose name contains weathername= (or simply the first).
                    List<SAM.Weather.WeatherData> weatherDatas = SAM.Weather.Tas.Convert.ToSAM_WeatherDatas(path_Weather);
                    weatherData = weatherName == null ? weatherDatas?.FirstOrDefault() : weatherDatas?.FirstOrDefault(x => x?.Name != null && x.Name.IndexOf(weatherName, StringComparison.OrdinalIgnoreCase) >= 0);
                }
                else
                {
                    AnalyticalModel weatherModel = SAM.Core.Convert.ToSAM<AnalyticalModel>(path_Weather)?.FirstOrDefault();
                    weatherModel?.TryGetValue(Analytical.AnalyticalModelParameter.WeatherData, out weatherData);
                }

                if (weatherData == null)
                {
                    Console.WriteLine("no weather data in " + path_Weather);
                    return 3;
                }

                model.SetValue(Analytical.AnalyticalModelParameter.WeatherData, weatherData);
                Console.WriteLine("weather: " + weatherData.Name);
            }

            string path_TBD = Path.Combine(directory, stem + ".tbd");
            string path_T3D = Path.Combine(directory, stem + ".t3d");
            string path_gbXML = Path.Combine(directory, stem + ".xml");

            // The exact model this run converted, so the comparison can be reproduced from the folder alone.
            SAM.Core.Convert.ToFile(model, Path.Combine(directory, stem + ".input.sam"), SAM.Core.SAMFileType.Json);

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
                SimulateTo = 365,

                // Weather the caller supplies is what the workflow derives its heating and cooling design days from (and so what sizes
                // the plant). A model that only carries weather as a parameter gets no design days, no sizing, and free-running zones.
                WeatherData = weatherData
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
                SAM.Core.Convert.ToFile(result, Path.Combine(directory, stem + ".result.json"), SAM.Core.SAMFileType.Json);
            }

            return result == null ? 5 : 0;
        }
    }
}
