﻿using SAM.Analytical.Systems;
using SAM.Geometry.Planar;
using System;
using System.Collections.Generic;
using TPD;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Query
    {
        public static ITransform2D Transform2D(this tpdDirection tpdDirection, Point2D location, AnalyticalSystemComponentType analyticalSystemComponentType)
        {
            Point2D location_Temp = location;

            List<ITransform2D> transforms = null;

            Vector2D vector2D = null;

            switch (analyticalSystemComponentType)
            {
                case Analytical.Systems.AnalyticalSystemComponentType.SystemFan:
                    switch(tpdDirection)
                    {
                        case tpdDirection.tpdTopBottom:
                            location_Temp = new Point2D(location_Temp.X + 0.2, location_Temp.Y - 0.2);
                            transforms = new List<ITransform2D>()
                            {
                                    Geometry.Planar.Transform2D.GetRotation(location_Temp, global::System.Math.PI / 2),
                                    Geometry.Planar.Transform2D.GetMirrorX(location_Temp),
                                    Geometry.Planar.Transform2D.GetMirrorY(location_Temp),
                            };
                            return new TransformGroup2D(transforms);


                        case tpdDirection.tpdBottomTop:
                            location_Temp = new Point2D(location_Temp.X + 0.2, location_Temp.Y - 0.2);
                            transforms = new List<ITransform2D>()
                            {
                                    Geometry.Planar.Transform2D.GetRotation(location_Temp, global::System.Math.PI / 2),
                                    Geometry.Planar.Transform2D.GetTranslation(new Vector2D(0, -0.2)),
                            };
                            return new TransformGroup2D(transforms);

                        default:
                            location_Temp = new Point2D(location_Temp.X + 0.3, location_Temp.Y - 0.2);
                            break;
                    }
                    break;

                case Analytical.Systems.AnalyticalSystemComponentType.SystemExchanger:
                case Analytical.Systems.AnalyticalSystemComponentType.SystemDesiccantWheel:
                    location_Temp = new Point2D(location_Temp.X + 0.2, location_Temp.Y - 0.2);
                    vector2D = new Vector2D(0.8, 0);
                    break;

                case Analytical.Systems.AnalyticalSystemComponentType.SystemSpace:
                    location_Temp = new Point2D(location_Temp.X + 0.3, location_Temp.Y - 0.3);
                    vector2D = new Vector2D(0, 0);
                    break;

                case Analytical.Systems.AnalyticalSystemComponentType.SystemDamper:
                    location_Temp = new Point2D(location_Temp.X + 0.2, location_Temp.Y - 0.2);
                    vector2D = new Vector2D(0, 0);
                    break;

                case Analytical.Systems.AnalyticalSystemComponentType.SystemAirJunction:
                    location_Temp = new Point2D(location_Temp.X + 0.1, location_Temp.Y - 0.1);
                    vector2D = new Vector2D(0, 0);
                    break;

                default:
                    location_Temp = new Point2D(location_Temp.X + 0.1, location_Temp.Y - 0.1);
                    vector2D = new Vector2D(0.2, 0);
                    break;
            }


            switch (tpdDirection)
            {
                case tpdDirection.tpdTopBottom:

                    transforms = new List<ITransform2D>()
                    {
                        Geometry.Planar.Transform2D.GetRotation(location_Temp, - global :: System.Math.PI / 2),
                        Geometry.Planar.Transform2D.GetTranslation(vector2D),
                    };
                    return new TransformGroup2D(transforms);

                case tpdDirection.tpdLeftRight:
                    return null;

                case tpdDirection.tpdRightLeft:
                    return Geometry.Planar.Transform2D.GetMirrorY(location_Temp);

                case tpdDirection.tpdBottomTop:
                    //return Geometry.Planar.Transform2D.GetRotation(location_Temp, Math.PI / 2);
                    transforms = new List<ITransform2D>()
                    {
                        Geometry.Planar.Transform2D.GetRotation(location_Temp,  global :: System.Math.PI / 2),
                        //Geometry.Planar.Transform2D.GetTranslation(vector2D),
                    };
                    return new TransformGroup2D(transforms);
            }

            return null;
        }

        public static ITransform2D Transform2D(this ISystemComponent systemComponent)
        {
            if(systemComponent == null)
            {
                return null;
            }

            dynamic @dynamic = systemComponent;

            Point2D location = ((TasPosition)@dynamic.GetPosition())?.ToSAM();

            tpdDirection tpdDirection = (tpdDirection)(int)@dynamic.GetDirection();

            return Transform2D(tpdDirection, location, AnalyticalSystemComponentType(systemComponent));
        }
    }
}