// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using TSD;

namespace SAM.Analytical.Tas.DirectT3D.Validation.ReadBack
{
    /// <summary>
    /// The P-ALIGN result read: the annual building weather and the annual zone gain, infiltration and load arrays of
    /// one TSD, exactly as TSD.exe returns them (lower bound and every element), in a fresh process and read-only. A stub
    /// TSD (a refused simulation) is never opened (it hangs TSD.exe): the caller checks the size first.
    /// </summary>
    public static class TsdReadBack
    {
        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;

        public const long StubThreshold = 1024;

        private static readonly tsdZoneArray[] zoneArrays = { tsdZoneArray.lightingGain, tsdZoneArray.equipmentSensibleGain, tsdZoneArray.occupantSensibleGain, tsdZoneArray.infiltration, tsdZoneArray.infVentGain, tsdZoneArray.ventilation, tsdZoneArray.heatingLoad, tsdZoneArray.coolingLoad, tsdZoneArray.dryBulbTemp };
        private static readonly tsdBuildingArray[] buildingArrays = { tsdBuildingArray.externalTemperature, tsdBuildingArray.externalHumidity, tsdBuildingArray.windSpeed };

        public static TsdSnapshot Read(string path)
        {
            TsdSnapshot result = new TsdSnapshot { Path = path, ProcessId = Process.GetCurrentProcess().Id, StartedUtc = DateTime.UtcNow.ToString("o", ci) };
            if (!File.Exists(path))
            {
                result.Errors.Add("TSD missing: " + path);
                return result;
            }

            result.Bytes = new FileInfo(path).Length;
            if (result.Bytes < StubThreshold)
            {
                result.Errors.Add("TSD is a " + result.Bytes + "-byte stub (a refused simulation); it is not opened");
                return result;
            }

            result.Sha256Before = SnapshotJson.Sha256(path);
            TSDDocument document = new TSDDocument();
            try
            {
                if (!document.openReadOnly(path))
                {
                    result.Errors.Add("TSD openReadOnly returned false");
                    return result;
                }

                SimulationData simulation = document.SimulationData;
                result.FirstDay = simulation.firstDay;
                result.LastDay = simulation.lastDay;
                BuildingData building = simulation.GetBuildingData();
                foreach (tsdBuildingArray array in buildingArrays)
                {
                    result.Building[array.ToString()] = Series(() => building.GetAnnualBuildingResult((int)array));
                }

                // TSD zones count from 1.
                for (int i = 1; i < 10000; i++)
                {
                    ZoneData zone = building.GetZoneData(i);
                    if (zone == null) break;
                    TsdZone item = new TsdZone { Index = i, Name = zone.name, Guid = zone.zoneGUID };
                    foreach (tsdZoneArray array in zoneArrays)
                    {
                        item.Arrays[array.ToString()] = Series(() => zone.GetAnnualZoneResult((int)array));
                    }

                    result.Zones.Add(item);
                }
            }
            catch (Exception exception)
            {
                result.Errors.Add("TSD read failed: " + (exception.InnerException ?? exception).GetType().Name + ": " + (exception.InnerException ?? exception).Message);
            }
            finally
            {
                try { document.close(); } catch { }
                Marshal.FinalReleaseComObject(document);
            }

            result.Sha256After = SnapshotJson.Sha256(path);
            return result;
        }

        private static NativeSeries Series(Func<object> get)
        {
            object value;
            try
            {
                value = get();
            }
            catch (Exception exception)
            {
                return NativeSeries.Unreadable((exception.InnerException ?? exception).GetType().Name);
            }

            if (!(value is Array array) || array.Rank != 1)
            {
                return value == null ? NativeSeries.Absent("null") : NativeSeries.Unreadable("not a one-dimensional array");
            }

            double[] values = new double[array.Length];
            int lowerBound = array.GetLowerBound(0);
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = System.Convert.ToDouble(array.GetValue(lowerBound + i), ci);
            }

            return NativeSeries.Of(values, lowerBound);
        }
    }
}
