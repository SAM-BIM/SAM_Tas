// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas.GenOpt
{
    /// <summary>One thermostat profile of a TBD internal condition, as the catalogue needs it (read-only data, no COM).</summary>
    public sealed class TasSetpointProfile
    {
        /// <param name="type">How the profile holds the setpoint.</param>
        /// <param name="factor">The profile factor; the blocks change only factor 1.</param>
        /// <param name="setpoint">The current setpoint (°C): the value, or the highest/lowest hour; null when unknown.</param>
        /// <param name="changedHours">For a 24-hour profile, how many hours hold the setpoint; otherwise 0.</param>
        public TasSetpointProfile(TasSetpointProfileType type, double factor, double? setpoint, int changedHours = 0)
        {
            Type = type;
            Factor = factor;
            Setpoint = setpoint;
            ChangedHours = changedHours;
        }

        public TasSetpointProfileType Type { get; }

        public double Factor { get; }

        /// <summary>The current setpoint in °C; null when unknown.</summary>
        public double? Setpoint { get; }

        /// <summary>For a 24-hour profile, the number of hours that hold the setpoint (the others are the setback).</summary>
        public int ChangedHours { get; }

        /// <summary>True when the blocks can change it: a value or 24-hour profile with factor 1 and a finite setpoint.</summary>
        public bool Supported => Type != TasSetpointProfileType.Unsupported && Factor == 1 && Setpoint.HasValue && !double.IsNaN(Setpoint.Value) && !double.IsInfinity(Setpoint.Value);
    }
}
