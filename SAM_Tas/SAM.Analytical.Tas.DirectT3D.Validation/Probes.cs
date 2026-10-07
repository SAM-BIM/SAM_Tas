// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.IO;
using System.Runtime.InteropServices;
using TAS3D;

namespace SAM.Analytical.Tas.DirectT3D.Validation
{
    using Zone = TAS3D.Zone;
    using Building = TAS3D.Building;

    /// <summary>Throw-away experiments that established how the TAS importer behaves; kept as evidence.</summary>
    public static class Probes
    {
        /// <summary>
        /// How TAS groups openings into TBD surfaces and names elements: three openings on one host with one window
        /// type, and two window types that share a name but differ in frame percentage.
        /// </summary>
        public static int Windows(string[] args)
        {
            string dir = args[1];
            Directory.CreateDirectory(dir);
            string tbd = Path.Combine(dir, "probe_windows2.tbd");
            if (File.Exists(tbd)) File.Delete(tbd);

            T3DDocument doc = new T3DDocument();
            try
            {
                doc.Create();
                Building b = doc.Building;
                Element wall = b.AddElement("WALL", 0x0080FF, 0.3);
                Element roof = b.AddElement("ROOF", 0x00FF00, 0.35);
                Element gf = b.AddElement("GROUND_FLOOR", 0x808080, 0.4); gf.ground = true;
                window wA = b.AddWindow("Windows: GLZ ", 0, 0x0000FF, 1.0, 1.0, 1.0); wA.isPercFrame = true; wA.framePerc = 10;
                window wB = b.AddWindow("Windows: GLZ ", 0, 0x0000FF, 1.0, 1.0, 1.0); wB.isPercFrame = true; wB.framePerc = 20;
                window wC = b.AddWindow("Windows: GLZ ", 0, 0x0000FF, 1.0, 1.0, 1.0); wC.isPercFrame = true; wC.framePerc = 10;
                Zone z = b.AddZoneSet("SAM", "", 0).AddZone();
                z.name = "Box";
                WrImportIDF imp = (WrImportIDF)doc.CreateIDFImport();
                imp.SetUseBEWidths(false);
                double W = 10, D = 4, H = 3;
                imp.AddSurface(z, gf, false, false, C(P(0, 0, 0), P(0, D, 0), P(W, D, 0), P(W, 0, 0)));
                imp.AddSurface(z, roof, false, false, C(P(0, 0, H), P(W, 0, H), P(W, D, H), P(0, D, H)));
                // south wall with: A, A (same window object twice), B, C (distinct object, same name+frame as A)
                imp.AddSurface(z, wall, false, false, C(P(0, 0, 0), P(W, 0, 0), P(W, 0, H), P(0, 0, H)));
                imp.AddOpening(wA, C(P(1, 0, 1), P(2, 0, 1), P(2, 0, 2), P(1, 0, 2)));
                imp.AddOpening(wA, C(P(3, 0, 1), P(4, 0, 1), P(4, 0, 2), P(3, 0, 2)));
                imp.AddOpening(wB, C(P(5, 0, 1), P(6, 0, 1), P(6, 0, 2), P(5, 0, 2)));
                imp.AddOpening(wC, C(P(7, 0, 1), P(8, 0, 1), P(8, 0, 2), P(7, 0, 2)));
                imp.AddSurface(z, wall, false, false, C(P(W, 0, 0), P(W, D, 0), P(W, D, H), P(W, 0, H)));
                imp.AddSurface(z, wall, false, false, C(P(W, D, 0), P(0, D, 0), P(0, D, H), P(W, D, H)));
                imp.AddSurface(z, wall, false, false, C(P(0, D, 0), P(0, 0, 0), P(0, 0, H), P(0, D, H)));
                Console.WriteLine("CreateImportedModel=" + imp.CreateImportedModel());
                doc.SetUseBEWidths(false);
                Console.WriteLine("ExportNew=" + doc.ExportNew(1, 365, 15, 1, 1, 1, tbd, 1, 0, 0));
                Console.WriteLine("window pane/frame GUIDs: A " + wA.paneGUID + "/" + wA.frameGUID + "  B " + wB.paneGUID + "/" + wB.frameGUID + "  C " + wC.paneGUID + "/" + wC.frameGUID);
                Marshal.ReleaseComObject(imp);
            }
            finally { try { doc.Close(); } catch { } Marshal.FinalReleaseComObject(doc); }

            Console.Write(TbdSnapshot.Read(tbd).ToText());
            return 0;
        }

