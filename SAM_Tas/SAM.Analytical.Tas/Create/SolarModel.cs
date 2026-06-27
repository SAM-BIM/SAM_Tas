// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Text;
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

            // building.year can be 0 / unset for some TBDs, which would make new DateTime(year, 1, 1)
            // throw ArgumentOutOfRangeException. The coverage comparison aligns on (month, day, hour)
            // and ignores the year, so the exact value is immaterial — fall back to a valid default
            // (2018, matching SAMAnalytical.SolarSimulation's default _year_) when out of range.
            int year = building.year;
            if (year < 1 || year > 9999)
            {
                year = 2018;
            }

            DateTime yearStart = new(year, 1, 1);

            // Read only the representative shade days from TAS's calendar (the "yellow days"
            // in TBD's UI — default 15-day step, user-customisable). TAS only computes
            // shading for those days; for any other day, GetShadeProportion returns an
            // on-the-fly interpolated value that's not real data. Iterating only the rep
            // days drops the COM call count ~14× (e.g. 25 days × N surfaces instead of 365).
            // Falls back to the full 1..365 loop if GetShadeDays() returns nothing usable.
            List<int> shadeDays = GetShadeDayNumbers(building);
            bool shadeDaysFallback = shadeDays.Count == 0;
            if (shadeDaysFallback)
            {
                shadeDays = new List<int>(365);
                for (int d = 1; d <= 365; d++) shadeDays.Add(d);
            }

            // Optional read-side diagnostic (SAM_DEBUG; surfaced on SAMAnalytical.FromTBD's debugLog
            // alongside the CopyResults log). Records exactly what coverage the import pulled from the
            // TBD per surface — the read half of the option-2 round-trip. Never throws.
            bool logEnabled = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAM_DEBUG"));
            StringBuilder log = logEnabled ? new StringBuilder() : null;
            void Log(string message) { if (log != null) { log.AppendLine(message); } }
            int loggedSurfaces = 0;
            Log("=== Create.SolarModel (TBD -> SAM read-back) ===");
            Log("year=" + year + "   shadeDays: count=" + shadeDays.Count
                + (shadeDaysFallback ? " (FALLBACK 1..365 — GetShadeDays returned nothing)" : "")
                + (shadeDays.Count <= 40 ? " [" + string.Join(",", shadeDays) + "]" : " (first=" + shadeDays[0] + " last=" + shadeDays[shadeDays.Count - 1] + ")"));

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

                        if (logEnabled && coverage.Count != 0)
                        {
                            double sum = 0.0, min = double.MaxValue, max = double.MinValue;
                            foreach (Tuple<DateTime, double> entry in coverage)
                            {
                                sum += entry.Item2;
                                if (entry.Item2 < min) min = entry.Item2;
                                if (entry.Item2 > max) max = entry.Item2;
                            }
                            loggedSurfaces++;
                            Log(string.Format("  zone={0} surface={1} hours={2} mean={3:0.000} min={4:0.000} max={5:0.000}", tbdZone.number, tbdZoneSurface.number, coverage.Count, sum / coverage.Count, min, max));
                        }

                        if (coverage.Count != 0)
                        {
                            // An exposed surface may have shade data but no attached buildingElement;
                            // fall back to the zoneSurface GUID rather than throwing and aborting the
                            // whole import for those models. (CopyResults matches by geometry, so the
                            // exact reference string here is not load-bearing.)
                            string reference = tbdZoneSurface.buildingElement?.GUID;
                            if (string.IsNullOrEmpty(reference))
                            {
                                reference = tbdZoneSurface.GUID;
                            }

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

            if (logEnabled)
            {
                Log("--- Summary --- surfaces with coverage: " + loggedSurfaces + "   total LinkedFace3Ds: " + (result.GetLinkedFace3Ds()?.Count ?? 0));
                try { System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_FromTBD.log"), log.ToString()); } catch { }
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
