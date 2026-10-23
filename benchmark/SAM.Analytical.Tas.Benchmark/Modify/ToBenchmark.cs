// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Linq;
using SAM.Analytical.Benchmark;

namespace SAM.Analytical.Tas.Benchmark
{
    public static partial class Modify
    {
        /// <summary>
        /// Maps a simulated SAM <see cref="AnalyticalModel"/> and its TAS run context to a B1a
        /// benchmark document (schema v1, route <c>Native-TAS</c>). Per-space measurements are read
        /// from the <see cref="SpaceSimulationResult"/> objects the TAS route related to each space
        /// (via the adjacency-cluster relation, exactly as the engine attached them — not by matching
        /// GUIDs, since a TAS result's <c>Reference</c> is the TAS zone GUID, not the SAM space GUID);
        /// the model-level annual energy/peaks come from the context's
        /// <see cref="AnalyticalModelSimulationResult"/>. This producer never re-reads a TSD or
        /// re-converts anything.
        /// </summary>
        /// <remarks>
        /// Units are emitted with explicit tokens to the SAME canonical magnitudes as the OpenStudio
        /// producer (B1b), so the two documents are directly comparable. The model annual consumption
        /// (<b>kWh</b>) and model peak loads (<b>kW</b>) are read through SAM's
        /// <c>Query.TryGetConsumptionOrPeakLoad</c>, which returns kWh/kW for a marked result as stored
        /// and converts a result SAM_Tas wrote before the units correction (unmarked, Wh/W) exactly once;
        /// a result whose units are not known is emitted as unavailable. Per-space loads and design loads are already in <b>W</b> and are
        /// emitted unscaled; hours are hour-of-year in 0..8759. A measurement that is present, finite
        /// and in range is <c>available</c>; anything missing or out of range is <c>available:false</c>
        /// with a null value. A measured zero stays a value of 0.
        /// <para>Peaks (S10 definitions, SAM_Tas S10 record):</para>
        /// <list type="bullet">
        /// <item>Hours are 0-based interval hours of the year: hour <i>h</i> is <i>h</i>:00 to <i>h</i>+1:00,
        /// local standard time, 0..8759 (TSD slot <i>n</i> is hour <i>n</i>-1).</item>
        /// <item>A space's <c>peakLoad</c>/<c>peakHour</c> are its <b>annual</b> peak: the maximum of its
        /// annual heating or cooling load over the 365-day simulation, from
        /// <see cref="SpaceSimulationResultParameter.AnnualPeak"/> (<c>Load</c>, <c>HourOfYear</c>). There is no
        /// fallback to the legacy <c>Load</c>/<c>LoadIndex</c>, which project the governing (possibly
        /// design-day) peak and keep Tas's 1-based index: a result without an annual peak emits both as
        /// unavailable. The design-day peak is never emitted; <c>designLoad</c> stays the TBD sizing load.</item>
        /// <item>The model peak is the maximum of the building heating or cooling profile, at the hour SAM
        /// stores (the earliest maximum).</item>
        /// <item>Zero rule (space and model): a peak load of 0 is a measured zero, available, and has no
        /// peak hour, so its hour is unavailable. A load above 0 without a valid hour leaves both
        /// unavailable, never a misleading zero.</item>
        /// </list>
        /// </remarks>
        /// <param name="analyticalModel">The neutral source model, carrying the TAS-attached per-space results.</param>
        /// <param name="tasBenchmarkContext">Run context: model-level result, hashes and provenance.</param>
        /// <returns>The benchmark document, or null when either argument is null.</returns>
        public static BenchmarkDocument ToBenchmark(this AnalyticalModel analyticalModel, TasBenchmarkContext tasBenchmarkContext)
        {
            if (analyticalModel == null || tasBenchmarkContext == null)
            {
                return null;
            }

            AnalyticalModelSimulationResult modelResult = tasBenchmarkContext.ModelResult;

            // The model result's Source is the authoritative TAS result source ("SAM.Analytical.Tas").
            // Per-space results are read filtered to it, so pre-existing OpenStudio or older attached
            // results in the input model can never be selected or leak into provenance.
            List<BenchmarkSpaceResult> spaces = Spaces(analyticalModel, modelResult?.Source, out HashSet<string> spaceSources);

            return new BenchmarkDocument
            {
                SchemaVersion = BenchmarkSchema.CurrentVersion,
                Provenance = Provenance(tasBenchmarkContext, modelResult, spaceSources),
                Model = Model(modelResult),
                Spaces = spaces,
            };
        }

