// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas.DirectT3D.Validation.ReadBack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

// COM-free: compiled into the licensed harness AND linked into the offline read-back tests. Every mutation works on
// copies (re-materialised from the serialised inputs for each case); the originals are never touched.
namespace SAM.Analytical.Tas.DirectT3D.Validation.Results
{
    /// <summary>The serialised inputs of one comparison, so each negative case starts from its own fresh copy.</summary>
    public sealed class ResultInputs
    {
        public string TsdJson { get; set; }
        public string TbdJson { get; set; }
        public CalibrationRules Rules { get; set; }

        /// <summary>The workflow's saved SAM model (<c>&lt;tbd&gt;.json</c>).</summary>
        public string ModelJson { get; set; }
        public string B1aJson { get; set; }

        /// <summary>The captured model-level result (<see cref="SubjectModelResult"/>), serialised.</summary>
        public string ModelResultJson { get; set; }
    }

    /// <summary>One materialised copy of the inputs, which a mutation changes.</summary>
    public sealed class ResultCase
    {
        public TsdResultSnapshot Tsd;
        public NativeSnapshot Tbd;
        public AnalyticalModel Model;
        public JsonNode B1a;
        public SubjectModelResult ModelResult;
        public bool TsdChanged, ModelChanged, B1aChanged, ModelResultChanged;

        public static ResultCase From(ResultInputs inputs)
        {
            return new ResultCase
            {
                Tsd = JsonSerializer.Deserialize<TsdResultSnapshot>(inputs.TsdJson, SnapshotJson.Options),
                Tbd = JsonSerializer.Deserialize<NativeSnapshot>(inputs.TbdJson, SnapshotJson.Options),
                Model = Core.Convert.ToSAM<AnalyticalModel>(inputs.ModelJson).FirstOrDefault(),
                B1a = JsonNode.Parse(inputs.B1aJson),
                ModelResult = JsonSerializer.Deserialize<SubjectModelResult>(inputs.ModelResultJson, SnapshotJson.Options),
            };
        }

        /// <summary>The subject as the licensed capture builds it, from this copy.</summary>
        public ResultSubjectSnapshot Subject(string mutation)
        {
            ResultSubjectSnapshot subject = new ResultSubjectSnapshot { Mutation = mutation, ModelResult = ModelResult };
            if (Model == null) subject.Errors.Add("the saved model could not be read");
            else ResultSubject.Extract(subject, Model);
            subject.B1a = ResultSubject.ParseB1a(B1a?.ToJsonString() ?? string.Empty);
            return subject;
        }

        public ResultComparison Compare(CalibrationRules rules, string mutation, out ResultReferenceModel reference)
        {
            reference = ResultReference.Build(Tsd, Tbd, rules);
            return ResultCompare.Run(reference, Subject(mutation));
        }
    }

    public sealed class NegativeOutcome
    {
        public string Case { get; set; }
        public string Description { get; set; }
        public bool Applicable { get; set; } = true;
        public string Applied { get; set; }

        /// <summary>FAIL or BLOCKED.</summary>
        public string ExpectedStatus { get; set; }

        /// <summary>The rows the intended mechanism must decide (ALL = every row).</summary>
        public List<string> IntendedRows { get; set; } = new List<string>();
        public string Verdict { get; set; }
        public int ExitCode { get; set; }
        public int Fail { get; set; }
        public int Blocked { get; set; }

        /// <summary>The intended rows with their status, as found.</summary>
        public List<string> Evidence { get; set; } = new List<string>();

        /// <summary>True when the verdict is not PASS and every intended row has the expected status (or the case is not applicable).</summary>
        public bool AsIntended { get; set; }

