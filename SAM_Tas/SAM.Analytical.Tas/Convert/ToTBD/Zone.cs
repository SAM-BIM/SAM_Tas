﻿using SAM.Geometry.Spatial;
using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Convert
    {
        public static TBD.zone ToTBD(this Space space, AnalyticalModel analyticalModel, TBD.Building building)
        {
            if(space == null || building == null)
            {
                return null;
            }

            AdjacencyCluster adjacencyCluster = analyticalModel?.AdjacencyCluster;

            Shell shell = adjacencyCluster.Shell(space);
            if(shell == null)
            {
                return null;
            }

            TBD.zone result = building.AddZone();
            result.name = space.Name;
            result.volume = System.Convert.ToSingle(shell.Volume());
            result.floorArea = System.Convert.ToSingle(shell.Area(0.1));

            TBD.room room = result.AddRoom();

            List<TBD.buildingElement> buildingElements = building.BuildingElements();
            List<TBD.Construction> constructions = building.Constructions();

            List<Panel> panels = adjacencyCluster?.GetPanels(space);
            if(panels != null || panels.Count != 0)
            {
                foreach(Panel panel in panels)
                {
                    string name_Panel = panel.Name;
                    if(string.IsNullOrWhiteSpace(name_Panel))
                    {
                        continue;
                    }

                    Face3D face3D_Panel = panel.GetFace3D(true);
                    if (face3D_Panel == null)
                    {
                        continue;
                    }

                    TBD.zoneSurface zoneSurface_Panel = result.AddSurface();
                    zoneSurface_Panel.orientation = System.Convert.ToSingle(Geometry.Spatial.Query.Azimuth(panel, Vector3D.WorldY));
                    zoneSurface_Panel.inclination = System.Convert.ToSingle(Geometry.Spatial.Query.Tilt(panel));
                    zoneSurface_Panel.area = System.Convert.ToSingle(face3D_Panel.GetArea());

                    TBD.RoomSurface roomSurface_Panel = room.AddSurface();
                    roomSurface_Panel.area = zoneSurface_Panel.area;
                    roomSurface_Panel.zoneSurface = zoneSurface_Panel;

                    TBD.Perimeter perimeter_Panel = Geometry.Tas.Convert.ToTBD(face3D_Panel, roomSurface_Panel);
                    if(perimeter_Panel == null)
                    {
                        continue;
                    }

                    PanelType panelType = panel.PanelType;

                    TBD.buildingElement buildingElement_Panel = buildingElements.Find(x => x.name == name_Panel);
                    if (buildingElement_Panel == null)
                    {
                        TBD.Construction construction_TBD = null;

                        Construction construction = panel.Construction;
                        if (construction != null)
                        {
                            construction_TBD = constructions.Find(x => x.name == construction.Name);
                            if (construction_TBD == null)
                            {
                                construction_TBD = building.AddConstruction(null);
                                construction_TBD.name = construction.Name;

                                List<ConstructionLayer> constructionLayers = construction.ConstructionLayers;
                                if (constructionLayers != null && constructionLayers.Count != 0)
                                {
                                    int index = 1;
                                    foreach (ConstructionLayer constructionLayer in constructionLayers)
                                    {
                                        Core.Material material = analyticalModel?.MaterialLibrary?.GetMaterial(constructionLayer.Name) as Core.Material;
                                        if (material == null)
                                        {
                                            continue;
                                        }

                                        TBD.material material_TBD = construction_TBD.AddMaterial(material);
                                        if (material_TBD != null)
                                        {
                                            material_TBD.width = System.Convert.ToSingle(constructionLayer.Thickness);
                                            construction_TBD.materialWidth[index] = System.Convert.ToSingle(constructionLayer.Thickness);
                                            index++;
                                        }
                                    }
                                }

                                constructions.Add(construction_TBD);
                            }

                            if (panelType == PanelType.Undefined && construction != null)
                            {
                                panelType = construction.PanelType();
                                if (panelType == PanelType.Undefined && construction.TryGetValue(ConstructionParameter.DefaultPanelType, out string panelTypeString))
                                {
                                    panelType = Core.Query.Enum<PanelType>(panelTypeString);
                                }
                            }
                        }

                        buildingElement_Panel = building.AddBuildingElement();
                        buildingElement_Panel.name = name_Panel;
                        buildingElement_Panel.colour = Core.Convert.ToUint(Analytical.Query.Color(panelType));
                        buildingElement_Panel.BEType = Query.BEType(panelType.Text());
                        buildingElement_Panel.AssignConstruction(construction_TBD);
                        buildingElements.Add(buildingElement_Panel);
                    }

                    if (buildingElement_Panel != null)
                    {
                        zoneSurface_Panel.buildingElement = buildingElement_Panel;
                    }

                    zoneSurface_Panel.type = Query.SurfaceType(panelType);

                    List<Aperture> apertures = panel.Apertures;
                    if(apertures != null && apertures.Count != 0)
                    {
                        foreach(Aperture aperture in apertures)
                        {
                            string name_Aperture = aperture.Name;
                            if (string.IsNullOrWhiteSpace(name_Aperture))
                            {
                                continue;
                            }

                            Face3D face3D_Aperture = aperture.Face3D;
                            if (face3D_Aperture == null)
                            {
                                continue;
                            }

                            TBD.zoneSurface zoneSurface_Aperture = result.AddSurface();
                            zoneSurface_Aperture.orientation = zoneSurface_Panel.orientation;
                            zoneSurface_Aperture.inclination = zoneSurface_Panel.inclination;
                            zoneSurface_Aperture.area = System.Convert.ToSingle(face3D_Aperture.GetArea());

                            zoneSurface_Aperture.type = zoneSurface_Panel.type;

                            TBD.RoomSurface roomSurface_Aperture = room.AddSurface();
                            roomSurface_Aperture.area = zoneSurface_Aperture.area;
                            roomSurface_Aperture.zoneSurface = zoneSurface_Aperture;

                            TBD.Perimeter perimeter_Aperture = Geometry.Tas.Convert.ToTBD(face3D_Aperture, roomSurface_Aperture);
                            if (perimeter_Aperture == null)
                            {
                                continue;
                            }

                            TBD.buildingElement buildingElement_Aperture = buildingElements.Find(x => x.name == name_Aperture);
                            if (buildingElement_Aperture == null)
                            {
                                TBD.Construction construction_TBD = null;

                                ApertureConstruction apertureConstruction = aperture.ApertureConstruction;
                                if (apertureConstruction != null)
                                {
                                    construction_TBD = constructions.Find(x => x.name == apertureConstruction.Name);
                                    if (construction_TBD == null)
                                    {
                                        construction_TBD = building.AddConstruction(null);
                                        construction_TBD.name = apertureConstruction.Name;

                                        List<ConstructionLayer> constructionLayers = apertureConstruction.PaneConstructionLayers;
                                        if (constructionLayers != null && constructionLayers.Count != 0)
                                        {
                                            int index = 1;
                                            foreach (ConstructionLayer constructionLayer in constructionLayers)
                                            {
                                                Core.Material material = analyticalModel?.MaterialLibrary?.GetMaterial(constructionLayer.Name) as Core.Material;
                                                if (material == null)
                                                {
                                                    continue;
                                                }

                                                TBD.material material_TBD = construction_TBD.AddMaterial(material);
                                                if (material_TBD != null)
                                                {
                                                    material_TBD.width = System.Convert.ToSingle(constructionLayer.Thickness);
                                                    construction_TBD.materialWidth[index] = System.Convert.ToSingle(constructionLayer.Thickness);
                                                    index++;
                                                }
                                            }
                                        }

                                        constructions.Add(construction_TBD);
                                    }
                                }

                                ApertureType apertureType = aperture.ApertureType;

                                buildingElement_Panel = building.AddBuildingElement();
                                buildingElement_Panel.name = name_Aperture;
                                buildingElement_Panel.colour = Core.Convert.ToUint(Analytical.Query.Color(apertureType));
                                buildingElement_Panel.BEType = Query.BEType(apertureType, false);
                                buildingElement_Panel.AssignConstruction(construction_TBD);
                                buildingElements.Add(buildingElement_Panel);
                            }

                            if (buildingElement_Panel != null)
                            {
                                zoneSurface_Panel.buildingElement = buildingElement_Panel;
                            }


                        }
                    }
                }
            }

            return result;
        }
    }
}