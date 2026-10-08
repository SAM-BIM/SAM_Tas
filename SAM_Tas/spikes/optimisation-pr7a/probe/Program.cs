// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

// Native Optimisation PR7a spike probe (not production code, not in SAM_Tas.sln).
//
//   Pr7aProbe inventory <folder>             what the Tas files in <folder> offer (read-only)
//   Pr7aProbe readers <tsd> [<tpd>] [<thr>]  the values SAM_Tas' own readers report for those files
//   Pr7aProbe glazing <tbd> <construction> <targetG> [save]
//                                            explores how a glazing construction's g-value can be reached
//
// Always run it on COPIES of the Tas files.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Pr7aProbe
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            if (args.Length < 2)
            {
                Console.Error.WriteLine("usage: inventory <folder> | readers <tsd> [<tpd>] [<threshold>] | glazing <tbd> <construction> <targetG> [save]");
                return 2;
            }

            switch (args[0])
            {
                case "inventory":
                    Inventory.Run(args[1]);
                    return 0;
                case "readers":
                    Readers.Run(args[1], args.Length > 2 ? args[2] : null, args.Length > 3 ? double.Parse(args[3], CultureInfo.InvariantCulture) : 28.0);
                    return 0;
                case "glazing":
                    Glazing.Run(args[1], args[2], double.Parse(args[3], CultureInfo.InvariantCulture), args.Length > 4 && args[4] == "save");
                    return 0;
                default:
                    Console.Error.WriteLine("unknown mode " + args[0]);
                    return 2;
            }
        }

        internal static string R(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        internal static float[] Floats(object @object)
        {
            if (@object is IEnumerable enumerable)
            {
                return enumerable.Cast<object>().Select(x => System.Convert.ToSingle(x, CultureInfo.InvariantCulture)).ToArray();
            }

            return null;
        }

        internal static void Release(object @object)
        {
            if (@object != null && Marshal.IsComObject(@object))
            {
                Marshal.FinalReleaseComObject(@object);
            }
        }
    }

    internal static class Inventory
    {
        public static void Run(string folder)
        {
            string tbd = Directory.GetFiles(folder, "*.tbd").FirstOrDefault();
            string tpd = Directory.GetFiles(folder, "*.tpd").FirstOrDefault();
            string tsd = Directory.GetFiles(folder, "*.tsd").FirstOrDefault();

            if (tbd != null)
            {
                Tbd(tbd);
            }

            if (tpd != null)
            {
                Tpd(tpd);
            }

            if (tsd != null)
            {
                Tsd(tsd);
            }
        }

        private static string Profile(TBD.profile profile)
        {
            if (profile == null)
            {
                return "null";
            }

            string text = string.Format(CultureInfo.InvariantCulture, "type={0} value={1} factor={2} setback={3} schedule={4} name='{5}' max={6} min={7}",
                profile.type, Program.R(profile.value), Program.R(profile.factor), Program.R(profile.setbackValue),
                profile.schedule?.name ?? "-", profile.name, Program.R(profile.GetExtremeValue(true)), Program.R(profile.GetExtremeValue(false)));

            if (profile.type == TBD.ProfileTypes.ticHourlyProfile || profile.type == TBD.ProfileTypes.ticHourlyFunctionProfile)
            {
                List<string> hours = new List<string>();
                for (int i = 1; i <= 24; i++)
                {
                    hours.Add(Program.R(profile.hourlyValues[i]));
                }

                text += " hourly=[" + string.Join(",", hours) + "]";
            }

            if (profile.type == TBD.ProfileTypes.ticYearlyProfile || profile.type == TBD.ProfileTypes.ticYearlyFunctionProfile)
            {
                float[] yearly = Program.Floats(profile.GetYearlyValues());
                if (yearly != null)
                {
                    text += string.Format(CultureInfo.InvariantCulture, " yearly n={0} distinct=[{1}]", yearly.Length, string.Join(",", yearly.Distinct().OrderBy(x => x).Take(12).Select(x => Program.R(x))));
                }
            }

            if (profile.type == TBD.ProfileTypes.ticFunctionProfile || profile.type == TBD.ProfileTypes.ticYearlyFunctionProfile || profile.type == TBD.ProfileTypes.ticHourlyFunctionProfile)
            {
                text += " function='" + profile.function + "'";
            }

            return text;
        }

        private static void Tbd(string path)
        {
            Console.WriteLine("== TBD " + Path.GetFileName(path));
            TBD.TBDDocument document = new TBD.TBDDocument();
            try
            {
                document.openReadOnly(path);
                TBD.Building building = document.Building;
                Console.WriteLine("building='" + building.name + "'");

                TBD.Calendar calendar = building.GetCalendar();
                for (int i = 1; i <= calendar.GetDayTypeCount(); i++)
                {
                    Console.WriteLine(string.Format("dayType[{0}]='{1}'", i, calendar.dayTypes(i)?.name));
                }

                for (int i = 0; ; i++)
                {
                    TBD.InternalCondition ic = building.GetIC(i);
                    if (ic == null)
                    {
                        break;
                    }

                    List<string> dayTypes = new List<string>();
                    for (int j = 0; ; j++)
                    {
                        TBD.dayType dayType = ic.GetDayType(j);
                        if (dayType == null)
                        {
                            break;
                        }

                        dayTypes.Add(dayType.name);
                    }

                    List<string> zones = new List<string>();
                    for (int j = 0; ; j++)
                    {
                        TBD.zone zone = ic.GetZone(j);
                        if (zone == null)
                        {
                            break;
                        }

                        zones.Add(zone.name);
                    }

                    TBD.Thermostat thermostat = ic.GetThermostat();
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "IC[{0}] name='{1}' description='{2}' upper={3} lower={4} dayTypes=[{5}] zones={6} [{7}]",
                        i, ic.name, ic.description, Program.R(ic.GetUpperLimit()), Program.R(ic.GetLowerLimit()), string.Join("|", dayTypes), zones.Count, string.Join("|", zones.Take(8))));
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  thermostat name='{0}' controlRange={1} proportional={2} radiant={3}",
                        thermostat.name, Program.R(thermostat.controlRange), thermostat.proportionalControl, Program.R(thermostat.radiantProportion)));
                    Console.WriteLine("  UL (cooling) " + Profile(thermostat.GetProfile((int)TBD.Profiles.ticUL)));
                    Console.WriteLine("  LL (heating) " + Profile(thermostat.GetProfile((int)TBD.Profiles.ticLL)));
                    TBD.InternalGain gain = ic.GetInternalGain();
                    Console.WriteLine("  OSG (occupant sensible) " + Profile(gain.GetProfile((int)TBD.Profiles.ticOSG)));
                }

                for (int i = 0; ; i++)
                {
                    TBD.zone zone = building.GetZone(i);
                    if (zone == null)
                    {
                        break;
                    }

                    List<string> ics = new List<string>();
                    for (int j = 0; ; j++)
                    {
                        TBD.InternalCondition ic = zone.GetIC(j);
                        if (ic == null)
                        {
                            break;
                        }

                        ics.Add(ic.name);
                    }

                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "zone[{0}] name='{1}' guid={2} area={3} ics=[{4}]", i, zone.name, zone.GUID, Program.R(zone.floorArea), string.Join("|", ics)));
                }

                Dictionary<string, int> uses = new Dictionary<string, int>();
                for (int i = 0; ; i++)
                {
                    TBD.buildingElement element = building.GetBuildingElement(i);
                    if (element == null)
                    {
                        break;
                    }

                    string construction = element.GetConstruction()?.name ?? "-";
                    Console.WriteLine(string.Format("element[{0}] name='{1}' type={2} construction='{3}'", i, element.name, (TBD.BuildingElementType)element.BEType, construction));
                    uses[construction] = uses.TryGetValue(construction, out int count) ? count + 1 : 1;
                }

                for (int i = 0; ; i++)
                {
                    TBD.Construction construction = building.GetConstruction(i);
                    if (construction == null)
                    {
                        break;
                    }

                    uses.TryGetValue(construction.name, out int used);
                    Console.WriteLine(string.Format("construction[{0}] name='{1}' type={2} elements={3}", i, construction.name, construction.type, used));
                    if (construction.type == TBD.ConstructionTypes.tcdTransparentConstruction)
                    {
                        float[] values = Program.Floats(construction.GetGlazingValues());
                        Console.WriteLine("  glazingValues=[" + string.Join(",", values.Select(x => Program.R(x))) + "]");
                    }

                    for (int j = 1; j < 50; j++)
                    {
                        TBD.material material = null;
                        try
                        {
                            material = construction.materials(j);
                        }
                        catch
                        {
                            break;
                        }

                        if (material == null)
                        {
                            break;
                        }

                        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  material[{0}] name='{1}' type={2} width={3} tau={4} rhoExt={5} rhoInt={6} epsExt={7} epsInt={8} light={9} k={10}",
                            j, material.name, (TBD.MaterialTypes)material.type, Program.R(material.width), Program.R(material.solarTransmittance), Program.R(material.externalSolarReflectance),
                            Program.R(material.internalSolarReflectance), Program.R(material.externalEmissivity), Program.R(material.internalEmissivity), Program.R(material.lightTransmittance), Program.R(material.conductivity)));
                    }
                }
            }
            finally
            {
                document.close();
                Program.Release(document);
            }
        }

        private static void Tpd(string path)
        {
            Console.WriteLine("== TPD " + Path.GetFileName(path));
            TPD.TPDDoc document = new TPD.TPDDoc();
            try
            {
                document.OpenReadOnly(path);
                TPD.EnergyCentre energyCentre = document.EnergyCentre;
                Console.WriteLine("energyCentre='" + energyCentre.Name + "' tsdData=" + energyCentre.GetTSDDataCount());
                for (int i = 1; i <= energyCentre.GetTSDDataCount(); i++)
                {
                    TPD.TSDData tsdData = energyCentre.GetTSDData(i);
                    Console.WriteLine(string.Format("  tsdData[{0}] file='{1}' tsd='{2}' tbd='{3}' building='{4}' days={5}..{6}", i, tsdData.FileName, tsdData.TSDPath, tsdData.TBDPath, tsdData.BuildingName, tsdData.StartDay, tsdData.EndDay));
                }

                for (int i = 1; i <= energyCentre.GetPlantRoomCount(); i++)
                {
                    TPD.PlantRoom plantRoom = energyCentre.GetPlantRoom(i);
                    Console.WriteLine(string.Format("plantRoom[{0}] name='{1}' controllers={2}", i, plantRoom.Name, plantRoom.GetControllerCount()));
                    for (int j = 1; j <= plantRoom.GetControllerCount(); j++)
                    {
                        TPD.PlantController controller = plantRoom.GetController(j);
                        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  controller[{0}] name='{1}' type={2} sensor={3} preset={4} setpoint={5} band={6} min={7} max={8} setbackSetpoint={9} flags={10} schedule={11}",
                            j, controller.Name, controller.ControlType, controller.SensorType, controller.SensorPresetType, Program.R(controller.Setpoint), Program.R(controller.Band),
                            Program.R(controller.Min), Program.R(controller.Max), Program.R(controller.SetbackSetpoint), controller.Flags, controller.GetSchedule()?.Name ?? "-"));
                    }
                }

                TPD.WrResultSet resultSet = (TPD.WrResultSet)energyCentre.GetResultSet(TPD.tpdResultsPeriod.tpdResultsPeriodAnnual, 0, 0, 0, null);
                foreach (TPD.tpdResultVectorType type in new[] { TPD.tpdResultVectorType.tpdConsumption, TPD.tpdResultVectorType.tpdCost, TPD.tpdResultVectorType.tpdCo2, TPD.tpdResultVectorType.tpdUnmetHours })
                {
                    int size = resultSet.GetVectorSize(type);
                    for (int k = 1; k <= size; k++)
                    {
                        TPD.WrResultItem item = (TPD.WrResultItem)resultSet.GetResultItem(type, k);
                        Array values = (Array)item.GetValues();
                        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  stored {0}[{1}] category='{2}' unit='{3}' fuel='{4}' component='{5}' n={6} v0={7}",
                            type, k, item.Category, item.GetUnitString(), item.GetFuelSource()?.Name, item.GetPlantComponentName(), values?.Length, values != null && values.Length > 0 ? Program.R((double)values.GetValue(0)) : "-"));
                    }
                }

                resultSet.Dispose();
            }
            finally
            {
                document.Close();
                Program.Release(document);
            }
        }

        private static void Tsd(string path)
        {
            Console.WriteLine("== TSD " + Path.GetFileName(path));
            TSD.TSDDocument document = new TSD.TSDDocument();
            try
            {
                document.openReadOnly(path);
                TSD.SimulationData simulationData = document.SimulationData;
                TSD.BuildingData buildingData = simulationData.GetBuildingData();
                Console.WriteLine(string.Format("days={0}..{1} zones={2} buildingPath='{3}' building='{4}' guid={5}", simulationData.firstDay, simulationData.lastDay, buildingData.zoneCount, simulationData.buildingPath, buildingData.name, buildingData.GUID));
                for (int i = 1; i <= buildingData.zoneCount; i++)
                {
                    TSD.ZoneData zoneData = buildingData.GetZoneData(i);
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  zoneData[{0}] number={1} name='{2}' guid={3} area={4}", i, zoneData.zoneNumber, zoneData.name, zoneData.zoneGUID, Program.R(zoneData.floorArea)));
                }
            }
            finally
            {
                document.close();
                Program.Release(document);
            }
        }
    }

    internal static class Readers
    {
        public static void Run(string tsd, string tpd, double threshold)
        {
            using (SAM.Core.Tas.SAMTSDDocument document = new SAM.Core.Tas.SAMTSDDocument(tsd, true))
            {
                TSD.BuildingData buildingData = document.TSDDocument.SimulationData.GetBuildingData();

                // As SAM.Analytical.Tas.Convert.ToSAM_AnalyticalModelSimulationResult: the building's annual
                // heating and cooling profiles, summed.
                List<double> heating = SAM.Weather.Tas.Query.AnnualBuildingResult<double>(buildingData, TSD.tsdBuildingArray.heatingProfile);
                List<double> cooling = SAM.Weather.Tas.Query.AnnualBuildingResult<double>(buildingData, TSD.tsdBuildingArray.coolingProfile);
                Console.WriteLine("sam.heating.n=" + heating.Count);
                Console.WriteLine("sam.heating.sum=" + Program.R(heating.Sum()));
                Console.WriteLine("sam.heating.peak=" + Program.R(heating.Max()));
                Console.WriteLine("sam.cooling.sum=" + Program.R(cooling.Sum()));
                Console.WriteLine("sam.cooling.peak=" + Program.R(cooling.Max()));

                // As SAM.Analytical.Tas.Convert.ToSAM (Results.cs): Query.Overheating per zone, every day of the year.
                List<TSD.ZoneData> zoneDatas = new List<TSD.ZoneData>();
                for (int i = 1; i <= buildingData.zoneCount; i++)
                {
                    zoneDatas.Add(buildingData.GetZoneData(i));
                }

                List<Dictionary<TSD.tsdZoneArray, float[]>> series = SAM.Analytical.Tas.Query.ZoneResultSeries(zoneDatas, 1, 365, SAM.Analytical.Tas.Query.OverheatingZoneArrays);
                int worst = -1;
                string worstZone = null;
                int worstOwn = -1;
                for (int i = 0; i < zoneDatas.Count; i++)
                {
                    Dictionary<TSD.tsdZoneArray, float[]> zone = series[i];
                    Dictionary<SAM.Analytical.SpaceSimulationResultParameter, object> overheating = SAM.Analytical.Tas.Query.Overheating(zone[TSD.tsdZoneArray.occupantSensibleGain], zone[TSD.tsdZoneArray.resultantTemp], zone[TSD.tsdZoneArray.dryBulbTemp]);
                    int occupied = (int)overheating[SAM.Analytical.SpaceSimulationResultParameter.OccupiedHours];
                    int hours28 = (int)overheating[SAM.Analytical.SpaceSimulationResultParameter.OccupiedHours28];

                    // The same count with the threshold as a parameter (equals OccupiedHours28 at 28).
                    int own = 0;
                    for (int h = 0; h < 8760; h++)
                    {
                        if (zone[TSD.tsdZoneArray.occupantSensibleGain][h] > 0 && zone[TSD.tsdZoneArray.resultantTemp][h] > threshold)
                        {
                            own++;
                        }
                    }

                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "sam.overheating.zone[{0}] name='{1}' occupied={2} hours25={3} hours28={4} own(>{5})={6} maxRes={7}",
                        i + 1, zoneDatas[i].name, occupied, overheating[SAM.Analytical.SpaceSimulationResultParameter.OccupiedHours25], hours28, Program.R(threshold), own, Program.R(zone[TSD.tsdZoneArray.resultantTemp].Max())));
                    if (occupied > 0 && hours28 > worst)
                    {
                        worst = hours28;
                        worstZone = zoneDatas[i].name;
                    }

                    if (occupied > 0 && own > worstOwn)
                    {
                        worstOwn = own;
                    }
                }

                Console.WriteLine("sam.overheating.worst28=" + worst + " zone='" + worstZone + "'");
                Console.WriteLine("sam.overheating.worstThreshold=" + worstOwn);
            }

            if (string.IsNullOrWhiteSpace(tpd))
            {
                return;
            }

            // As SAM.Core.Tas.TPD.Query.SystemEnergyCentreResults (annual, not per area, not detailed, all energy):
            // the sum of the first value of every result item. That is also SAM.Analytical.Tas.TPD.Modify.CopyResults'
            // AnnualTotalConsumption / AnnualCost / AnnualCO2Emission.
            List<SAM.Analytical.Systems.SystemEnergyCentreResult> results = SAM.Core.Tas.TPD.Query.SystemEnergyCentreResults(tpd, SAM.Core.Tas.TPD.ResultPeriod.Annual,
                new[] { SAM.Analytical.Systems.SystemEnergyCentreDataType.Consumption, SAM.Analytical.Systems.SystemEnergyCentreDataType.Cost, SAM.Analytical.Systems.SystemEnergyCentreDataType.Co2 });
            foreach (SAM.Analytical.Systems.SystemEnergyCentreResult result in results)
            {
                double sum = 0;
                foreach (SAM.Analytical.Systems.SystemEnergyCentreValues values in result.SystemEnergyCentreValues)
                {
                    sum += values[0];
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "sam.plant.{0}.item name='{1}' category='{2}' unit='{3}' v0={4}", result.SystemEnergyCentreDataType, values.Name, values.Category, values.UnitName, Program.R(values[0])));
                }

                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "sam.plant.{0}.sum={1}", result.SystemEnergyCentreDataType, Program.R(sum)));
            }
        }
    }
}
