// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

// COM-free: compiled into the licensed harness AND linked into the offline read-back tests. It reads SAM objects and the
// B1a JSON as they are stored; it computes nothing (the reference does).
namespace SAM.Analytical.Tas.DirectT3D.Validation.Results
{
    /// <summary>
    /// The S10 subject: the values under test, exactly as stored. The SAM layer is the workflow's saved model (every
    /// <see cref="SpaceSimulationResult"/> related to each space, whatever its source) and the model-level result the
    /// production converter made from the TSD; the B1a layer is the benchmark document, parsed as plain JSON.
    /// </summary>
    public static class ResultSubject
    {
        public static readonly string[] B1aModelMetrics = { "consumptionHeating", "consumptionCooling", "peakHeatingLoad", "peakHeatingHour", "peakCoolingLoad", "peakCoolingHour", "floorArea", "volume" };

        public static readonly string[] B1aConditionMetrics = { "designLoad", "peakLoad", "peakHour", "unmetHours" };

        /// <summary>Fills the spaces and their related results from the saved model.</summary>
        public static void Extract(ResultSubjectSnapshot subject, AnalyticalModel analyticalModel)
        {
            AdjacencyCluster adjacencyCluster = analyticalModel?.AdjacencyCluster;
            if (adjacencyCluster == null)
            {
                subject.Errors.Add("the saved model has no adjacency cluster");
                return;
            }

            HashSet<Guid> related = new HashSet<Guid>();
            foreach (Space space in adjacencyCluster.GetSpaces() ?? new List<Space>())
            {
                if (space == null) continue;

                SubjectSpace item = new SubjectSpace { Guid = space.Guid.ToString("N"), Name = space.Name };
                foreach (SpaceSimulationResult result in adjacencyCluster.GetResults<SpaceSimulationResult>(space) ?? new List<SpaceSimulationResult>())
                {
                    if (result == null) continue;
                    related.Add(result.Guid);
                    item.Results.Add(Result(result));
                }

                subject.Spaces.Add(item);
            }

            subject.UnrelatedSpaceResults = (adjacencyCluster.GetObjects<SpaceSimulationResult>() ?? new List<SpaceSimulationResult>()).Count(x => x != null && !related.Contains(x.Guid));
        }

        public static SubjectSpaceResult Result(SpaceSimulationResult result)
        {
            return new SubjectSpaceResult
            {
                Source = result.Source,
                Reference = result.Reference,
                LoadType = Text(result, global::SAM.Analytical.SpaceSimulationResultParameter.LoadType),
                Load = Number(result, global::SAM.Analytical.SpaceSimulationResultParameter.Load),
                LoadIndex = Whole(Number(result, global::SAM.Analytical.SpaceSimulationResultParameter.LoadIndex)),
                SizingMethod = Text(result, global::SAM.Analytical.SpaceSimulationResultParameter.SizingMethod),
                DesignLoad = Number(result, global::SAM.Analytical.SpaceSimulationResultParameter.DesignLoad),
                UnmetHours = Number(result, global::SAM.Analytical.SpaceSimulationResultParameter.UnmetHours),
                AnnualPeak = Peak(result, global::SAM.Analytical.SpaceSimulationResultParameter.AnnualPeak),
                DesignDayPeak = Peak(result, global::SAM.Analytical.SpaceSimulationResultParameter.DesignDayPeak),
            };
        }

        /// <summary>The model-level result as SAM reads it: consumption and peaks through the units query (kWh, kW).</summary>
        public static SubjectModelResult ModelResult(AnalyticalModelSimulationResult result)
        {
            if (result == null)
            {
                return new SubjectModelResult { Present = false };
            }

            return new SubjectModelResult
            {
                Present = true,
                Source = result.Source,
                UnitsState = result.ResultUnitsState().ToString(),
                ConsumptionHeating = Kilo(result, global::SAM.Analytical.AnalyticalModelSimulationResultParameter.ConsumptionHeating),
                ConsumptionCooling = Kilo(result, global::SAM.Analytical.AnalyticalModelSimulationResultParameter.ConsumptionCooling),
                PeakHeatingLoad = Kilo(result, global::SAM.Analytical.AnalyticalModelSimulationResultParameter.PeakHeatingLoad),
                PeakCoolingLoad = Kilo(result, global::SAM.Analytical.AnalyticalModelSimulationResultParameter.PeakCoolingLoad),
                PeakHeatingHour = Whole(Number(result, global::SAM.Analytical.AnalyticalModelSimulationResultParameter.PeakHeatingHour)),
                PeakCoolingHour = Whole(Number(result, global::SAM.Analytical.AnalyticalModelSimulationResultParameter.PeakCoolingHour)),
                FloorArea = Number(result, global::SAM.Analytical.AnalyticalModelSimulationResultParameter.FloorArea),
                Volume = Number(result, global::SAM.Analytical.AnalyticalModelSimulationResultParameter.Volume),
            };
        }

