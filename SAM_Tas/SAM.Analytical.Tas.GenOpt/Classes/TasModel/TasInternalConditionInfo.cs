// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas.GenOpt
{
    /// <summary>A TBD internal condition and its thermostat setpoints (read-only data, no COM).</summary>
    public sealed class TasInternalConditionInfo
    {
        public TasInternalConditionInfo(string name, string description, int zoneCount, TasSetpointProfile heating, TasSetpointProfile cooling)
        {
            Name = name;
            Description = description;
            ZoneCount = zoneCount;
            Heating = heating;
            Cooling = cooling;
        }

        /// <summary>The exact name (untrimmed: a Systems Demo name has a trailing space).</summary>
        public string Name { get; }

        public string Description { get; }

        /// <summary>The number of zones the internal condition is assigned to.</summary>
        public int ZoneCount { get; }

        /// <summary>The thermostat lower limit (ticLL); null when the condition has no thermostat.</summary>
        public TasSetpointProfile Heating { get; }

        /// <summary>The thermostat upper limit (ticUL); null when the condition has no thermostat.</summary>
        public TasSetpointProfile Cooling { get; }
    }
}
