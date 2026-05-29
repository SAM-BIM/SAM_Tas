using SAM.Core.Tas;
using System;
using System.Collections.Generic;
using SAM.Geometry.Spatial;
using SAM.Geometry.SolarCalculator;

namespace SAM.Analytical.Tas
{
    public static partial class Modify
    {
        public static bool UpdateShading(string path_TBD, AnalyticalModel analyticalModel, double tolerance = Core.Tolerance.Distance)
        {
            if(analyticalModel == null || string.IsNullOrWhiteSpace(path_TBD))
            {
                return false;
            }

            bool result = false;
            using (SAMTBDDocument sAMTBDDocument = new (path_TBD))
            {
                result = UpdateShading(sAMTBDDocument?.TBDDocument, analyticalModel, tolerance);
                if (result)
                {
                    sAMTBDDocument.Save();
                }
            }

            return result;
        }

        public static bool UpdateShading(this TBD.TBDDocument tBDDocument, AnalyticalModel analyticalModel, double tolerance = Core.Tolerance.Distance)
        {
            if(tBDDocument == null || analyticalModel == null)
            {
                return false;
            }

            return UpdateShading(tBDDocument?.Building, analyticalModel, tolerance);
        }

        public static bool UpdateShading(this TBD.Building building, AnalyticalModel analyticalModel, double tolerance = Core.Tolerance.Distance)
        {
            if(building == null || analyticalModel == null)
            {
                return false;
            }

            List<TBD.zone> zones = building.Zones();
            if (zones == null)
            {
                return false;
            }

            List<Panel> panels = analyticalModel.GetPanels();
            if (panels == null)
            {
                return false;
            }

            TBD.WeatherYear weatherYear = building?.GetWeatherYear();
            if(weatherYear == null)
            {
                return false;
            }

            List<double> globalSolarRadiations = Weather.Tas.Query.AnnualParameter<double>(weatherYear, Weather.WeatherDataType.GlobalSolarRadiation);

            List<Tuple<Face3D, Point3D, BoundingBox3D, Core.SolarCalculator.ISolarSimulationResult>> tuples_solarSimulationResult = [];
            foreach (Panel panel in panels)
            {
                List<Core.SolarCalculator.ISolarSimulationResult> solarSimulationResults = analyticalModel.GetResults<Core.SolarCalculator.ISolarSimulationResult>(panel);
                if (solarSimulationResults == null || solarSimulationResults.Count == 0)
                {
                    continue;
                }

                Face3D face3D = panel.GetFace3D(true);
                if (face3D == null || !face3D.IsValid())
                {
                    continue;
                }

                BoundingBox3D boundingBox3D = face3D.GetBoundingBox();
                Point3D point3D = face3D.GetInternalPoint3D(tolerance);

                foreach (Core.SolarCalculator.ISolarSimulationResult solarSimulationResult in solarSimulationResults)
                {
                    tuples_solarSimulationResult.Add(new Tuple<Face3D, Point3D, BoundingBox3D, Core.SolarCalculator.ISolarSimulationResult>(face3D, point3D, boundingBox3D, solarSimulationResult));
                }

                List<Aperture> apertures = panel.Apertures;
                if (apertures != null && apertures.Count != 0)
                {
                    foreach (Aperture aperture in apertures)
                    {
                        Face3D face3D_Aperture = aperture.Face3D;
                        if (face3D_Aperture == null || !face3D_Aperture.IsValid())
                        {
                            continue;
                        }

                        BoundingBox3D boundingBox3D_Aperture = face3D_Aperture.GetBoundingBox();
                        Point3D point3D_Aperture = face3D_Aperture.GetInternalPoint3D(tolerance);

                        foreach (Core.SolarCalculator.ISolarSimulationResult solarSimulationResult in solarSimulationResults)
                        {
                            tuples_solarSimulationResult.Add(new Tuple<Face3D, Point3D, BoundingBox3D, Core.SolarCalculator.ISolarSimulationResult>(face3D_Aperture, point3D_Aperture, boundingBox3D, solarSimulationResult));
                        }
                    }
                }
            }

            if(tuples_solarSimulationResult == null || tuples_solarSimulationResult.Count == 0)
            {
                return false;
            }

            List<Tuple<Face3D, BoundingBox3D, TBD.IZoneSurface>> tuples_ZoneSurfaces = [];
            foreach (TBD.zone zone in zones)
            {
                List<TBD.IZoneSurface> zoneSurfaces_Zone =  zone?.ZoneSurfaces();
                if(zoneSurfaces_Zone == null)
                {
                    continue;
                }

                foreach(TBD.IZoneSurface zoneSurface in zoneSurfaces_Zone)
                {
                    List<TBD.IRoomSurface> roomSurfaces = zoneSurface.RoomSurfaces();
                    if(roomSurfaces == null)
                    {
                        continue;
                    }

                    foreach(TBD.IRoomSurface roomSurface in roomSurfaces)
                    {
                        Face3D face3D = Geometry.Tas.Convert.ToSAM(roomSurface?.GetPerimeter());
                        if(face3D == null || !face3D.IsValid())
                        {
                            continue;
                        }

                        tuples_ZoneSurfaces.Add(new Tuple<Face3D, BoundingBox3D, TBD.IZoneSurface>(face3D, face3D.GetBoundingBox(), zoneSurface));
                    }
                }
            }

            if(tuples_ZoneSurfaces == null || tuples_ZoneSurfaces.Count == 0)
            {
                return false;
            }

            building.ClearShadingData();

            List<TBD.DaysShade> daysShades = [];
            foreach(Tuple<Face3D, Point3D, BoundingBox3D, Core.SolarCalculator.ISolarSimulationResult> tuple in tuples_solarSimulationResult)
            {
                List<DateTime> dateTimes = tuple?.Item4?.DateTimes;
                if(dateTimes == null || dateTimes.Count == 0)
                {
                    continue;
                }

                for (int i = dateTimes.Count - 1; i >= 0; i--)
                {
                    int index = Core.Query.HourOfYear(dateTimes[i]);
                    if (index >= 0 && index < globalSolarRadiations.Count)
                    {
                        if (globalSolarRadiations[index - 1] < 10)
                        {
                            dateTimes.RemoveAt(i);
                        }
                    }
                }

                if(dateTimes.Count == 0)
                {
                    continue;
                }

                Core.SolarCalculator.ISolarSimulationResult solarSimulationResult = null;
                if(tuple.Item4 is SolarFaceSimulationResult solarFaceSimulationResult)
                {
                    solarSimulationResult = new SolarFaceSimulationResult(solarFaceSimulationResult, dateTimes);
                }
                else if (tuple.Item4 is SolarCoverageSimulationResult solarCoverageSimulationResult)
                {
                    solarSimulationResult = new SolarCoverageSimulationResult(solarCoverageSimulationResult, dateTimes);
                }
                else
                {
                    continue;
                }

                List<Tuple<Face3D, BoundingBox3D, TBD.IZoneSurface>> tuples_ZoneSurfaces_BoundingBox = tuples_ZoneSurfaces.FindAll(x => x.Item2.InRange(tuple.Item3, Core.Tolerance.MacroDistance));
                List<Tuple<Face3D, BoundingBox3D, TBD.IZoneSurface>> tuples_ZoneSurfaces_Temp = tuples_ZoneSurfaces_BoundingBox?.FindAll(x => x.Item1.On(tuple.Item2, Core.Tolerance.MacroDistance));
                if(tuples_ZoneSurfaces_Temp == null || tuples_ZoneSurfaces_Temp.Count == 0)
                {
                    continue;
                }

                foreach(Tuple<Face3D, BoundingBox3D, TBD.IZoneSurface> tuple_ZoneSurface in tuples_ZoneSurfaces_Temp)
                {
                    UpdateSurfaceShades(building, daysShades, (TBD.zoneSurface)tuple_ZoneSurface.Item3, solarSimulationResult);
                }
            }

            return true;
        }

