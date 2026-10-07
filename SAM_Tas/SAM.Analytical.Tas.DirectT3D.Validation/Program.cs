// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;

namespace SAM.Analytical.Tas.DirectT3D.Validation
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            string mode = args.Length > 0 ? args[0] : string.Empty;
            try
            {
                switch (mode)
                {
                    case "dump":
                        Console.Write(TbdSnapshot.Read(args[1]).ToText());
                        return 0;

                    case "volumes":
                        return VolumeExperiment.Run(args);

                    case "probe":
                        return Probes.Run(args);

                    case "probe-windows":
                        return Probes.Windows(args);

                    case "synthetic":
                        return SyntheticValidation.Run(args[1]);

                    case "scale":
                        return ScaleExperiment.Run(args);

                    case "reversed-real":
                        return ReversedExperiment.RunReal(args[1], args[2]);

                    case "reversed":
                        return ReversedExperiment.Run(args[1]);

                    case "deep":
                        return TbdDeepCompare.Run(args);

                    case "t3d":
                        return T3dDump.Run(args);

                    case "models":
                        return ModelCompare.Run(args);

                    case "widths":
                        return WidthsExperiment.Run(args);

                    case "gating":
                        return GatingExperiment.Run(args);

                    case "shade":
                        return ShadeExperiment.Run(args[1]);

                    case "compare":
                        return TbdCompare.Run(args);

                    case "inspect":
                        return Inspect.Run(args);

                    case "workflow":
                        return WorkflowRun.Run(args);

                    case "weather-list":
                        foreach (SAM.Weather.WeatherData weatherData in SAM.Weather.Tas.Convert.ToSAM_WeatherDatas(args[1]) ?? new List<SAM.Weather.WeatherData>())
                        {
                            Console.WriteLine(weatherData?.Name);
                        }

                        return 0;

                    case "inputs":
                        return LoadInputs.Run(args);

                    case "tsd":
                        return TsdCompare.Run(args);

                    case "tsd-hours":
                        return TsdCompare.RunHours(args);

                    case "tsd-order":
                        return TsdCompare.RunWithOrderNoise(args);

                    default:
                        Console.WriteLine("modes: dump <tbd> | probe <outDir> | synthetic <outDir> | inspect <model.sam> [out.txt] | scale <outDir> <nx> <ny> [gbxml] | reversed <outDir> | deep <a.tbd> <b.tbd> [out.txt] | t3d <file.t3d> [out.txt] | models <gbxml.result.json> <direct.result.json> <outPrefix> | widths <model.sam> <outDir> | gating <model.sam> <outDir> [gbxml.tbd] | shade <outDir> | compare <gbxml.tbd> <direct.tbd> <outPrefix> | workflow <model.sam|loadsensitive[-reversed][-shuffled|-rotated]> <outDir> <gbxml|direct> [simulate] [widths] [name=<stem>] [weather=<model.sam>] | inputs <gbxml.tbd> <direct.tbd> [outPrefix] | tsd <gbxml.tsd> <direct.tsd> [outPrefix] | tsd-order <outPrefix> <gbxml.tsd> <direct.tsd> [<gbxml-reordered.tsd> <direct-reordered.tsd>]... | volumes <model.sam> <outDir> <label>=<file.t3d>...");
                        return 2;
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("ERROR: " + exception);
                return 1;
            }
        }
    }
}