        static object C(params double[][] pts)
        {
            double[,] t = new double[3, pts.Length];
            for (int i = 0; i < pts.Length; i++) for (int j = 0; j < 3; j++) t[j, i] = pts[i][j];
            return t;
        }

        static double[] P(double x, double y, double z) { return new[] { x, y, z }; }

        public static int Run(string[] args)
        {
            string dir = args[1];
            Directory.CreateDirectory(dir);
            string t3d = Path.Combine(dir, "probe_windows.t3d");
            string tbd = Path.Combine(dir, "probe_windows.tbd");
            if (File.Exists(tbd)) File.Delete(tbd);

            T3DDocument doc = new T3DDocument();
            try
            {
                doc.Create();
                Building b = doc.Building;
                Element wall = b.AddElement("WALL", 0x0080FF, 0.3);
                Element roof = b.AddElement("ROOF", 0x00FF00, 0.35);
                Element gf = b.AddElement("GROUND_FLOOR", 0x808080, 0.4); gf.ground = true;
                window[] wins = new window[5];
                for (int t = 0; t < 5; t++)
                {
                    wins[t] = b.AddWindow("WIN_T" + t, t < 3 ? t : 0, 0x0000FF, 1.0, 1.0, 1.0);
                    if (t >= 3) { try { wins[t].positionType = t; } catch (Exception e) { Console.WriteLine("set positionType " + t + " failed: " + e.Message); } }
                    Console.WriteLine("window type arg " + t + " -> positionType=" + wins[t].positionType + " framePerc=" + wins[t].framePerc + " isPercFrame=" + wins[t].isPercFrame + " frameWidth=" + wins[t].frameWidth + " transparent=" + wins[t].transparent + " internalShadows=" + wins[t].internalShadows);
                }
                Zone z = b.AddZoneSet("SAM", "", 0).AddZone();
                z.name = "Box"; z.description = "{AAAAAAAA-0000-0000-0000-000000000001}";
                Console.WriteLine("zone sets: " + b.GetZoneSet(0)?.name + "/" + b.GetZoneSet(0)?.numOfZones);

                WrImportIDF imp = (WrImportIDF)doc.CreateIDFImport();
                double W = 5, D = 4, H = 3;
                imp.AddSurface(z, gf, false, false, C(P(0, 0, 0), P(0, D, 0), P(W, D, 0), P(W, 0, 0)));
                imp.AddSurface(z, roof, false, false, C(P(0, 0, H), P(W, 0, H), P(W, D, H), P(0, D, H)));
                imp.AddSurface(z, wall, false, false, C(P(0, 0, 0), P(W, 0, 0), P(W, 0, H), P(0, 0, H)));
                imp.AddOpening(wins[0], C(P(1, 0, 1), P(2, 0, 1), P(2, 0, 2), P(1, 0, 2)));
                imp.AddOpening(wins[2], C(P(3, 0, 0), P(4, 0, 0), P(4, 0, 2), P(3, 0, 2)));
                imp.AddSurface(z, wall, false, false, C(P(W, 0, 0), P(W, D, 0), P(W, D, H), P(W, 0, H)));
                imp.AddSurface(z, wall, false, false, C(P(W, D, 0), P(0, D, 0), P(0, D, H), P(W, D, H)));
                imp.AddSurface(z, wall, false, false, C(P(0, D, 0), P(0, 0, 0), P(0, 0, H), P(0, D, H)));
                Console.WriteLine("CreateImportedModel=" + imp.CreateImportedModel());
                Console.WriteLine("zone after import: GUID=" + z.GUID + " desc=" + z.description + " floor=" + z.floorArea + " vol=" + z.volume);
                for (int t = 0; t < 5; t++)
                    Console.WriteLine("after import window " + t + " positionType=" + wins[t].positionType);
                Console.WriteLine("Save=" + doc.Save(t3d));
                Console.WriteLine("ExportNew=" + doc.ExportNew(1, 365, 15, 1, 1, 1, tbd, 1, 0, 0));
                Marshal.ReleaseComObject(imp);
            }
            finally { try { doc.Close(); } catch { } Marshal.FinalReleaseComObject(doc); }

            Console.Write(TbdSnapshot.Read(tbd).ToText());
            return 0;
        }
    }
}
