// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace SAM.Analytical.Tas.GenOpt
{
    public static partial class Query
    {
        /// <summary>
        /// Reads what a Tas project folder offers to the "tas-model" engine (licensed Tas, COM; every document is opened
        /// read-only, closed and released): the TBD's internal conditions (thermostat profiles) and the transparent
        /// constructions its building elements use (found through the elements, past the <c>GetConstruction(i)</c> null
        /// gaps), the TSD's annual heating and cooling demand, and the TPD's plant rooms, controllers and stored annual
        /// results. The folder must hold at most one TBD, TSD and TPD (the files TasGenExecute discovers).
        /// </summary>
        /// <param name="projectFolder">The Tas project folder. Only read.</param>
        public static TasModelInventory TasModelInventory(string projectFolder)
        {
            if (string.IsNullOrWhiteSpace(projectFolder) || !Directory.Exists(projectFolder))
            {
                throw new DirectoryNotFoundException("The Tas project folder does not exist: '" + projectFolder + "'.");
            }

            // Tas opens files in its own server process, whose working folder is not ours: always pass full paths.
            projectFolder = Path.GetFullPath(projectFolder);
            string tbd = SingleFile(projectFolder, ".tbd");
            string tsd = SingleFile(projectFolder, ".tsd");
            string tpd = SingleFile(projectFolder, ".tpd");

            List<TasInternalConditionInfo> internalConditions = new List<TasInternalConditionInfo>();
            List<TasGlazingConstructionInfo> glazingConstructions = new List<TasGlazingConstructionInfo>();
            if (tbd != null)
            {
                ReadTbd(tbd, internalConditions, glazingConstructions);
            }

            List<TasPlantRoomInfo> plantRooms = new List<TasPlantRoomInfo>();
            TasModelInventory values = new TasModelInventory(null, null, null);
            if (tsd != null)
            {
                ReadTsd(tsd, values);
            }

            if (tpd != null)
            {
                ReadTpd(tpd, values, plantRooms);
            }

            TasModelInventory result = new TasModelInventory(Path.GetFileName(tbd), Path.GetFileName(tsd), Path.GetFileName(tpd), internalConditions, glazingConstructions, plantRooms)
            {
                TpdCostUnit = values.TpdCostUnit,
                HeatingDemand = values.HeatingDemand,
                CoolingDemand = values.CoolingDemand,
                PlantEnergy = values.PlantEnergy,
                PlantCost = values.PlantCost,
                PlantCO2 = values.PlantCO2,
            };

            return result;
        }

        private static string SingleFile(string projectFolder, string extension)
        {
            List<string> paths = Directory.GetFiles(projectFolder, "*", SearchOption.TopDirectoryOnly)
                .Where(x => string.Equals(Path.GetExtension(x), extension, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (paths.Count > 1)
            {
                throw new InvalidOperationException("The Tas project folder holds " + paths.Count + " " + extension + " files (" + string.Join(", ", paths.Select(Path.GetFileName)) + "); the tas-model engine needs at most one.");
            }

            return paths.FirstOrDefault();
        }

        private static void ReadTbd(string path, List<TasInternalConditionInfo> internalConditions, List<TasGlazingConstructionInfo> glazingConstructions)
        {
            TBD.TBDDocument document = new TBD.TBDDocument();
            try
            {
                document.openReadOnly(path);
                TBD.Building building = document.Building;
                for (int i = 0; ; i++)
                {
                    TBD.InternalCondition internalCondition = building.GetIC(i);
                    if (internalCondition == null)
                    {
                        break;
                    }

                    int zoneCount = 0;
                    while (internalCondition.GetZone(zoneCount) != null)
                    {
                        zoneCount++;
                    }

                    TBD.Thermostat thermostat = internalCondition.GetThermostat();
                    internalConditions.Add(new TasInternalConditionInfo(
                        internalCondition.name,
                        internalCondition.description,
                        zoneCount,
                        thermostat == null ? null : SetpointProfile(thermostat.GetProfile((int)TBD.Profiles.ticLL), true),
                        thermostat == null ? null : SetpointProfile(thermostat.GetProfile((int)TBD.Profiles.ticUL), false)));
                }

                // Glazing through the elements: GetConstruction(i) has null gaps (PR7a), the elements do not.
                List<string> names = new List<string>();
                Dictionary<string, List<string>> elements = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                Dictionary<string, TBD.Construction> constructions = new Dictionary<string, TBD.Construction>(StringComparer.Ordinal);
                for (int i = 0; ; i++)
                {
                    TBD.buildingElement buildingElement = building.GetBuildingElement(i);
                    if (buildingElement == null)
                    {
                        break;
                    }

                    TBD.Construction construction = buildingElement.GetConstruction();
                    if (construction == null || construction.type != TBD.ConstructionTypes.tcdTransparentConstruction)
                    {
                        continue;
                    }

                    if (!elements.TryGetValue(construction.name, out List<string> list))
                    {
                        elements[construction.name] = list = new List<string>();
                        constructions[construction.name] = construction;
                        names.Add(construction.name);
                    }

                    list.Add(buildingElement.name);
                }

                foreach (string name in names)
                {
                    float[] glazing = Floats(constructions[name].GetGlazingValues());
                    float[] u = Floats(constructions[name].GetUValue());
                    glazingConstructions.Add(new TasGlazingConstructionInfo(
                        name,
                        elements[name],
                        glazing != null && glazing.Length > 5 ? glazing[5] : double.NaN,
                        u != null && u.Length > 6 ? u[6] : double.NaN,
                        glazing != null && glazing.Length > 0 ? glazing[0] : double.NaN));
                }
            }
            finally
            {
                document.close();
                Marshal.FinalReleaseComObject(document);
            }
        }

        /// <summary>A thermostat profile as the setpoint blocks see it (PR7a, question 3).</summary>
        private static TasSetpointProfile SetpointProfile(TBD.profile profile, bool heating)
        {
            if (profile == null)
            {
                return null;
            }

            if (profile.type == TBD.ProfileTypes.ticValueProfile)
            {
                return new TasSetpointProfile(TasSetpointProfileType.Value, profile.factor, profile.value);
            }

            if (profile.type == TBD.ProfileTypes.ticHourlyProfile)
            {
                float[] hours = new float[24];
                for (int h = 1; h <= 24; h++)
                {
                    hours[h - 1] = profile.hourlyValues[h];
                }

                float setpoint = heating ? hours.Max() : hours.Min();
                return new TasSetpointProfile(TasSetpointProfileType.Hourly, profile.factor, setpoint, hours.Count(x => x == setpoint));
            }

            return new TasSetpointProfile(TasSetpointProfileType.Unsupported, profile.factor, null);
        }

        private static void ReadTsd(string path, TasModelInventory tasModelInventory)
        {
            TSD.TSDDocument document = new TSD.TSDDocument();
            try
            {
                document.openReadOnly(path);
                TSD.BuildingData buildingData = document.SimulationData.GetBuildingData();

                // W each hour, summed over the year = Wh; reported in kWh (as the generated script).
                tasModelInventory.HeatingDemand = Sum(buildingData.GetAnnualBuildingResult((int)TSD.tsdBuildingArray.heatingProfile)) / 1000;
                tasModelInventory.CoolingDemand = Sum(buildingData.GetAnnualBuildingResult((int)TSD.tsdBuildingArray.coolingProfile)) / 1000;
            }
            finally
            {
                document.close();
                Marshal.FinalReleaseComObject(document);
            }
        }

        private static void ReadTpd(string path, TasModelInventory tasModelInventory, List<TasPlantRoomInfo> plantRooms)
        {
            TPD.TPDDoc document = new TPD.TPDDoc();
            try
            {
                document.OpenReadOnly(path);
                TPD.EnergyCentre energyCentre = document.EnergyCentre;
                for (int i = 1; i <= energyCentre.GetPlantRoomCount(); i++)
                {
                    TPD.PlantRoom plantRoom = energyCentre.GetPlantRoom(i);
                    List<TasPlantControllerInfo> controllers = new List<TasPlantControllerInfo>();
                    for (int j = 1; j <= plantRoom.GetControllerCount(); j++)
                    {
                        TPD.PlantController plantController = plantRoom.GetController(j);
                        controllers.Add(new TasPlantControllerInfo(plantController.Name, plantController.SensorType.ToString(), plantController.Setpoint));
                    }

                    plantRooms.Add(new TasPlantRoomInfo(plantRoom.Name, controllers));
                }

                // The stored annual results (the last plant simulation), summed as the generated script sums them.
                TPD.WrResultSet resultSet = (TPD.WrResultSet)energyCentre.GetResultSet(TPD.tpdResultsPeriod.tpdResultsPeriodAnnual, 0, 0, 0, null);
                try
                {
                    tasModelInventory.PlantEnergy = ResultSum(resultSet, TPD.tpdResultVectorType.tpdConsumption, out _);
                    tasModelInventory.PlantCost = ResultSum(resultSet, TPD.tpdResultVectorType.tpdCost, out string costUnit);
                    tasModelInventory.TpdCostUnit = costUnit;
                    tasModelInventory.PlantCO2 = ResultSum(resultSet, TPD.tpdResultVectorType.tpdCo2, out _);
                }
                finally
                {
                    // TPD.exe intermittently faults when a result set or document is released (PR7a F2; seen here on
                    // Dispose of a read-only TPD after the values were read): the values stand, the release is ignored.
                    IgnoreServerFault(resultSet.Dispose);
                }
            }
            finally
            {
                IgnoreServerFault(() => document.Close());
                Marshal.FinalReleaseComObject(document);
            }
        }

        private static void IgnoreServerFault(Action action)
        {
            try
            {
                action();
            }
            catch (COMException)
            {
            }
        }

        /// <summary>The sum of the first value of every result item of a type; null when there is none. The unit is the items' common unit text.</summary>
        private static double? ResultSum(TPD.WrResultSet resultSet, TPD.tpdResultVectorType type, out string unit)
        {
            unit = null;
            int size = resultSet.GetVectorSize(type);
            if (size <= 0)
            {
                return null;
            }

            double sum = 0;
            HashSet<string> units = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 1; i <= size; i++)
            {
                TPD.WrResultItem item = (TPD.WrResultItem)resultSet.GetResultItem(type, i);
                Array values = (Array)item.GetValues();
                sum += System.Convert.ToDouble(values.GetValue(0), System.Globalization.CultureInfo.InvariantCulture);
                units.Add(item.GetUnitString() ?? string.Empty);
            }

            unit = units.Count == 1 ? units.First() : null;
            return sum;
        }

        private static double Sum(object values)
        {
            double result = 0;
            foreach (float value in Floats(values) ?? new float[0])
            {
                result += value;
            }

            return result;
        }

        /// <summary>A COM SAFEARRAY (1-based or not) as floats, by enumeration.</summary>
        private static float[] Floats(object values)
        {
            if (!(values is IEnumerable enumerable))
            {
                return null;
            }

            return enumerable.Cast<object>().Select(x => System.Convert.ToSingle(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        }
    }
}
