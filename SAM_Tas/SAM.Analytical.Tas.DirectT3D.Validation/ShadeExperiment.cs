using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using TBD;

namespace SAM.Analytical.Tas.DirectT3D.Validation
{
    /// <summary>
    /// Does WrImportIDF.AddShadeSurface take part in the T3D -> TBD shading calculation? Answered with the one signal
    /// TAS exposes for it: the shade proportion TBD stores per exposed zone surface, per representative day, per
    /// hour (the same read Create.SolarModel makes). The same south-facing window is imported with and without a
    /// canopy above it; if the canopy is real, the window's shade proportion must rise in the hours the sun is
    /// high enough to be blocked, and not otherwise.
    /// </summary>
    public static class ShadeExperiment
    {
        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;

        public sealed class Coverage
        {
            /// <summary>The rounded hourly values on two representative days, as one string - equal strings, equal shading.</summary>
            public string Signature;
            public double SunlitHours;
            public int HoursWithSun;
        }

        public static int Run(string directory)
        {
            Directory.CreateDirectory(directory);
            Checker check = new Checker();

            SAM.Core.Location location = new SAM.Core.Location("London", -0.12, 51.5, 20);

            check.Section("shade experiment: the south window of the reference box, with and without a 2 m canopy at the wall top (London)");
            Coverage none = Measure(check, directory, "shade_none", WithLocation(SyntheticModels.BoxWithShade(shade: false), location));
            Coverage shaded = Measure(check, directory, "shade_canopy", WithLocation(SyntheticModels.BoxWithShade(shade: true, canopyDepth: 2.0, canopyZ: 3.0), location));

            // The established route over the very same models: the direct route is only as good as it is equivalent to this.
            Coverage none_gbXML = Measure(check, directory, "gbxml_shade_none", WithLocation(SyntheticModels.BoxWithShade(shade: false), location), gbXML: true);
            Coverage shaded_gbXML = Measure(check, directory, "gbxml_shade_canopy", WithLocation(SyntheticModels.BoxWithShade(shade: true, canopyDepth: 2.0, canopyZ: 3.0), location), gbXML: true);
            if (shaded != null && shaded_gbXML != null)
            {
                check.True(shaded.Signature == shaded_gbXML.Signature, "direct and gbXML routes give the same hourly shade proportions for the canopy case");
                check.Info("direct: " + shaded.Signature);
                check.Info("gbXML : " + shaded_gbXML.Signature);
            }

            // A deep canopy right at the window head: shades far more of the day than the 1 m gap above does.
            Coverage deep = Measure(check, directory, "shade_canopy_low_deep", WithLocation(SyntheticModels.BoxWithShade(shade: true, canopyDepth: 3.0, canopyZ: 2.1), location));

            // TBD's "shade proportion" is the SUNLIT fraction of the surface (1 = no shading, 0.28 = 72% shaded) and -1 where
            // there is no direct sun on it at all - night, sun behind the plane, or the surface wholly in shadow. So the
            // measure is the sunlit-hours total: the sum of the sunlit fraction over every hour with sun on the surface.
            if (none != null && shaded != null)
            {
                check.True(shaded.SunlitHours < none.SunlitHours - 10, string.Format(ci, "a canopy over the window removes direct sun ({0:F1} -> {1:F1} sunlit window-hours over the representative days)", none.SunlitHours, shaded.SunlitHours));
            }

            if (shaded != null && deep != null)
            {
                check.True(deep.SunlitHours < shaded.SunlitHours - 10, string.Format(ci, "a lower, deeper canopy removes more ({0:F1} -> {1:F1})", shaded.SunlitHours, deep.SunlitHours));
            }

            check.Write(Path.Combine(directory, "shade-results.txt"));
            Console.WriteLine("shade: passed={0} failed={1}", check.Passed, check.Failed);
            return check.Failed == 0 ? 0 : 1;
        }

        private static AnalyticalModel WithLocation(AnalyticalModel model, SAM.Core.Location location)
        {
            return new AnalyticalModel(model, location);
        }

        private static Coverage Measure(Checker check, string directory, string name, AnalyticalModel model, bool gbXML = false)
        {
            DirectRunResult result = gbXML ? GbXmlRun.Run(model, false, directory, name) : DirectRun.Run(model, new ToT3DOptions { UseWidths = false, ImportShades = true }, directory, name);
            if (result.Report != null)
            {
                check.Info(name + ": " + result.Report);
                foreach (string line in result.Report.Skipped) check.Info("skipped: " + line);
            }

            check.True(result.Converted && result.Exported, name + ": converted and exported");
            if (!result.Exported) return null;

            Coverage coverage = ReadPaneCoverage(result.Path_TBD, check);
            if (coverage != null)
            {
                check.Info(string.Format(ci, "{0}: pane hours with direct sun={1}, sunlit window-hours={2:F1}", name, coverage.HoursWithSun, coverage.SunlitHours));
            }

            return coverage;
        }

        // The 24 hourly values of the pane's shade proportion on one representative day, as text.
        private static string Day(TBD.Building b, zone z, zoneSurface zs, int day)
        {
            dynamic shadeProp = b.GetShadeProportion(z.number, zs.number, day);
            if (shadeProp == null) return "(null)";
            List<string> values = new List<string>();
            foreach (float value in shadeProp) values.Add(value.ToString("F2", ci));
            return string.Join(" ", values);
        }

        private static Coverage ReadPaneCoverage(string path, Checker check)
        {
            TBDDocument doc = new TBDDocument();
            try
            {
                // Read-write, as FromTBD reads it, so what is measured is what the import would see.
                if (doc.open(path) == 0 && doc.openReadOnly(path) == 0)
                {
                    check.True(false, "cannot open " + path);
                    return null;
                }

                TBD.Building b = doc.Building;
                List<double> values = new List<double>();

                for (int i = 0; ; i++)
                {
                    zone z = b.GetZone(i);
                    if (z == null) break;
                    for (int j = 0; ; j++)
                    {
                        zoneSurface zs = z.GetSurface(j);
                        if (zs == null) break;
                        string beName = zs.buildingElement?.name ?? string.Empty;
                        if (!beName.EndsWith("-pane")) continue;

                        for (int day = 1; day <= 365; day += 15)
                        {
                            dynamic shadeProp = b.GetShadeProportion(z.number, zs.number, day);
                            if (shadeProp == null) continue;
                            foreach (float value in shadeProp)
                            {
                                if (value >= 0f) values.Add(value);
                            }
                        }
                    }
                }

                if (values.Count == 0)
                {
                    check.True(false, "no shade proportions could be read for the pane");
                    return null;
                }

                string signature = string.Empty;
                for (int j = 0; ; j++)
                {
                    zone z1 = b.GetZone(0);
                    zoneSurface zs1 = z1?.GetSurface(j);
                    if (zs1 == null) break;
                    if (!(zs1.buildingElement?.name ?? string.Empty).EndsWith("-pane")) continue;
                    signature = Day(b, z1, zs1, 166) + " / " + Day(b, z1, zs1, 346);
                    break;
                }

                return new Coverage { Signature = signature, SunlitHours = values.Sum(), HoursWithSun = values.Count };
            }
            finally
            {
                try { doc.close(); } catch { }
                Marshal.FinalReleaseComObject(doc);
            }
        }
    }
}
