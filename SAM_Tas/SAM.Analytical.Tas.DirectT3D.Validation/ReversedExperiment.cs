using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SAM.Analytical.Tas.DirectT3D.Validation
{
    /// <summary>
    /// Which side of an internal surface is "reversed" (its construction's layers run the other way round for that zone)
    /// matters whenever the layers are not symmetric - a paint film on one face only. SAM's own TBD export fixes a convention
    /// (the panel's first space is not reversed, the second is); the gbXML route follows it. What does the importer do with
    /// reverseElement false and true, over the cases where the answer is not obvious?
    /// </summary>
    public static class ReversedExperiment
    {
        /// <summary>The real model: for every internal panel, the flags TAS gave each side with reverseElement false, true.</summary>
        public static int RunReal(string path_Model, string directory)
        {
            Directory.CreateDirectory(directory);
            AnalyticalModel model = SAM.Core.Convert.ToSAM<AnalyticalModel>(path_Model).First();

            Dictionary<bool, TbdSnapshot> snapshots = new Dictionary<bool, TbdSnapshot>();
            T3DImportPlan plan_Reference = null;
            foreach (bool reverse in new[] { false, true })
            {
                T3DImportPlan plan = model.T3DImportPlan(new ToT3DOptions { UseWidths = false });
                foreach (T3DSurfaceSpec surface in plan.Surfaces.Where(x => x.Kind == T3DSurfaceKind.Internal)) surface.ReverseElement = reverse;
                snapshots[reverse] = RunPlan(plan, directory, "revreal_" + reverse).Tbd;
                plan_Reference = plan;
            }

            Func<TbdSnapshot, string, double, double, double, string> flags = (tbd, zoneName, area, orient, dummy) =>
                string.Join(",", tbd.Zones.First(z => z.Name == zoneName).Surfaces.Where(s => s.Type == "tbdLink" && Math.Abs(s.Area - area) < 0.01 && Math.Abs(s.Orientation - orient) < 1).Select(s => s.Reversed.ToString()).OrderBy(x => x));

            Console.WriteLine("panel (A->B, area, azimuth of normal): flags A / B with reverseElement=false  |  true");
            foreach (T3DSurfaceSpec surface in plan_Reference.Surfaces.Where(x => x.Kind == T3DSurfaceKind.Internal))
            {
                List<Geometry.Spatial.Point3D> points = new List<Geometry.Spatial.Point3D>();
                for (int i = 0; i < surface.Coordinates.GetLength(1); i++) points.Add(new Geometry.Spatial.Point3D(surface.Coordinates[0, i], surface.Coordinates[1, i], surface.Coordinates[2, i]));
                Geometry.Spatial.Vector3D n = points.NewellNormal();
                double azimuth = (Math.Atan2(n.X, n.Y) * 180 / Math.PI + 360) % 360;
                string a = plan_Reference.Zones[surface.Zone].Name;
                string b = plan_Reference.Zones[surface.Zone2].Name;
                Console.WriteLine("{0,-12} -> {1,-12} {2,5:F0} {3,4:F0}:  A[{4}] B[{5}]  |  A[{6}] B[{7}]", a, b, n.Length / 2, azimuth,
                    flags(snapshots[false], a, n.Length / 2, azimuth, 0), flags(snapshots[false], b, n.Length / 2, (azimuth + 180) % 360, 0),
                    flags(snapshots[true], a, n.Length / 2, azimuth, 0), flags(snapshots[true], b, n.Length / 2, (azimuth + 180) % 360, 0));
            }

            return 0;
        }

        public static int Run(string directory)
        {
            Directory.CreateDirectory(directory);
            Checker check = new Checker();

            foreach (Tuple<string, Func<AnalyticalModel>> testCase in new[]
            {
                Tuple.Create<string, Func<AnalyticalModel>>("two_zones (partition, A first)", () => SyntheticModels.TwoZones()),
                Tuple.Create<string, Func<AnalyticalModel>>("stacked, lower first", () => SyntheticModels.StackedZones(false)),
                Tuple.Create<string, Func<AnalyticalModel>>("stacked, upper first", () => SyntheticModels.StackedZones(true)),
            })
            {
                check.Section(testCase.Item1);

                DirectRunResult gbXML = GbXmlRun.Run(testCase.Item2(), false, directory, "rev_gbxml");
                check.Info("gbXML route:   " + Describe(gbXML));

                foreach (bool reverse in new[] { false, true })
                {
                    AnalyticalModel model = testCase.Item2();
                    ToT3DOptions options = new ToT3DOptions { UseWidths = false };
                    T3DImportPlan plan = model.T3DImportPlan(options);
                    foreach (T3DSurfaceSpec surface in plan.Surfaces.Where(x => x.Kind == T3DSurfaceKind.Internal)) surface.ReverseElement = reverse;

                    DirectRunResult direct = RunPlan(plan, directory, "rev_direct_" + reverse);
                    check.Info(string.Format("direct, reverseElement={0}: {1}", reverse, Describe(direct)));
                }
            }

            check.Write(Path.Combine(directory, "reversed-results.txt"));
            return 0;
        }

        private static DirectRunResult RunPlan(T3DImportPlan plan, string directory, string name)
        {
            DirectRunResult result = new DirectRunResult { Path_T3D = Path.Combine(directory, name + ".t3d"), Path_TBD = Path.Combine(directory, name + ".tbd") };
            if (File.Exists(result.Path_TBD)) File.Delete(result.Path_TBD);
            using (SAM.Core.Tas.SAMT3DDocument sAMT3DDocument = new SAM.Core.Tas.SAMT3DDocument())
            {
                result.Converted = plan.ToT3D(sAMT3DDocument.T3DDocument, new ToT3DOptions { UseWidths = false });
                result.Exported = result.Converted && Convert.ToTBD(sAMT3DDocument.T3DDocument, result.Path_TBD, 1, 365, 15, true, false);
            }

            if (result.Exported) result.Tbd = TbdSnapshot.Read(result.Path_TBD);
            return result;
        }

        // Per linked surface: zone, orientation/inclination and reversed, ordered by zone name.
        private static string Describe(DirectRunResult result)
        {
            if (result.Tbd == null) return "(no TBD)";
            return string.Join("  ", result.Tbd.Zones.OrderBy(z => z.Name).SelectMany(z => z.Surfaces.Where(s => s.Type == "tbdLink").Select(s => string.Format("{0}[incl {1:F0} orient {2:F0}] reversed={3}", z.Name, s.Inclination, s.Orientation, s.Reversed))));
        }
    }
}
