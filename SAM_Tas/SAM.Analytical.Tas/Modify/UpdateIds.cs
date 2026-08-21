// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Modify
    {
        public static bool UpdateIds(this AdjacencyCluster adjacencyCluster, TBD.Building building, double tolerance = Core.Tolerance.MacroDistance)
        {
            if (building == null || adjacencyCluster == null)
            {
                return false;
            }

            List<Space> spaces = adjacencyCluster.GetSpaces();
            if(spaces != null && spaces.Count != 0)
            {
                foreach(Space space in spaces)
                {
                    space.RemoveValue(SpaceParameter.ZoneGuid);
                    adjacencyCluster.AddObject(space);
                }

                List<Panel> panels = adjacencyCluster.GetPanels();
                if(panels != null && panels.Count != 0)
                {
                    foreach (Panel panel in panels)
                    {
                        panel.RemoveValue(PanelParameter.ZoneSurfaceReference_1);
                        panel.RemoveValue(PanelParameter.ZoneSurfaceReference_2);
                        adjacencyCluster.AddObject(panel);
                    }
                }
                
                List<TBD.zone> zones = building.Zones();
                if (zones != null && zones.Count != 0)
                {
                    // Index zones by GUID and by name once; the original `space.Match(zones)` did two linear
                    // scans (.ToList().Find()) per space, giving O(spaces * zones) — 67 s on a 625-zone model.
                    Dictionary<string, TBD.zone> zonesByGuid = new Dictionary<string, TBD.zone>(zones.Count);
                    Dictionary<string, TBD.zone> zonesByName = new Dictionary<string, TBD.zone>(zones.Count);
                    foreach (TBD.zone z in zones)
                    {
                        if (z == null) continue;
                        if (!string.IsNullOrWhiteSpace(z.GUID))
                            zonesByGuid[z.GUID] = z;
                        if (!string.IsNullOrWhiteSpace(z.name))
                            zonesByName[z.name] = z;
                    }

                    foreach(Space space in spaces)
                    {
                        TBD.zone zone = null;
                        if (space.TryGetValue(SpaceParameter.ZoneGuid, out string spaceZoneGuid) && !string.IsNullOrWhiteSpace(spaceZoneGuid))
                            zonesByGuid.TryGetValue(spaceZoneGuid, out zone);
                        if (zone == null && !string.IsNullOrWhiteSpace(space?.Name))
                            zonesByName.TryGetValue(space.Name, out zone);
                        if(zone == null)
                        {
                            continue;
                        }

                        space.SetValue(SpaceParameter.ZoneGuid, zone.GUID);
                        adjacencyCluster.AddObject(space);

                        List<Panel> panels_Space = adjacencyCluster.GetPanels(space);
                        if(panels_Space == null || panels_Space.Count == 0)
                        {
                            continue;
                        }

                        List<TBD.IZoneSurface> zoneSurfaces = zone?.ZoneSurfaces();
                        if(zoneSurfaces == null || zoneSurfaces.Count == 0)
                        {
                            continue;
                        }

                        foreach (TBD.IZoneSurface zoneSurface in zoneSurfaces)
                        {
                            Panel panel = zoneSurface.Match(panels_Space, tolerance);
                            if(panel == null)
                            {
                                continue;
                            }

                            Core.Tas.ZoneSurfaceReference zoneSurfaceReference = new Core.Tas.ZoneSurfaceReference(zoneSurface.number, zone.GUID);
                            panel.SetValue(PanelParameter.BuildingElementGuid, zoneSurface.buildingElement?.GUID);

                            Core.Tas.ZoneSurfaceReference zoneSurfaceReference_1;

                            List<Aperture> apertures = panel.Apertures;
                            if (apertures != null && apertures.Count != 0)
                            {
                                Aperture aperture = zoneSurface.Match(apertures, out AperturePart aperturePart, tolerance);
                                if (aperture != null)
                                {
                                    ApertureParameter apertureParameter_1 = aperturePart == AperturePart.Frame ? ApertureParameter.FrameZoneSurfaceReference_1 : ApertureParameter.PaneZoneSurfaceReference_1;
                                    ApertureParameter apertureParameter_2 = aperturePart == AperturePart.Frame ? ApertureParameter.FrameZoneSurfaceReference_2 : ApertureParameter.PaneZoneSurfaceReference_2;
                                    if (!aperture.TryGetValue(apertureParameter_1, out zoneSurfaceReference_1) || zoneSurfaceReference_1 == null)
                                    {
                                        aperture.SetValue(apertureParameter_1, zoneSurfaceReference);
                                    }
                                    else
                                    {
                                        aperture.SetValue(apertureParameter_2, zoneSurfaceReference);
                                    }

                                    // The definition-binding stamp: which TBD building element this physical
                                    // surface's own element currently is. Re-stamped every pass, unlike the
                                    // ZoneSurfaceReference pair above, because it names a DEFINITION rather
                                    // than a physical instance - many apertures may legitimately share it,
                                    // and it is what lets a later Modify.UpdateBuildingElements pass resolve
                                    // an aperture's element WITHOUT decoding a GUID out of the element's own
                                    // name (which a shared, definition-derived name no longer carries).
                                    ApertureParameter buildingElementGuidParameter = aperturePart == AperturePart.Frame ? ApertureParameter.FrameBuildingElementGuid : ApertureParameter.PaneBuildingElementGuid;
                                    aperture.SetValue(buildingElementGuidParameter, zoneSurface.buildingElement?.GUID);

                                    panel.RemoveAperture(aperture.Guid);
                                    panel.AddAperture(aperture);
                                    adjacencyCluster.AddObject(panel);
                                    continue;
                                }
                            }

                            if (!panel.TryGetValue(PanelParameter.ZoneSurfaceReference_1, out zoneSurfaceReference_1) || zoneSurfaceReference_1 == null)
                            {
                                panel.SetValue(PanelParameter.ZoneSurfaceReference_1, zoneSurfaceReference);
                            }
                            else
                            {
                                panel.SetValue(PanelParameter.ZoneSurfaceReference_2, zoneSurfaceReference);
                            }

                            adjacencyCluster.AddObject(panel);
                        }
                    }

                }
            }

            return true;
        }
    }
}