// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

// COM-free: compiled into the licensed harness AND linked into the offline read-back tests.
namespace SAM.Analytical.Tas.DirectT3D.Validation.Results
{
    public enum RowStatus
    {
        Pass,
        Fail,
        Blocked
    }

    /// <summary>What the subject holds for a row.</summary>
    public sealed class Observed
    {
        /// <summary>Value, NoValue (unavailable / absent / no hour) or Missing (not produced: a FAIL).</summary>
        public string State { get; set; }
        public double Value { get; set; } = double.NaN;
        public string Text { get; set; }
        public string Detail { get; set; }

        public static Observed Of(double value, string detail = null) => new Observed { State = "Value", Value = value, Detail = detail };
        public static Observed OfText(string text, string detail = null) => text == null ? Missing(detail ?? "no text") : new Observed { State = "Value", Text = text, Detail = detail };
        public static Observed None(string detail) => new Observed { State = "NoValue", Detail = detail };
        public static Observed Missing(string detail) => new Observed { State = "Missing", Detail = detail };

        public override string ToString()
        {
            if (State != "Value") return State + (Detail == null ? string.Empty : " (" + Detail + ")");
            return (Text != null ? "'" + Text + "'" : Value.ToString("R", CultureInfo.InvariantCulture)) + (Detail == null ? string.Empty : " (" + Detail + ")");
        }
    }

    public sealed class ResultRow
    {
        public string Id { get; set; }
        public string Layer { get; set; }
        public string Scope { get; set; }
        public string ZoneName { get; set; }
        public string LoadType { get; set; }
        public string Quantity { get; set; }
        public string Class { get; set; }
        public string Unit { get; set; }
        public string Expected { get; set; }
        public string Actual { get; set; }
        public double? Difference { get; set; }
        public RowStatus Status { get; set; }
        public string Reason { get; set; }
    }

    public sealed class ResultComparison
    {
        public const string CurrentSchema = "s10-result-comparison/1";

        public string Schema { get; set; } = CurrentSchema;

        /// <summary>PASS only when every required row is PASS (and there is at least one); else FAIL when any row fails, else BLOCKED.</summary>
        public string Verdict { get; set; }
        public int ExitCode { get; set; }
        public int Pass { get; set; }
        public int Fail { get; set; }
        public int Blocked { get; set; }
        public string Mutation { get; set; }
        public List<string> SubjectErrors { get; set; } = new List<string>();
        public List<ResultRow> Rows { get; set; } = new List<ResultRow>();
    }

    /// <summary>
    /// The S10 comparison (COM-free): every reference row is a required row (complete coverage by construction), each looked
    /// up in the subject. Hours are compared separately from loads and exactly, against the full set of tied maxima. A
    /// value the subject does not hold is a FAIL, never a skip; a row whose reference is blocked is BLOCKED.
    /// </summary>
    public static class ResultCompare
    {
        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;

        // S10 OD-3 result tolerances.
        public const double R1Relative = 1e-12;
        public const double R2Relative = 1e-9;
        public const double R2Absolute = 1e-9;

        public const int ExitPass = 0;
        public const int ExitFail = 10;
        public const int ExitBlocked = 11;

        private static readonly Dictionary<string, string> b1aUnits = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["consumption"] = "kWh", ["peakLoad|model"] = "kW", ["peakHour"] = "hourOfYear", ["floorArea"] = "m2", ["volume"] = "m3",
            ["peakLoad|zone"] = "W", ["designLoad"] = "W", ["unmetHours"] = "h",
        };

