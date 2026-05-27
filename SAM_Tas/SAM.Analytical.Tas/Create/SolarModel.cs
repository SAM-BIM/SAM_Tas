// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using SAM.Geometry.Object.Spatial;
using SAM.Geometry.SolarCalculator;
using SAM.Geometry.Spatial;

namespace SAM.Analytical.Tas
{
    public static partial class Create
    {
        // Direct C# translation of Duncan/EDSL's reference VBA:
        //
        //   i = 0
        //   While Not tbdBuild.GetZone(i) Is Nothing
        //       Set tbdZone = tbdBuild.GetZone(i)
        //       j = 0
        //       While Not tbdZone.GetSurface(j) Is Nothing
        //           Set tbdZoneSurface = tbdZone.GetSurface(j)
        //           If tbdZoneSurface.Type = tbdExposed Then
        //               shadeProp = tbdBuild.GetShadeProportion(tbdZone.Number, j, 15)
        //           End If
        //           j = j + 1
        //       Wend
        //       i = i + 1
        //   Wend
        //
        public static SolarModel SolarModel(this TBD.Building building)
        {
            if (building is null)
            {
                return null;
            }

            Core.Location location = new(building.name, building.longitude, building.latitude, 0);
            SolarModel result = new(location);

            DateTime yearStart = new(building.year, 1, 1);

            int i = 0;
            while (building.GetZone(i) is TBD.zone tbdZone)
            {
                int j = 0;
                while (tbdZone.GetSurface(j) is TBD.zoneSurface tbdZoneSurface)
                {
                    if (tbdZoneSurface.type == TBD.SurfaceType.tbdExposed)
                    {
                        // shadeProp = tbdBuild.GetShadeProportion(tbdZone.Number, j, day)
                        // For each shade-day, GetShadeProportion returns 24 hourly fractions.
                        List<Tuple<DateTime, float>> coverage = new();
                        for (int day = 1; day <= 365; day++)
                        {
                            dynamic shadeProp = building.GetShadeProportion(tbdZone.number, j, day);
                            if (shadeProp == null)
                            {
                                continue;
                            }

                            DateTime dayStart = yearStart.AddDays(day - 1);
                            int hour = 0;
                            foreach (float value in shadeProp)
                            {
                                if (value >= 0f)
                                {
                                    coverage.Add(Tuple.Create(dayStart.AddHours(hour), value));
                                }

                                hour++;
                            }
                        }

                        if (coverage.Count != 0)
                        {
                            int roomSurfaceIndex = 0;
                            TBD.IRoomSurface roomSurface = tbdZoneSurface.GetRoomSurface(roomSurfaceIndex);
                            while (roomSurface != null)
                            {
                                Polygon3D polygon3D = Geometry.Tas.Convert.ToSAM(roomSurface?.GetPerimeter()?.GetFace());
                                if (polygon3D != null && polygon3D.GetPlane() != null)
                                {
                                    Face3D face3D = new(polygon3D);
                                    Guid faceGuid = Guid.NewGuid();
                                    LinkedFace3D linkedFace3D = new(faceGuid, face3D, tbdZoneSurface.GUID);

                                    if (result.Add(linkedFace3D))
                                    {
                                        SolarCoverageSimulationResult solarCoverageSimulationResult = new(
                                            tbdZoneSurface.GUID,
                                            "TAS",
                                            faceGuid.ToString(),
                                            coverage);

                                        result.Add(solarCoverageSimulationResult, faceGuid);
                                    }
                                }

                                roomSurfaceIndex++;
                                roomSurface = tbdZoneSurface.GetRoomSurface(roomSurfaceIndex);
                            }
                        }
                    }

                    j++;
                }

                i++;
            }

            return result;
        }
    }
}
