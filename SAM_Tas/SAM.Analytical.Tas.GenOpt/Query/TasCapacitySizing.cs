// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SAM.Analytical.Tas.GenOpt
{
    public static partial class Query
    {
        /// <summary>
        /// True for a TBD zone sizing type whose capacity Tas sizes again in every simulation: <c>tbdSizing</c> and
        /// <c>tbdDesignSizingOnly</c>. Measured (PR7b-2, 2026-10-12, copies): a zone capacity set to 50 W before
        /// <c>simulate</c> is sized again by it for both (Systems Demo, Energy Simulation with Sizing off), and stays 50 W
        /// for <c>tbdNoSizing</c> (Energy Simulation with Sizing on). Any other type is not accepted: its behaviour is not
        /// measured.
        /// </summary>
        public static bool SizedEverySimulation(int sizingType)
        {
            return sizingType == (int)TBD.SizingType.tbdSizing || sizingType == (int)TBD.SizingType.tbdDesignSizingOnly;
        }

        /// <summary>
        /// The state of a TBD's zone capacities (<see cref="GenOpt.TasCapacitySizing"/>): <see cref="TasCapacitySizing.Unknown"/>
        /// when not read (null) or there is no zone; <see cref="TasCapacitySizing.Fixed"/> when a zone is <c>tbdNoSizing</c>
        /// (whatever its capacity: 0 is a capacity too); <see cref="TasCapacitySizing.Unrecognised"/> for a type not
        /// measured; <see cref="TasCapacitySizing.SizedEverySimulation"/> only when every zone is accepted.
        /// </summary>
        public static TasCapacitySizing CapacitySizing(IEnumerable<TasZoneSizing> zoneSizing)
        {
            List<TasZoneSizing> zones = zoneSizing?.Where(x => x != null).ToList();
            if (zones == null || zones.Count == 0)
            {
                return TasCapacitySizing.Unknown;
            }

            if (zones.Any(x => x.HeatingSizing == (int)TBD.SizingType.tbdNoSizing || x.CoolingSizing == (int)TBD.SizingType.tbdNoSizing))
            {
                return TasCapacitySizing.Fixed;
            }

            return zones.All(x => x.SizedEverySimulation) ? TasCapacitySizing.SizedEverySimulation : TasCapacitySizing.Unrecognised;
        }

        /// <summary>
        /// Why the model's TBD targets (zone heating/cooling setpoints, glazing) cannot be optimised or applied, or null
        /// when they can (every zone sized in every simulation). PR7b-2 D1: with capacities fixed in the TBD each
        /// evaluation keeps them, while the next Energy Simulation sizes them again for the changed design, so the
        /// optimised result would not hold. Plant controllers are not affected.
        /// </summary>
        public static string TasModelSizingRefusal(TasModelInventory tasModelInventory)
        {
            const string what = "Zone setpoints and glazing cannot be optimised on this model: ";
            const string controllers = " Plant controllers can still be optimised.";
            IReadOnlyList<TasZoneSizing> zones = tasModelInventory?.ZoneSizing;
            switch (CapacitySizing(zones))
            {
                case TasCapacitySizing.SizedEverySimulation:
                    return null;

                case TasCapacitySizing.Fixed:
                    List<TasZoneSizing> fixedZones = zones.Where(x => x.HeatingSizing == (int)TBD.SizingType.tbdNoSizing || x.CoolingSizing == (int)TBD.SizingType.tbdNoSizing).ToList();
                    return what + "its TBD holds zone heating/cooling capacities fixed by Energy Simulation's Sizing (" + ZoneList(fixedZones) + "). Each optimisation simulation would keep them, but the next Energy Simulation sizes them again for the changed design, so the optimised result would not hold." + controllers
                        + " To optimise them, run Energy Simulation with Sizing off first: Tas then sizes the plant within every simulation, without a design-day limit, which is a different design problem.";

                case TasCapacitySizing.Unrecognised:
                    List<TasZoneSizing> other = zones.Where(x => !x.SizedEverySimulation).ToList();
                    return what + "the sizing of " + ZoneList(other) + " (" + string.Join(", ", other.SelectMany(x => new[] { x.HeatingSizing, x.CoolingSizing }).Where(x => !SizedEverySimulation(x)).Distinct().Select(SizingTypeText)) + ") has not been measured, so the tas-model engine cannot tell whether an optimised result would hold." + controllers;

                default:
                    return what + "the sizing of its TBD zones is not known (" + (zones == null ? "not read" : "the TBD has no zone") + "), so the tas-model engine cannot tell whether an optimised result would hold." + controllers;
            }
        }

        /// <summary>The name of a <c>TBD.SizingType</c> value.</summary>
        public static string SizingTypeText(int sizingType)
        {
            return Enum.IsDefined(typeof(TBD.SizingType), sizingType) ? ((TBD.SizingType)sizingType).ToString() : "sizing type " + sizingType.ToString(CultureInfo.InvariantCulture);
        }

        private static string ZoneList(List<TasZoneSizing> zones)
        {
            List<string> names = zones.Select(x => "“" + x.ZoneName + "”").ToList();
            return (names.Count == 1 ? "zone " : names.Count + " zones: ") + string.Join(", ", names.Take(3)) + (names.Count > 3 ? " and " + (names.Count - 3).ToString(CultureInfo.InvariantCulture) + " more" : string.Empty);
        }

        /// <summary>Every zone's sizing as the TBD holds it now (licensed Tas, COM; the building stays open).</summary>
        internal static List<TasZoneSizing> ZoneSizing(TBD.Building building)
        {
            List<TasZoneSizing> result = new List<TasZoneSizing>();
            for (int i = 0; ; i++)
            {
                TBD.zone zone = building.GetZone(i);
                if (zone == null)
                {
                    break;
                }

                result.Add(new TasZoneSizing(zone.name, zone.sizeHeating, zone.maxHeatingLoad, zone.sizeCooling, zone.maxCoolingLoad));
            }

            return result;
        }

        /// <summary>
        /// Throws when the open TBD's zones are not all sized in every simulation (<see cref="TasModelSizingRefusal"/>):
        /// the check of the file itself, before a TBD target is written, whatever inventory a caller passed.
        /// </summary>
        internal static void RefuseFixedCapacities(TBD.Building building)
        {
            string refusal = TasModelSizingRefusal(new TasModelInventory(null, null, null) { ZoneSizing = ZoneSizing(building) });
            if (refusal != null)
            {
                throw new InvalidOperationException(refusal);
            }
        }
    }
}