        public static ResultComparison Run(ResultReferenceModel reference, ResultSubjectSnapshot subject)
        {
            ResultComparison result = new ResultComparison { Mutation = subject?.Mutation };
            Mapping mapping = new Mapping(reference, subject);
            if (subject == null) result.SubjectErrors.Add("no subject");
            else result.SubjectErrors.AddRange(subject.Errors);
            if (subject?.B1a?.ParseError != null) result.SubjectErrors.Add("B1a: " + subject.B1a.ParseError);

            foreach (ReferenceRow row in reference.Rows)
            {
                ResultRow item = new ResultRow
                {
                    Id = row.Id,
                    Layer = row.Layer,
                    Scope = row.Scope,
                    ZoneName = row.ZoneName,
                    LoadType = row.LoadType,
                    Quantity = row.Quantity,
                    Class = row.Class,
                    Unit = row.Unit,
                    Expected = Describe(row),
                };

                if (row.State == ExpectedState.Blocked)
                {
                    item.Status = RowStatus.Blocked;
                    item.Reason = row.Reason;
                    item.Actual = "-";
                }
                else if (result.SubjectErrors.Count > 0)
                {
                    item.Status = RowStatus.Blocked;
                    item.Reason = "the subject could not be captured: " + string.Join("; ", result.SubjectErrors);
                    item.Actual = "-";
                }
                else
                {
                    Observed observed = mapping.Observe(row);
                    item.Actual = observed.ToString();
                    Decide(row, observed, item);
                }

                result.Rows.Add(item);
            }

            result.Pass = result.Rows.Count(x => x.Status == RowStatus.Pass);
            result.Fail = result.Rows.Count(x => x.Status == RowStatus.Fail);
            result.Blocked = result.Rows.Count(x => x.Status == RowStatus.Blocked);
            if (result.Rows.Count > 0 && result.Pass == result.Rows.Count)
            {
                result.Verdict = "PASS";
                result.ExitCode = ExitPass;
            }
            else if (result.Fail > 0)
            {
                result.Verdict = "FAIL";
                result.ExitCode = ExitFail;
            }
            else
            {
                result.Verdict = "BLOCKED";
                result.ExitCode = ExitBlocked;
            }

            return result;
        }

        private static void Decide(ReferenceRow row, Observed observed, ResultRow item)
        {
            if (observed.State == "Missing")
            {
                Set(item, RowStatus.Fail, "not produced: " + observed.Detail);
                return;
            }

            if (row.State == ExpectedState.NoValue)
            {
                if (observed.State == "NoValue") Set(item, RowStatus.Pass, row.Reason);
                else Set(item, RowStatus.Fail, "a value where none is expected (" + row.Reason + ")");
                return;
            }

            if (observed.State == "NoValue")
            {
                Set(item, RowStatus.Fail, "no value where one is expected (" + observed.Detail + ")");
                return;
            }

            if (row.Text != null)
            {
                if (string.Equals(row.Text, observed.Text, StringComparison.Ordinal)) Set(item, RowStatus.Pass, row.Reason);
                else Set(item, RowStatus.Fail, "text differs");
                return;
            }

            if (observed.Text != null || double.IsNaN(observed.Value) || double.IsInfinity(observed.Value))
            {
                Set(item, RowStatus.Fail, "not a finite number");
                return;
            }

            if (row.Allowed != null)
            {
                // Hours and indices: exact, any tied maximum accepted; a non-maximal hour is never within tolerance.
                bool any = row.Allowed.Any(x => x == observed.Value);
                item.Difference = row.Allowed.Count == 0 ? (double?)null : row.Allowed.Min(x => Math.Abs(observed.Value - x));
                if (any) Set(item, RowStatus.Pass, row.Reason + (row.Allowed.Count > 1 ? "; observed is " + (observed.Value == row.Allowed[0] ? "the earliest" : "a later") + " of " + row.Allowed.Count + " tied" : string.Empty));
                else Set(item, RowStatus.Fail, "not one of the " + row.Allowed.Count + " expected value(s)");
                return;
            }

            double difference = observed.Value - row.Value;
            item.Difference = difference;
            double tolerance = Tolerance(row.Class, row.Value);
            if (Math.Abs(difference) <= tolerance) Set(item, RowStatus.Pass, row.Reason);
            else Set(item, RowStatus.Fail, "|difference| " + Math.Abs(difference).ToString("R", ci) + " > " + row.Class + " tolerance " + tolerance.ToString("R", ci));
        }

