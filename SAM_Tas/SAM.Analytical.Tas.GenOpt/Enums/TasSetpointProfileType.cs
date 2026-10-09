// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas.GenOpt
{
    /// <summary>How a TBD thermostat profile holds its setpoint (PR7a, question 3).</summary>
    public enum TasSetpointProfileType
    {
        /// <summary>A profile the "tas-model" blocks do not change (function, yearly, ...).</summary>
        Unsupported,

        /// <summary>A value profile: the setpoint is its value; its setback and schedule are kept.</summary>
        Value,

        /// <summary>A 24-hour profile: the setpoint is the highest (heating) or lowest (cooling) hour; the other hours are kept.</summary>
        Hourly,
    }
}
