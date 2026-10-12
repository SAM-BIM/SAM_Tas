// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas.GenOpt
{
    /// <summary>
    /// How one TBD zone's heating and cooling capacities are sized: the zone's <c>sizeHeating</c>/<c>sizeCooling</c>
    /// (<c>TBD.SizingType</c>) and <c>maxHeatingLoad</c>/<c>maxCoolingLoad</c> (W), as read from the TBD (PR7b-2 D1).
    /// </summary>
    public sealed class TasZoneSizing
    {
        /// <summary><c>TBD.SizingType.tbdNoSizing</c>: the capacity is the value in the TBD (Energy Simulation's Sizing leaves this).</summary>
        public const int NoSizing = (int)TBD.SizingType.tbdNoSizing;

        /// <summary><c>TBD.SizingType.tbdDesignSizingOnly</c>: sized on the design days by every simulation (the Systems Demo).</summary>
        public const int DesignSizingOnly = (int)TBD.SizingType.tbdDesignSizingOnly;

        /// <summary><c>TBD.SizingType.tbdSizing</c>: sized within every simulation (Energy Simulation with Sizing off).</summary>
        public const int Sizing = (int)TBD.SizingType.tbdSizing;

        public TasZoneSizing(string zoneName, int heatingSizing, double heatingCapacity, int coolingSizing, double coolingCapacity)
        {
            ZoneName = zoneName;
            HeatingSizing = heatingSizing;
            HeatingCapacity = heatingCapacity;
            CoolingSizing = coolingSizing;
            CoolingCapacity = coolingCapacity;
        }

        public string ZoneName { get; }

        /// <summary>The zone's <c>sizeHeating</c> (<c>TBD.SizingType</c> as an integer).</summary>
        public int HeatingSizing { get; }

        /// <summary>The zone's <c>maxHeatingLoad</c> (W).</summary>
        public double HeatingCapacity { get; }

        /// <summary>The zone's <c>sizeCooling</c> (<c>TBD.SizingType</c> as an integer).</summary>
        public int CoolingSizing { get; }

        /// <summary>The zone's <c>maxCoolingLoad</c> (W).</summary>
        public double CoolingCapacity { get; }

        /// <summary>True when Tas sizes both capacities in every simulation (<see cref="Query.SizedEverySimulation(int)"/>).</summary>
        public bool SizedEverySimulation => Query.SizedEverySimulation(HeatingSizing) && Query.SizedEverySimulation(CoolingSizing);
    }

    /// <summary>
    /// Whether the TBD's zone capacities follow a changed design within each simulation, the condition for optimising a
    /// TBD target (zone setpoints, glazing) on it (PR7b-2 D1, owner decision option (a), 2026-10-12).
    /// </summary>
    public enum TasCapacitySizing
    {
        /// <summary>The zones' sizing was not read, or the TBD has no zone: TBD targets are refused.</summary>
        Unknown,

        /// <summary>
        /// Every zone is sized by Tas in every simulation (<c>tbdSizing</c> or <c>tbdDesignSizingOnly</c>, measured: a
        /// capacity changed before <c>simulate</c> is sized again by it), so an evaluation and any later simulation agree.
        /// </summary>
        SizedEverySimulation,

        /// <summary>
        /// At least one zone has capacities fixed in the TBD (<c>tbdNoSizing</c>, as Energy Simulation's Sizing leaves
        /// them): evaluations keep them, the next Energy Simulation sizes them again for the changed design. TBD targets are
        /// refused.
        /// </summary>
        Fixed,

        /// <summary>At least one zone has a sizing type whose behaviour has not been measured (e.g. <c>tbdMixed</c>): TBD targets are refused.</summary>
        Unrecognised,
    }
}
