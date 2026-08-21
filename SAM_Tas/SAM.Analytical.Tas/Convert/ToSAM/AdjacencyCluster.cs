// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using SAM.Core.Tas;
using System.Collections.Generic;
using TSD;
using SAM.Geometry.Spatial;
using System.Linq;
using System;

namespace SAM.Analytical.Tas
{
    public static partial class Convert
    {      
        public static AdjacencyCluster ToSAM_AdjacencyCluster(this BuildingData buildingData, IEnumerable<SpaceDataType> spaceDataTypes = null, IEnumerable <PanelDataType> panelDataTypes = null, IEnumerable<string> spaceNames = null)
        {
            if (buildingData == null)
            {
                return null;
            }

            List<ZoneData> zoneDatas = buildingData.ZoneDatas();
            if (zoneDatas == null)
            {
                return null;
            }

            AdjacencyCluster result = new AdjacencyCluster();

            foreach(ZoneData zoneData in zoneDatas)
            {
                if (zoneData == null)
                {
                    continue;
                }

                if(spaceNames != null && !spaceNames.Contains(zoneData.name))
                {
                    continue;
                }

                Space space = zoneData.ToSAM(spaceDataTypes);
                if (space != null)
                {
                    result.AddObject(space);
                }

                List<SurfaceData> surfaceDatas = zoneData.SurfaceDatas();
                if (surfaceDatas == null)
                {
                    continue;
                }

                foreach(SurfaceData surfaceData in surfaceDatas)
                {
                    if (surfaceData == null)
                    {
                        continue;
                    }

                    Panel panel = surfaceData.ToSAM(panelDataTypes);
                    if (panel == null)
                    {
                        continue;
                    }

                    result.AddObject(panel);

                    if (space != null)
                    {
                        result.AddRelation(space, panel);
                    }
                }
            }

            return result;

        }

        public static AdjacencyCluster ToSAM_AdjacencyCluster(this SAMTSDDocument sAMTSDDocument, IEnumerable<SpaceDataType> spaceDataTypes = null, IEnumerable<PanelDataType> panelDataTypes = null)
        {
            return ToSAM_AdjacencyCluster(sAMTSDDocument?.TSDDocument, spaceDataTypes, panelDataTypes);
        }

        public static AdjacencyCluster ToSAM_AdjacencyCluster(this TSDDocument tSDDocument, IEnumerable<SpaceDataType> spaceDataTypes = null, IEnumerable<PanelDataType> panelDataTypes = null)
        {
            return ToSAM_AdjacencyCluster(tSDDocument?.SimulationData?.GetBuildingData(), spaceDataTypes, panelDataTypes);
        }

        public static AdjacencyCluster ToSAM_AdjacencyCluster(this string path_TSD, IEnumerable<SpaceDataType> spaceDataTypes = null, IEnumerable<PanelDataType> panelDataTypes = null)
        {
            if (string.IsNullOrWhiteSpace(path_TSD))
                return null;

            AdjacencyCluster result = null;

            using (SAMTSDDocument sAMTSDDocument = new SAMTSDDocument(path_TSD))
            {
                result = sAMTSDDocument.ToSAM_AdjacencyCluster(spaceDataTypes, panelDataTypes);
            }

            return result;
        }

        public static AdjacencyCluster ToSAM(this TBD.Building building)
        {
            return ToSAM(building, null);
        }

