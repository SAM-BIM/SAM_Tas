// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas.DirectT3D.Validation.ReadBack;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using TSD;

namespace SAM.Analytical.Tas.DirectT3D.Validation.Results
{
    /// <summary>
    /// The S10 independent result read: the raw TSD result arrays, exactly as TSD.exe returns them, in a fresh process and
    /// read-only (sha256 before and after). It is the reference side of the S10 comparison, so it calls TSD's own COM API
    /// only and nothing of the code under test (no SAM_Tas converter, query or document wrapper; enforced by a source-scan
    /// test). A stub TSD (a refused simulation) is never opened (it hangs TSD.exe).
    /// </summary>
    public static class TsdResultRead
    {
        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;

        /// <summary>The zone arrays S10 reads: the plant loads, the air temperature, and every term of the Tas sensible load breakdown.</summary>
        public static readonly tsdZoneArray[] AnnualZoneArrays =
        {
            tsdZoneArray.heatingLoad, tsdZoneArray.coolingLoad, tsdZoneArray.dryBulbTemp,
            tsdZoneArray.solarGain, tsdZoneArray.lightingGain, tsdZoneArray.occupantSensibleGain, tsdZoneArray.equipmentSensibleGain,
            tsdZoneArray.infVentGain, tsdZoneArray.airMovementGain, tsdZoneArray.buildingHeatTransfer,
            tsdZoneArray.externalConductionOpaque, tsdZoneArray.externalConductionGlazing, tsdZoneArray.AHUGain,
        };

        public static readonly tsdZoneArray[] DesignZoneArrays = { tsdZoneArray.heatingLoad, tsdZoneArray.coolingLoad, tsdZoneArray.dryBulbTemp };

        public static readonly tsdBuildingArray[] BuildingArrays = { tsdBuildingArray.heatingProfile, tsdBuildingArray.coolingProfile };

        public static TsdResultSnapshot Read(string path)
        {
            TsdResultSnapshot result = new TsdResultSnapshot
            {
                Path = path,
                ProcessId = Process.GetCurrentProcess().Id,
                StartedUtc = DateTime.UtcNow.ToString("o", ci),
                Machine = Environment.MachineName,
                ReaderAssembly = typeof(TsdResultRead).Assembly.Location,
            };
            result.ReaderAssemblySha256 = SnapshotJson.Sha256(result.ReaderAssembly);

            if (!File.Exists(path))
            {
                result.Errors.Add("TSD missing: " + path);
                return result;
            }

            result.Bytes = new FileInfo(path).Length;
            if (result.Bytes < TsdReadBack.StubThreshold)
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
                result.FirstDay = Field(() => simulation.firstDay);
                result.LastDay = Field(() => simulation.lastDay);

                BuildingData building = simulation.GetBuildingData();
                if (building == null)
                {
                    result.Errors.Add("TSD has no building data");
                    return result;
                }

                foreach (tsdBuildingArray array in BuildingArrays)
                {
                    result.Building[array.ToString()] = Series(() => building.GetAnnualBuildingResult((int)array));
                }

                // TSD zones count from 1.
                for (int i = 1; i < 10000; i++)
                {
                    ZoneData zone = building.GetZoneData(i);
                    if (zone == null) break;
                    result.Zones.Add(Zone(zone, i, AnnualZoneArrays));
                }

                result.AnnualPeakZoneGains[tsdZoneArray.heatingLoad.ToString()] = Table(() => building.GetPeakZoneGains(new short[] { (short)tsdZoneArray.heatingLoad }));
                result.AnnualPeakZoneGains[tsdZoneArray.coolingLoad.ToString()] = Table(() => building.GetPeakZoneGains(new short[] { (short)tsdZoneArray.coolingLoad }));

                // Design-day data sets count from 0, their zones from 1.
                for (int i = 0; i < 1000; i++)
                {
                    CoolingDesignData data = simulation.GetCoolingDesignData(i);
                    if (data == null) break;
                    TsdDesignDataset dataset = new TsdDesignDataset { Role = "Cooling", Index = i, Name = Text(() => data.name) };
                    for (int j = 1; j < 10000; j++)
                    {
                        ZoneData zone = data.GetZoneData(j);
                        if (zone == null) break;
                        dataset.Zones.Add(Zone(zone, j, DesignZoneArrays));
                    }

                    dataset.PeakZoneGains = Table(() => data.GetPeakZoneGains(new short[] { (short)tsdZoneArray.coolingLoad }));
                    result.DesignDatasets.Add(dataset);
                }

                for (int i = 0; i < 1000; i++)
                {
                    HeatingDesignData data = simulation.GetHeatingDesignData(i);
                    if (data == null) break;
                    TsdDesignDataset dataset = new TsdDesignDataset { Role = "Heating", Index = i, Name = Text(() => data.name) };
                    for (int j = 1; j < 10000; j++)
                    {
                        ZoneData zone = data.GetZoneData(j);
                        if (zone == null) break;
                        dataset.Zones.Add(Zone(zone, j, DesignZoneArrays));
                    }

                    dataset.PeakZoneGains = Table(() => data.GetPeakZoneGains(new short[] { (short)tsdZoneArray.heatingLoad }));
                    result.DesignDatasets.Add(dataset);
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
            if (result.Sha256After != result.Sha256Before)
            {
                result.Errors.Add("the TSD changed during the read-only read (sha256 " + result.Sha256Before + " -> " + result.Sha256After + ")");
            }

            return result;
        }

        private static TsdResultZone Zone(ZoneData zone, int index, tsdZoneArray[] arrays)
        {
            TsdResultZone result = new TsdResultZone
            {
                Index = index,
                Number = Field(() => zone.zoneNumber),
                Name = Text(() => zone.name),
                Guid = Text(() => zone.zoneGUID),
                FloorArea = Field(() => zone.floorArea),
                Volume = Field(() => zone.volume),
            };

            foreach (tsdZoneArray array in arrays)
            {
                result.Arrays[array.ToString()] = Series(() => zone.GetAnnualZoneResult((int)array));
            }

            return result;
        }

        /// <summary>A one-dimensional native array, element by element (<see cref="NativeConvert.Series"/>): never a 0 for a null.</summary>
        private static ResultSeries Series(Func<object> get)
        {
            try
            {
                return NativeConvert.Series(get());
            }
            catch (Exception exception)
            {
                return ResultSeries.Unreadable((exception.InnerException ?? exception).GetType().Name);
            }
        }

        private static NativeTable Table(Func<object> get)
        {
            try
            {
                return NativeConvert.Table(get());
            }
            catch (Exception exception)
            {
                return new NativeTable { Kind = FieldKind.Unreadable, Detail = (exception.InnerException ?? exception).GetType().Name };
            }
        }

        private static NativeField Field(Func<object> get)
        {
            try
            {
                return NativeConvert.Field(get());
            }
            catch (Exception exception)
            {
                return NativeField.Unreadable((exception.InnerException ?? exception).GetType().Name);
            }
        }

        private static string Text(Func<string> get)
        {
            try
            {
                return get();
            }
            catch
            {
                return null;
            }
        }
    }
}