        /// <summary>R0 exact; R1 1e-12 x max(1, |ref|); R2 1e-9 x |ref| + 1e-9; counts exact.</summary>
        public static double Tolerance(string @class, double reference)
        {
            switch (@class)
            {
                case "R1": return R1Relative * Math.Max(1, Math.Abs(reference));
                case "R2": return R2Relative * Math.Abs(reference) + R2Absolute;
                default: return 0;
            }
        }

        private static void Set(ResultRow item, RowStatus status, string reason)
        {
            item.Status = status;
            item.Reason = reason;
        }

        private static string Describe(ReferenceRow row)
        {
            switch (row.State)
            {
                case ExpectedState.Blocked: return "BLOCKED";
                case ExpectedState.NoValue: return "no value";
                default:
                    if (row.Text != null) return "'" + row.Text + "'";
                    if (row.Allowed != null) return row.Allowed.Count == 1 ? row.Allowed[0].ToString("R", ci) : "one of " + string.Join(",", row.Allowed.Take(20).Select(x => x.ToString("R", ci))) + (row.Allowed.Count > 20 ? ",..." : string.Empty);
                    return row.Value.ToString("R", ci);
            }
        }

        /// <summary>Finds the subject value of each row: zones to SAM spaces by unique exact name, SAM spaces to B1a spaces by GUID.</summary>
        private sealed class Mapping
        {
            private readonly ResultSubjectSnapshot subject;
            private readonly Dictionary<string, List<SubjectSpace>> spacesByZone = new Dictionary<string, List<SubjectSpace>>(StringComparer.Ordinal);

            public Mapping(ResultReferenceModel reference, ResultSubjectSnapshot subject)
            {
                this.subject = subject;
                foreach (ReferenceRow row in reference.Rows.Where(x => x.Scope != "model" && x.ZoneName != null))
                {
                    if (!spacesByZone.ContainsKey(row.Scope))
                    {
                        spacesByZone[row.Scope] = subject?.Spaces?.Where(x => string.Equals(x.Name, row.ZoneName, StringComparison.Ordinal)).ToList() ?? new List<SubjectSpace>();
                    }
                }
            }

            public Observed Observe(ReferenceRow row)
            {
                switch (row.Layer)
                {
                    case "Identity": return Identity(row);
                    case "SAM": return row.Scope == "model" ? SamModel(row) : SamZone(row);
                    case "B1a": return row.Scope == "model" ? B1aModel(row) : B1aZone(row);
                    default: return Observed.Missing("unknown layer " + row.Layer);
                }
            }

            private Observed Identity(ReferenceRow row)
            {
                switch (row.Quantity)
                {
                    case "samSpaceCount": return Observed.Of(subject.Spaces.Count);
                    case "b1aSpaceCount": return Observed.Of(subject.B1a.Spaces.Count);
                    case "unrelatedSpaceResults": return Observed.Of(subject.UnrelatedSpaceResults);
                    case "b1aResultSources": return Observed.OfText(string.Join(",", subject.B1a.ResultSources ?? new List<string>()));
                    case "b1aState": return Observed.OfText(subject.B1a.State, "provenance.state");
                    case "samSpace": return Observed.Of(spacesByZone[row.Scope].Count, "SAM spaces named '" + row.ZoneName + "'");
                    case "b1aSpace":
                        SubjectSpace space = Space(row.Scope);
                        return space == null ? Observed.Of(0, "no unique SAM space") : Observed.Of(subject.B1a.Spaces.Count(x => x.Guid == space.Guid && string.Equals(x.Name, space.Name, StringComparison.Ordinal)), "B1a spaces with GUID " + space.Guid + " and name '" + space.Name + "'");
                    default: return Observed.Missing("unknown identity quantity " + row.Quantity);
                }
            }

