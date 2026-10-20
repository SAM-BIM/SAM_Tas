// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.InputEquivalence
{
    /// <summary>
    /// The D-F2 ventilation states of S07 section 8. Requirement and delivery are separate quantities.
    /// </summary>
    public enum VentilationState
    {
        /// <summary>V0: no requirement.</summary>
        V0,

        /// <summary>V1: a requirement and no delivery selected. Tas expects no <c>ticV</c>.</summary>
        V1,

        /// <summary>V2: a Building Simulator delivery is explicitly selected (a Ventilation profile).</summary>
        V2,

        /// <summary>A Ventilation profile without a requirement. D-F2 does not define this state, so it is neither V1 nor V2.</summary>
        Indeterminate
    }

    /// <summary>The four SAM supply-air bases of S07 8a (raw stored values; NaN = not stated).</summary>
    public sealed class VentilationRequirement
    {
        /// <summary>SupplyAirFlow, m3/s.</summary>
        public double Flow { get; init; } = double.NaN;

        /// <summary>SupplyAirFlowPerArea, m3/s per m2.</summary>
        public double FlowPerArea { get; init; } = double.NaN;

        /// <summary>SupplyAirFlowPerPerson, m3/s per person.</summary>
        public double FlowPerPerson { get; init; } = double.NaN;

        /// <summary>SupplyAirChangesPerHour, 1/h.</summary>
        public double AirChangesPerHour { get; init; } = double.NaN;

        internal IEnumerable<double> Stated => new[] { Flow, FlowPerArea, FlowPerPerson, AirChangesPerHour }.Where(x => !double.IsNaN(x));
    }

    /// <summary>
    /// Independent expected values for gains, infiltration and ventilation (S07 rows 4, 7 and 8). Inputs are the raw
    /// stored SAM parameters; the stored <c>SpaceParameter.Area</c> is the density denominator (S07 section 3).
    /// </summary>
    public static partial class Query
    {
        private const double secondsPerHour = 3600;

        /// <summary>
        /// S07 4a: a gain density, W / stored Area. No gain stated is S-UNSPEC; a missing, NaN or non-positive stored
        /// Area for a stated gain is S-INVALID; a negative gain is S-INVALID; 0 is S-ZERO.
        /// </summary>
        public static ExpectedValue ExpectedGainDensity(double totalPower, double storedArea)
        {
            if (double.IsNaN(totalPower))
            {
                return ExpectedValue.Unspecified("no gain is stated");
            }

            if (double.IsInfinity(totalPower) || totalPower < 0)
            {
                return ExpectedValue.Invalid("the gain is negative or infinite");
            }

            if (double.IsNaN(storedArea) || double.IsInfinity(storedArea) || storedArea <= 0)
            {
                return ExpectedValue.Invalid("the stored SpaceParameter.Area is missing or not positive (S07 section 3)");
            }

            return ExpectedValue.FromValue(totalPower / storedArea);
        }

        /// <summary>
        /// S07 4a: people per m2. The people count is <c>Occupancy</c> when stated, otherwise stored Area / AreaPerPerson
        /// (S07 section 3); then divided by the stored Area.
        /// </summary>
        public static ExpectedValue ExpectedPeopleDensity(double occupancy, double areaPerPerson, double storedArea)
        {
            double people;
            if (!double.IsNaN(occupancy))
            {
                people = occupancy;
            }
            else if (!double.IsNaN(areaPerPerson))
            {
                if (double.IsInfinity(areaPerPerson) || areaPerPerson <= 0)
                {
                    return ExpectedValue.Invalid("AreaPerPerson is not positive");
                }

                if (double.IsNaN(storedArea) || double.IsInfinity(storedArea) || storedArea <= 0)
                {
                    return ExpectedValue.Invalid("the stored SpaceParameter.Area is missing or not positive (S07 section 3)");
                }

                people = storedArea / areaPerPerson;
            }
            else
            {
                return ExpectedValue.Unspecified("neither Occupancy nor AreaPerPerson is stated");
            }

            return ExpectedGainDensity(people, storedArea);
        }

        /// <summary>S07 7b: infiltration flow, ACH x geometric volume / 3600, in m3/s. The volume is the independent geometric one (D-F9 A), not the stored volume.</summary>
        public static ExpectedValue ExpectedInfiltrationFlow(double airChangesPerHour, double geometricVolume)
        {
            return FlowFromAirChanges(airChangesPerHour, geometricVolume, "infiltration ACH");
        }

        /// <summary>
        /// D-F2 classification from the frozen model: a stated requirement and a Ventilation profile give V2; a
        /// requirement alone V1; neither V0; a profile without a requirement is <see cref="VentilationState.Indeterminate"/>.
        /// </summary>
        public static VentilationState ClassifyVentilation(VentilationRequirement requirement, bool hasVentilationProfile)
        {
            bool hasRequirement = requirement != null && requirement.Stated.Any(x => x > 0);

            if (hasRequirement)
            {
                return hasVentilationProfile ? VentilationState.V2 : VentilationState.V1;
            }

            return hasVentilationProfile ? VentilationState.Indeterminate : VentilationState.V0;
        }

        /// <summary>
        /// The summed SAM supply requirement in m3/s: Flow + FlowPerArea x Area + FlowPerPerson x people + ACH x Volume / 3600
        /// (the four bases of S07 8a, summed). A stated base that needs a missing or non-positive area, people count
        /// or volume is S-INVALID; no base stated is S-UNSPEC. This is the requirement, not the delivery.
        /// </summary>
        public static ExpectedValue ExpectedRequiredSupplyFlow(VentilationRequirement requirement, double storedArea, double people, double geometricVolume)
        {
            if (requirement == null || !requirement.Stated.Any())
            {
                return ExpectedValue.Unspecified("no supply requirement is stated");
            }

            if (requirement.Stated.Any(x => double.IsInfinity(x) || x < 0))
            {
                return ExpectedValue.Invalid("a supply base is negative or infinite");
            }

            double sum = 0;
            if (!double.IsNaN(requirement.Flow))
            {
                sum += requirement.Flow;
            }

            if (!double.IsNaN(requirement.FlowPerArea))
            {
                if (double.IsNaN(storedArea) || double.IsInfinity(storedArea) || storedArea <= 0)
                {
                    return ExpectedValue.Invalid("SupplyAirFlowPerArea needs a positive stored Area");
                }

                sum += requirement.FlowPerArea * storedArea;
            }

            if (!double.IsNaN(requirement.FlowPerPerson))
            {
                if (double.IsNaN(people) || double.IsInfinity(people) || people < 0)
                {
                    return ExpectedValue.Invalid("SupplyAirFlowPerPerson needs a people count");
                }

                sum += requirement.FlowPerPerson * people;
            }

            if (!double.IsNaN(requirement.AirChangesPerHour))
            {
                ExpectedValue flow = FlowFromAirChanges(requirement.AirChangesPerHour, geometricVolume, "SupplyAirChangesPerHour");
                if (flow.State == SamInputState.Invalid)
                {
                    return flow;
                }

                sum += flow.Value;
            }

            return ExpectedValue.FromValue(sum);
        }

        /// <summary>
        /// S07 8b, per D-F2: V0 and V1 deliver nothing (Tas expects no <c>ticV</c>, so the expectation is S-UNSPEC and an
        /// absent native object is NOT-APPLICABLE); V2 delivers the required flow; the indeterminate state is S-INVALID.
        /// </summary>
        public static ExpectedValue ExpectedDeliveredOutdoorAir(VentilationState state, ExpectedValue requiredFlow)
        {
            switch (state)
            {
                case VentilationState.V0:
                case VentilationState.V1:
                    return ExpectedValue.Unspecified("no delivery is selected in " + state + ": the Tas intent is no ticV");

                case VentilationState.V2:
                    return requiredFlow ?? ExpectedValue.Invalid("no required flow was supplied for V2");

                default:
                    return ExpectedValue.Invalid("a Ventilation profile without a requirement is not defined by D-F2");
            }
        }

        private static ExpectedValue FlowFromAirChanges(double airChangesPerHour, double volume, string name)
        {
            if (double.IsNaN(airChangesPerHour))
            {
                return ExpectedValue.Unspecified(name + " is not stated");
            }

            if (double.IsInfinity(airChangesPerHour) || airChangesPerHour < 0)
            {
                return ExpectedValue.Invalid(name + " is negative or infinite");
            }

            if (double.IsNaN(volume) || double.IsInfinity(volume) || volume <= 0)
            {
                return ExpectedValue.Invalid(name + " needs a positive geometric volume");
            }

            return ExpectedValue.FromValue(airChangesPerHour * volume / secondsPerHour);
        }

        /// <summary>
        /// Propagates the reader states through a product of native reads (zone gain total = factor x floorArea; native
        /// flow = ACH x volume / 3600): an unreadable input makes the product unreadable, an absent one absent, a NaN a
        /// NaN, and only two numbers give a number. A state is never turned into a zero.
        /// </summary>
        public static ReaderResult Multiply(ReaderResult a, ReaderResult b, double scale = 1)
        {
            if (a == null || b == null)
            {
                return ReaderResult.Unreadable("a factor was not read");
            }

            foreach (ReaderResult r in new[] { a, b })
            {
                if (r.Kind == ReaderKind.Unreadable)
                {
                    return ReaderResult.Unreadable(r.Detail);
                }
            }

            foreach (ReaderResult r in new[] { a, b })
            {
                if (r.Kind == ReaderKind.Absent)
                {
                    return ReaderResult.Absent(r.Detail);
                }
            }

            foreach (ReaderResult r in new[] { a, b })
            {
                if (r.Kind == ReaderKind.NaN)
                {
                    return ReaderResult.NotANumber(r.Detail);
                }
            }

            return ReaderResult.FromValue(a.Value * b.Value * scale);
        }
    }
}
