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
            return SolarModel(building, null);
        }

        /// <summary>
        /// SolarModel build with optional shared polygon3D cache. Pass the cache populated by
        /// <c>AdjacencyCluster.ToSAM</c> to avoid re-converting the same roomSurface polygons
        /// over COM. Key format: <c>zoneSurface.GUID + "/" + roomSurfaceIndex</c>.
        /// </summary>
        public static SolarModel SolarModel(this TBD.Building building, Dictionary<string, Polygon3D> polygonCache)
        {
            if (building is null)
            {
                return null;
            }

            Core.Location location = new(building.name, building.longitude, building.latitude, 0);
            SolarModel result = new(location);

            DateTime yearStart = new(building.year, 1, 1);

            // Read only the representative shade days from TAS's calendar (the "yellow days"
            // in TBD's UI — default 15-day step, user-customisable). TAS only computes
            // shading for those days; for any other day, GetShadeProportion returns an
            // on-the-fly interpolated value that's not real data. Iterating only the rep
            // days drops the COM call count ~14× (e.g. 25 days × N surfaces instead of 365).
            // Falls back to the full 1..365 loop if GetShadeDays() returns nothing usable.
            List<int> shadeDays = GetShadeDayNumbers(building);
            if (shadeDays.Count == 0)
            {
                shadeDays = new List<int>(365);
                for (int d = 1; d <= 365; d++) shadeDays.Add(d);
            }

            int i = 0;
            while (building.GetZone(i) is TBD.zone tbdZone)
            {
                int j = 0;
                while (tbdZone.GetSurface(j) is TBD.zoneSurface tbdZoneSurface)
                {
                    if (tbdZoneSurface.type == TBD.SurfaceType.tbdExposed)
                    {
                        // For each rep shade-day, GetShadeProportion returns 24 hourly fractions.
                        List<Tuple<DateTime, double>> coverage = new();
                        foreach (int day in shadeDays)
                        {
                            dynamic shadeProp = building.GetShadeProportion(tbdZone.number, tbdZoneSurface.number, day);
                            if (shadeProp == null)
                            {
                                continue;
                            }

                            DateTime dayStart = yearStart.AddDays(day - 1);
                            int hour = 1;
                            foreach (float value in shadeProp)
                            {
                                if (value >= 0f)
                                {
                                    coverage.Add(Tuple.Create(dayStart.AddHours(hour), (double)value));
                                }
                                hour++;
                            }
                        }

                        if (coverage.Count != 0)
                        {
                            string reference = tbdZoneSurface.buildingElement.GUID;

                            int roomSurfaceIndex = 0;
                            TBD.IRoomSurface roomSurface = tbdZoneSurface.GetRoomSurface(roomSurfaceIndex);
                            while (roomSurface != null)
                            {
                                Polygon3D polygon3D = GetOrConvertPolygon(polygonCache, tbdZoneSurface.GUID, roomSurfaceIndex, roomSurface);
                                if (polygon3D != null && polygon3D.GetPlane() != null)
                                {
                                    Guid guid = Guid.NewGuid();
                                    LinkedFace3D linkedFace3D = new(guid, new(polygon3D), reference);

                                    if (result.Add(linkedFace3D))
                                    {
                                        SolarCoverageSimulationResult solarCoverageSimulationResult = new(
                                            tbdZoneSurface.GUID,
                                            "TAS",
                                            guid.ToString(),
                                            coverage);

                                        result.Add(solarCoverageSimulationResult, guid);
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

        /// <summary>
        /// Returns the list of representative shade-day numbers (the yellow days in
        /// TBD's calendar) for which TAS actually computed shading. Default in TAS is
        /// every 15 days (1, 16, 31, …, 361) but the user can customise the step in
        /// TBD. Wraps <c>Building.GetShadeDays()</c>, which marshals as a VARIANT that
        /// may contain either a SAFEARRAY of int day-numbers or a SAFEARRAY of
        /// <c>DaysShade</c> COM objects (each with a <c>.day</c> property). Returns
        /// an empty list on any failure — caller should fall back to the full 1..365
        /// loop in that case.
        /// </summary>
        private static List<int> GetShadeDayNumbers(TBD.Building building)
        {
            List<int> result = new List<int>();
            if (building == null) return result;

            object shadeDaysRaw;
            try { shadeDaysRaw = building.GetShadeDays(); }
            catch { return result; }
            if (shadeDaysRaw == null) return result;

            if (shadeDaysRaw is System.Array array)
            {
                int lowerBound = array.GetLowerBound(0);
                int upperBound = array.GetUpperBound(0);
                for (int idx = lowerBound; idx <= upperBound; idx++)
                {
                    object item;
                    try { item = array.GetValue(idx); }
                    catch { continue; }
                    if (item == null) continue;

                    // Case 1: SAFEARRAY of integer day-numbers.
                    int? day = TryReadAsInt(item);
                    if (day.HasValue) { result.Add(day.Value); continue; }

                    // Case 2: SAFEARRAY of DaysShade COM objects with a .day property.
                    try
                    {
                        dynamic ds = item;
                        result.Add((int)ds.day);
                    }
                    catch { /* unknown item shape — skip */ }
                }
            }
            else
            {
                // Sometimes a single VARIANT-of-DaysShade COM object instead of an array.
                int? day = TryReadAsInt(shadeDaysRaw);
                if (day.HasValue) { result.Add(day.Value); }
                else
                {
                    try { dynamic ds = shadeDaysRaw; result.Add((int)ds.day); }
                    catch { }
                }
            }

            // De-duplicate and sort, just in case.
            HashSet<int> uniq = new HashSet<int>(result);
            result.Clear();
            foreach (int d in uniq) result.Add(d);
            result.Sort();

            // Sanity-clamp to valid day numbers.
            result.RemoveAll(d => d < 1 || d > 365);
            return result;
        }

        private static int? TryReadAsInt(object item)
        {
            try { return System.Convert.ToInt32(item); }
            catch { return null; }
        }
    }
}
