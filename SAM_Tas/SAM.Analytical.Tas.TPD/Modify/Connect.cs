﻿using SAM.Analytical.Systems;
using SAM.Core;
using SAM.Core.Systems;
using SAM.Geometry.Planar;
using SAM.Geometry.Systems;
using System.Collections.Generic;
using System.Linq;
using TPD;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Modify
    {
        public static List<DisplaySystemConnection> Connect(this SystemPlantRoom systemPlantRoom, global::TPD.System system)
        {
            if (systemPlantRoom == null || system == null)
            {
                return null;
            }

            string reference_System = (system as dynamic).GUID;

            AirSystem airSystem = systemPlantRoom.Find<AirSystem>(x => x?.Reference() == reference_System);
            if(airSystem == null)
            {
                return null;
            }

            List<global::TPD.SystemComponent> systemComponents = Query.SystemComponents<global::TPD.SystemComponent>(system, true);
            if(systemComponents == null || systemComponents.Count == 0)
            {
                return null;
            }

            List<DisplaySystemConnection> result = new List<DisplaySystemConnection>();
            foreach (global::TPD.SystemComponent systemComponent in systemComponents)
            {
                if(systemComponent == null)
                {
                    continue;
                }

                string reference_1 = (systemComponent as dynamic).GUID;

                Core.Systems.ISystemComponent systemComponent_1 = systemPlantRoom.Find<Core.Systems.ISystemComponent>(x => x.Reference() == reference_1);

                foreach (Direction direction in new Direction[] { Direction.In, Direction.Out})
                {
                    List<Duct> ducts = Query.Ducts(systemComponent, direction);
                    if (ducts != null)
                    {
                        foreach (Duct duct in ducts)
                        {
                            string reference_2 = (direction == Direction.In ? duct.GetUpstreamComponent() as dynamic : duct.GetDownstreamComponent() as dynamic)?.GUID;

                            Core.Systems.ISystemComponent systemComponent_2 = systemPlantRoom.Find<Core.Systems.ISystemComponent>(x => x.Reference() == reference_2);
                            if (systemComponent_2 == null)
                            {
                                continue;
                            }

                            if(!Geometry.Systems.Query.TryGetIndexes(systemPlantRoom, systemComponent_1, systemComponent_2, out int index_1, out int index_2, new SystemType(airSystem), direction))
                            {
                                continue;
                            }

                            systemPlantRoom.Connect(systemComponent_1, systemComponent_2, airSystem, index_1, index_2);

                            if(systemComponent_1 is IDisplaySystemObject<SystemGeometryInstance> && systemComponent_2 is IDisplaySystemObject<SystemGeometryInstance>)
                            {
                                Point2D point2D_1 = (systemComponent_1 as dynamic).SystemGeometry.GetPoint2D(index_1);
                                if(point2D_1 == null)
                                {
                                    continue;
                                }

                                Point2D point2D_2 = (systemComponent_2 as dynamic).SystemGeometry.GetPoint2D(index_2);
                                if(point2D_2 == null)
                                {
                                    continue;
                                }

                                List<Point2D> point2Ds = Query.Point2Ds(duct);
                                if (point2Ds == null || point2Ds.Count == 0)
                                {
                                    point2Ds = new List<Point2D>() { point2D_1, point2D_2 };
                                }
                                else
                                {
                                    if (point2D_1.Distance(point2Ds.First()) + point2D_2.Distance(point2Ds.Last()) < point2D_1.Distance(point2Ds.Last()) + point2D_2.Distance(point2Ds.First()))
                                    {
                                        point2Ds.Insert(0, point2D_1);
                                        point2Ds.Add(point2D_2);
                                    }
                                    else
                                    {
                                        point2Ds.Insert(0, point2D_2);
                                        point2Ds.Add(point2D_1);
                                    }

                                }

                                if (point2Ds == null || point2Ds.Count < 2)
                                {
                                    continue;
                                }

                                DisplaySystemConnection displaySystemConnection = new DisplaySystemConnection(new SystemConnection(airSystem, systemComponent_1, index_1, systemComponent_2, index_2), point2Ds?.ToArray());
                                if (displaySystemConnection != null)
                                {
                                    systemPlantRoom.Connect(systemComponent_1, displaySystemConnection);
                                    systemPlantRoom.Connect(systemComponent_2, displaySystemConnection);
                                    result.Add(displaySystemConnection);
                                }
                            }
                        }
                    }
                }
            }

            return result;
        }
    }
}