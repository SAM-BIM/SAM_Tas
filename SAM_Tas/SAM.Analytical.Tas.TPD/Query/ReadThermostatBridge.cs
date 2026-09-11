// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using System;
using System.Collections;
using System.Collections.Generic;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Query
    {
        /// <summary>
        /// Reads the thermostat bridge's second TSD: each planned room's <c>ResultantTemperature</c>, and the
        /// check that the copied building actually held the room's air at the imposed achieved temperature.
        /// <para>
        /// <b>By guid, once.</b> The TSD's zones are walked a single time into a <c>zoneGUID</c> index and each
        /// room is then one probe; two zones answering one guid are refused.
        /// </para>
        /// <para>
        /// <b>The achieved-air check is the bridge's own acceptance.</b> Imposing the Systems air temperature
        /// on both thermostat limits only reconstructs the Systems state if TAS then holds the air there. The
        /// simulated dry bulb is compared with the imposed series hour by hour, and a room whose largest
        /// deviation exceeds <paramref name="achievedAirTemperatureTolerance"/> is refused - its resultant
        /// temperature would belong to a building the Systems simulation did not have.
        /// </para>
        /// <para>
        /// Hour <c>k</c> of TSD's annual array is 0-based hour <c>k</c>, the hour written into thermostat slot
        /// <c>k + 1</c>: measured on licensed TAS, see the PR3 evidence.
        /// </para>
        /// </summary>
        /// <param name="buildingData">The second TSD's building data.</param>
        /// <param name="thermostatBridgePlan">The plan the copy was written from.</param>
        /// <param name="thermostatBridgeRooms">The writer's measurements, completed here by room guid.</param>
        /// <param name="achievedAirTemperatureTolerance">Largest accepted |dry bulb - imposed| in any hour, K.</param>
        /// <param name="refusals">Receives every reason the results are not the bridge's answer.</param>
        public static List<ResultantTemperatureResult> ReadThermostatBridge(
            this TSD.BuildingData buildingData,
            ThermostatBridgePlan thermostatBridgePlan,
            IEnumerable<ThermostatBridgeRoom> thermostatBridgeRooms,
            double achievedAirTemperatureTolerance,
            List<string> refusals)
        {
            List<ResultantTemperatureResult> result = new List<ResultantTemperatureResult>();

            if (buildingData == null || thermostatBridgePlan == null || !thermostatBridgePlan.IsValid)
            {
                refusals.Add("No second TSD or no valid plan was given to the thermostat bridge reader.");
                return result;
            }

            Dictionary<Guid, ThermostatBridgeRoom> room_By_Space = new Dictionary<Guid, ThermostatBridgeRoom>();
            if (thermostatBridgeRooms != null)
            {
                foreach (ThermostatBridgeRoom thermostatBridgeRoom in thermostatBridgeRooms)
                {
                    if (thermostatBridgeRoom != null)
                    {
                        room_By_Space[thermostatBridgeRoom.Guid_Space] = thermostatBridgeRoom;
                    }
                }
            }

            Dictionary<string, TSD.ZoneData> zoneData_By_Key = new Dictionary<string, TSD.ZoneData>(StringComparer.Ordinal);
            HashSet<string> keys_Duplicate = new HashSet<string>(StringComparer.Ordinal);

            //TSD's zone accessor is 1-based.
            int index = 1;
            TSD.ZoneData zoneData;

            while ((zoneData = buildingData.GetZoneData(index)) != null)
            {
                index++;

                string key = ZoneReferenceKey(zoneData.zoneGUID);
                if (key == null)
                {
                    continue;
                }

                if (zoneData_By_Key.ContainsKey(key))
                {
                    keys_Duplicate.Add(key);
                    continue;
                }

                zoneData_By_Key[key] = zoneData;
            }

            foreach (ThermostatBridgeTransfer thermostatBridgeTransfer in thermostatBridgePlan.Transfers)
            {
                room_By_Space.TryGetValue(thermostatBridgeTransfer.Guid_Space, out ThermostatBridgeRoom thermostatBridgeRoom);

                if (keys_Duplicate.Contains(thermostatBridgeTransfer.Key))
                {
                    result.Add(Refused(thermostatBridgeTransfer, string.Format("more than one zone in the second TSD answers guid {0}.", thermostatBridgeTransfer.Reference_Zone)));
                    continue;
                }

                if (!zoneData_By_Key.TryGetValue(thermostatBridgeTransfer.Key, out zoneData))
                {
                    result.Add(Refused(thermostatBridgeTransfer, string.Format("the second TSD has no zone {0}.", thermostatBridgeTransfer.Reference_Zone)));
                    continue;
                }

                List<double> resultantTemperatures = AnnualSeries(zoneData.GetAnnualZoneResult((int)TSD.tsdZoneArray.resultantTemp));
                List<double> dryBulbTemperatures = AnnualSeries(zoneData.GetAnnualZoneResult((int)TSD.tsdZoneArray.dryBulbTemp));

                IndexedDoubles indexedDoubles = new IndexedDoubles();

                int count_Finite = 0;

                for (int i = 0; i < resultantTemperatures.Count; i++)
                {
                    double value = resultantTemperatures[i];

                    indexedDoubles[thermostatBridgeTransfer.StartHour + i] = value;

                    if (!double.IsNaN(value) && !double.IsInfinity(value))
                    {
                        count_Finite++;
                    }
                }

                if (thermostatBridgeRoom != null)
                {
                    thermostatBridgeRoom.Count_ResultantTemperature = resultantTemperatures.Count;
                    thermostatBridgeRoom.Count_ResultantTemperature_Finite = count_Finite;
                }

                string refusal_AchievedAir = AchievedAirTemperatureRefusal(thermostatBridgeTransfer, dryBulbTemperatures, achievedAirTemperatureTolerance, out double max, out double mean);

                if (thermostatBridgeRoom != null)
                {
                    thermostatBridgeRoom.MaxAchievedAirTemperatureDeviation = max;
                    thermostatBridgeRoom.MeanAchievedAirTemperatureDeviation = mean;
                }

                if (refusal_AchievedAir != null)
                {
                    refusals.Add(string.Format("Room {0}: {1}", thermostatBridgeTransfer.Guid_Space, refusal_AchievedAir));
                }

                result.Add(new ResultantTemperatureResult(
                    thermostatBridgeTransfer.Guid_Space,
                    thermostatBridgeTransfer.Reference_Zone,
                    ThermostatBridgePlan.StartHour,
                    ThermostatBridgePlan.EndHour,
                    indexedDoubles,
                    null));
            }

            return result;
        }

        /// <summary>
        /// Why the copied building's simulated air temperature does not follow the imposed achieved series
        /// closely enough, or null when it does. Compares hour by hour against the Systems value itself, not
        /// against its single-precision copy.
        /// </summary>
        /// <param name="max">Largest |dry bulb - imposed|, K; NaN when the series cannot be compared.</param>
        /// <param name="mean">Mean |dry bulb - imposed|, K; NaN when the series cannot be compared.</param>
        public static string AchievedAirTemperatureRefusal(
            ThermostatBridgeTransfer thermostatBridgeTransfer,
            IList<double> dryBulbTemperatures,
            double achievedAirTemperatureTolerance,
            out double max,
            out double mean)
        {
            max = double.NaN;
            mean = double.NaN;

            if (thermostatBridgeTransfer == null)
            {
                return "no transfer to compare the simulated air temperature against.";
            }

            if (double.IsNaN(achievedAirTemperatureTolerance) || double.IsInfinity(achievedAirTemperatureTolerance) || achievedAirTemperatureTolerance < 0)
            {
                return string.Format("the achieved-air tolerance {0} is not a temperature difference.", achievedAirTemperatureTolerance);
            }

            if (dryBulbTemperatures == null || dryBulbTemperatures.Count != thermostatBridgeTransfer.Count)
            {
                return string.Format(
                    "the second simulation answered {0} air temperature(s) for {1} imposed hour(s), so whether the air was held at the "
                    + "achieved temperature cannot be shown.",
                    dryBulbTemperatures == null ? 0 : dryBulbTemperatures.Count,
                    thermostatBridgeTransfer.Count);
            }

            double sum = 0;
            double max_Temp = 0;
            int hour_Max = -1;

            for (int i = 0; i < dryBulbTemperatures.Count; i++)
            {
                double delta = global::System.Math.Abs(dryBulbTemperatures[i] - thermostatBridgeTransfer.ZoneTemperature(i));

                if (double.IsNaN(delta) || double.IsInfinity(delta))
                {
                    return string.Format("the simulated air temperature at hour {0} is {1}.", thermostatBridgeTransfer.StartHour + i, dryBulbTemperatures[i]);
                }

                sum += delta;

                if (delta > max_Temp)
                {
                    max_Temp = delta;
                    hour_Max = thermostatBridgeTransfer.StartHour + i;
                }
            }

            max = max_Temp;
            mean = dryBulbTemperatures.Count == 0 ? 0 : sum / dryBulbTemperatures.Count;

            if (max_Temp > achievedAirTemperatureTolerance)
            {
                return string.Format(
                    "the copied building's air temperature stands up to {0:0.0000} K from the imposed achieved temperature (hour {1}), "
                    + "beyond the {2} K the bridge accepts, so its resultant temperature is not the Systems building's.",
                    max_Temp,
                    hour_Max,
                    achievedAirTemperatureTolerance);
            }

            return null;
        }

        private static ResultantTemperatureResult Refused(ThermostatBridgeTransfer thermostatBridgeTransfer, string diagnostic)
        {
            return new ResultantTemperatureResult(
                thermostatBridgeTransfer.Guid_Space,
                thermostatBridgeTransfer.Reference_Zone,
                ThermostatBridgePlan.StartHour,
                ThermostatBridgePlan.EndHour,
                null,
                diagnostic);
        }

        /// <summary>
        /// A TSD annual result as doubles, in the order TSD returned it. A null or unconvertible element becomes
        /// NaN - which the completeness check then refuses - never a plausible zero.
        /// </summary>
        private static List<double> AnnualSeries(object value)
        {
            List<double> result = new List<double>();

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable == null)
            {
                return result;
            }

            foreach (object @object in enumerable)
            {
                double value_Temp = double.NaN;

                if (@object != null)
                {
                    try
                    {
                        value_Temp = global::System.Convert.ToDouble(@object, global::System.Globalization.CultureInfo.InvariantCulture);
                    }
                    catch
                    {
                        value_Temp = double.NaN;
                    }
                }

                result.Add(value_Temp);
            }

            return result;
        }
    }
}
