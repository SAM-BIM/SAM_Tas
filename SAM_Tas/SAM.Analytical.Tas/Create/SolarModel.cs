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
        public static SolarModel SolarModel(this TBD.Building building)
        {
            if (building is null)
            {
                return null;
            }

            Core.Location location = new(building.name, building.longitude, building.latitude, 0);

            SolarModel result = new(location);

            dynamic dayIndexes = building.GetShadeDays();
            if (dayIndexes == null)
            {
                return result;
            }

            // Materialise day indices once: dayIndexes is a COM array we'll iterate per surface.
            List<int> dayList = new();
            foreach (int dayIndex in dayIndexes)
            {
                dayList.Add(dayIndex);
            }

            if (dayList.Count == 0)
            {
                return result;
            }

            DateTime yearStart = new(building.year, 1, 1);

            int i = 1;
            while (building.GetZone(i) is TBD.zone zone)
            {
                int j = 1;
                while (zone.GetSurface(j) is TBD.zoneSurface zoneSurface)
                {
                    // Only exposed (sun-facing) surfaces carry shade data in TAS.
                    // Internal/link/null-link surfaces are skipped before any geometry
                    // conversion — the biggest single saving on typical models where
                    // internals dominate by 3-5x.
                    if (zoneSurface.type != TBD.SurfaceType.tbdExposed)
                    {
                        j++;
                        continue;
                    }

                    // Pull shade data BEFORE doing geometry conversion. If a surface has
                    // no coverage we skip the (expensive, per-vertex COM-marshalled)
                    // polygon conversion below.
                    List<Tuple<DateTime, float>> coverage = new(dayList.Count * 24);
                    foreach (int dayIndex in dayList)
                    {
                        dynamic values = building.GetShadeProportion(i, j, dayIndex);
                        if (values == null)
                        {
                            continue;
                        }

                        DateTime dayStart = yearStart.AddDays(dayIndex - 1);
                        int hour = 0;
                        foreach (float value in values)
                        {
                            coverage.Add(Tuple.Create(dayStart.AddHours(hour), value));
                            hour++;
                        }
                    }

                    if (coverage.Count == 0)
                    {
                        j++;
                        continue;
                    }

                    // Heavy work only happens for surfaces that actually have shade data.
                    // We read TBD polygon vertices inline (one COM call per GetPoint + 3 per
                    // x/y/z) and construct Polygon3D directly. The standard SAM helper
                    // Spatial.Create.Polygon3D fits a plane AND projects every point onto it
                    // *before* the Polygon3D ctor fits the plane again — wasted work for TBD
                    // polygons whose vertices are guaranteed coplanar by TAS.
                    int roomSurfaceIndex = 0;
                    TBD.IRoomSurface roomSurface = zoneSurface.GetRoomSurface(roomSurfaceIndex);
                    while (roomSurface != null)
                    {
                        TBD.Polygon polygon = roomSurface.GetPerimeter()?.GetFace();
                        if (polygon != null)
                        {
                            List<Point3D> point3Ds = new(8);
                            int pointIndex = 0;
                            TBD.TasPoint tasPoint = polygon.GetPoint(pointIndex);
                            while (tasPoint != null)
                            {
                                point3Ds.Add(new Point3D(tasPoint.x, tasPoint.y, tasPoint.z));
                                pointIndex++;
                                tasPoint = polygon.GetPoint(pointIndex);
                            }

                            if (point3Ds.Count >= 3)
                            {
                                Polygon3D polygon3D = new(point3Ds);
                                if (polygon3D.GetPlane() != null)
                                {
                                    Face3D face3D = new(polygon3D);
                                    Guid faceGuid = Guid.NewGuid();
                                    LinkedFace3D linkedFace3D = new(faceGuid, face3D, zoneSurface.GUID);

                                    if (result.Add(linkedFace3D))
                                    {
                                        // Reference = linkedFace3D.Guid.ToString() so coverage
                                        // results can be matched back to faces by the same
                                        // convention used by the SAM solar engine. The TBD
                                        // zoneSurface.GUID is retained on linkedFace3D.Reference
                                        // (external provenance, separate from the matching key).
                                        SolarCoverageSimulationResult solarCoverageSimulationResult = new(
                                            zoneSurface.GUID,
                                            "TAS",
                                            faceGuid.ToString(),
                                            coverage);

                                        result.Add(solarCoverageSimulationResult, faceGuid);
                                    }
                                }
                            }
                        }

                        roomSurfaceIndex++;
                        roomSurface = zoneSurface.GetRoomSurface(roomSurfaceIndex);
                    }

                    j++;
                }
                i++;
            }

            return result;
        }
    }
}
