// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using SAM.Geometry.Spatial;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        /// <summary>
        /// Decides everything the direct SAM -> T3D conversion will build, from the SAM model alone (no TAS COM
        /// object is touched), and returns it as a <see cref="T3DImportPlan"/> for <c>Convert.ToT3D</c> to replay.
        /// <para><b>Mapping</b></para>
        /// <list type="bullet">
        /// <item><description><b>Zones</b> - one per space (<c>Space</c> or <c>ExternalSpace</c>) that bounds at least one importable panel. The name is
        /// the space name; the space GUID is written into the description (<see cref="ZoneDescription"/>).</description></item>
        /// <item><description><b>Elements</b> - one per SAM <c>Construction</c> (plus a split where one construction is used both on and off the ground,
        /// or as air), unless <see cref="ToT3DOptions.ElementPerPanel"/> asks for one per panel. Width, colour, transparency, BE type and ground
        /// follow the rules <c>Query.UpdateT3D</c> applies to the elements the gbXML route creates.</description></item>
        /// <item><description><b>Window types</b> - one window object per aperture by default, because TAS folds the openings of one window object on one
        /// host into a single zone surface (see <see cref="ToT3DOptions.SharedWindowTypes"/>); with that option, one per <c>ApertureConstruction</c> (split only
        /// where the frame percentage or the host's position type differ).</description></item>
        /// <item><description><b>Surfaces</b> - a shade panel (<c>Analytical.PanelType.Shade</c>, or no space) is a shade; one space is <c>AddSurface</c> (adiabatic per
        /// <see cref="Analytical.Query.Adiabatic(Panel)"/>); two spaces are <c>AddInternalSurface</c> - or, when adiabatic, one adiabatic
        /// <c>AddSurface</c> per zone, which is what the gbXML route's <c>UpdateAdiabatic</c> ends up with.</description></item>
        /// <item><description><b>Orientation</b> - the polygon normal is made to point out of the first zone, taken from the space's own closed shell. An
        /// open shell cannot say which way is out; the panel's own normal is then used and the report says so.</description></item>
        /// <item><description><b>Openings</b> - each aperture follows its host panel's surface, snapped onto the host plane and wound the same way.</description></item>
        /// </list>
        /// </summary>
        /// <param name="analyticalModel">The SAM model.</param>
        /// <param name="options">The conversion options; null means the defaults.</param>
        /// <returns>The plan, or null when there is no model or adjacency cluster.</returns>
        public static T3DImportPlan T3DImportPlan(this AnalyticalModel analyticalModel, ToT3DOptions options = null)
        {
            AdjacencyCluster adjacencyCluster = analyticalModel?.AdjacencyCluster;
            if (adjacencyCluster == null)
            {
                return null;
            }

            options = options ?? new ToT3DOptions();

            T3DImportPlan plan = new T3DImportPlan();
            T3DImportReport report = plan.Report;

            plan.Name = analyticalModel.Name;
            plan.Description = analyticalModel.Description;

            if (analyticalModel.TryGetValue(Analytical.AnalyticalModelParameter.NorthAngle, out double northAngle) && !double.IsNaN(northAngle))
            {
                //The same rule Query.UpdateT3D applies on the gbXML route: degrees to one decimal, and never
                //below half a degree.
                // A negative angle is the same direction as its positive equivalent; it must not be clamped to the half degree.
                double degrees = global::System.Math.Round(Units.Convert.ToDegrees(northAngle), 1);
                degrees = ((degrees % 360) + 360) % 360;
                plan.NorthAngle = global::System.Math.Max(degrees, 0.5);
            }

            // ---- Which spaces bound which panels, read once ------------------------------------------------
            List<ISpace> spaces = new List<ISpace>();
            spaces.AddRange(adjacencyCluster.GetObjects<Space>() ?? new List<Space>());
            spaces.AddRange(adjacencyCluster.GetObjects<ExternalSpace>() ?? new List<ExternalSpace>());

            Dictionary<Guid, List<ISpace>> spacesByPanel = new Dictionary<Guid, List<ISpace>>();
            foreach (ISpace space in spaces)
            {
                List<Panel> panels_Space = adjacencyCluster.GetRelatedObjects<Panel>(space as IJSAMObject);
                if (panels_Space == null)
                {
                    continue;
                }

                foreach (Panel panel_Space in panels_Space)
                {
                    if (panel_Space == null)
                    {
                        continue;
                    }

                    if (!spacesByPanel.TryGetValue(panel_Space.Guid, out List<ISpace> spaces_Panel))
                    {
                        spaces_Panel = new List<ISpace>();
                        spacesByPanel[panel_Space.Guid] = spaces_Panel;
                    }

                    spaces_Panel.Add(space);
                }
            }

            MaterialLibrary materialLibrary = analyticalModel.MaterialLibrary;

            Dictionary<Guid, int> zoneIndexes = new Dictionary<Guid, int>();
            Dictionary<string, T3DElementSpec> elements = new Dictionary<string, T3DElementSpec>();
            Dictionary<string, T3DWindowSpec> windows = new Dictionary<string, T3DWindowSpec>();
            HashSet<string> elementNames = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> windowNames = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<Guid, Dictionary<Guid, Vector3D>> outwardNormals = new Dictionary<Guid, Dictionary<Guid, Vector3D>>();
            HashSet<Guid> openShells = new HashSet<Guid>();

            List<Panel> panels = adjacencyCluster.GetPanels();
            if (panels == null)
            {
                panels = new List<Panel>();
            }

            // ZONES IN SAM's SPACE ORDER, before any panel is read. The order is not cosmetic: TAS gives the LATER zone of a
            // vertical internal surface the reversed side of the construction (layers running the other way round for that
            // zone), which matters whenever the layers are not symmetric - a paint film on one face only. SAM's own TBD export
            // and the gbXML route both put the later space of the pair there, so the zones are created in that order here,
            // rather than in the order the panels happen to mention them.
            HashSet<Guid> spaces_Bounding = new HashSet<Guid>();
            foreach (Panel panel in panels)
            {
                if (panel != null && panel.PanelType != Analytical.PanelType.Shade && spacesByPanel.TryGetValue(panel.Guid, out List<ISpace> spaces_Temp) && spaces_Temp.Count <= 2)
                {
                    spaces_Temp.ForEach(x => spaces_Bounding.Add((x as SAMObject)?.Guid ?? Guid.Empty));
                }
            }

            foreach (ISpace space in spaces)
            {
                if (spaces_Bounding.Contains((space as SAMObject)?.Guid ?? Guid.Empty))
                {
                    ZoneIndex(plan, zoneIndexes, space);
                }
            }

            foreach (Panel panel in panels)
            {
                if (panel == null)
                {
                    continue;
                }

                string identity = PanelIdentity(panel);

                spacesByPanel.TryGetValue(panel.Guid, out List<ISpace> spaces_Panel);
                int count_Spaces = spaces_Panel == null ? 0 : spaces_Panel.Count;

                // ---- Shades -------------------------------------------------------------------------------
                if (panel.PanelType == Analytical.PanelType.Shade || count_Spaces == 0)
                {
                    report.Shades++;

                    // AddShadeSurface takes no openings: an aperture on a shade (or on a panel no space bounds) is not imported,
                    // and is said so rather than lost.
                    foreach (Aperture aperture_Shade in panel.Apertures ?? new List<Aperture>())
                    {
                        if (aperture_Shade != null)
                        {
                            report.Skipped.Add(string.Format("Aperture '{0}' ({1}) on panel {2}: the panel is imported as a shade (it is a shade, or no space bounds it), and a shade carries no openings, so no opening was imported.", string.IsNullOrWhiteSpace(aperture_Shade.Name) ? "unnamed" : aperture_Shade.Name, aperture_Shade.Guid, identity));
                        }
                    }

                    if (!options.ImportShades)
                    {
                        report.Skipped.Add(string.Format("Shade panel {0}: not imported, ImportShades is off.", identity));
                        continue;
                    }

                    Face3D face3D_Shade = panel.GetFace3D(false);
                    List<Point3D> polygon_Shade = face3D_Shade.TasPolygon(face3D_Shade?.GetPlane()?.Normal, options.Tolerance, out bool _, out int holes_Shade);
                    if (polygon_Shade == null)
                    {
                        report.Skipped.Add(string.Format("Shade panel {0}: degenerate geometry (fewer than three distinct, non-collinear vertices or no area).", identity));
                        continue;
                    }

                    if (holes_Shade != 0)
                    {
                        report.Notes.Add(string.Format("Shade panel {0}: {1} internal edge(s) ignored - AddShadeSurface takes one outer loop.", identity, holes_Shade));
                    }

                    plan.Shades.Add(new T3DShadeSpec
                    {
                        PanelGuid = panel.Guid,
                        PanelName = panel.Name,
                        ElementKey = ElementSpec(plan, elements, elementNames, panel, 0, false, false, materialLibrary, adjacencyCluster, options, report).Key,
                        Coordinates = polygon_Shade.ToTasCoordinates()
                    });
                    report.ShadesImported++;
                    continue;
                }

                if (count_Spaces > 2)
                {
                    report.Skipped.Add(string.Format("Panel {0}: bounds {1} spaces; a surface has one or two sides, so it was not imported.", identity, count_Spaces));
                    continue;
                }

                // ---- Geometry: outer loop, oriented out of the first zone ----------------------------------
                Face3D face3D = panel.GetFace3D(false);
                if (face3D == null)
                {
                    report.Skipped.Add(string.Format("Panel {0}: no geometry.", identity));
                    continue;
                }

                ISpace space_A = spaces_Panel[0];
                ISpace space_B = count_Spaces == 2 ? spaces_Panel[1] : null;

                Vector3D normal_A = OutwardNormal(adjacencyCluster, space_A, panel, outwardNormals, openShells, options.Tolerance, out bool verified_A);
                if (normal_A == null && space_B != null)
                {
                    // The first space's shell is open: the second space's, if closed, still says which way is out - the other way for this one.
                    Vector3D normal_B = OutwardNormal(adjacencyCluster, space_B, panel, outwardNormals, openShells, options.Tolerance, out bool verified_B);
                    if (verified_B && normal_B != null)
                    {
                        normal_A = normal_B.GetNegated();
                        verified_A = true;
                    }
                }

                if (normal_A == null)
                {
                    normal_A = face3D.GetPlane()?.Normal;
                }

                List<Point3D> polygon = face3D.TasPolygon(normal_A, options.Tolerance, out bool _, out int holes);
                if (polygon == null)
                {
                    report.Skipped.Add(string.Format("Panel {0}: degenerate or non-polygonal geometry (fewer than three distinct, non-collinear vertices, no area, or a curved boundary that is not a polygon), so it was not imported.", identity));
                    continue;
                }

                if (!verified_A)
                {
                    report.Notes.Add(string.Format("Panel {0}: its space's shell is not closed, so the outward side could not be verified; the panel's own normal was used.", identity));
                }

                if (holes != 0)
                {
                    report.Notes.Add(string.Format("Panel {0}: {1} internal edge(s) (non-aperture holes) ignored - AddSurface takes one outer loop, so the surface is imported without the hole(s).", identity, holes));
                }

                int zone_A = ZoneIndex(plan, zoneIndexes, space_A);
                int zone_B = space_B == null ? -1 : ZoneIndex(plan, zoneIndexes, space_B);

                bool adiabatic = Analytical.Query.Adiabatic(panel);
                bool ground = zone_B == -1 && IsGround(panel);

                T3DElementSpec elementSpec = ElementSpec(plan, elements, elementNames, panel, count_Spaces, ground, panel.PanelType == Analytical.PanelType.Air, materialLibrary, adjacencyCluster, options, report);

                // ---- Surface(s) ----------------------------------------------------------------------------
                Vector3D normal_Polygon = polygon.NewellNormal();
                Point3D origin_Polygon = polygon[0];

                if (zone_B != -1 && adiabatic)
                {
                    // An adiabatic panel between two zones: one null-linked surface per zone.
                    List<Point3D> polygon_B = new List<Point3D>(polygon);
                    polygon_B.Reverse();

                    T3DSurfaceSpec surface_A = Surface(panel, T3DSurfaceKind.InternalAdiabaticSide, zone_A, -1, elementSpec.Key, polygon);
                    T3DSurfaceSpec surface_B = Surface(panel, T3DSurfaceKind.InternalAdiabaticSide, zone_B, -1, elementSpec.Key, polygon_B);
                    Openings(plan, windows, windowNames, surface_A, panel, polygon, origin_Polygon, normal_Polygon, adjacencyCluster, materialLibrary, options, identity);
                    Openings(plan, windows, windowNames, surface_B, panel, polygon_B, origin_Polygon, normal_Polygon.GetNegated(), adjacencyCluster, materialLibrary, options, identity);

                    plan.Surfaces.Add(surface_A);
                    plan.Surfaces.Add(surface_B);

                    report.Surfaces += 2;
                    report.AdiabaticSurfaces += 2;
                    continue;
                }

                T3DSurfaceKind kind = zone_B != -1 ? T3DSurfaceKind.Internal : adiabatic ? T3DSurfaceKind.Adiabatic : ground ? T3DSurfaceKind.Ground : T3DSurfaceKind.External;

                T3DSurfaceSpec surface = Surface(panel, kind, zone_A, zone_B, elementSpec.Key, polygon);
                Openings(plan, windows, windowNames, surface, panel, polygon, origin_Polygon, normal_Polygon, adjacencyCluster, materialLibrary, options, identity);
                plan.Surfaces.Add(surface);

                report.Surfaces++;
                switch (kind)
                {
                    case T3DSurfaceKind.Internal:
                        report.InternalSurfaces++;
                        break;

                    case T3DSurfaceKind.Adiabatic:
                        report.AdiabaticSurfaces++;
                        break;

                    case T3DSurfaceKind.Ground:
                        report.GroundSurfaces++;
                        break;

                    default:
                        report.ExternalSurfaces++;
                        break;
                }
            }

            // A zone none of whose panels could be imported would only upset TAS: dropped, and every surface's zone index rewritten.
            RemoveEmptyZones(plan);

            report.Zones = plan.Zones.Count;
            report.Elements = plan.Elements.Count;
            report.Windows = plan.Windows.Count;
            report.Openings = 0;
            foreach (T3DSurfaceSpec surface in plan.Surfaces)
            {
                report.Openings += surface.Openings.Count;
            }

            foreach (T3DZoneSpec zone in plan.Zones)
            {
                report.ZoneNames[zone.SpaceGuid] = zone.Name;
            }

            return plan;
        }

        private static void RemoveEmptyZones(T3DImportPlan plan)
        {
            HashSet<int> used = new HashSet<int>();
            foreach (T3DSurfaceSpec surface in plan.Surfaces)
            {
                used.Add(surface.Zone);
                if (surface.Zone2 != -1)
                {
                    used.Add(surface.Zone2);
                }
            }

            if (used.Count == plan.Zones.Count)
            {
                return;
            }

            int[] map = new int[plan.Zones.Count];
            List<T3DZoneSpec> zones = new List<T3DZoneSpec>();
            for (int i = 0; i < plan.Zones.Count; i++)
            {
                if (used.Contains(i))
                {
                    map[i] = zones.Count;
                    zones.Add(plan.Zones[i]);
                }
                else
                {
                    map[i] = -1;
                    plan.Report.Skipped.Add(string.Format("Space '{0}' ({1}): none of its panels could be imported, so no zone was made for it.", plan.Zones[i].Name, plan.Zones[i].SpaceGuid));
                }
            }

            foreach (T3DSurfaceSpec surface in plan.Surfaces)
            {
                surface.Zone = map[surface.Zone];
                if (surface.Zone2 != -1)
                {
                    surface.Zone2 = map[surface.Zone2];
                }
            }

            plan.Zones.Clear();
            plan.Zones.AddRange(zones);
        }

        private static string PanelIdentity(Panel panel)
        {
            return string.Format("'{0}' ({1})", string.IsNullOrWhiteSpace(panel.Name) ? "unnamed" : panel.Name, panel.Guid);
        }

        private static bool IsGround(Panel panel)
        {
            if (panel.PanelType.Ground())
            {
                return true;
            }

            return panel.Construction != null && panel.Construction.TryGetValue(Analytical.ConstructionParameter.IsGround, out bool ground) && ground;
        }

        private static T3DSurfaceSpec Surface(Panel panel, T3DSurfaceKind kind, int zone, int zone2, string elementKey, List<Point3D> polygon)
        {
            return new T3DSurfaceSpec
            {
                PanelGuid = panel.Guid,
                PanelName = panel.Name,
                Kind = kind,
                Zone = zone,
                Zone2 = zone2,
                ElementKey = elementKey,
                ReverseElement = false,
                Coordinates = polygon.ToTasCoordinates()
            };
        }

        // The direction out of the space at this panel, from the space's own closed shell. Computed once per
        // space; null (and "not verified") when the shell is open or the panel could not be resolved in it.
        private static Vector3D OutwardNormal(AdjacencyCluster adjacencyCluster, ISpace space, Panel panel, Dictionary<Guid, Dictionary<Guid, Vector3D>> cache, HashSet<Guid> openShells, double tolerance, out bool verified)
        {
            verified = false;

            Guid spaceGuid = (space as SAMObject)?.Guid ?? Guid.Empty;
            if (spaceGuid == Guid.Empty)
            {
                return null;
            }

            if (!cache.TryGetValue(spaceGuid, out Dictionary<Guid, Vector3D> normals))
            {
                normals = new Dictionary<Guid, Vector3D>();
                cache[spaceGuid] = normals;

                Dictionary<IPanel, Vector3D> dictionary = adjacencyCluster.NormalDictionary(space, out Shell shell, true);
                if (dictionary != null && shell != null && shell.IsClosed(tolerance))
                {
                    foreach (KeyValuePair<IPanel, Vector3D> keyValuePair in dictionary)
                    {
                        Guid guid = (keyValuePair.Key as SAMObject)?.Guid ?? Guid.Empty;
                        if (guid != Guid.Empty && keyValuePair.Value != null && keyValuePair.Value.Length > 0)
                        {
                            normals[guid] = keyValuePair.Value;
                        }
                    }
                }
                else
                {
                    openShells.Add(spaceGuid);
                }
            }

            if (normals.TryGetValue(panel.Guid, out Vector3D result))
            {
                verified = true;
                return result;
            }

            return null;
        }

        private static int ZoneIndex(T3DImportPlan plan, Dictionary<Guid, int> zoneIndexes, ISpace space)
        {
            Guid guid = (space as SAMObject)?.Guid ?? Guid.Empty;
            if (zoneIndexes.TryGetValue(guid, out int index))
            {
                return index;
            }

            T3DZoneSpec zone = new T3DZoneSpec
            {
                Name = (space as SAMObject)?.Name,
                SpaceGuid = guid,
                Description = guid.ZoneDescription(),
                External = space is ExternalSpace
            };

            if (space is SAMObject sAMObject_Space && sAMObject_Space.TryGetValue(Analytical.SpaceParameter.LevelName, out string levelName) && !string.IsNullOrWhiteSpace(levelName))
            {
                zone.LevelName = levelName.Trim();
            }

            if (space is Space space_Space && space_Space.TryGetValue(Analytical.SpaceParameter.Color, out SAMColor sAMColor) && sAMColor != null)
            {
                zone.Colour = Core.Convert.ToUint(sAMColor.ToColor());
            }

            index = plan.Zones.Count;
            plan.Zones.Add(zone);
            zoneIndexes[guid] = index;
            return index;
        }

        // ---- Elements ---------------------------------------------------------------------------------------

        private static T3DElementSpec ElementSpec(T3DImportPlan plan, Dictionary<string, T3DElementSpec> elements, HashSet<string> elementNames, Panel panel, int spaces, bool ground, bool ghost, MaterialLibrary materialLibrary, AdjacencyCluster adjacencyCluster, ToT3DOptions options, T3DImportReport report)
        {
            Construction construction = panel.Construction;

            string key = options.ElementPerPanel
                ? "panel:" + panel.Guid.ToString("N")
                : string.Format(CultureInfo.InvariantCulture, "{0}|ground={1}|ghost={2}", construction == null ? "type:" + panel.PanelType : construction.Guid.ToString("N"), ground, ghost);

            if (elements.TryGetValue(key, out T3DElementSpec result))
            {
                return result;
            }

            result = new T3DElementSpec { Key = key, Ground = ground, Ghost = ghost };

            // Name: the construction's (the rest of SAM_Tas finds elements by it - "EXT", "ADIABATIC" and the
            // construction match all read it). A second element for the same construction, or two constructions
            // sharing a name, is told apart by a short stable suffix rather than silently merged.
            string name = construction != null && !string.IsNullOrWhiteSpace(construction.Name) ? construction.Name : panel.PanelType.Text();
            if (options.ElementPerPanel)
            {
                name = name + " [" + panel.Guid.ToString("N").Substring(0, 8) + "]";
            }

            if (elementNames.Contains(name))
            {
                string name_Qualified = name + "_" + ShortHash(key);
                report.Notes.Add(string.Format("Element name '{0}' is already taken by another element (a different construction, or the same construction used {1}); this element is named '{2}'.", name, ground ? "on the ground" : ghost ? "as air" : "otherwise", name_Qualified));
                name = name_Qualified;
            }

            elementNames.Add(name);
            result.Name = name;
            result.Description = construction == null ? null : construction.Guid.ToString("D");

            // Width: the construction's default thickness, else its layers' - the rule Query.UpdateT3D applies.
            double width = double.NaN;
            if (construction != null)
            {
                width = construction.GetValue<double>(Analytical.ConstructionParameter.DefaultThickness);
                if (double.IsNaN(width) || width == 0)
                {
                    width = construction.GetThickness(false);
                }
            }

            result.Width = double.IsNaN(width) ? 0 : width;

            // Colour.
            global::System.Drawing.Color color = global::System.Drawing.Color.Empty;
            if (construction == null || !construction.TryGetValue(Analytical.ConstructionParameter.Color, out color))
            {
                color = CosmeticColor(() => Analytical.Query.Color(panel.PanelType)) ?? global::System.Drawing.Color.Empty;
            }

            result.Colour = color == global::System.Drawing.Color.Empty ? Core.Convert.ToUint(global::System.Drawing.Color.Gray) : Core.Convert.ToUint(color);

            // Transparent / internal shadows.
            bool transparent = false;
            if (construction != null)
            {
                MaterialType materialType = Analytical.Query.MaterialType(construction.ConstructionLayers, materialLibrary);
                if (materialType == MaterialType.Undefined)
                {
                    if (construction.TryGetValue(Analytical.ConstructionParameter.Transparent, out bool transparent_Parameter))
                    {
                        transparent = transparent_Parameter;
                    }
                }
                else
                {
                    transparent = materialType == MaterialType.Transparent;
                }
            }

            result.Transparent = transparent;
            result.InternalShadows = construction != null && construction.TryGetValue(Analytical.ConstructionParameter.IsInternalShadow, out bool internalShadows) ? internalShadows : transparent;

            // BE type: what the construction states (its panel type, then its default panel type), else what the panel
            // is. The gbXML route inherits TAS's own choice from the gbXML surface type for a generic 'Wall' or 'Floor'
            // construction, which TAS's BE type list has no entry for; the direct route has no gbXML surface type, so it
            // derives the same answer from how the panel is used (see PanelBEType).
            Analytical.PanelType panelType = construction == null ? Analytical.PanelType.Undefined : construction.PanelType();
            result.BEType = panelType == Analytical.PanelType.Undefined ? -1 : BEType(panelType.Text());
            if (result.BEType == -1 && construction != null && construction.TryGetValue(Analytical.ConstructionParameter.DefaultPanelType, out string defaultPanelType) && !string.IsNullOrEmpty(defaultPanelType))
            {
                result.BEType = BEType(defaultPanelType);
            }

            if (result.BEType == -1)
            {
                result.BEType = PanelBEType(panel, spaces, ground);
            }

            if (result.BEType != -1)
            {
                panelType = PanelType(result.BEType);
            }
            else if (panelType == Analytical.PanelType.Undefined)
            {
                panelType = panel.PanelType;
            }

            result.ZoneFloorArea = panelType.PanelGroup() == PanelGroup.Floor || panel.PanelGroup == PanelGroup.Floor;

            if (construction != null)
            {
                if (construction.TryGetValue(Analytical.ConstructionParameter.IsGround, out bool isGround) && isGround)
                {
                    result.Ground = true;
                }

                if (construction.TryGetValue(Analytical.ConstructionParameter.IsAir, out bool isAir) && isAir)
                {
                    result.Ghost = true;
                }
            }

            elements[key] = result;
            plan.Elements.Add(result);
            return result;
        }

        // The TAS BE type of a panel from what it is and how it is used: its own panel type where TAS's list has an
        // entry for it, and otherwise (a generic Wall, Floor or Ceiling) from whether it separates two spaces or is
        // exposed. -1 where there is no sensible answer; the TAS default is then left.
        private static int PanelBEType(Panel panel, int spaces, bool ground)
        {
            int result = BEType(panel.PanelType.Text());
            if (result != -1)
            {
                return result;
            }

            switch (panel.PanelType)
            {
                case Analytical.PanelType.Wall:
                    return spaces == 2 ? BEType("Internal Wall") : BEType("External Wall");

                case Analytical.PanelType.Floor:
                    return ground ? BEType("Slab on Grade") : spaces == 2 ? BEType("Internal Floor") : BEType("Exposed Floor");

                case Analytical.PanelType.Ceiling:
                    return BEType("Internal Ceiling");
            }

            return -1;
        }

        // ---- Openings ---------------------------------------------------------------------------------------

        private static void Openings(T3DImportPlan plan, Dictionary<string, T3DWindowSpec> windows, HashSet<string> windowNames, T3DSurfaceSpec surface, Panel panel, List<Point3D> polygon_Host, Point3D origin_Host, Vector3D normal_Host, AdjacencyCluster adjacencyCluster, MaterialLibrary materialLibrary, ToT3DOptions options, string identity_Panel)
        {
            List<Aperture> apertures = panel.Apertures;
            if (apertures == null || apertures.Count == 0)
            {
                return;
            }

            T3DImportReport report = plan.Report;

            foreach (Aperture aperture in apertures)
            {
                if (aperture == null)
                {
                    continue;
                }

                string identity = string.Format("Aperture '{0}' ({1}) on panel {2}", string.IsNullOrWhiteSpace(aperture.Name) ? "unnamed" : aperture.Name, aperture.Guid, identity_Panel);

                IClosedPlanar3D externalEdge3D = aperture.GetFace3D()?.GetExternalEdge3D();
                List<Point3D> points = (externalEdge3D as ISegmentable3D)?.GetPoints();
                if (points == null)
                {
                    report.Skipped.Add(identity + ": no polygonal geometry (missing, or a curved boundary), so no opening was imported.");
                    continue;
                }

                List<Point3D> projected = points.ProjectedOnPlane(origin_Host, normal_Host, out double maxDistance);
                if (maxDistance > options.SnapTolerance)
                {
                    report.Notes.Add(string.Format(CultureInfo.InvariantCulture, "{0}: lies up to {1:F3} m off its host panel's plane (more than the {2:F3} m snap tolerance); it was snapped onto the host plane.", identity, maxDistance, options.SnapTolerance));
                }

                List<Point3D> polygon = projected.TasPolygon(normal_Host, options.Tolerance, out bool _);
                if (polygon == null)
                {
                    report.Skipped.Add(identity + ": degenerate or non-polygonal geometry, so no opening was imported.");
                    continue;
                }

                T3DWindowSpec window = WindowSpec(plan, windows, windowNames, aperture, panel, polygon, adjacencyCluster, materialLibrary, options);

                surface.Openings.Add(new T3DOpeningSpec { ApertureGuid = aperture.Guid, WindowKey = window.Key, Coordinates = polygon.ToTasCoordinates() });
            }
        }

        private static T3DWindowSpec WindowSpec(T3DImportPlan plan, Dictionary<string, T3DWindowSpec> windows, HashSet<string> windowNames, Aperture aperture, Panel panel, List<Point3D> polygon, AdjacencyCluster adjacencyCluster, MaterialLibrary materialLibrary, ToT3DOptions options)
        {
            ApertureConstruction apertureConstruction = aperture.ApertureConstruction;
            Analytical.ApertureType apertureType = aperture.ApertureType;

            // The host decides how TAS positions the opening: the rule Query.UpdateT3D applies.
            int openingType = apertureType == Analytical.ApertureType.Door ? 2 : panel.PanelGroup == PanelGroup.Roof ? 1 : 0;
            int positionType = -1;
            switch (panel.PanelGroup)
            {
                case PanelGroup.Wall:
                    positionType = apertureType == Analytical.ApertureType.Door ? 2 : apertureType == Analytical.ApertureType.Window ? 0 : -1;
                    break;

                case PanelGroup.Roof:
                    positionType = 1;
                    break;

                case PanelGroup.Floor:
                    positionType = 4;
                    break;
            }

            // The aperture's own frame percentage, exactly. A shared window type can only carry one value for all its
            // apertures, so there it is rounded to a tenth of a percent to let near-identical frames share.
            double framePercent = double.IsNaN(aperture.GetFrameFactor()) ? double.NaN : aperture.GetFrameFactor() * 100;
            if (options.SharedWindowTypes && !double.IsNaN(framePercent))
            {
                framePercent = global::System.Math.Round(framePercent, 1);
            }

            // One window object per aperture unless the caller asked for shared types: TAS folds the openings of one
            // window object on one host into ONE zone surface, so sharing loses the one-surface-per-aperture identity.
            string key = options.SharedWindowTypes
                ? string.Format(CultureInfo.InvariantCulture, "{0}|position={1}|frame={2}|type={3}", apertureConstruction == null ? "none" : apertureConstruction.Guid.ToString("N"), positionType, framePercent, apertureType)
                : "aperture:" + aperture.Guid.ToString("N");

            if (windows.TryGetValue(key, out T3DWindowSpec result))
            {
                return result;
            }

            result = new T3DWindowSpec { Key = key, OpeningType = openingType, PositionType = positionType, FramePercent = framePercent };

            // The name TAS builds its two building elements from. See T3DWindowSpec.Name.
            string name;
            if (options.SharedWindowTypes)
            {
                string baseName = BuildingElementNamePrefix(apertureType) + ConstructionNameBase(apertureConstruction?.Name ?? "Aperture");
                name = baseName;
                if (windowNames.Contains(name))
                {
                    name = baseName + "_" + ShortHash(key);
                }

                windowNames.Add(name);
                result.Description = apertureConstruction?.Guid.ToString("D");
            }
            else
            {
                // 'Windows: <name> <aperture GUID> ' - the instance name Query.UniqueNameDecomposition reads the GUID back
                // out of, and Query.NamesContainingApertureGuid recognises as a physical aperture's, never a shared definition.
                name = BuildingElementNamePrefix(apertureType) + ConstructionNameBase(string.IsNullOrWhiteSpace(aperture.Name) ? apertureConstruction?.Name : aperture.Name) + " " + aperture.Guid.ToString("D");
                result.Description = aperture.Guid.ToString("D");
            }

            result.Name = name + " ";

            // Colour: the rule the gbXML route applies per aperture (Query.Color(aperture, Pane)).
            global::System.Drawing.Color? color = CosmeticColor(() => Color(aperture, Analytical.AperturePart.Pane));
            if (color == null || !color.HasValue || color.Value == global::System.Drawing.Color.Empty)
            {
                color = apertureConstruction == null ? global::System.Drawing.Color.Empty : CosmeticColor(() => Analytical.Query.Color(apertureConstruction.ApertureType));
            }

            result.Colour = color.HasValue && color.Value != global::System.Drawing.Color.Empty ? Core.Convert.ToUint(color.Value) : Core.Convert.ToUint(global::System.Drawing.Color.Black);

            // Transparency and internal shadows - the same rules as Query.UpdateT3D's window block.
            bool transparent = false;
            if (apertureConstruction != null)
            {
                MaterialType materialType = Analytical.Query.MaterialType(apertureConstruction.PaneConstructionLayers, materialLibrary);
                if (materialType == MaterialType.Undefined)
                {
                    if (apertureConstruction.TryGetValue(ApertureConstructionParameter.Transparent, out bool transparent_Parameter))
                    {
                        transparent = transparent_Parameter;
                    }
                }
                else
                {
                    transparent = materialType == MaterialType.Transparent;
                }
            }

            result.Transparent = transparent;
            result.InternalShadows = false;
            if (transparent && apertureConstruction != null)
            {
                if (apertureConstruction.TryGetValue(ApertureConstructionParameter.IsInternalShadow, out bool internalShadows))
                {
                    result.InternalShadows = internalShadows;
                }
                else
                {
                    if (!plan.AllHostsExternal.TryGetValue(apertureConstruction.Guid, out bool allExternal))
                    {
                        List<Panel> panels = adjacencyCluster.GetPanels(apertureConstruction);
                        allExternal = panels != null && panels.Count != 0 && panels.TrueForAll(x => adjacencyCluster.External(x));
                        plan.AllHostsExternal[apertureConstruction.Guid] = allExternal;
                    }

                    result.InternalShadows = allExternal;
                }
            }

            // Frame width.
            if (apertureConstruction != null)
            {
                if (apertureConstruction.TryGetValue(ApertureConstructionParameter.DefaultFrameWidth, out double frameWidth) && !double.IsNaN(frameWidth))
                {
                    result.FrameWidth = frameWidth;
                }

                double frameThickness = apertureConstruction.GetFrameThickness();
                if (!double.IsNaN(frameThickness))
                {
                    result.FrameWidth = frameThickness;
                }
            }

            // The opening polygon - not these - sets the size TAS builds; they describe the window type.
            double width = aperture.GetWidth();
            double height = aperture.GetHeight();
            result.Width = double.IsNaN(width) || width <= 0 ? 1 : width;
            result.Height = double.IsNaN(height) || height <= 0 ? 1 : height;
            double level = double.MaxValue;
            foreach (Point3D point3D in polygon)
            {
                level = global::System.Math.Min(level, point3D.Z);
            }

            result.Level = level == double.MaxValue ? 0 : level;

            windows[key] = result;
            plan.Windows.Add(result);
            return result;
        }

        // A colour is cosmetic: it must never be the reason an import fails. Where System.Drawing.Common is unavailable (a
        // process that is not a Windows desktop one - e.g. the COM-free unit tests) the colour is simply not stated and
        // the default is used.
        private static global::System.Drawing.Color? CosmeticColor(Func<global::System.Drawing.Color?> colour)
        {
            try
            {
                return colour();
            }
            catch (PlatformNotSupportedException)
            {
                return null;
            }
        }

        // A stable 32-bit FNV-1a hash as eight hex digits: a name discriminator, not a security measure, so nothing that a FIPS-only
        // crypto policy can refuse.
        private static string ShortHash(string text)
        {
            uint hash = 2166136261;
            foreach (byte value in Encoding.UTF8.GetBytes(text))
            {
                hash ^= value;
                hash *= 16777619;
            }

            return hash.ToString("X8", CultureInfo.InvariantCulture);
        }
    }
}
