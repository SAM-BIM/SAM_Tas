// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Systems;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Create
    {
        /// <summary>
        /// How far a recirculation airflow may stand outside its law's range, and how far a ventilation flow
        /// may depart from its design, and still be the same flow [l/s]. Licensed evidence (SAM#111 PR5B):
        /// the native solver holds both to within 0.03 l/s over a full year.
        /// </summary>
        public const double RecirculationCoolingTolerance_Flow_Lps = 0.05;

        /// <summary>
        /// How far the coil outlet may stand from the published table and still be the table [K] - half the
        /// table's own 0.1 K resolution. Licensed evidence: 0 K.
        /// </summary>
        public const double RecirculationCoolingTolerance_Table_K = 0.05;

        /// <summary>The temperature difference [K] below which a coil is judged to have done nothing.</summary>
        public const double RecirculationCoolingTolerance_Idle_K = 1e-6;

        /// <summary>The departure [l/s] from the ideal law that is counted - and only counted - as off-law.</summary>
        public const double RecirculationCoolingOffLaw_Lps = 0.5;

        /// <summary>Air rho.cp [J/(m3.K)] as TAS Systems uses it (measured, SAM#111 PR5A Phase 0).</summary>
        public const double RecirculationCoolingRhoCp = 1214.4;

        /// <summary>
        /// PR5B (SAM#111): reduces one branch's hourly native results to the checked evidence. COM-free - the
        /// native reading is <c>Modify.RecirculationCoolingResults</c>'s; this only judges the numbers.
        /// <para>
        /// <b>What is refused.</b> Any heating hour; any hour the coil cooled with its mixed return below the
        /// cooling-enable temperature; any recirculation airflow outside the law's range; a coil outlet off
        /// the published table held at its edges; any ventilation flow off its design; any missing or
        /// non-finite value. <b>What is only counted</b> is the native within-hour departure from the ideal
        /// law - measured, and not a property of the declared control.
        /// </para>
        /// </summary>
        /// <param name="canonicalDeviation_Lps">Each hour's largest departure of any ventilation flow of the same air system from its design, or null when not read.</param>
        public static RecirculationCoolingResult RecirculationCoolingResult(
            MechanicalVentilationRecirculationCooling mechanicalVentilationRecirculationCooling,
            int startHour,
            IList<double> outdoorTemperature_C,
            IList<double> mixedReturnTemperature_C,
            IList<double> operatingAirFlow_Lps,
            IList<double> supplyTemperature_C,
            IList<double> canonicalDeviation_Lps)
        {
            if (mechanicalVentilationRecirculationCooling == null)
            {
                return null;
            }

            MechanicalVentilationCoolingSettings settings = mechanicalVentilationRecirculationCooling.Settings;
            List<string> refusals = new List<string>();

            string label = string.Format("Recirculation cooling of air system {0}", mechanicalVentilationRecirculationCooling.Guid_AirSystem);

            int count = mixedReturnTemperature_C?.Count ?? 0;

            if (settings == null || settings.Refusal() != null)
            {
                refusals.Add(string.Format("{0} carries no valid cooling settings to judge it against.", label));
                count = 0;
            }
            else if (count == 0
                || outdoorTemperature_C == null || outdoorTemperature_C.Count != count
                || operatingAirFlow_Lps == null || operatingAirFlow_Lps.Count != count
                || supplyTemperature_C == null || supplyTemperature_C.Count != count
                || (canonicalDeviation_Lps != null && canonicalDeviation_Lps.Count != count))
            {
                refusals.Add(string.Format("{0}: TAS did not answer a complete hourly series for the coil inlet, outlet, airflow and the outdoor air.", label));
                count = 0;
            }

            double ceiling = settings?.MaximumOperatingAirFlow_Lps ?? double.NaN;
            double minimum = settings?.MinimumOperatingAirFlow_Lps ?? double.NaN;
            double gate = settings?.CoolingEnableTemperature_C ?? double.NaN;

            double[] q = new double[count];
            double[] tMix = new double[count];
            double[] tOut = new double[count];
            double[] odb = new double[count];

            int count_Cooling = 0, count_Heating = 0, count_BelowGate = 0, count_GateViolation = 0, count_OutOfRange = 0, count_OffLaw = 0, count_InDomain = 0, count_NonFinite = 0;
            double maximumTableError = 0, maximumCanonicalDeviation = 0, cooling_Wh = 0;

            VentilationUnitPerformanceTable table = settings?.SupplyAirTemperatureTable;
            int[] axisIndexes = new int[MechanicalVentilationCoolingSettings.AxisNames.Count];
            for (int i = 0; count != 0 && i < axisIndexes.Length; i++)
            {
                axisIndexes[i] = table.AxisIndex(MechanicalVentilationCoolingSettings.AxisNames[i]);
            }

            for (int h = 0; h < count; h++)
            {
                q[h] = operatingAirFlow_Lps[h];
                tMix[h] = mixedReturnTemperature_C[h];
                tOut[h] = supplyTemperature_C[h];
                odb[h] = outdoorTemperature_C[h];

                if (!Finite(q[h]) || !Finite(tMix[h]) || !Finite(tOut[h]) || !Finite(odb[h]))
                {
                    count_NonFinite++;
                    continue;
                }

                bool cooling = tOut[h] < tMix[h] - RecirculationCoolingTolerance_Idle_K;
                bool heating = tOut[h] > tMix[h] + RecirculationCoolingTolerance_Idle_K;

                if (cooling)
                {
                    count_Cooling++;
                    cooling_Wh += q[h] / 1000.0 * RecirculationCoolingRhoCp * (tMix[h] - tOut[h]);
                }

                if (heating)
                {
                    count_Heating++;
                }

                if (tMix[h] < gate)
                {
                    count_BelowGate++;
                    if (cooling)
                    {
                        count_GateViolation++;
                    }
                }

                if (q[h] < minimum - RecirculationCoolingTolerance_Flow_Lps || q[h] > ceiling + RecirculationCoolingTolerance_Flow_Lps)
                {
                    count_OutOfRange++;
                }

                double law = ceiling * settings.FlowFractionByControlTemperature.FlowFraction(tMix[h]);
                if (System.Math.Abs(q[h] - law) > RecirculationCoolingOffLaw_Lps)
                {
                    count_OffLaw++;
                }

                double[] coordinates = new double[table.AxisCount];
                coordinates[axisIndexes[0]] = odb[h];
                coordinates[axisIndexes[1]] = tMix[h];
                coordinates[axisIndexes[2]] = q[h];

                if (table.InDomain(coordinates))
                {
                    count_InDomain++;
                }

                //The coil: idle below the gate; above it, never warmer than its inlet and otherwise the
                //published value held at the table's edges.
                double lookup = table.Value(VentilationUnitPerformanceOutput.Name_SupplyAirTemperature, coordinates, Analytical.Enums.PerformanceDomainPolicy.ClampToDomain);
                double expected = tMix[h] < gate ? tMix[h] : System.Math.Min(tMix[h], lookup);
                maximumTableError = System.Math.Max(maximumTableError, System.Math.Abs(tOut[h] - expected));

                if (canonicalDeviation_Lps != null)
                {
                    maximumCanonicalDeviation = System.Math.Max(maximumCanonicalDeviation, Finite(canonicalDeviation_Lps[h]) ? canonicalDeviation_Lps[h] : double.PositiveInfinity);
                }
            }

            if (count_NonFinite != 0)
            {
                refusals.Add(string.Format("{0}: {1} hour(s) answered a value that is not finite.", label, count_NonFinite));
            }

            if (count_Heating != 0)
            {
                refusals.Add(string.Format("{0} heated the air in {1} hour(s); the module never heats.", label, count_Heating));
            }

            if (count_GateViolation != 0)
            {
                refusals.Add(string.Format("{0} cooled in {1} hour(s) with its mixed return below the {2} C cooling-enable temperature.", label, count_GateViolation, gate));
            }

            if (count_OutOfRange != 0)
            {
                refusals.Add(string.Format("{0} carried a recirculation airflow outside {1:0.###}..{2:0.###} l/s in {3} hour(s).", label, minimum, ceiling, count_OutOfRange));
            }

            if (count != 0 && maximumTableError > RecirculationCoolingTolerance_Table_K)
            {
                refusals.Add(string.Format("{0}: the coil outlet departs from the published table by up to {1:0.####} K.", label, maximumTableError));
            }

            if (maximumCanonicalDeviation > RecirculationCoolingTolerance_Flow_Lps)
            {
                refusals.Add(string.Format("{0}: a ventilation flow of the same air system departed from its design by up to {1:0.####} l/s - the cooling loop disturbed the ventilation.", label, maximumCanonicalDeviation));
            }

            return new RecirculationCoolingResult(
                mechanicalVentilationRecirculationCooling.Guid_AirHandlingUnit,
                mechanicalVentilationRecirculationCooling.Guid_AirSystem,
                startHour,
                q,
                tMix,
                tOut,
                odb,
                ceiling,
                minimum,
                gate,
                count_Cooling,
                count_Heating,
                count_BelowGate,
                count_GateViolation,
                count_OutOfRange,
                count_OffLaw,
                count_InDomain,
                maximumTableError,
                canonicalDeviation_Lps == null ? double.NaN : maximumCanonicalDeviation,
                cooling_Wh / 1000.0,
                refusals);
        }

        private static bool Finite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
