// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Tas.GenOpt
{
    /// <summary>A TPD plant room and its controllers (read-only data, no COM).</summary>
    public sealed class TasPlantRoomInfo
    {
        public TasPlantRoomInfo(string name, IEnumerable<TasPlantControllerInfo> controllers)
        {
            Name = name;
            Controllers = (controllers ?? Enumerable.Empty<TasPlantControllerInfo>()).Where(x => x != null).ToList().AsReadOnly();
        }

        /// <summary>The exact plant room name.</summary>
        public string Name { get; }

        public IReadOnlyList<TasPlantControllerInfo> Controllers { get; }
    }

    /// <summary>A TPD plant controller (read-only data, no COM).</summary>
    public sealed class TasPlantControllerInfo
    {
        /// <param name="name">The exact controller name.</param>
        /// <param name="sensorType">The TPD sensor type name, for example "tpdTempSensor".</param>
        /// <param name="setpoint">The current setpoint, in the sensor's unit.</param>
        public TasPlantControllerInfo(string name, string sensorType, double setpoint)
        {
            Name = name;
            SensorType = sensorType;
            Setpoint = setpoint;
        }

        public string Name { get; }

        public string SensorType { get; }

        public double Setpoint { get; }

        /// <summary>True for a temperature sensor ("tpdTempSensor"): its setpoint is in °C.</summary>
        public bool IsTemperatureSensor => SensorType == "tpdTempSensor";
    }
}
