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

            //Every physical surface each aperture part was matched to, collected over the whole pass and
            //stamped canonically afterwards - one slot per ZONE, ordered by zone GUID - rather than
            //_1-then-_2 in the order the zones happened to be walked. See Query.ApertureZoneSurfaceSides.
            Dictionary<System.Guid, ApertureSurfaceCollector> dictionary_ApertureSurfaces = new Dictionary<System.Guid, ApertureSurfaceCollector>();

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

                        //The APERTURE stamps are cleared too. Only the panel ones were, while the aperture
                        //write below filled _1 when it was empty and _2 otherwise - so a second UpdateIds pass
                        //found _1 already occupied by the previous run, wrote side 1 into _2, and then wrote
                        //side 2 over the top of it. The stale _1 is not merely redundant: TAS need not have
                        //reassigned the same surface number, so it points somewhere real and wrong.
                        List<Aperture> apertures_Panel = panel.Apertures;
                        if (apertures_Panel != null && apertures_Panel.Count != 0)
                        {
                            foreach (Aperture aperture_Panel in apertures_Panel)
                            {
                                if (aperture_Panel == null)
                                {
                                    continue;
                                }

                                aperture_Panel.RemoveApertureZoneSurfaceReferences(AperturePart.Pane);
                                aperture_Panel.RemoveApertureZoneSurfaceReferences(AperturePart.Frame);

                                panel.RemoveAperture(aperture_Panel.Guid);
                                panel.AddAperture(aperture_Panel);
                            }
                        }

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
                            Panel panel = zoneSurface.Match(panels_Space, zone.GUID, tolerance);
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
                                Aperture aperture = zoneSurface.Match(apertures, zone.GUID, out AperturePart aperturePart, tolerance);
                                if (aperture != null)
                                {
                                    //The physical stamp is COLLECTED here and written after the pass, so both
                                    //sides of an internal aperture are in hand before either takes a slot.
                                    ZoneSurfaceKey zoneSurfaceKey = Query.ZoneSurfaceKey(zone.GUID, zoneSurface.number);
                                    if (zoneSurfaceKey != null && aperturePart != AperturePart.Undefined)
                                    {
                                        if (!dictionary_ApertureSurfaces.TryGetValue(aperture.Guid, out ApertureSurfaceCollector collector))
                                        {
                                            collector = new ApertureSurfaceCollector { PanelGuid = panel.Guid };
                                            dictionary_ApertureSurfaces[aperture.Guid] = collector;
                                        }

                                        collector.Add(aperturePart, zoneSurfaceKey, zoneSurfaceReference);
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

            // ---------------------------------------------------------------------------------------------
            // The collected physical stamps, written canonically. A slot is a SIDE and a side is a ZONE, so
            // an aperture two surfaces are ordered by zone GUID rather than by whichever zone this pass
            // reached first - which is what makes a repeated UpdateIds a no-op instead of a reshuffle.
            // ---------------------------------------------------------------------------------------------
            foreach (KeyValuePair<System.Guid, ApertureSurfaceCollector> keyValuePair in dictionary_ApertureSurfaces)
            {
                Panel panel = adjacencyCluster.GetObject<Panel>(keyValuePair.Value.PanelGuid);
                Aperture aperture = panel?.GetAperture(keyValuePair.Key);
                if (aperture == null)
                {
                    continue;
                }

                foreach (KeyValuePair<AperturePart, Dictionary<ZoneSurfaceKey, Core.Tas.ZoneSurfaceReference>> keyValuePair_Part in keyValuePair.Value.References)
                {
                    aperture.SetApertureZoneSurfaceReferences(keyValuePair_Part.Key, keyValuePair_Part.Value.Values, out string _);
                }

                panel.RemoveAperture(aperture.Guid);
                panel.AddAperture(aperture);
                adjacencyCluster.AddObject(panel);
            }

            return true;
        }

        /// <summary>
        /// One aperture's physical surfaces as <c>UpdateIds</c> meets them - one entry per part, keyed by the
        /// physical <see cref="ZoneSurfaceKey"/> so the same surface met twice counts once, holding the
        /// reference with the zone GUID spelt as TAS spelt it.
        /// <para>
        /// Collected rather than stamped in place because both sides of an internal aperture have to be in hand
        /// before either can be told which slot it occupies.
        /// </para>
        /// </summary>
        private sealed class ApertureSurfaceCollector
        {
            public System.Guid PanelGuid;

            public Dictionary<AperturePart, Dictionary<ZoneSurfaceKey, Core.Tas.ZoneSurfaceReference>> References { get; } = new Dictionary<AperturePart, Dictionary<ZoneSurfaceKey, Core.Tas.ZoneSurfaceReference>>();

            public void Add(AperturePart aperturePart, ZoneSurfaceKey zoneSurfaceKey, Core.Tas.ZoneSurfaceReference zoneSurfaceReference)
            {
                if (!References.TryGetValue(aperturePart, out Dictionary<ZoneSurfaceKey, Core.Tas.ZoneSurfaceReference> dictionary))
                {
                    dictionary = new Dictionary<ZoneSurfaceKey, Core.Tas.ZoneSurfaceReference>();
                    References[aperturePart] = dictionary;
                }

                dictionary[zoneSurfaceKey] = zoneSurfaceReference;
            }
        }
    }
}