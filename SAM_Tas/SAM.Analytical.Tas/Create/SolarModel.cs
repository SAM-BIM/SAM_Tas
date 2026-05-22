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
        /// <summary>
        /// Builds a SAM <see cref="SolarModel"/> from a TBD building by reading every
        /// zoneSurface's geometry and TAS-computed shade-proportion data.
        ///
        /// This is the SIMPLE, no-optimisation baseline:
        ///   • iterate every zoneSurface (no surface-type pre-filter);
        ///   • convert the polygon FIRST via the proven Geometry.Tas helper
        ///     (Spatial.Create.Polygon3D — best-fit plane + projection, handles TAS's
        ///     slight non-planarity);
        ///   • pull all shade-proportion data via GetShadeProportion;
        ///   • attach a per-face SolarCoverageSimulationResult to the model.
        ///
        /// No daytime / sunrise-sunset filter is applied — every TAS-reported hour is
        /// stored, including zeros, so the per-face DateTime grid matches the source
        /// shade-day/hour grid 1:1.
        /// </summary>
        public static SolarModel SolarModel(this TBD.Building building)
        {
            if (building is null)
            {
                return null;
            }

            Core.Location location = new(building.name, building.longitude, building.latitude, 0);
            string timeZoneDescription = Core.Query.Description(Core.Query.UTC(building.timeZone));
            if (!string.IsNullOrEmpty(timeZoneDescription))
            {
                location.SetValue(Core.LocationParameter.TimeZone, timeZoneDescription);
            }

            SolarModel result = new(location);

            dynamic dayIndexes = building.GetShadeDays();
            if (dayIndexes == null)
            {
                return result;
            }

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

            // The -30 min shift mirrors SAMAnalytical.SolarSimulation: TAS reports
            // "hour 9" using the 8:30 sun position, so we store coverage against 08:30
            // to align the DateTime keys for comparison.
            const double timeShiftMinutes = -30.0;

            int i = 0;
            while (building.GetZone(i) is TBD.zone zone)
            {
                int j = 0;
                while (zone.GetSurface(j) is TBD.zoneSurface zoneSurface)
                {
                    // External reference for traceability. buildingElement may be null on
                    // certain surfaces (link/null-link), so fall back to zoneSurface.GUID.
                    string reference = zoneSurface.buildingElement?.GUID;
                    if (string.IsNullOrEmpty(reference))
                    {
                        reference = zoneSurface.GUID;
                    }

                    int roomSurfaceIndex = 0;
                    TBD.IRoomSurface roomSurface = zoneSurface.GetRoomSurface(roomSurfaceIndex);
                    while (roomSurface != null)
                    {
                        Polygon3D polygon3D = Geometry.Tas.Convert.ToSAM(roomSurface?.GetPerimeter()?.GetFace());
                        if (polygon3D != null && polygon3D.GetPlane() != null)
                        {
                            Face3D face3D = new(polygon3D);
                            Guid faceGuid = Guid.NewGuid();
                            LinkedFace3D linkedFace3D = new(faceGuid, face3D, reference);

                            if (result.Add(linkedFace3D))
                            {
                                // Pull every TAS-reported shade-proportion value.
                                //
                                // Sentinel handling: TAS returns -1f for hours where it has
                                // no computed shade data (observed by Duncan/EDSL — debug
                                // session showed building.GetShadeProportion(0, 2, 1) ⇒
                                // 24× -1f on a 'SIM_EXT_SLD' surface). These are NOT valid
                                // coverage values in [0, 1] and must not be stored as such;
                                // we drop them so the SolarCoverageSimulationResult only
                                // carries hours where TAS actually has data.
                                //
                                // TODO(EDSL): the (i, j) index convention that yields real
                                // shade data is still under investigation — Duncan emailed
                                // re. the correct API surface. Until then the function may
                                // produce empty coverage lists for many faces.
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
                                        // -1 sentinel ⇒ "no shade data for this hour".
                                        // Anything outside [0, 1] is also rejected as a
                                        // safety net (TAS is documented to emit fractions).
                                        if (value < 0f || value > 1f)
                                        {
                                            hour++;
                                            continue;
                                        }

                                        DateTime shiftedDT = dayStart.AddHours(hour).AddMinutes(timeShiftMinutes);
                                        coverage.Add(Tuple.Create(shiftedDT, value));
                                        hour++;
                                    }
                                }

                                SolarCoverageSimulationResult solarCoverageSimulationResult = new(
                                    zoneSurface.GUID,
                                    "TAS",
                                    reference,
                                    coverage);

                                result.Add(solarCoverageSimulationResult, faceGuid);
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
