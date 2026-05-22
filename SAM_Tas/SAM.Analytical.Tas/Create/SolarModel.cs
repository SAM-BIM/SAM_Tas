// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Innovative.SolarCalculator;
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
            // Preserve the TBD timezone on the SAM Location — mirrors
            // ToSAM_AnalyticalModel so downstream nodes see consistent metadata.
            string timeZoneDescription = Core.Query.Description(Core.Query.UTC(building.timeZone));
            if (!string.IsNullOrEmpty(timeZoneDescription))
            {
                location.SetValue(Core.LocationParameter.TimeZone, timeZoneDescription);
            }

            // Resolve a numeric UTC offset (hours) for the SolarTimes sunrise/sunset
            // filter below. TAS hour slots are stored in the building's local time
            // (yearStart.AddDays(dayIndex - 1).AddHours(hour)); evaluating SolarTimes
            // with offset = 0 silently shifts the daytime window by the site's UTC offset.
            int timeZoneOffset = 0;
            Core.UTC uTC = Core.Query.UTC(building.timeZone);
            if (uTC != Core.UTC.Undefined)
            {
                timeZoneOffset = System.Convert.ToInt32(Core.Query.Double(uTC));
            }

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

            // ── DateTime alignment ────────────────────────────────────────────────────
            // SAMAnalytical.SolarSimulation applies a –30 min time-shift so "hour 9"
            // is evaluated using the 8:30 sun position (same convention as Tas EDSL).
            // The result is stored against the SHIFTED DateTime (08:30).
            //
            // To produce identical DateTime keys in both models we must:
            //   1. Apply the same –30 min shift to each TAS hour's storage key.
            //   2. Keep only hours where the sun is above the horizon
            //      (same filter as Weather.SolarCalculator.Modify.Simulate lines 66-78:
            //      skip when sunDirection.Z > 0 OR elevation < minHorizonAngle).
            //
            // Pre-computing the valid shifted DateTimes once (26 shade-days × 24 h = ~624
            // sun-direction queries) avoids repeating the calculation per surface.
            // ─────────────────────────────────────────────────────────────────────────
            const double timeShiftMinutes = -30.0;

            // Use Innovative.SolarCalculator.SolarTimes directly (the same library that
            // SAM.Geometry.SolarCalculator.Query.SunDirection delegates to under the hood).
            // Calling SolarTimes here keeps this method independent of the upstream
            // SAM.Geometry.SolarCalculator.dll signature — the project reference is a
            // HintPath and a stale build of that dll would otherwise break compilation.
            Innovative.Geometry.Angle latitudeAngle = new(location.Latitude);
            Innovative.Geometry.Angle longitudeAngle = new(location.Longitude);

            // The dictionary maps (dayIndex, hour) → shiftedDateTime for the valid timesteps.
            Dictionary<(int day, int hour), DateTime> validShiftedMap = new(dayList.Count * 24);
            foreach (int dayIndex in dayList)
            {
                DateTime dayStart = yearStart.AddDays(dayIndex - 1);
                for (int hour = 0; hour < 24; hour++)
                {
                    DateTime shiftedDT = dayStart.AddHours(hour).AddMinutes(timeShiftMinutes);

                    // Filter to daytime hours using the same sunrise/sunset boundary that
                    // Query.SunDirection enforces internally (returns null outside that range).
                    SolarTimes solarTimes = new(shiftedDT, timeZoneOffset, latitudeAngle, longitudeAngle);
                    if (shiftedDT < solarTimes.Sunrise || shiftedDT > solarTimes.Sunset)
                    {
                        continue;
                    }

                    validShiftedMap[(dayIndex, hour)] = shiftedDT;
                }
            }

            Dictionary<string, LinkedFace3D> dictionary = [];

            int i = 0;
            while (building.GetZone(i) is TBD.zone zone)
            {
                int j = 0;
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

                    string reference = zoneSurface.buildingElement.GUID;
                    if(dictionary.ContainsKey(reference))
                    {
                        continue;
                    }

                    // Pull shade data BEFORE doing geometry conversion. If a surface has
                    // no coverage we skip the (expensive, per-vertex COM-marshalled)
                    // polygon conversion below.
                    //
                    // Coverage is built with shifted DateTimes and filtered to daytime-only
                    // hours — same convention as SAMAnalytical.SolarSimulation so the two
                    // SolarModels share identical DateTime key sets and list lengths.
                    List<Tuple<DateTime, float>> coverage = new(validShiftedMap.Count);
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
                            if (validShiftedMap.TryGetValue((dayIndex, hour), out DateTime shiftedDT))
                            {
                                coverage.Add(Tuple.Create(shiftedDT, value));
                            }

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
                                    LinkedFace3D linkedFace3D = new(faceGuid, face3D, reference);

                                    if (result.Add(linkedFace3D))
                                    {
                                        // Coverage series is kept INTACT — including zeros and
                                        // negative-clip samples — so the per-face DateTime grid
                                        // exactly matches the source TAS shade-day/hour grid.
                                        // Dropping <= 0 entries would discard valid "fully
                                        // unshaded" timesteps and bias error metrics in the
                                        // comparison node toward only-shaded moments.

                                        // Reference = linkedFace3D.Guid.ToString() so coverage
                                        // results can be matched back to faces by the same
                                        // convention used by the SAM solar engine. The TBD
                                        // zoneSurface.GUID is retained on linkedFace3D.Reference
                                        // (external provenance, separate from the matching key).
                                        SolarCoverageSimulationResult solarCoverageSimulationResult = new(
                                            zoneSurface.GUID,
                                            "TAS",
                                            reference,
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
