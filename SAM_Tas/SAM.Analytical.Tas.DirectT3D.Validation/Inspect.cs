// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Geometry.Spatial;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SAM.Analytical.Tas.DirectT3D.Validation
{
    /// <summary>Describes a .sam model and the direct-route plan made from it - COM-free, no TAS needed.</summary>
    public static class Inspect
    {
        public static int Run(string[] args)
        {
            AnalyticalModel model = SAM.Core.Convert.ToSAM<AnalyticalModel>(args[1])?.FirstOrDefault();
            if (model == null)
            {
                Console.WriteLine("cannot read model");
                return 3;
            }

            CultureInfo ci = CultureInfo.InvariantCulture;
            StringBuilder sb = new StringBuilder();
            AdjacencyCluster cluster = model.AdjacencyCluster;

            List<Space> spaces = cluster.GetSpaces() ?? new List<Space>();
            List<Panel> panels = cluster.GetPanels() ?? new List<Panel>();
            List<Aperture> apertures = cluster.GetApertures() ?? new List<Aperture>();
            sb.AppendLine(string.Format(ci, "model '{0}' spaces={1} externalSpaces={2} panels={3} apertures={4} constructions={5} apertureConstructions={6}",
                model.Name, spaces.Count, (cluster.GetObjects<ExternalSpace>() ?? new List<ExternalSpace>()).Count, panels.Count, apertures.Count, (cluster.GetConstructions() ?? new List<Construction>()).Count, (cluster.GetApertureConstructions() ?? new List<ApertureConstruction>()).Count));

            sb.AppendLine("panel types: " + string.Join(", ", panels.GroupBy(x => x.PanelType).Select(x => x.Key + "=" + x.Count())));
            foreach (Construction c in cluster.GetConstructions() ?? new List<Construction>())
                sb.AppendLine(string.Format(ci, "construction '{0}' thickness={1:F3} adiabatic={2} layers=[{3}]", c.Name, c.GetThickness(), Analytical.Query.Adiabatic(c), string.Join(" | ", (c.ConstructionLayers ?? new List<ConstructionLayer>()).Select(l => l.Name + " " + l.Thickness.ToString("F3", ci)))));
            foreach (ApertureConstruction ac in cluster.GetApertureConstructions() ?? new List<ApertureConstruction>())
                sb.AppendLine(string.Format(ci, "apertureConstruction '{0}' type={1} paneLayers={2} frameLayers={3}", ac.Name, ac.ApertureType, ac.PaneConstructionLayers?.Count, ac.FrameConstructionLayers?.Count));

            foreach (Panel panel in panels)
            {
                List<Space> ps = cluster.GetSpaces(panel) ?? new List<Space>();
                Face3D f = panel.GetFace3D(false);
                int holes = f?.GetInternalEdge3Ds()?.Count ?? 0;
                int points = (f?.GetExternalEdge3D() as ISegmentable3D)?.GetPoints()?.Count ?? 0;
                sb.AppendLine(string.Format(ci, "panel '{0}' type={1} constr='{2}' area={3:F3} spaces=[{4}] adiabatic={5} points={6} holes={7} apertures={8}",
                    panel.Name, panel.PanelType, panel.Construction?.Name, panel.GetArea(), string.Join("|", ps.Select(x => x.Name)), Analytical.Query.Adiabatic(panel), points, holes, panel.Apertures?.Count ?? 0));
                foreach (Aperture a in panel.Apertures ?? new List<Aperture>())
                {
                    Face3D af = a.GetFace3D();
                    int ah = af?.GetInternalEdge3Ds()?.Count ?? 0;
                    List<Point3D> pts = (af?.GetExternalEdge3D() as ISegmentable3D)?.GetPoints();
                    BoundingBox3D bb = af?.GetBoundingBox();
                    sb.AppendLine(string.Format(ci, "    aperture '{0}' {1} ac='{2}' area={3:F3} points={4} holes={5} frameFactor={6:F3} bbox=({7:F2},{8:F2},{9:F2})-({10:F2},{11:F2},{12:F2})",
                        a.Name, a.Guid, a.ApertureConstruction?.Name, a.GetArea(), pts?.Count, ah, a.GetFrameFactor(), bb?.Min.X, bb?.Min.Y, bb?.Min.Z, bb?.Max.X, bb?.Max.Y, bb?.Max.Z));
                }
            }

            T3DImportPlan plan = model.T3DImportPlan(new ToT3DOptions());
            sb.AppendLine();
            sb.AppendLine("PLAN " + plan.Report);
            foreach (string line in plan.Report.Skipped) sb.AppendLine("  skipped: " + line);
            foreach (string line in plan.Report.Notes) sb.AppendLine("  note: " + line);
            foreach (T3DElementSpec e in plan.Elements)
                sb.AppendLine(string.Format(ci, "  element '{0}' width={1:F3} BEType={2} ground={3} ghost={4} transparent={5} key={6}", e.Name, e.Width, e.BEType, e.Ground, e.Ghost, e.Transparent, e.Key));
            foreach (T3DWindowSpec w in plan.Windows)
                sb.AppendLine(string.Format(ci, "  window '{0}' openingType={1} positionType={2} framePct={3:F1} frameWidth={4} transparent={5} internalShadows={6} key={7}", w.Name, w.OpeningType, w.PositionType, w.FramePercent, w.FrameWidth, w.Transparent, w.InternalShadows, w.Key));
            foreach (T3DSurfaceSpec surf in plan.Surfaces.Where(x => x.Kind == T3DSurfaceKind.Internal))
            {
                List<Point3D> pts = new List<Point3D>();
                for (int i = 0; i < surf.Coordinates.GetLength(1); i++) pts.Add(new Point3D(surf.Coordinates[0, i], surf.Coordinates[1, i], surf.Coordinates[2, i]));
                Vector3D n = pts.NewellNormal();
                double azimuth = (Math.Atan2(n.X, n.Y) * 180 / Math.PI + 360) % 360;
                sb.AppendLine(string.Format(ci, "  INTERNAL panel='{0}' A='{1}'(idx {2}) B='{3}'(idx {4}) area={5:F2} azimuthOfNormal={6:F0} spaces(panel order)=[{7}]", surf.PanelName, plan.Zones[surf.Zone].Name, surf.Zone, plan.Zones[surf.Zone2].Name, surf.Zone2, n.Length / 2, azimuth,
                    string.Join("|", (cluster.GetSpaces(cluster.GetObject<Panel>(surf.PanelGuid)) ?? new List<Space>()).Select(x => x.Name))));
            }

            foreach (T3DZoneSpec z in plan.Zones)
                sb.AppendLine(string.Format(ci, "  zone '{0}' external={1} {2}", z.Name, z.External, z.Description));

            Console.Write(sb.ToString());
            if (args.Length > 2) File.WriteAllText(args[2], sb.ToString());
            return 0;
        }
    }
}