        private static BenchmarkProvenance Provenance(TasBenchmarkContext context, AnalyticalModelSimulationResult modelResult, HashSet<string> spaceSources)
        {
            return new BenchmarkProvenance
            {
                SourceModelName = context.SourceModelName,
                SourceModelGuid = context.SourceModelGuid,
                SourceFileHash = context.SourceFileHash,
                CanonicalModelHash = context.CanonicalModelHash,
                CanonicalizationVersion = context.CanonicalizationVersion,
                SamCommit = context.SamCommit,
                RunnerCommit = context.RunnerCommit,
                Engine = new BenchmarkEngine
                {
                    Kind = EngineKind.Tas,
                    Name = context.EngineName,
                    Version = context.EngineVersion,
                    SdkVersion = context.SdkVersion,
                },
                Route = BenchmarkRoute.NativeTas,
                Weather = new BenchmarkWeather { Identity = context.WeatherIdentity, Hash = context.WeatherHash },
                DesignDaySource = context.DesignDaySource,
                RunTimestampUtc = context.RunTimestampUtc,
                DurationSeconds = context.DurationSeconds,
                State = context.State,
                ResultSources = ResultSources(modelResult, spaceSources),
                Warnings = new List<string>(context.Warnings ?? new List<string>()),
                Notes = new List<string>(context.Notes ?? new List<string>()),
            };
        }

        /// <summary>Distinct, non-empty SAM <c>Query.Source()</c> values retained from the attached results.</summary>
        private static List<string> ResultSources(AnalyticalModelSimulationResult modelResult, HashSet<string> spaceSources)
        {
            HashSet<string> sources = new HashSet<string>(spaceSources ?? new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(modelResult?.Source))
            {
                sources.Add(modelResult.Source);
            }

            return sources.ToList();
        }

        private static BenchmarkModelResult Model(AnalyticalModelSimulationResult modelResult)
        {
            // Qualified as Analytical.* — SAM.Analytical.Tas ALSO defines an
            // AnalyticalModelSimulationResultParameter (different members), and the bare name would
            // bind to it from this namespace. Consumption in kWh and peak loads in kW, whichever units
            // the result was written in (see Kilo).
            (MetricValue heatingLoad, MetricValue heatingHour) = Peak(
                Kilo(modelResult, Analytical.AnalyticalModelSimulationResultParameter.PeakHeatingLoad),
                Integer(modelResult, Analytical.AnalyticalModelSimulationResultParameter.PeakHeatingHour),
                MetricUnit.Kilowatt);

            (MetricValue coolingLoad, MetricValue coolingHour) = Peak(
                Kilo(modelResult, Analytical.AnalyticalModelSimulationResultParameter.PeakCoolingLoad),
                Integer(modelResult, Analytical.AnalyticalModelSimulationResultParameter.PeakCoolingHour),
                MetricUnit.Kilowatt);

            return new BenchmarkModelResult
            {
                ConsumptionHeating = NonNegative(Kilo(modelResult, Analytical.AnalyticalModelSimulationResultParameter.ConsumptionHeating), MetricUnit.KilowattHour),
                ConsumptionCooling = NonNegative(Kilo(modelResult, Analytical.AnalyticalModelSimulationResultParameter.ConsumptionCooling), MetricUnit.KilowattHour),
                PeakHeatingLoad = heatingLoad,
                PeakHeatingHour = heatingHour,
                PeakCoolingLoad = coolingLoad,
                PeakCoolingHour = coolingHour,
                FloorArea = NonNegative(Double(modelResult, Analytical.AnalyticalModelSimulationResultParameter.FloorArea), MetricUnit.SquareMetre),
                Volume = NonNegative(Double(modelResult, Analytical.AnalyticalModelSimulationResultParameter.Volume), MetricUnit.CubicMetre),
            };
        }

        private static List<BenchmarkSpaceResult> Spaces(AnalyticalModel analyticalModel, string resultSource, out HashSet<string> spaceSources)
        {
            spaceSources = new HashSet<string>(StringComparer.Ordinal);
            List<BenchmarkSpaceResult> result = new List<BenchmarkSpaceResult>();

            AdjacencyCluster adjacencyCluster = analyticalModel.AdjacencyCluster;
            List<Space> spaces = adjacencyCluster?.GetSpaces();
            if (spaces == null)
            {
                return result;
            }

            foreach (Space space in spaces)
            {
                if (space == null)
                {
                    continue;
                }

                // Read the results the TAS route related to THIS space (via the adjacency-cluster
                // relation), regardless of how the result's own Reference (a TAS zone GUID) is keyed,
                // and filtered to the TAS result source so pre-existing non-TAS results are ignored.
                // With no known source (a failure document) there are no measurements to emit.
                List<SpaceSimulationResult> spaceResults = string.IsNullOrWhiteSpace(resultSource)
                    ? new List<SpaceSimulationResult>()
                    : (adjacencyCluster.GetResults<SpaceSimulationResult>(space, resultSource) ?? new List<SpaceSimulationResult>());
                foreach (SpaceSimulationResult spaceResult in spaceResults)
                {
                    if (!string.IsNullOrWhiteSpace(spaceResult?.Source))
                    {
                        spaceSources.Add(spaceResult.Source);
                    }
                }

                result.Add(new BenchmarkSpaceResult
                {
                    Guid = space.Guid.ToString("N"),
                    Name = space.Name,
                    Area = NonNegative(Double(space, SpaceParameter.Area), MetricUnit.SquareMetre),
                    Volume = NonNegative(Double(space, SpaceParameter.Volume), MetricUnit.CubicMetre),
                    Heating = Condition(spaceResults, LoadType.Heating),
                    Cooling = Condition(spaceResults, LoadType.Cooling),
                });
            }

            return result;
        }