            private SubjectSpace Space(string zoneGuid)
            {
                return spacesByZone.TryGetValue(zoneGuid, out List<SubjectSpace> spaces) && spaces.Count == 1 ? spaces[0] : null;
            }

            private Observed SamModel(ReferenceRow row)
            {
                SubjectModelResult result = subject.ModelResult;
                if (result == null || !result.Present) return Observed.Missing("no model-level result");
                bool heating = row.LoadType == ResultReference.Heating;
                switch (row.Quantity)
                {
                    case "consumption": return Number(heating ? result.ConsumptionHeating : result.ConsumptionCooling, "units " + result.UnitsState);
                    case "peakLoad": return Number(heating ? result.PeakHeatingLoad : result.PeakCoolingLoad, "units " + result.UnitsState);
                    case "peakHour": return Number(heating ? result.PeakHeatingHour : result.PeakCoolingHour, null);
                    case "floorArea": return Number(result.FloorArea, null);
                    case "volume": return Number(result.Volume, null);
                    case "source": return Observed.OfText(result.Source);
                    case "unitsState": return Observed.OfText(result.UnitsState);
                    default: return Observed.Missing("unknown model quantity " + row.Quantity);
                }
            }

            private static Observed Number(double? value, string detail)
            {
                return value.HasValue ? Observed.Of(value.Value, detail) : Observed.Missing("not stored or not readable" + (detail == null ? string.Empty : ", " + detail));
            }

            private static Observed Number(int? value, string detail)
            {
                return value.HasValue ? Observed.Of(value.Value, detail) : Observed.Missing("not stored or not a whole number");
            }

            private Observed SamZone(ReferenceRow row)
            {
                SubjectSpace space = Space(row.Scope);
                if (space == null) return Observed.Missing((spacesByZone.TryGetValue(row.Scope, out List<SubjectSpace> s) ? s.Count : 0) + " SAM space(s) named '" + row.ZoneName + "'");

                List<SubjectSpaceResult> results = space.Results.Where(x => string.Equals(x.Source, ResultReference.TasSource, StringComparison.Ordinal) && string.Equals(x.LoadType, row.LoadType, StringComparison.Ordinal)).ToList();
                if (row.Quantity == "tasResultCount")
                {
                    int other = space.Results.Count(x => string.Equals(x.LoadType, row.LoadType, StringComparison.Ordinal)) - results.Count;
                    return Observed.Of(results.Count, other > 0 ? other + " result(s) of this load type from another source ignored" : null);
                }

                if (results.Count != 1) return Observed.Missing(results.Count + " Tas " + row.LoadType + " result(s) related to the space");
                SubjectSpaceResult result = results[0];
                switch (row.Quantity)
                {
                    case "reference":
                        return Observed.OfText(ResultReference.SameGuid(result.Reference, row.Scope) ? row.Scope : result.Reference ?? "(none)");
                    case "annualPeak.load": return result.AnnualPeak == null ? Observed.None("no annual peak") : Basis(result.AnnualPeak, "AnnualSimulation") ?? Observed.Of(result.AnnualPeak.Load);
                    case "annualPeak.hourOfYear": return result.AnnualPeak == null ? Observed.None("no annual peak") : Basis(result.AnnualPeak, "AnnualSimulation") ?? Hour(result.AnnualPeak.HourOfYear);
                    case "annualPeak.hourOfDay":
                        if (result.AnnualPeak == null) return Observed.None("no annual peak");
                        if (result.AnnualPeak.HourOfDay.HasValue && result.AnnualPeak.HourOfYear.HasValue && result.AnnualPeak.HourOfDay.Value != result.AnnualPeak.HourOfYear.Value % 24)
                        {
                            return Observed.Of(double.NaN, "hour of day " + result.AnnualPeak.HourOfDay + " is not hour of year " + result.AnnualPeak.HourOfYear + " mod 24");
                        }

                        return Basis(result.AnnualPeak, "AnnualSimulation") ?? Hour(result.AnnualPeak.HourOfDay);
                    case "designDayPeak.load": return result.DesignDayPeak == null ? Observed.None("no design-day peak") : Basis(result.DesignDayPeak, "DesignDay") ?? Observed.Of(result.DesignDayPeak.Load);
                    case "designDayPeak.hourOfDay": return result.DesignDayPeak == null ? Observed.None("no design-day peak") : Basis(result.DesignDayPeak, "DesignDay") ?? Hour(result.DesignDayPeak.HourOfDay);
                    case "designDayPeak.name": return result.DesignDayPeak == null ? Observed.None("no design-day peak") : Observed.OfText(result.DesignDayPeak.DesignDayName, "no design-day name");
                    case "legacy.sizingMethod": return Observed.OfText(result.SizingMethod, "no sizing method");
                    case "legacy.load": return Number(result.Load, null);
                    case "legacy.loadIndex": return Number(result.LoadIndex, null);
                    case "designLoad": return Number(result.DesignLoad, null);
                    case "unmetHours": return Number(result.UnmetHours, null);
                    default: return Observed.Missing("unknown zone quantity " + row.Quantity);
                }
            }

