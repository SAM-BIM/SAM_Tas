// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.Tas.TM59
{
    public static partial class Convert
    {
        /// <summary>
        /// The TM59 export with the ventilation system type derived from the model.
        /// <para>
        /// <b>Derivation #2, and superseded.</b> Any related <c>VentilationSystem</c> that
        /// <c>IsMechanicalVentilation()</c> decides the whole building's rooms, overriding the internal
        /// condition that <c>Space.ToTM59</c> would otherwise read. It disagrees both with that internal
        /// condition and with the criterion the assessment applies, which is how one real run exported
        /// "Nat Vent" and "Mech Vent" mixed across three identical flats. Prefer the overload that takes a
        /// <see cref="VentilationStrategyMap"/>; this one remains for callers that have no scenario to state.
        /// </para>
        /// </summary>
        public static Building ToTM59(this AnalyticalModel analyticalModel, TM59Manager tM59Manager)
        {
            if (analyticalModel == null)
            {
                return null;
            }

            List<Zone> zones = new List<Zone>();

            AdjacencyCluster adjacencyCluster = analyticalModel?.AdjacencyCluster;
            if (adjacencyCluster != null)
            {
                List<Space> spaces = adjacencyCluster?.GetSpaces();
                if (spaces != null)
                {
                    foreach (Space space in spaces)
                    {
                        SystemType systemType = SystemType.Undefined;

                        List<VentilationSystem> ventilationSystems = adjacencyCluster.MechanicalSystems<VentilationSystem>(space);
                        if(ventilationSystems != null && ventilationSystems.Count != 0)
                        {
                            VentilationSystem ventilationSystem = ventilationSystems.Find(x => x != null && x.IsMechanicalVentilation());
                            systemType = ventilationSystem == null ? SystemType.NaturalVentilation : SystemType.MechanicalVentilation;
                        }

                        Zone zone = space.ToTM59(tM59Manager, systemType);
                        if (zone != null)
                        {
                            zones.Add(zone);
                        }
                    }
                }
            }

            return new Building(BuildingCategory.Category_II, false, false, zones);
        }

        /// <summary>
        /// The TM59 export with the ventilation system type stated by the <c>OverheatingScenario</c>s rather
        /// than derived from the model.
        /// <para>
        /// <b>The scenario is authoritative here for the same reason it is in the assessment.</b> A space's
        /// internal condition and its related mechanical systems are inputs to a simulation; neither is a
        /// statement about how the dwelling being assessed is ventilated. Where the map is supplied, both
        /// derivations are bypassed - the strategy goes in through <c>Space.ToTM59</c>'s existing
        /// <c>systemType</c> parameter, which is the seam that already existed for exactly this, and a
        /// non-<c>Undefined</c> value means that method's own internal-condition fallback is never reached.
        /// </para>
        /// <para>
        /// <b>A refusal loses the whole document, not one room</b> - and that is deliberately different from
        /// the assessment, which drops the refused space and reports it. This XML is configuration for the
        /// external TAS TM59 tool, which has no way to be told that a room is missing: it would assess what
        /// it was given and produce a complete-looking answer for an incomplete building. A null return with
        /// the reasons in <paramref name="ventilationStrategyRefusals"/> cannot be mistaken for that.
        /// </para>
        /// <para>
        /// <b>The XML vocabulary has only two values</b>, "Nat Vent" and "Mech Vent", so the corridor
        /// criterion the assessment distinguishes has no representation here and a <c>UV</c> space is exported
        /// as naturally ventilated. That is pre-existing and unchanged - the mapping used is the same
        /// <c>Query.IsMechanicalVentilation</c> the old derivations used, so making the scenario authoritative
        /// changed which strategy applies and not what a strategy means.
        /// </para>
        /// </summary>
        /// <param name="ventilationStrategyMap">
        /// What the scenarios state. Null falls back to the model-derived overload above, unchanged, with no
        /// refusals - so a caller with no scenario is not silently given an empty export.
        /// </param>
        /// <param name="ventilationStrategyRefusals">
        /// Why the export was refused, one sentence per space. Never null; empty on success.
        /// </param>
        /// <returns>The building, or null where any space's ventilation strategy was not settled.</returns>
        public static Building ToTM59(this AnalyticalModel analyticalModel, TM59Manager tM59Manager, VentilationStrategyMap ventilationStrategyMap, out List<string> ventilationStrategyRefusals)
        {
            ventilationStrategyRefusals = new List<string>();

            if (ventilationStrategyMap == null)
            {
                return ToTM59(analyticalModel, tM59Manager);
            }

            AdjacencyCluster adjacencyCluster = analyticalModel?.AdjacencyCluster;

            List<Space> spaces = adjacencyCluster?.GetSpaces();
            if (spaces == null)
            {
                return null;
            }

            List<Zone> zones = new List<Zone>();

            foreach (Space space in spaces)
            {
                VentilationStrategySelection ventilationStrategySelection = ventilationStrategyMap.Selection(space);
                if (!ventilationStrategySelection.IsSelected)
                {
                    ventilationStrategyRefusals.Add(ventilationStrategySelection.Reason);

                    //Every space is still visited, so one unstated dwelling reports one reason rather than
                    //hiding the others behind it.
                    continue;
                }

                //Not Undefined, so Space.ToTM59 uses this instead of reading the internal condition.
                SystemType systemType = Analytical.Query.IsMechanicalVentilation(ventilationStrategySelection.VentilationStrategy)
                    ? SystemType.MechanicalVentilation
                    : SystemType.NaturalVentilation;

                Zone zone = space.ToTM59(tM59Manager, systemType);
                if (zone != null)
                {
                    zones.Add(zone);
                }
            }

            return ventilationStrategyRefusals.Count == 0 ? new Building(BuildingCategory.Category_II, false, false, zones) : null;
        }
    }
}
