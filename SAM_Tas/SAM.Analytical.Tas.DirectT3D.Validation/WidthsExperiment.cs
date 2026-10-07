// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SAM.Analytical.Tas.DirectT3D.Validation
{
    /// <summary>
    /// Are SAM panel polygons centre lines (so TAS's building element widths should offset them) or the boundary of the
    /// room (so they should be taken as they are)? SAM has its own answer - the volume of the shell the panels make - and
    /// TAS reproduces it only with widths OFF. Run on the real model, per zone, with widths ON and OFF.
    /// </summary>
    public static class WidthsExperiment
    {
        public static int Run(string[] args)
        {
            // widths <model.sam> <outDir>
            AnalyticalModel model = SAM.Core.Convert.ToSAM<AnalyticalModel>(args[1])?.FirstOrDefault();
            string directory = args[2];
            CultureInfo ci = CultureInfo.InvariantCulture;
            Checker check = new Checker();

            Dictionary<string, double> volumes_SAM = new Dictionary<string, double>();
            foreach (Space space in model.AdjacencyCluster.GetSpaces())
            {
                Geometry.Spatial.Shell shell = model.AdjacencyCluster.Shell(space);
                volumes_SAM[space.Name] = shell == null ? double.NaN : SAM.Geometry.Spatial.Query.Volume(shell);
            }

            check.Section("widths OFF vs ON on the real model, against the volume SAM's own shell gives each space");
            DirectRunResult off = DirectRun.Run(model, new ToT3DOptions { UseWidths = false }, directory, "widths_off");
            DirectRunResult on = DirectRun.Run(model, new ToT3DOptions { UseWidths = true }, directory, "widths_on");
            if (off.Tbd == null || on.Tbd == null)
            {
                check.True(false, "both runs exported");
                check.Write(Path.Combine(directory, "widths-results.txt"));
                return 1;
            }

            check.Info(string.Format(ci, "{0,-14} {1,10} {2,10} {3,10} | {4,10} {5,10}", "zone", "SAM vol", "OFF vol", "ON vol", "OFF floor", "ON floor"));
            foreach (TbdSnapshot.Zn zOff in off.Tbd.Zones)
            {
                TbdSnapshot.Zn zOn = on.Tbd.Zones.FirstOrDefault(x => x.Name == zOff.Name);
                volumes_SAM.TryGetValue(zOff.Name, out double volumeSam);
                check.Info(string.Format(ci, "{0,-14} {1,10:F3} {2,10:F3} {3,10:F3} | {4,10:F3} {5,10:F3}", zOff.Name, volumeSam, zOff.Volume, zOn?.Volume ?? double.NaN, zOff.FloorArea, zOn?.FloorArea ?? double.NaN));
                if (!double.IsNaN(volumeSam))
                {
                    check.Near(zOff.Volume, volumeSam, 0.5, zOff.Name + ": widths OFF reproduces SAM's shell volume");
                }
            }

            check.Info(string.Format(ci, "TOTAL volume: OFF {0:F3}  ON {1:F3}  SAM {2:F3}", off.Tbd.TotalVolume, on.Tbd.TotalVolume, volumes_SAM.Values.Where(x => !double.IsNaN(x)).Sum()));
            check.Info(string.Format(ci, "TOTAL floor  : OFF {0:F3}  ON {1:F3}", off.Tbd.TotalFloorArea, on.Tbd.TotalFloorArea));
            check.True(on.Tbd.TotalVolume < off.Tbd.TotalVolume - 1, "widths ON shrinks the volume (TAS treats the polygons as centre lines and offsets them)");

            check.Write(Path.Combine(directory, "widths-results.txt"));
            Console.WriteLine("widths: passed={0} failed={1}", check.Passed, check.Failed);
            return check.Failed == 0 ? 0 : 1;
        }
    }
}