            private static Observed Basis(SubjectPeak peak, string basis)
            {
                return string.Equals(peak.Basis, basis, StringComparison.Ordinal) ? null : Observed.Of(double.NaN, "peak basis " + peak.Basis + ", expected " + basis);
            }

            private static Observed Hour(int? hour)
            {
                return hour.HasValue ? Observed.Of(hour.Value) : Observed.None("no hour");
            }

            private Observed B1aModel(ReferenceRow row)
            {
                string name;
                switch (row.Quantity)
                {
                    case "consumption": name = row.LoadType == ResultReference.Heating ? "consumptionHeating" : "consumptionCooling"; break;
                    case "peakLoad": name = row.LoadType == ResultReference.Heating ? "peakHeatingLoad" : "peakCoolingLoad"; break;
                    case "peakHour": name = row.LoadType == ResultReference.Heating ? "peakHeatingHour" : "peakCoolingHour"; break;
                    default: name = row.Quantity; break;
                }

                subject.B1a.Model.TryGetValue(name, out B1aMetric metric);
                return Metric(metric, "model." + name, Unit(row.Quantity, true));
            }

            private Observed B1aZone(ReferenceRow row)
            {
                SubjectSpace space = Space(row.Scope);
                if (space == null) return Observed.Missing("no unique SAM space named '" + row.ZoneName + "'");
                List<B1aSpace> spaces = subject.B1a.Spaces.Where(x => x.Guid == space.Guid).ToList();
                if (spaces.Count != 1) return Observed.Missing(spaces.Count + " B1a space(s) with GUID " + space.Guid);
                Dictionary<string, B1aMetric> condition = row.LoadType == ResultReference.Heating ? spaces[0].Heating : spaces[0].Cooling;
                condition.TryGetValue(row.Quantity, out B1aMetric metric);
                return Metric(metric, row.LoadType.ToLowerInvariant() + "." + row.Quantity, Unit(row.Quantity, false));
            }

            private static string Unit(string quantity, bool model)
            {
                return b1aUnits.TryGetValue(quantity + (model ? "|model" : "|zone"), out string unit) || b1aUnits.TryGetValue(quantity, out unit) ? unit : null;
            }

            private static Observed Metric(B1aMetric metric, string what, string unit)
            {
                if (metric == null || !metric.Present) return Observed.Missing("B1a " + what + " absent");
                if (unit != null && !string.Equals(metric.Unit, unit, StringComparison.Ordinal)) return Observed.Of(double.NaN, "B1a " + what + " unit token '" + metric.Unit + "', expected '" + unit + "'");
                if (!metric.Available) return metric.Value.HasValue ? Observed.Of(double.NaN, "unavailable but carries a value") : Observed.None("unavailable");
                return metric.Value.HasValue ? Observed.Of(metric.Value.Value) : Observed.Of(double.NaN, "available without a value");
            }
        }
    }
}