        [System.Text.Json.Serialization.JsonIgnore] public ResultCase Copy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore] public ResultComparison Comparison { get; set; }
    }

    /// <summary>
    /// The S10 negative controls: each mutates a copy of the reference input (the TSD/TBD reads) or of the subject (the saved
    /// model, the captured model result, the B1a document) and must FAIL or be BLOCKED by its intended mechanism. Targets are
    /// chosen from the unmutated reference, so the same cases run on the synthetic offline triple and on the licensed runs.
    /// </summary>
    public static class ResultNegatives
    {
        private delegate string Mutation(ResultCase copy, Targets targets, NegativeOutcome outcome);

        private sealed class Spec
        {
            public string Name, Description, Expected;
            public Mutation Apply;
        }

        /// <summary>Rows of the unmutated reference the cases pick their targets from.</summary>
        private sealed class Targets
        {
            public ResultReferenceModel Reference;
            public ResultCase Baseline;

            public IEnumerable<ReferenceRow> Rows(string layer, string quantity) => Reference.Rows.Where(x => x.Layer == layer && x.Quantity == quantity && x.State == ExpectedState.Value);

            public ReferenceRow Row(string layer, string scope, string loadType, string quantity) => Reference.Rows.FirstOrDefault(x => x.Layer == layer && x.Scope == scope && x.LoadType == loadType && x.Quantity == quantity);
        }

        public static string ControlName => "control";

        private static readonly List<Spec> specs = new List<Spec>
        {
            new Spec { Name = "f4-b1a-hour-is-raw-index", Expected = "FAIL", Description = "F4 replay: B1a peakHour = Tas's raw 1-based index (hour + 1)", Apply = F4 },
            new Spec { Name = "f5-b1a-governing-design-day", Expected = "FAIL", Description = "F5 replay: B1a peakLoad/peakHour from the governing design day", Apply = F5 },
            new Spec { Name = "f6-zero-peak-hour-0-available", Expected = "FAIL", Description = "F6 replay: a zero peak's placeholder hour 0 emitted as an available B1a hour", Apply = F6 },
            new Spec { Name = "sam-hour-plus-1", Expected = "FAIL", Description = "SAM AnnualPeak hour of year + 1", Apply = (c, t, o) => ShiftHour(c, t, o, +1) },
            new Spec { Name = "sam-hour-minus-1", Expected = "FAIL", Description = "SAM AnnualPeak hour of year - 1", Apply = (c, t, o) => ShiftHour(c, t, o, -1) },
            new Spec { Name = "sam-heating-cooling-swapped", Expected = "FAIL", Description = "SAM heating and cooling results swapped", Apply = Swap },
            new Spec { Name = "b1a-model-consumption-x1000", Expected = "FAIL", Description = "B1a model consumption x 1000 (unit slip)", Apply = (c, t, o) => ScaleModel(c, t, o, "consumption", 1000) },
            new Spec { Name = "b1a-space-peak-x1000", Expected = "FAIL", Description = "B1a space peakLoad x 1000 (unit slip)", Apply = ScaleSpacePeak },
            new Spec { Name = "b1a-model-consumption-1e-6", Expected = "FAIL", Description = "B1a model consumption off by 1e-6 relative", Apply = (c, t, o) => ScaleModel(c, t, o, "consumption", 1 + 1e-6) },
            new Spec { Name = "sam-model-peak-hour-plus-1", Expected = "FAIL", Description = "captured SAM model peak hour + 1", Apply = ModelHour },
            new Spec { Name = "sam-non-maximal-hour", Expected = "FAIL", Description = "SAM AnnualPeak hour set to an hour that is not a maximum", Apply = NonMaximal },
            new Spec { Name = "sam-missing-result", Expected = "FAIL", Description = "a space's Tas result removed", Apply = (c, t, o) => Results(c, t, o, "missing") },
            new Spec { Name = "sam-duplicate-result", Expected = "FAIL", Description = "a space's Tas result duplicated", Apply = (c, t, o) => Results(c, t, o, "duplicate") },
            new Spec { Name = "sam-non-tas-source", Expected = "FAIL", Description = "a space's result from a non-Tas source", Apply = (c, t, o) => Results(c, t, o, "foreign") },
            new Spec { Name = "b1a-metric-missing", Expected = "FAIL", Description = "a B1a space designLoad metric removed", Apply = MissingMetric },
            new Spec { Name = "tsd-part-year", Expected = "BLOCKED", Description = "the TSD simulation window is days 1..364", Apply = (c, t, o) => { c.Tsd.LastDay = NativeField.Of(364); c.TsdChanged = true; o.IntendedRows.Add("ALL"); return "lastDay 364"; } },
            new Spec { Name = "tsd-stub", Expected = "BLOCKED", Description = "a stub TSD (refused simulation)", Apply = (c, t, o) => { c.Tsd.Errors.Add("TSD is a 22-byte stub (a refused simulation); it is not opened"); c.TsdChanged = true; o.IntendedRows.Add("ALL"); return "reader error: stub"; } },
            new Spec { Name = "tsd-non-unique-zone-name", Expected = "BLOCKED", Description = "two TSD zones share a name", Apply = NonUniqueName },
            new Spec { Name = "tsd-negative-load", Expected = "BLOCKED", Description = "an unexpected negative element in a zone load array", Apply = (c, t, o) => Element(c, t, o, s => { s.Values[0] = -1; return "element 0 = -1"; }) },
            new Spec { Name = "tsd-unread-element", Expected = "BLOCKED", Description = "a zone load element that could not be read (never a zero)", Apply = (c, t, o) => Element(c, t, o, s => { s.Values[100] = double.NaN; s.UnreadElements.Add(s.LowerBound + 100); return "element 100 unread"; }) },
            new Spec { Name = "tsd-nan-element", Expected = "BLOCKED", Description = "a NaN zone load element", Apply = (c, t, o) => Element(c, t, o, s => { s.Values[200] = double.NaN; return "element 200 NaN"; }) },
            new Spec { Name = "tsd-short-array", Expected = "BLOCKED", Description = "a zone load array of 8759 elements", Apply = (c, t, o) => Element(c, t, o, s => { s.Values = s.Values.Take(s.Values.Length - 1).ToArray(); return "last element dropped"; }) },
            new Spec { Name = "tsd-lower-bound-0", Expected = "BLOCKED", Description = "a zone load array at lower bound 0", Apply = (c, t, o) => Element(c, t, o, s => { s.LowerBound = 0; return "lower bound 0"; }) },
            new Spec { Name = "tsd-absent-array", Expected = "BLOCKED", Description = "a zone load array not returned", Apply = (c, t, o) => Element(c, t, o, s => { s.Kind = FieldKind.Absent; s.Values = null; s.Detail = "null"; return "array absent"; }) },
            new Spec { Name = "tsd-building-profile-absent", Expected = "BLOCKED", Description = "the building heating profile not returned", Apply = BuildingAbsent },
            new Spec { Name = "tsd-loads-in-kw", Expected = "BLOCKED", Description = "zone loads divided by 1000 (kW): the balance no longer closes", Apply = (c, t, o) => Loads(c, t, o, "kw") },
            new Spec { Name = "tsd-load-roles-swapped", Expected = "BLOCKED", Description = "zone heating and cooling load arrays swapped: the balance no longer closes", Apply = (c, t, o) => Loads(c, t, o, "swap") },
            new Spec { Name = "tsd-design-day-short", Expected = "BLOCKED", Description = "a design-day series one hour short", Apply = DesignShort },
        };

        public static IReadOnlyList<string> Names => specs.Select(x => x.Name).ToList();

        /// <summary>Runs the control and every case. The control must reproduce <paramref name="expectedControlVerdict"/> when given.</summary>
        public static List<NegativeOutcome> Run(ResultInputs inputs, string expectedControlVerdict = null)
        {
            List<NegativeOutcome> result = new List<NegativeOutcome>();
            ResultCase baseline = ResultCase.From(inputs);
            ResultComparison control = baseline.Compare(inputs.Rules, null, out ResultReferenceModel reference);
            result.Add(new NegativeOutcome
            {
                Case = ControlName,
                Description = "unmutated copies of every input",
                ExpectedStatus = expectedControlVerdict ?? control.Verdict,
                Verdict = control.Verdict,
                ExitCode = control.ExitCode,
                Fail = control.Fail,
                Blocked = control.Blocked,
                AsIntended = expectedControlVerdict == null || control.Verdict == expectedControlVerdict,
                Applied = "none",
                Copy = baseline,
                Comparison = control,
            });

            Targets targets = new Targets { Reference = reference, Baseline = baseline };
            foreach (Spec spec in specs)
            {
                ResultCase copy = ResultCase.From(inputs);
                NegativeOutcome outcome = new NegativeOutcome { Case = spec.Name, Description = spec.Description, ExpectedStatus = spec.Expected, Copy = copy };
                string applied;
                try
                {
                    applied = spec.Apply(copy, targets, outcome);
                }
                catch (Exception exception)
                {
                    applied = null;
                    outcome.Evidence.Add("mutation failed: " + exception.GetType().Name + ": " + exception.Message);
                }

                if (applied == null)
                {
                    outcome.Applicable = false;
                    outcome.Applied = outcome.Evidence.FirstOrDefault() ?? "not applicable to this input";
                    outcome.AsIntended = !outcome.Evidence.Any(x => x.StartsWith("mutation failed", StringComparison.Ordinal));
                    result.Add(outcome);
                    continue;
                }

                outcome.Applied = applied;
                ResultComparison comparison = copy.Compare(inputs.Rules, spec.Name, out _);
                outcome.Comparison = comparison;
                outcome.Verdict = comparison.Verdict;
                outcome.ExitCode = comparison.ExitCode;
                outcome.Fail = comparison.Fail;
                outcome.Blocked = comparison.Blocked;
                RowStatus expected = spec.Expected == "FAIL" ? RowStatus.Fail : RowStatus.Blocked;
                List<ResultRow> intended = outcome.IntendedRows.Contains("ALL") ? comparison.Rows : comparison.Rows.Where(x => outcome.IntendedRows.Contains(x.Id)).ToList();
                outcome.Evidence.AddRange(intended.Take(10).Select(x => x.Id + ": " + x.Status + " (" + x.Reason + ")"));
                outcome.AsIntended = comparison.Verdict != "PASS" && intended.Count > 0 && intended.All(x => x.Status == expected);
                result.Add(outcome);
            }

            return result;
        }

        // ---- target helpers -------------------------------------------------------------------------------------------

        /// <summary>A zone/type whose annual peak is positive with a single maximum (so a shifted hour is never a tie).</summary>
        private static ReferenceRow UniqueHourRow(Targets t, Func<ReferenceRow, bool> also = null)
        {
            return t.Rows("SAM", "annualPeak.hourOfYear").FirstOrDefault(x => x.Allowed != null && x.Allowed.Count == 1 && (also == null || also(x)));
        }

        private static SpaceSimulationResult TasResult(AdjacencyCluster cluster, string zoneName, string loadType, out Space space)
        {
            space = cluster.GetSpaces().Single(x => x.Name == zoneName);
            return cluster.GetResults<SpaceSimulationResult>(space, ResultReference.TasSource).Single(x => x.TryGetValue(global::SAM.Analytical.SpaceSimulationResultParameter.LoadType, out string type) && type == loadType);
        }

        private static void Replace(ResultCase copy, AdjacencyCluster cluster)
        {
            copy.Model = new AnalyticalModel(copy.Model, cluster);
            copy.ModelChanged = true;
        }

        private static JsonNode B1aSpace(ResultCase copy, string zoneName)
        {
            return copy.B1a["spaces"].AsArray().Single(x => (string)x["name"] == zoneName);
        }

        // ---- mutations ------------------------------------------------------------------------------------------------

        private static string F4(ResultCase copy, Targets t, NegativeOutcome o)
        {
            ReferenceRow row = t.Rows("B1a", "peakHour").FirstOrDefault(x => x.Allowed != null && x.Allowed.Count == 1);
            if (row == null) return null;
            JsonNode metric = B1aSpace(copy, row.ZoneName)[row.LoadType.ToLowerInvariant()]["peakHour"];
            double hour = (double)metric["value"];
            metric["value"] = hour + 1;
            copy.B1aChanged = true;
            o.IntendedRows.Add(row.Id);
            return "zone '" + row.ZoneName + "' " + row.LoadType + " peakHour " + hour + " -> " + (hour + 1);
        }

        private static string F5(ResultCase copy, Targets t, NegativeOutcome o)
        {
            foreach (ReferenceRow dd in t.Rows("SAM", "designDayPeak.load"))
            {
                ReferenceRow annual = t.Row("B1a", dd.Scope, dd.LoadType, "peakLoad");
                ReferenceRow ddHour = t.Row("SAM", dd.Scope, dd.LoadType, "designDayPeak.hourOfDay");
                if (annual?.State != ExpectedState.Value || dd.Value == annual.Value || ddHour?.State != ExpectedState.Value) continue;

                JsonNode condition = B1aSpace(copy, dd.ZoneName)[dd.LoadType.ToLowerInvariant()];
                condition["peakLoad"]["value"] = dd.Value;
                condition["peakLoad"]["available"] = true;
                condition["peakHour"]["value"] = ddHour.Allowed[0];
                condition["peakHour"]["available"] = true;
                copy.B1aChanged = true;
                o.IntendedRows.Add(annual.Id);
                return "zone '" + dd.ZoneName + "' " + dd.LoadType + " B1a peak = design day " + dd.Value + " W at index " + ddHour.Allowed[0];
            }

            o.Evidence.Add("no zone has a design-day peak different from its annual peak");
            return null;
        }

        private static string F6(ResultCase copy, Targets t, NegativeOutcome o)
        {
            ReferenceRow model = t.Reference.Rows.FirstOrDefault(x => x.Layer == "B1a" && x.Scope == "model" && x.Quantity == "peakHour" && x.State == ExpectedState.NoValue);
            if (model != null)
            {
                JsonNode metric = copy.B1a["model"][model.LoadType == ResultReference.Heating ? "peakHeatingHour" : "peakCoolingHour"];
                metric["value"] = 0;
                metric["available"] = true;
                copy.B1aChanged = true;
                o.IntendedRows.Add(model.Id);
                return "model " + model.LoadType + " all-zero profile: peak hour 0 emitted as available";
            }

            ReferenceRow space = t.Reference.Rows.FirstOrDefault(x => x.Layer == "B1a" && x.Scope != "model" && x.Quantity == "peakHour" && x.State == ExpectedState.NoValue);
            if (space != null)
            {
                JsonNode metric = B1aSpace(copy, space.ZoneName)[space.LoadType.ToLowerInvariant()]["peakHour"];
                metric["value"] = 0;
                metric["available"] = true;
                copy.B1aChanged = true;
                o.IntendedRows.Add(space.Id);
                return "zone '" + space.ZoneName + "' " + space.LoadType + " zero peak: hour 0 emitted as available";
            }

            o.Evidence.Add("no zero peak in this input (no model profile or zone load is zero all year)");
            return null;
        }

        private static string ShiftHour(ResultCase copy, Targets t, NegativeOutcome o, int shift)
        {
            ReferenceRow row = UniqueHourRow(t, x => x.Value + shift >= 0 && x.Value + shift <= 8759);
            if (row == null) return null;
            AdjacencyCluster cluster = copy.Model.AdjacencyCluster;
            SpaceSimulationResult result = TasResult(cluster, row.ZoneName, row.LoadType, out _);
            result.TryGetValue(global::SAM.Analytical.SpaceSimulationResultParameter.AnnualPeak, out SpaceLoadPeak peak);
            int hour = peak.HourOfYear.Value + shift;
            result.SetValue(global::SAM.Analytical.SpaceSimulationResultParameter.AnnualPeak, new SpaceLoadPeak(peak) { HourOfYear = hour, HourOfDay = hour % 24 });
            cluster.AddObject(result);
            Replace(copy, cluster);
            o.IntendedRows.Add(row.Id);
            return "zone '" + row.ZoneName + "' " + row.LoadType + " AnnualPeak hour " + peak.HourOfYear + " -> " + hour;
        }

        private static string NonMaximal(ResultCase copy, Targets t, NegativeOutcome o)
        {
            ReferenceRow row = UniqueHourRow(t);
            if (row == null) return null;
            TsdResultZone zone = t.Baseline.Tsd.Zones.Single(x => x.Guid == row.Scope);
            double[] values = zone.Arrays[row.LoadType == ResultReference.Heating ? "heatingLoad" : "coolingLoad"].Values;
            int hour = Array.FindIndex(values, x => x < values.Max());
            AdjacencyCluster cluster = copy.Model.AdjacencyCluster;
            SpaceSimulationResult result = TasResult(cluster, row.ZoneName, row.LoadType, out _);
            result.TryGetValue(global::SAM.Analytical.SpaceSimulationResultParameter.AnnualPeak, out SpaceLoadPeak peak);
            result.SetValue(global::SAM.Analytical.SpaceSimulationResultParameter.AnnualPeak, new SpaceLoadPeak(peak) { HourOfYear = hour, HourOfDay = hour % 24 });
            cluster.AddObject(result);
            Replace(copy, cluster);
            o.IntendedRows.Add(row.Id);
            return "zone '" + row.ZoneName + "' " + row.LoadType + " AnnualPeak hour " + peak.HourOfYear + " -> " + hour + " (value " + values[hour] + " < max " + values.Max() + ")";
        }

        private static string Swap(ResultCase copy, Targets t, NegativeOutcome o)
        {
            foreach (ReferenceRow heating in t.Rows("SAM", "annualPeak.load").Where(x => x.LoadType == ResultReference.Heating))
            {
                ReferenceRow cooling = t.Row("SAM", heating.Scope, ResultReference.Cooling, "annualPeak.load");
                if (cooling?.State != ExpectedState.Value || cooling.Value == heating.Value) continue;

                AdjacencyCluster cluster = copy.Model.AdjacencyCluster;
                SpaceSimulationResult h = TasResult(cluster, heating.ZoneName, ResultReference.Heating, out _);
                SpaceSimulationResult c = TasResult(cluster, heating.ZoneName, ResultReference.Cooling, out _);
                h.SetValue(global::SAM.Analytical.SpaceSimulationResultParameter.LoadType, ResultReference.Cooling);
                c.SetValue(global::SAM.Analytical.SpaceSimulationResultParameter.LoadType, ResultReference.Heating);
                cluster.AddObject(h);
                cluster.AddObject(c);
                Replace(copy, cluster);
                o.IntendedRows.Add(heating.Id);
                o.IntendedRows.Add(cooling.Id);
                return "zone '" + heating.ZoneName + "' heating and cooling results swapped";
            }

            o.Evidence.Add("no zone with different heating and cooling annual peaks");
            return null;
        }

        private static string ScaleModel(ResultCase copy, Targets t, NegativeOutcome o, string quantity, double factor)
        {
            ReferenceRow row = t.Reference.Rows.FirstOrDefault(x => x.Layer == "B1a" && x.Scope == "model" && x.Quantity == quantity && x.State == ExpectedState.Value && x.Value != 0);
            if (row == null)
            {
                o.Evidence.Add("no non-zero model " + quantity);
                return null;
            }

            JsonNode metric = copy.B1a["model"][row.LoadType == ResultReference.Heating ? "consumptionHeating" : "consumptionCooling"];
            double value = (double)metric["value"];
            metric["value"] = value * factor;
            copy.B1aChanged = true;
            o.IntendedRows.Add(row.Id);
            return "B1a model " + row.LoadType + " " + quantity + " " + value + " x " + factor;
        }

        private static string ScaleSpacePeak(ResultCase copy, Targets t, NegativeOutcome o)
        {
            ReferenceRow row = t.Rows("B1a", "peakLoad").FirstOrDefault(x => x.Scope != "model" && x.Value != 0);
            if (row == null) return null;
            JsonNode metric = B1aSpace(copy, row.ZoneName)[row.LoadType.ToLowerInvariant()]["peakLoad"];
            double value = (double)metric["value"];
            metric["value"] = value * 1000;
            copy.B1aChanged = true;
            o.IntendedRows.Add(row.Id);
            return "zone '" + row.ZoneName + "' " + row.LoadType + " B1a peakLoad " + value + " x 1000";
        }

        private static string ModelHour(ResultCase copy, Targets t, NegativeOutcome o)
        {
            ReferenceRow row = t.Reference.Rows.FirstOrDefault(x => x.Layer == "SAM" && x.Scope == "model" && x.Quantity == "peakHour" && x.State == ExpectedState.Value && x.Value < 8759);
            if (row == null || copy.ModelResult == null) return null;
            if (row.LoadType == ResultReference.Heating) copy.ModelResult.PeakHeatingHour += 1;
            else copy.ModelResult.PeakCoolingHour += 1;
            copy.ModelResultChanged = true;
            o.IntendedRows.Add(row.Id);
            return "captured model " + row.LoadType + " peak hour + 1";
        }

        private static string Results(ResultCase copy, Targets t, NegativeOutcome o, string kind)
        {
            ReferenceRow row = t.Rows("SAM", "tasResultCount").FirstOrDefault();
            if (row == null) return null;
            AdjacencyCluster cluster = copy.Model.AdjacencyCluster;
            SpaceSimulationResult result = TasResult(cluster, row.ZoneName, row.LoadType, out Space space);
            switch (kind)
            {
                case "missing":
                    cluster.RemoveObject(result);
                    break;
                case "duplicate":
                    SpaceSimulationResult duplicate = new SpaceSimulationResult(Guid.NewGuid(), result);
                    cluster.AddObject(duplicate);
                    cluster.AddRelation(space, duplicate);
                    break;
                default:
                    SpaceSimulationResult foreign = new SpaceSimulationResult(result.Name, "SAM.Analytical.OpenStudio", result.Reference);
                    foreach (global::SAM.Analytical.SpaceSimulationResultParameter parameter in new[] { global::SAM.Analytical.SpaceSimulationResultParameter.LoadType, global::SAM.Analytical.SpaceSimulationResultParameter.Load, global::SAM.Analytical.SpaceSimulationResultParameter.LoadIndex, global::SAM.Analytical.SpaceSimulationResultParameter.SizingMethod, global::SAM.Analytical.SpaceSimulationResultParameter.DesignLoad, global::SAM.Analytical.SpaceSimulationResultParameter.UnmetHours, global::SAM.Analytical.SpaceSimulationResultParameter.AnnualPeak, global::SAM.Analytical.SpaceSimulationResultParameter.DesignDayPeak })
                    {
                        if (result.TryGetValue(parameter, out object value)) foreign.SetValue(parameter, value);
                    }

                    cluster.RemoveObject(result);
                    cluster.AddObject(foreign);
                    cluster.AddRelation(space, foreign);
                    break;
            }

            Replace(copy, cluster);
            o.IntendedRows.Add(row.Id);
            return "zone '" + row.ZoneName + "' " + row.LoadType + " Tas result " + kind;
        }

        private static string MissingMetric(ResultCase copy, Targets t, NegativeOutcome o)
        {
            ReferenceRow row = t.Reference.Rows.FirstOrDefault(x => x.Layer == "B1a" && x.Quantity == "designLoad" && x.State == ExpectedState.Value);
            if (row == null) return null;
            B1aSpace(copy, row.ZoneName)[row.LoadType.ToLowerInvariant()].AsObject().Remove("designLoad");
            copy.B1aChanged = true;
            o.IntendedRows.Add(row.Id);
            return "zone '" + row.ZoneName + "' " + row.LoadType + " B1a designLoad removed";
        }

        private static string NonUniqueName(ResultCase copy, Targets t, NegativeOutcome o)
        {
            if (copy.Tsd.Zones.Count < 2)
            {
                o.Evidence.Add("one zone: no second zone to share its name");
                return null;
            }

            string name = copy.Tsd.Zones[1].Name;
            copy.Tsd.Zones[1].Name = copy.Tsd.Zones[0].Name;
            copy.TsdChanged = true;
            o.IntendedRows.AddRange(t.Reference.Rows.Where(x => x.Scope != "model" && x.Scope == copy.Tsd.Zones[1].Guid && x.Layer != "Identity").Select(x => x.Id));
            return "zone '" + name + "' renamed '" + copy.Tsd.Zones[0].Name + "'";
        }

        private static string Element(ResultCase copy, Targets t, NegativeOutcome o, Func<ResultSeries, string> change)
        {
            ReferenceRow row = t.Rows("SAM", "annualPeak.load").FirstOrDefault();
            if (row == null) return null;
            TsdResultZone zone = copy.Tsd.Zones.Single(x => x.Guid == row.Scope);
            ResultSeries series = zone.Arrays[row.LoadType == ResultReference.Heating ? "heatingLoad" : "coolingLoad"];
            string applied = change(series);
            copy.TsdChanged = true;
            o.IntendedRows.Add(row.Id);
            o.IntendedRows.Add(t.Row("B1a", row.Scope, row.LoadType, "peakLoad").Id);
            return "zone '" + row.ZoneName + "' " + row.LoadType + " load: " + applied;
        }

        private static string BuildingAbsent(ResultCase copy, Targets t, NegativeOutcome o)
        {
            copy.Tsd.Building["heatingProfile"] = ResultSeries.Absent("null");
            copy.TsdChanged = true;
            o.IntendedRows.AddRange(t.Reference.Rows.Where(x => x.Scope == "model" && x.LoadType == ResultReference.Heating).Select(x => x.Id));
            return "building heatingProfile absent";
        }

        private static string Loads(ResultCase copy, Targets t, NegativeOutcome o, string kind)
        {
            TsdResultZone zone = copy.Tsd.Zones.FirstOrDefault(x => x.Arrays["heatingLoad"].Values?.Any(v => v > 0) == true || x.Arrays["coolingLoad"].Values?.Any(v => v > 0) == true);
            if (zone == null)
            {
                o.Evidence.Add("no zone with a non-zero load");
                return null;
            }

            if (kind == "kw")
            {
                foreach (string name in new[] { "heatingLoad", "coolingLoad" })
                {
                    zone.Arrays[name].Values = zone.Arrays[name].Values.Select(x => x / 1000).ToArray();
                }
            }
            else
            {
                ResultSeries heating = zone.Arrays["heatingLoad"];
                zone.Arrays["heatingLoad"] = zone.Arrays["coolingLoad"];
                zone.Arrays["coolingLoad"] = heating;
            }

            copy.TsdChanged = true;
            o.IntendedRows.AddRange(t.Reference.Rows.Where(x => x.Scope == zone.Guid && x.Quantity == "annualPeak.load").Select(x => x.Id));
            return "zone '" + zone.Name + "' loads " + (kind == "kw" ? "divided by 1000" : "heating/cooling arrays swapped");
        }

        private static string DesignShort(ResultCase copy, Targets t, NegativeOutcome o)
        {
            TsdDesignDataset dataset = copy.Tsd.DesignDatasets.FirstOrDefault(x => x.Zones.Count > 0);
            if (dataset == null)
            {
                o.Evidence.Add("the TSD has no design-day data set");
                return null;
            }

            string load = dataset.Role == ResultReference.Cooling ? "coolingLoad" : "heatingLoad";
            ResultSeries series = dataset.Zones[0].Arrays[load];
            series.Values = series.Values.Take(series.Values.Length - 1).ToArray();
            copy.TsdChanged = true;
            o.IntendedRows.AddRange(t.Reference.Rows.Where(x => x.Layer == "SAM" && x.LoadType == dataset.Role && x.Quantity.StartsWith("designDayPeak", StringComparison.Ordinal)).Select(x => x.Id));
            return dataset.Role + " design-day data set '" + dataset.Name + "' zone '" + dataset.Zones[0].Name + "' " + load + " one element short";
        }
    }
}