        /// <summary>The B1a document as plain JSON: every metric keeps whether it is present, available, its value and unit token.</summary>
        public static B1aDocument ParseB1a(string json)
        {
            B1aDocument result = new B1aDocument();
            try
            {
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    JsonElement root = document.RootElement;
                    if (root.TryGetProperty("provenance", out JsonElement provenance) && provenance.ValueKind == JsonValueKind.Object)
                    {
                        result.State = provenance.TryGetProperty("state", out JsonElement state) && state.ValueKind == JsonValueKind.String ? state.GetString() : null;
                        if (provenance.TryGetProperty("resultSources", out JsonElement sources) && sources.ValueKind == JsonValueKind.Array)
                        {
                            result.ResultSources = sources.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.ToString()).ToList();
                        }
                    }

                    JsonElement model = root.TryGetProperty("model", out JsonElement m) ? m : default;
                    foreach (string name in B1aModelMetrics)
                    {
                        result.Model[name] = Metric(model, name);
                    }

                    if (root.TryGetProperty("spaces", out JsonElement spaces) && spaces.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement space in spaces.EnumerateArray())
                        {
                            B1aSpace item = new B1aSpace
                            {
                                Guid = space.TryGetProperty("guid", out JsonElement guid) && guid.ValueKind == JsonValueKind.String ? guid.GetString() : null,
                                Name = space.TryGetProperty("name", out JsonElement name) && name.ValueKind == JsonValueKind.String ? name.GetString() : null,
                            };

                            JsonElement heating = space.TryGetProperty("heating", out JsonElement h) ? h : default;
                            JsonElement cooling = space.TryGetProperty("cooling", out JsonElement c) ? c : default;
                            foreach (string metric in B1aConditionMetrics)
                            {
                                item.Heating[metric] = Metric(heating, metric);
                                item.Cooling[metric] = Metric(cooling, metric);
                            }

                            result.Spaces.Add(item);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                result.ParseError = exception.GetType().Name + ": " + exception.Message;
            }

            return result;
        }

        private static B1aMetric Metric(JsonElement parent, string name)
        {
            if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(name, out JsonElement element) || element.ValueKind != JsonValueKind.Object)
            {
                return new B1aMetric { Present = false };
            }

            B1aMetric result = new B1aMetric { Present = true };
            result.Available = element.TryGetProperty("available", out JsonElement available) && available.ValueKind == JsonValueKind.True;
            result.Value = element.TryGetProperty("value", out JsonElement value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : (double?)null;
            result.Unit = element.TryGetProperty("unit", out JsonElement unit) && unit.ValueKind == JsonValueKind.String ? unit.GetString() : null;
            return result;
        }

        private static SubjectPeak Peak(SpaceSimulationResult result, global::SAM.Analytical.SpaceSimulationResultParameter parameter)
        {
            if (!result.TryGetValue(parameter, out SpaceLoadPeak peak) || peak == null)
            {
                return null;
            }

            return new SubjectPeak { Basis = peak.Basis.ToString(), Load = peak.Load, HourOfYear = peak.HourOfYear, HourOfDay = peak.HourOfDay, DesignDayName = peak.DesignDayName };
        }

        private static double? Kilo(AnalyticalModelSimulationResult result, global::SAM.Analytical.AnalyticalModelSimulationResultParameter parameter)
        {
            return result.TryGetConsumptionOrPeakLoad(parameter, out double value) ? value : (double?)null;
        }

        /// <summary>A stored number (any CLR numeric type), or null; text and other values are not numbers.</summary>
        private static double? Number(Core.SAMObject sAMObject, Enum parameter)
        {
            if (sAMObject is Core.ParameterizedSAMObject parameterized && parameterized.TryGetValue(parameter, out object value) && NativeConvert.TryNumber(value, out double number))
            {
                return number;
            }

            return null;
        }

        private static int? Whole(double? value)
        {
            return value.HasValue && value.Value == Math.Truncate(value.Value) && Math.Abs(value.Value) < int.MaxValue ? (int)value.Value : (int?)null;
        }

        private static string Text(Core.SAMObject sAMObject, Enum parameter)
        {
            if (sAMObject is Core.ParameterizedSAMObject parameterized && parameterized.TryGetValue(parameter, out object value) && value != null)
            {
                return value.ToString();
            }

            return null;
        }
    }
}
