// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        /// <summary>
        /// <b>What a space's <c>ticV</c> factor must be, in ACH</b> - the volumetric supply air TAS has no
        /// other field for, and <b>only</b> that.
        /// <para>
        /// <see cref="Analytical.Query.CalculatedSupplyAirFlow(Space)"/> SUMS four bases:
        /// <c>SupplyAirFlow</c>, <c>SupplyAirFlowPerArea</c>, <c>SupplyAirChangesPerHour</c> and
        /// <c>SupplyAirFlowPerPerson</c>. Three of those have nowhere else to go and belong in the factor.
        /// The fourth does not: <c>Modify.UpdateInternalCondition</c> has already written it to
        /// <c>InternalGain.freshAirRate</c>, TAS's own per-person outside-air field, from the very same
        /// parameter. Adding it to the factor as well states the occupants' fresh air TWICE.
        /// </para>
        /// <para>
        /// <b>And it compounded.</b> The import writes the factor it reads back into
        /// <c>SupplyAirChangesPerHour</c> - one basis carrying the whole previous total - so the next export
        /// summed the per-person term on top of a figure that already contained it. Each generation added the
        /// same constant again: on the licensed 9-zone fixture a bedroom went 1.72 -> 2.44 -> 3.16 ACH and a
        /// studio 2.44 -> 3.88 -> 5.32, growing without bound over repeated round trips.
        /// </para>
        /// <para>
        /// Excluding the per-person term makes <c>TBD -> SAM -> TBD</c> a FIXED POINT for both fields:
        /// <c>ticV.factor</c> returns as the ACH basis it was imported from, and <c>freshAirRate</c> returns
        /// through <c>SupplyAirFlowPerPerson</c> independently. That is the invariant
        /// <c>VentilationAirflowMagnitudeTests</c> already declared - "a ticV rate carried on the ACH basis
        /// must round-trip unchanged, whatever the volume" - which held only while no per-person rate was
        /// present alongside it.
        /// </para>
        /// <para>
        /// <b>Subtraction, not re-derivation.</b> The total is taken from
        /// <c>Analytical.Query.CalculatedSupplyAirFlow</c> and only the per-person term is removed, so a basis
        /// added to that query in future is inherited here rather than silently dropped.
        /// </para>
        /// </summary>
        /// <param name="space">The space, which supplies both the occupancy the per-person term needs and the volume.</param>
        /// <returns>The ACH to write, or <c>double.NaN</c> when the space states no supply air at all.</returns>
        public static double VentilationAirChangesPerHour(this Space space)
        {
            if (space == null)
            {
                return double.NaN;
            }

            double airFlow = Analytical.Query.CalculatedSupplyAirFlow(space);
            if (double.IsNaN(airFlow))
            {
                return double.NaN;
            }

            double airFlow_PerPerson = VentilationSupplyAirFlowPerPersonComponent(space);
            if (!double.IsNaN(airFlow_PerPerson))
            {
                airFlow = airFlow - airFlow_PerPerson;
            }

            //Never negative: the per-person term is one of the summands, so this can only go below zero
            //through floating-point noise.
            if (airFlow < 0)
            {
                airFlow = 0;
            }

            if (!space.TryGetValue(Analytical.SpaceParameter.Volume, out double volume) || double.IsNaN(volume) || volume <= 0)
            {
                //No volume to convert through. The caller's own fallback decides what to do; saying NaN here
                //is honest, and matches what CalculatedSupplyAirFlow says when it can state nothing.
                return double.NaN;
            }

            return airFlow / volume * 3600.0;
        }

        /// <summary>
        /// The per-person summand of <see cref="Analytical.Query.CalculatedSupplyAirFlow(Space)"/>, in m3/s -
        /// <b>mirroring that method's own rule exactly</b>, because it is being subtracted from that method's
        /// result and the two must agree term for term.
        /// <para>
        /// Deliberately NOT <c>Analytical.Query.CalculatedOccupancy</c>: that helper answers 0 where
        /// <c>AreaPerPerson</c> is 0, while <c>CalculatedSupplyAirFlow</c> divides by it. Mirroring the
        /// summand keeps the subtraction exact on every input the summand itself accepts.
        /// </para>
        /// </summary>
        private static double VentilationSupplyAirFlowPerPersonComponent(Space space)
        {
            InternalCondition internalCondition = space?.InternalCondition;
            if (internalCondition == null)
            {
                return double.NaN;
            }

            if (!space.TryGetValue(Analytical.SpaceParameter.Occupancy, out double occupancy))
            {
                occupancy = double.NaN;
            }

            if (double.IsNaN(occupancy))
            {
                if (space.TryGetValue(Analytical.SpaceParameter.Area, out double area) && !double.IsNaN(area) && area > 0)
                {
                    if (internalCondition.TryGetValue(Analytical.InternalConditionParameter.AreaPerPerson, out double areaPerPerson))
                    {
                        occupancy = area / areaPerPerson;
                    }
                }
            }

            if (double.IsNaN(occupancy) || occupancy <= 0)
            {
                return double.NaN;
            }

            if (!internalCondition.TryGetValue(Analytical.InternalConditionParameter.SupplyAirFlowPerPerson, out double airFlowPerPerson))
            {
                return double.NaN;
            }

            return airFlowPerPerson * occupancy;
        }
    }
}