        private static BenchmarkConditionResult Condition(List<SpaceSimulationResult> spaceResults, LoadType loadType)
        {
            SpaceSimulationResult match = spaceResults.FirstOrDefault(x => IsLoadType(x, loadType));

            // The annual peak only (S10): never the legacy Load/LoadIndex, which project the governing
            // peak (a design day when it is not exceeded) with Tas's raw 1-based index.
            SpaceLoadPeak annualPeak = AnnualPeak(match);
            (MetricValue peakLoad, MetricValue peakHour) = Peak(annualPeak?.Load, annualPeak?.HourOfYear, MetricUnit.Watt);

            return new BenchmarkConditionResult
            {
                DesignLoad = NonNegative(Double(match, Analytical.SpaceSimulationResultParameter.DesignLoad), MetricUnit.Watt),
                PeakLoad = peakLoad,
                PeakHour = peakHour,
                UnmetHours = NonNegative(Double(match, Analytical.SpaceSimulationResultParameter.UnmetHours), MetricUnit.Hour),
            };
        }

        private static bool IsLoadType(SpaceSimulationResult spaceSimulationResult, LoadType loadType)
        {
            return spaceSimulationResult != null
                && spaceSimulationResult.TryGetValue(Analytical.SpaceSimulationResultParameter.LoadType, out string value)
                && string.Equals(value, loadType.ToString(), StringComparison.Ordinal);
        }

        /// <summary>
        /// A model consumption in kWh or peak load in kW, or null: SAM's
        /// <c>Query.TryGetConsumptionOrPeakLoad</c> does the only conversion (a legacy unmarked TAS
        /// result, Wh/W, ÷1000 once) and refuses a result whose units are not known.
        /// </summary>
        private static double? Kilo(AnalyticalModelSimulationResult modelResult, Analytical.AnalyticalModelSimulationResultParameter parameter)
        {
            if (modelResult != null && modelResult.TryGetConsumptionOrPeakLoad(parameter, out double value) && IsFinite(value))
            {
                return value;
            }

            return null;
        }

        private static MetricValue NonNegative(double? value, MetricUnit unit)
        {
            if (value.HasValue && IsFinite(value.Value) && value.Value >= 0)
            {
                return MetricValue.AvailableValue(value.Value, unit);
            }

            return MetricValue.Unavailable(unit);
        }

        /// <summary>
        /// The space's annual-simulation peak, or null when the result carries none (or carries a peak
        /// of another basis under that parameter).
        /// </summary>
        private static SpaceLoadPeak AnnualPeak(SpaceSimulationResult spaceSimulationResult)
        {
            if (spaceSimulationResult != null
                && spaceSimulationResult.TryGetValue(Analytical.SpaceSimulationResultParameter.AnnualPeak, out SpaceLoadPeak spaceLoadPeak)
                && spaceLoadPeak != null
                && spaceLoadPeak.Basis == LoadPeakBasis.AnnualSimulation)
            {
                return spaceLoadPeak;
            }

            return null;
        }

        /// <summary>
        /// A peak load and its hour-of-year. A load of exactly 0 is a measured zero: the load is
        /// available and the hour is not (there is no peak timestep), whatever hour was stored. A load
        /// above 0 is available only together with an integer hour in 0..8759; otherwise both are
        /// unavailable (a peak without a valid hour is missing, never a measured zero).
        /// </summary>
        private static (MetricValue load, MetricValue hour) Peak(double? loadValue, int? hourValue, MetricUnit loadUnit)
        {
            bool loadValid = loadValue.HasValue && IsFinite(loadValue.Value) && loadValue.Value >= 0;
            if (loadValid && loadValue.Value == 0)
            {
                return (MetricValue.AvailableValue(0, loadUnit), MetricValue.Unavailable(MetricUnit.HourOfYear));
            }

            bool hourValid = hourValue.HasValue && hourValue.Value >= 0 && hourValue.Value <= 8759;
            if (hourValid && loadValid)
            {
                return (MetricValue.AvailableValue(loadValue.Value, loadUnit), MetricValue.AvailableValue(hourValue.Value, MetricUnit.HourOfYear));
            }

            return (MetricValue.Unavailable(loadUnit), MetricValue.Unavailable(MetricUnit.HourOfYear));
        }

        private static double? Double(Core.SAMObject sAMObject, Enum parameter)
        {
            if (sAMObject != null && sAMObject.TryGetValue(parameter, out double value) && IsFinite(value))
            {
                return value;
            }

            return null;
        }

        private static int? Integer(Core.SAMObject sAMObject, Enum parameter)
        {
            if (sAMObject != null && sAMObject.TryGetValue(parameter, out int value))
            {
                return value;
            }

            // Integer parameters are sometimes stored as doubles (e.g. a peak hour read back as a
            // double); accept a whole-number double as the hour-of-year.
            double? asDouble = Double(sAMObject, parameter);
            if (asDouble.HasValue && asDouble.Value == Math.Truncate(asDouble.Value))
            {
                return (int)asDouble.Value;
            }

            return null;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