        //public static bool UpdateShading(this AnalyticalModel analyticalModel, TBD.Building building, double tolerance = Core.Tolerance.Distance)
        //{
        //    if (building == null || analyticalModel == null)
        //    {
        //        return false;
        //    }

        //    List<TBD.zone> zones = building.Zones();
        //    if (zones == null)
        //    {
        //        return false;
        //    }

        //    List<Tuple<Face3D, BoundingBox3D, TBD.IZoneSurface, int, int>> tuples_ZoneSurfaces = new List<Tuple<Face3D, BoundingBox3D, TBD.IZoneSurface, int, int>>();
            
        //    int index_Zone = 0;
        //    foreach (TBD.zone zone in zones)
        //    {
        //        List<TBD.IZoneSurface> zoneSurfaces_Zone = zone?.ZoneSurfaces();
        //        if (zoneSurfaces_Zone == null)
        //        {
        //            continue;
        //        }

        //        int index_Surface = 0;
        //        foreach (TBD.IZoneSurface zoneSurface in zoneSurfaces_Zone)
        //        {
        //            List<TBD.IRoomSurface> roomSurfaces = zoneSurface.RoomSurfaces();
        //            if (roomSurfaces == null)
        //            {
        //                continue;
        //            }

        //            foreach (TBD.IRoomSurface roomSurface in roomSurfaces)
        //            {
        //                Face3D face3D = Geometry.Tas.Convert.ToSAM(roomSurface?.GetPerimeter());
        //                if (face3D == null || !face3D.IsValid())
        //                {
        //                    continue;
        //                }

        //                tuples_ZoneSurfaces.Add(new Tuple<Face3D, BoundingBox3D, TBD.IZoneSurface, int, int>(face3D, face3D.GetBoundingBox(), zoneSurface, index_Zone, index_Surface));
        //            }

        //            index_Surface++;
        //        }
        //        index_Zone++;
        //    }

        //    if (tuples_ZoneSurfaces == null || tuples_ZoneSurfaces.Count == 0)
        //    {
        //        return false;
        //    }

        //    List<Panel> panels = analyticalModel.GetPanels();
        //    if (panels == null)
        //    {
        //        return false;
        //    }

        //    List<Tuple<Face3D, Point3D, BoundingBox3D, Panel>> tuples_Panel = new List<Tuple<Face3D, Point3D, BoundingBox3D, Panel>>();
        //    foreach (Panel panel in panels)
        //    {
        //        Face3D face3D = panel.Face3D;
        //        if (face3D == null || !face3D.IsValid())
        //        {
        //            continue;
        //        }

        //        BoundingBox3D boundingBox3D = face3D.GetBoundingBox();
        //        Point3D point3D = face3D.GetInternalPoint3D(tolerance);

        //    }

        //    throw new NotImplementedException();
        //}
  }
}