        /// <summary>
        /// AdjacencyCluster build with optional shared polygon3D cache. Pass a non-null
        /// <paramref name="polygonCache"/> to share converted Polygon3D objects with downstream
        /// callers (e.g. <c>Create.SolarModel</c>) — same roomSurface won't be re-marshaled
        /// over COM. Key format: <c>zoneSurface.GUID + "/" + roomSurfaceIndex</c>.
        /// </summary>
        public static AdjacencyCluster ToSAM(this TBD.Building building, Dictionary<string, Polygon3D> polygonCache)
        {
            if (building == null)
            {
                return null;
            }

            AdjacencyCluster adjacencyCluster = new ();

            Dictionary<string, Construction> dictionary_Construction = [];
            List<ApertureConstruction> apertureConstructions = [];

            //double groundElevation = 0;

            Dictionary<string, Space> dictionary_Space = [];
            Dictionary<string, List<Panel>> dictionary_Panel = [];

            //Dictionary<string, List<Tuple<string, string>>> dictionary_Relations = new Dictionary<string, List<Tuple<string, string>>>();

            foreach (TBD.zone zone in building.Zones())
            {
                Space space = zone.ToSAM(out List<InternalCondition> internalConditions);
                if (space == null)
                {
                    continue;
                }

                if(internalConditions != null && internalConditions.Count != 0)
                {
                    InternalCondition internalCondition = internalConditions.Find(x => !x.Name.EndsWith("HDD") && !x.Name.EndsWith("CDD"));
                    if(internalCondition == null)
                    {
                        internalCondition = internalConditions[0];
                    }

                    space.InternalCondition = internalCondition;
                    internalConditions.Remove(internalCondition);
                    internalConditions.ForEach(x => adjacencyCluster.AddObject(x));
                }

                space.SetValue(SpaceParameter.ZoneGuid, zone.GUID);

                //List<Tuple<string, string>> tuples_Relations = new List<Tuple<string, string>>();
                //dictionary_Relations[zone.GUID] = tuples_Relations;

                adjacencyCluster.AddObject(space);

                dictionary_Space[zone.GUID] = space;

                List<TBD.IZoneSurface> zoneSurfaces = zone.ZoneSurfaces();
                if(zoneSurfaces == null)
                {
                    continue;
                }

                foreach(TBD.IZoneSurface zoneSurface in zoneSurfaces)
                {
                    TBD.buildingElement buildingElement = zoneSurface.buildingElement;
                    if(buildingElement == null)
                    {
                        continue;
                    }

                    //tuples_Relations.Add(new Tuple<string, string>(zoneSurface?.GUID, zoneSurface?.linkSurface?.GUID));

                    //Add link surface for internal Panels
                    //zoneSurface.linkSurface

                    PanelType panelType = Query.PanelType(buildingElement.BEType);
                    if (panelType == PanelType.Undefined)
                    {
                        if(buildingElement.BEType != 0)
                        {
                            continue;
                        }
                        panelType = PanelType.Air;
                    }

                    //bool ground = Analytical.Query.Ground(panelType);

                    Construction construction = null;

                    TBD.Construction construction_TBD = buildingElement.GetConstruction();

                    if(construction_TBD != null)
                    {
                        if (!dictionary_Construction.TryGetValue(construction_TBD.GUID, out construction) || construction == null)
                        {
                            construction = construction_TBD.ToSAM();
                            construction.SetValue(Analytical.ConstructionParameter.DefaultPanelType, panelType);
                            dictionary_Construction[construction_TBD.GUID] = construction;
                        }
                    }

                    List<Panel> panels_Link = null;

                    TBD.IZoneSurface zoneSurface_Link = zoneSurface.linkSurface;
                    if (zoneSurface_Link != null)
                    {
                        dictionary_Panel.TryGetValue(zoneSurface_Link.GUID, out panels_Link);
                    }

                    bool adiabatic = zoneSurface.type == TBD.SurfaceType.tbdNullLink;

                    ZoneSurfaceReference zoneSurfaceReference = new (zoneSurface.number, zone.GUID);

                    int roomSurfaceIndex_panel = 0;
                    foreach (TBD.IRoomSurface roomSurface in zoneSurface.RoomSurfaces())
                    {
                        Polygon3D polygon3D = GetOrConvertPolygon(polygonCache, zoneSurface.GUID, roomSurfaceIndex_panel, roomSurface);
                        roomSurfaceIndex_panel++;
                        if (polygon3D == null)
                        {
                            continue;
                        }

                        Face3D face3D = new Face3D(polygon3D);

                        //if(ground)
                        //{
                        //    groundElevation = Math.Max(groundElevation, face3D.GetBoundingBox().Max.Z);
                        //}

                        Panel panel = null;
                        if (panels_Link != null && panels_Link.Count != 0)
                        {
                            panel = panels_Link.Find(x => face3D.InRange(x.GetInternalPoint3D()));
                            if(panel is null)
                            {
                                panel = panels_Link.Find(x => face3D.InRange(x.GetInternalPoint3D(), Tolerance.MacroDistance));
                                if (panel is null && panels_Link.Count == 1)
                                {
                                    panel = panels_Link[0];
                                }
                            }
                        }

                        if (panel == null)
                        {
                            panel = Analytical.Create.Panel(construction, panelType, face3D);
                        }

                        if (panel == null)
                        {
                            continue;
                        }

                        if(adiabatic)
                        {
                            panel.SetValue(Analytical.PanelParameter.Adiabatic, true);
                        }

                        PanelParameter panelParameter = panel.HasValue(PanelParameter.ZoneSurfaceReference_1) ? PanelParameter.ZoneSurfaceReference_2 : PanelParameter.ZoneSurfaceReference_1;
                        panel.SetValue(panelParameter, zoneSurfaceReference);
                        panel.SetValue(PanelParameter.BuildingElementGuid, buildingElement.GUID);

                        adjacencyCluster.AddObject(panel);
                        adjacencyCluster.AddRelation(panel, space);

                        if(!dictionary_Panel.TryGetValue(zoneSurface.GUID, out List<Panel>  panels))
                        {
                            panels = [];
                            dictionary_Panel[zoneSurface.GUID] = panels;
                        }

                        panels.Add(panel);

                        if (zoneSurface_Link != null)
                        {
                            if (dictionary_Space.TryGetValue(zoneSurface_Link.zone.GUID, out Space space_Link))
                            {
                                adjacencyCluster.AddRelation(panel, space_Link);
                            }
                        }

                    }
                }

                Dictionary<Guid, List<Tuple<Polygon3D, TBD.IZoneSurface>>> dictionary = new Dictionary<Guid, List<Tuple<Polygon3D, TBD.IZoneSurface>>>();

                foreach(TBD.IZoneSurface zoneSurface in zoneSurfaces)
                {
                    TBD.buildingElement buildingElement = zoneSurface.buildingElement;
                    if (buildingElement == null)
                    {
                        continue;
                    }

                    ApertureType apertureType = Query.ApertureType(buildingElement.BEType);
                    if (apertureType == ApertureType.Undefined)
                    {
                        continue;
                    }

                    TBD.Construction construction_TBD = buildingElement.GetConstruction();
                    ApertureConstruction apertureConstruction = construction_TBD.ToSAM_ApertureConstruction(apertureType);
                    int index = apertureConstructions.FindIndex(x => x.Name == apertureConstruction.Name);

                    if (index == -1)
                    {
                        index = apertureConstructions.Count;
                        apertureConstructions.Add(apertureConstruction);
                    }
                    else
                    {
                        // A Tas window/door is two building elements sharing a base name: one
                        // "… -pane", one "… -frame". Each converts to an ApertureConstruction
                        // carrying only its own layer list. Combine the two sides — keeping
                        // whichever side already has layers and filling the empty side from the
                        // just-converted construction — so neither pane nor frame is dropped.
                        ApertureConstruction apertureConstruction_Existing = apertureConstructions[index];

                        List<ConstructionLayer> paneConstructionLayers = apertureConstruction_Existing.HasPaneConstructionLayers() ? apertureConstruction_Existing.PaneConstructionLayers : apertureConstruction.PaneConstructionLayers;
                        List<ConstructionLayer> frameConstructionLayers = apertureConstruction_Existing.HasFrameConstructionLayers() ? apertureConstruction_Existing.FrameConstructionLayers : apertureConstruction.FrameConstructionLayers;

                        apertureConstruction = new ApertureConstruction(apertureConstruction_Existing.Guid, apertureConstruction_Existing.Name, apertureConstruction_Existing.ApertureType, paneConstructionLayers, frameConstructionLayers);
                        apertureConstructions[index] = apertureConstruction;
                    }

                    if(apertureConstruction == null)
                    {
                        continue;
                    }

                    int roomSurfaceIndex_aperture = 0;
                    foreach (TBD.IRoomSurface roomSurface in zoneSurface.RoomSurfaces())
                    {
                        Polygon3D polygon3D = GetOrConvertPolygon(polygonCache, zoneSurface.GUID, roomSurfaceIndex_aperture, roomSurface);
                        roomSurfaceIndex_aperture++;
                        if (polygon3D == null)
                        {
                            continue;
                        }

                        Aperture aperture = Analytical.Query.Apertures(adjacencyCluster, polygon3D.InternalPoint3D(), 1, Tolerance.MacroDistance)?.FirstOrDefault();
                        if(aperture != null)
                        {
                            continue;
                        }

                        if(!dictionary.TryGetValue(apertureConstruction.Guid, out List<Tuple<Polygon3D, TBD.IZoneSurface>> tuples) || tuples == null)
                        {
                            tuples = [];
                            dictionary[apertureConstruction.Guid] = tuples;
                        }

                        tuples.Add(new Tuple<Polygon3D, TBD.IZoneSurface>(polygon3D, zoneSurface));
                    }
                }

                foreach (KeyValuePair<Guid, List<Tuple<Polygon3D, TBD.IZoneSurface>>> keyValuePair in dictionary)
                {
                    if(keyValuePair.Value == null || keyValuePair.Value.Count == 0)
                    {
                        continue;
                    }

                    ApertureConstruction apertureConstruction = apertureConstructions.Find(x => x.Guid == keyValuePair.Key);
                    if(apertureConstruction == null)
                    {
                        continue;
                    }

                    List<Tuple<Polygon3D, TBD.IZoneSurface>> tuples = keyValuePair.Value;

                    // Grouped by the pure, COM-free Query.GroupAperturePolygons rather than in-line: a lone
                    // pane with no coincident frame is a genuine one-member group (not an empty one that
                    // silently drops every stamp below), and the group's seed is captured before anything
                    // is removed, not read back off whatever the shrinking list happens to have at index 0.
                    foreach (List<Tuple<Polygon3D, TBD.IZoneSurface>> tuples_Temp in Query.GroupAperturePolygons(tuples, Tolerance.MacroDistance))
                    {
                        Face3D face3D = null;
                        if (tuples_Temp.Count == 1)
                        {
                            face3D = new Face3D(tuples_Temp[0].Item1);
                        }
                        else
                        {
                            List<Face3D> face3Ds = Geometry.Spatial.Create.Face3Ds(tuples_Temp.ConvertAll(x => x.Item1));
                            if (face3Ds != null && face3Ds.Count != 0)
                            {
                                if (face3Ds.Count > 1)
                                {
                                    face3Ds.Sort((x, y) => y.ExternalEdge2D.GetArea().CompareTo(x.ExternalEdge2D.GetArea()));
                                }

                                face3D = face3Ds.FirstOrDefault();
                            }
                        }

                        Aperture aperture = new Aperture(apertureConstruction, face3D);

                        //TODO: New code added to include Aperture Guid TO BE CHECKED 2023.01.30
                        List<TBD.IZoneSurface> zoneSurfaces_Aperture = tuples_Temp.ConvertAll(x => x.Item2);
                        if (zoneSurfaces_Aperture != null && zoneSurfaces_Aperture.Count != 0)
                        {
                            TBD.IZoneSurface zoneSurface_Pane = null;
                            TBD.IZoneSurface zoneSurface_Frame = null;
                            foreach (TBD.IZoneSurface zoneSurface in zoneSurfaces_Aperture)
                            {
                                TBD.Construction construction = zoneSurface?.buildingElement?.GetConstruction();
                                if (construction == null)
                                {
                                    continue;
                                }

                                string name = construction.name;
                                if (string.IsNullOrWhiteSpace(name))
                                {
                                    continue;
                                }

                                if (name.EndsWith("-pane"))
                                {
                                    zoneSurface_Pane = zoneSurface;
                                }

                                if (name.EndsWith("-frame"))
                                {
                                    zoneSurface_Frame = zoneSurface;
                                }

                                if (zoneSurface_Frame != null && zoneSurface_Pane != null)
                                {
                                    break;
                                }

                            }

                            if (zoneSurfaces_Aperture.Count == 1)
                            {
                                //A single-member group is exactly what its one surface already claims to
                                //be: a lone pane with no separately written frame ring is NOT also its own
                                //frame. (This block only sees singletons since GroupAperturePolygons made a
                                //lone pane a genuine one-member group; the [0] fallbacks in the else below
                                //predate that and would stamp one physical glazing surface as both pane and
                                //frame, and frame-first reference matching then classifies the pane as a
                                //frame.) When the construction name carries no -pane/-frame suffix, take the
                                //part from the surface's own element type instead of fabricating the
                                //missing half.
                                if (zoneSurface_Pane == null && zoneSurface_Frame == null)
                                {
                                    TBD.buildingElement buildingElement_Singleton = zoneSurfaces_Aperture[0]?.buildingElement;
                                    if (buildingElement_Singleton != null)
                                    {
                                        switch ((TBD.BuildingElementType)buildingElement_Singleton.BEType)
                                        {
                                            case TBD.BuildingElementType.GLAZING:
                                                zoneSurface_Pane = zoneSurfaces_Aperture[0];
                                                break;

                                            case TBD.BuildingElementType.FRAMEELEMENT:
                                                zoneSurface_Frame = zoneSurfaces_Aperture[0];
                                                break;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (zoneSurface_Frame == null)
                                {
                                    zoneSurface_Frame = zoneSurfaces_Aperture[0];
                                }

                                if (zoneSurface_Pane == null)
                                {
                                    zoneSurface_Pane = zoneSurfaces_Aperture[0];
                                }
                            }

                            if (zoneSurface_Frame != null)
                            {
                                ApertureParameter apertureParameter = aperture.HasValue(ApertureParameter.FrameZoneSurfaceReference_1) ? ApertureParameter.FrameZoneSurfaceReference_2 : ApertureParameter.FrameZoneSurfaceReference_1;
                                aperture.SetValue(apertureParameter, new ZoneSurfaceReference(zoneSurface_Frame.number, zone.GUID));

                                TBD.buildingElement buildingElement = zoneSurface_Frame.buildingElement;
                                if (buildingElement != null)
                                {
                                    aperture.SetValue(ApertureParameter.FrameBuildingElementGuid, buildingElement.GUID);
                                }
                            }

                            if (zoneSurface_Pane != null)
                            {
                                ApertureParameter apertureParameter = aperture.HasValue(ApertureParameter.PaneZoneSurfaceReference_1) ? ApertureParameter.PaneZoneSurfaceReference_2 : ApertureParameter.PaneZoneSurfaceReference_1;
                                aperture.SetValue(apertureParameter, new ZoneSurfaceReference(zoneSurface_Pane.number, zone.GUID));

                                TBD.buildingElement buildingElement = zoneSurface_Pane.buildingElement;
                                if (buildingElement != null)
                                {
                                    aperture.SetValue(ApertureParameter.PaneBuildingElementGuid, buildingElement.GUID);

                                    // Import the operable aperture types (TBD ApertureType) assigned to
                                    // the pane building element into SAM OpeningProperties, so they
                                    // round-trip back out via Modify.SetApertureTypes on export.
                                    IOpeningProperties openingProperties = Convert.ToSAM_OpeningProperties(buildingElement);
                                    if (openingProperties != null)
                                    {
                                        aperture.SetValue(Analytical.ApertureParameter.OpeningProperties, openingProperties);
                                    }
                                }
                            }
                        }

                        adjacencyCluster.AddAperture(aperture, tolerance_Distance: Tolerance.MacroDistance);

                    }
                }

                if (internalConditions != null)
                {
                    foreach (InternalCondition internalCondition in internalConditions)
                    {
                        adjacencyCluster.AddObject(internalCondition);
                        adjacencyCluster.AddRelation(space, internalCondition);
                    }
                }
            }

            List<TBD.ZoneGroup> zoneGroups = building.ZoneGroups();
            if(zoneGroups != null && zoneGroups.Count != 0)
            {
                foreach(TBD.ZoneGroup zoneGroup in zoneGroups)
                {
                    Zone zone = new Zone(zoneGroup.name);
                    zone.SetValue(Analytical.ZoneParameter.ZoneCategory, zoneGroup.description);
                    zone.SetValue(ZoneParameter.TBDZoneGroup, Query.TBDZoneGroup(zoneGroup.type));

                    adjacencyCluster.AddObject(zone);

                    List<TBD.zone> zones = zoneGroup.Zones();
                    if(zones != null)
                    {
                        foreach(TBD.zone zone_Temp in zones)
                        {
                            if(!dictionary_Space.TryGetValue(zone_Temp.GUID, out Space space))
                            {
                                continue;
                            }

                            adjacencyCluster.AddRelation(zone, space);
                        }
                    }

                }
            }

            //adjacencyCluster.UpdatePanelTypes(groundElevation);

            return adjacencyCluster;
        }

        /// <summary>
        /// Convert a TAS roomSurface perimeter to a SAM Polygon3D, consulting the optional
        /// shared cache first. Key format: <c>zoneSurfaceGuid + "/" + roomSurfaceIndex</c>.
        /// If <paramref name="polygonCache"/> is null, performs the conversion uncached.
        /// </summary>
        private static Polygon3D GetOrConvertPolygon(Dictionary<string, Polygon3D> polygonCache, string zoneSurfaceGuid, int roomSurfaceIndex, TBD.IRoomSurface roomSurface)
        {
            string cacheKey = zoneSurfaceGuid + "/" + roomSurfaceIndex;
            if (polygonCache != null && polygonCache.TryGetValue(cacheKey, out Polygon3D cached))
            {
                return cached;
            }

            Polygon3D polygon3D = Geometry.Tas.Convert.ToSAM(roomSurface?.GetPerimeter()?.GetFace());
            if (polygonCache != null && polygon3D != null)
            {
                polygonCache[cacheKey] = polygon3D;
            }
            return polygon3D;
        }
    }
}
