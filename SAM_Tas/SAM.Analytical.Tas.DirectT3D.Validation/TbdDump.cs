using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using TBD;

namespace SAM.Analytical.Tas.DirectT3D.Validation
{
    /// <summary>
    /// A COM-free snapshot of a TBD, read independently of the code under test (through TAS's own 0-based
    /// Get*(index) accessors, as the licensed-harness notes recommend) so a check never relies on the helpers
    /// it is verifying.
    /// </summary>
    public sealed class TbdSnapshot
    {
        public sealed class Be { public string Name; public int BEType; public double Width; public bool Ground; public string Guid; public string Construction; public int Surfaces; }
        public sealed class Surf { public string Zone; public int Number; public string Type; public double Area; public double Orientation; public double Inclination; public string Be; public bool Linked; public int BEType; }
        public sealed class Zn { public string Name; public string Description; public string Guid; public double FloorArea; public double Volume; public bool External; public List<Surf> Surfaces = new List<Surf>(); }

        public List<Be> BuildingElements = new List<Be>();
        public List<Zn> Zones = new List<Zn>();
        public int ApertureTypes;
        public int Constructions;

        public double TotalFloorArea { get { double d = 0; foreach (Zn z in Zones) d += z.FloorArea; return d; } }
        public double TotalVolume { get { double d = 0; foreach (Zn z in Zones) d += z.Volume; return d; } }

        public IEnumerable<Surf> AllSurfaces()
        {
            foreach (Zn z in Zones)
                foreach (Surf f in z.Surfaces)
                    yield return f;
        }

        public static TbdSnapshot Read(string path)
        {
            TbdSnapshot s = new TbdSnapshot();
            TBDDocument doc = new TBDDocument();
            try
            {
                if (doc.openReadOnly(path) == 0)
                    throw new IOException("cannot open " + path);

                TBD.Building b = doc.Building;
                for (int i = 0; ; i++)
                {
                    buildingElement be = b.GetBuildingElement(i);
                    if (be == null) break;
                    Be e = new Be { Name = be.name, BEType = be.BEType, Width = be.width, Ground = be.ground != 0, Guid = be.GUID };
                    try { e.Construction = be.GetConstruction()?.name; } catch { }
                    s.BuildingElements.Add(e);
                }
                for (int i = 0; b.GetConstruction(i) != null; i++) s.Constructions++;
                for (int i = 0; b.GetApertureType(i) != null; i++) s.ApertureTypes++;

                for (int i = 0; ; i++)
                {
                    zone z = b.GetZone(i);
                    if (z == null) break;
                    Zn zn = new Zn { Name = z.name, Description = z.description, Guid = z.GUID, FloorArea = z.floorArea, Volume = z.volume, External = z.external != 0 };
                    for (int j = 0; ; j++)
                    {
                        zoneSurface zs = z.GetSurface(j);
                        if (zs == null) break;
                        buildingElement be = zs.buildingElement;
                        zn.Surfaces.Add(new Surf
                        {
                            Zone = zn.Name, Number = zs.number, Type = zs.type.ToString(), Area = zs.area, Orientation = zs.orientation, Inclination = zs.inclination,
                            Be = be?.name ?? string.Empty, BEType = be == null ? -1 : be.BEType, Linked = zs.linkSurface != null
                        });
                    }
                    s.Zones.Add(zn);
                }

                foreach (Surf surf in s.AllSurfaces())
                {
                    Be be = s.BuildingElements.Find(x => x.Name == surf.Be);
                    if (be != null) be.Surfaces++;
                }
            }
            finally
            {
                try { doc.close(); } catch { }
                Marshal.FinalReleaseComObject(doc);
            }
            return s;
        }

        public string ToText()
        {
            CultureInfo ci = CultureInfo.InvariantCulture;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(string.Format(ci, "TBD zones={0} floorArea={1:F3} volume={2:F3} BEs={3} constructions={4} apertureTypes={5}", Zones.Count, TotalFloorArea, TotalVolume, BuildingElements.Count, Constructions, ApertureTypes));
            foreach (Be e in BuildingElements)
                sb.AppendLine(string.Format(ci, "BE '{0}' BEType={1} width={2:F3} ground={3} surfaces={4} construction='{5}'", e.Name, e.BEType, e.Width, e.Ground, e.Surfaces, e.Construction));
            foreach (Zn z in Zones)
            {
                sb.AppendLine(string.Format(ci, "ZONE '{0}' desc='{1}' floor={2:F3} vol={3:F3} external={4} surfaces={5}", z.Name, z.Description, z.FloorArea, z.Volume, z.External, z.Surfaces.Count));
                foreach (Surf f in z.Surfaces)
                    sb.AppendLine(string.Format(ci, "   #{0} {1} area={2:F3} orient={3:F1} incl={4:F1} BE='{5}' linked={6}", f.Number, f.Type, f.Area, f.Orientation, f.Inclination, f.Be, f.Linked));
            }
            return sb.ToString();
        }
    }
}
