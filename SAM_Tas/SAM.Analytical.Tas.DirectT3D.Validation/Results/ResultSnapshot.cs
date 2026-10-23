// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas.DirectT3D.Validation.ReadBack;
using System.Collections.Generic;
using System.Text.Json.Serialization;

// COM-free: compiled into the licensed harness AND linked into the offline read-back tests. It touches no TAS type.
namespace SAM.Analytical.Tas.DirectT3D.Validation.Results
{
    /// <summary>
    /// One raw TSD result array exactly as TSD.exe returned it: the CLR lower bound, every element, and which elements could
    /// not be read as a number. An element that is null or not numeric is recorded in <see cref="UnreadElements"/> and held as
    /// NaN in <see cref="Values"/>; it is never converted to 0 (<c>System.Convert.ToDouble(null)</c> is 0, which is why the S08
    /// P-ALIGN reader is not reused for results).
    /// </summary>
    public sealed class ResultSeries
    {
        /// <summary>Absent or Unreadable when there is no array; Finite when an array was read (elements may still be unread or NaN).</summary>
        public FieldKind Kind { get; set; }
        public int LowerBound { get; set; }
        public double[] Values { get; set; }

        /// <summary>CLR indexes of the elements that were null or not a number.</summary>
        public List<int> UnreadElements { get; set; } = new List<int>();
        public string Detail { get; set; }

        [JsonIgnore]
        public bool IsRead => Kind == FieldKind.Finite && Values != null;

        public static ResultSeries Absent(string detail = null)
        {
            return new ResultSeries { Kind = FieldKind.Absent, Detail = detail };
        }

        public static ResultSeries Unreadable(string detail)
        {
            return new ResultSeries { Kind = FieldKind.Unreadable, Detail = detail };
        }

        public static ResultSeries Of(double[] values, int lowerBound, List<int> unreadElements = null)
        {
            return new ResultSeries { Kind = FieldKind.Finite, Values = values, LowerBound = lowerBound, UnreadElements = unreadElements ?? new List<int>() };
        }
    }

    /// <summary>A raw two-dimensional native table (GetPeakZoneGains) with its bounds; recorded as a cross-check only.</summary>
    public sealed class NativeTable
    {
        public FieldKind Kind { get; set; }
        public string Detail { get; set; }
        public int[] LowerBounds { get; set; }
        public int[] Lengths { get; set; }

        /// <summary>Cells[row][column], rows and columns counted from the lower bounds.</summary>
        public List<List<NativeField>> Cells { get; set; } = new List<List<NativeField>>();
    }

    /// <summary>One TSD zone of a data set (the annual building data or a design-day data set).</summary>
    public sealed class TsdResultZone
    {
        /// <summary>The 1-based GetZoneData(i) index it was read at.</summary>
        public int Index { get; set; }
        public NativeField Number { get; set; }
        public string Name { get; set; }
        public string Guid { get; set; }
        public NativeField FloorArea { get; set; }
        public NativeField Volume { get; set; }

        /// <summary>GetAnnualZoneResult per tsdZoneArray name.</summary>
        public Dictionary<string, ResultSeries> Arrays { get; set; } = new Dictionary<string, ResultSeries>();
    }

    /// <summary>One TSD design-day data set (GetCoolingDesignData(i) or GetHeatingDesignData(i)).</summary>
    public sealed class TsdDesignDataset
    {
        /// <summary>"Cooling" or "Heating".</summary>
        public string Role { get; set; }

        /// <summary>The 0-based GetCooling/HeatingDesignData(i) index it was read at.</summary>
        public int Index { get; set; }
        public string Name { get; set; }
        public string Detail { get; set; }
        public List<TsdResultZone> Zones { get; set; } = new List<TsdResultZone>();

        /// <summary>GetPeakZoneGains of the role's load array; a native cross-check, never a reference value.</summary>
        public NativeTable PeakZoneGains { get; set; }
    }

    /// <summary>
    /// The independent result read of one TSD (<c>TsdResultRead</c>), in a fresh process, read-only, with the file's sha256
    /// before and after. Consumed COM-free by <c>ResultReference</c>.
    /// </summary>
    public sealed class TsdResultSnapshot
    {
        public const string CurrentSchema = "s10-tsd-result-snapshot/1";

        public string Schema { get; set; } = CurrentSchema;
        public string Path { get; set; }
        public long Bytes { get; set; }
        public int ProcessId { get; set; }
        public string StartedUtc { get; set; }
        public string Machine { get; set; }
        public string ReaderAssembly { get; set; }
        public string ReaderAssemblySha256 { get; set; }
        public string Sha256Before { get; set; }
        public string Sha256After { get; set; }

        /// <summary>Fatal reader problems (missing, stub, open, COM). Any entry blocks every result row.</summary>
        public List<string> Errors { get; set; } = new List<string>();
        public NativeField FirstDay { get; set; }
        public NativeField LastDay { get; set; }

        /// <summary>GetAnnualBuildingResult per tsdBuildingArray name (heatingProfile, coolingProfile).</summary>
        public Dictionary<string, ResultSeries> Building { get; set; } = new Dictionary<string, ResultSeries>();
        public List<TsdResultZone> Zones { get; set; } = new List<TsdResultZone>();

        /// <summary>The building data's GetPeakZoneGains per load array name; a native cross-check, never a reference value.</summary>
        public Dictionary<string, NativeTable> AnnualPeakZoneGains { get; set; } = new Dictionary<string, NativeTable>();
        public List<TsdDesignDataset> DesignDatasets { get; set; } = new List<TsdDesignDataset>();
    }

    // ---- the subject: what SAM imported and what B1a emitted --------------------------------------------------------------

    /// <summary>A <c>SpaceLoadPeak</c> as stored on a SAM result.</summary>
    public sealed class SubjectPeak
    {
        public string Basis { get; set; }
        public double Load { get; set; }
        public int? HourOfYear { get; set; }
        public int? HourOfDay { get; set; }
        public string DesignDayName { get; set; }
    }

    /// <summary>One <c>SpaceSimulationResult</c> related to a SAM space, with the values S10 checks, exactly as stored.</summary>
    public sealed class SubjectSpaceResult
    {
        public string Source { get; set; }
        public string Reference { get; set; }
        public string LoadType { get; set; }
        public double? Load { get; set; }
        public int? LoadIndex { get; set; }
        public string SizingMethod { get; set; }
        public double? DesignLoad { get; set; }
        public double? UnmetHours { get; set; }
        public string DesignDayName { get; set; }
        public SubjectPeak AnnualPeak { get; set; }
        public SubjectPeak DesignDayPeak { get; set; }
    }

    public sealed class SubjectSpace
    {
        public string Guid { get; set; }
        public string Name { get; set; }
        public List<SubjectSpaceResult> Results { get; set; } = new List<SubjectSpaceResult>();
    }

    /// <summary>
    /// The model-level result the production converter (<c>Convert.ToSAM_AnalyticalModelSimulationResult</c>) made from the
    /// TSD, captured in a fresh process. Consumption and peaks are read through SAM's units query (kWh, kW); null = not read.
    /// </summary>
    public sealed class SubjectModelResult
    {
        public bool Present { get; set; }
        public string Source { get; set; }
        public string UnitsState { get; set; }
        public double? ConsumptionHeating { get; set; }
        public double? ConsumptionCooling { get; set; }
        public double? PeakHeatingLoad { get; set; }
        public double? PeakCoolingLoad { get; set; }
        public int? PeakHeatingHour { get; set; }
        public int? PeakCoolingHour { get; set; }
        public double? FloorArea { get; set; }
        public double? Volume { get; set; }
    }

    /// <summary>One B1a metric exactly as the document holds it.</summary>
    public sealed class B1aMetric
    {
        public bool Present { get; set; }
        public bool Available { get; set; }
        public double? Value { get; set; }
        public string Unit { get; set; }
    }

    public sealed class B1aSpace
    {
        public string Guid { get; set; }
        public string Name { get; set; }
        public Dictionary<string, B1aMetric> Heating { get; set; } = new Dictionary<string, B1aMetric>();
        public Dictionary<string, B1aMetric> Cooling { get; set; } = new Dictionary<string, B1aMetric>();
    }

    /// <summary>The B1a document, parsed COM-free and independently of the producer library (plain JSON).</summary>
    public sealed class B1aDocument
    {
        public string ParseError { get; set; }
        public string State { get; set; }
        public List<string> ResultSources { get; set; } = new List<string>();
        public Dictionary<string, B1aMetric> Model { get; set; } = new Dictionary<string, B1aMetric>();
        public List<B1aSpace> Spaces { get; set; } = new List<B1aSpace>();
    }

    /// <summary>Everything under test, captured in a fresh process (<c>results-subject</c>).</summary>
    public sealed class ResultSubjectSnapshot
    {
        public const string CurrentSchema = "s10-result-subject/1";

        public string Schema { get; set; } = CurrentSchema;
        public int ProcessId { get; set; }
        public string StartedUtc { get; set; }
        public string ModelJsonPath { get; set; }
        public string ModelJsonSha256 { get; set; }
        public string TsdPath { get; set; }
        public string TsdSha256Before { get; set; }
        public string TsdSha256After { get; set; }
        public string B1aPath { get; set; }
        public string B1aSha256 { get; set; }

        /// <summary>A mutation applied to copies of the files (offline negatives); null for the produced files.</summary>
        public string Mutation { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public SubjectModelResult ModelResult { get; set; } = new SubjectModelResult();
        public List<SubjectSpace> Spaces { get; set; } = new List<SubjectSpace>();

        /// <summary>Space results in the adjacency cluster that are related to no space.</summary>
        public int UnrelatedSpaceResults { get; set; }
        public B1aDocument B1a { get; set; } = new B1aDocument();
    }


    // ---- the TBD design-day coverage (S10 R2) ------------------------------------------------------------------------

    /// <summary>One TBD design day of a group, every field with its reader state (a read that threw is recorded in Detail).</summary>
    public sealed class DesignCoverageDay
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public string DayType { get; set; }
        public NativeField Repetitions { get; set; }
        public string Detail { get; set; }
    }

    /// <summary>One TBD design-day group (GetCoolingDesignDay(i) / GetHeatingDesignDay(i)).</summary>
    public sealed class DesignCoverageGroup
    {
        /// <summary>"Cooling" or "Heating".</summary>
        public string Role { get; set; }
        public int Index { get; set; }
        public string Name { get; set; }

        /// <summary>Why the group or its day list could not be read completely; null when it was.</summary>
        public string Detail { get; set; }
        public List<DesignCoverageDay> Days { get; set; } = new List<DesignCoverageDay>();
    }

    /// <summary>
    /// The design-day groups the TBD asks the sizing run to simulate: the independent expectation of which TSD design-day
    /// data sets must exist (S10 R2). Read in a fresh process, read-only, by <c>TbdDesignCoverageRead</c>. Unlike an
    /// enumeration that stops at the first exception, a read that throws is recorded in <see cref="Errors"/> or the group's
    /// Detail, so unreadable coverage is never mistaken for "no design days".
    /// </summary>
    public sealed class TbdDesignCoverage
    {
        public const string CurrentSchema = "s10-tbd-design-coverage/1";

        public string Schema { get; set; } = CurrentSchema;
        public string Path { get; set; }
        public int ProcessId { get; set; }
        public string StartedUtc { get; set; }
        public string ReaderAssembly { get; set; }
        public string ReaderAssemblySha256 { get; set; }
        public string Sha256Before { get; set; }
        public string Sha256After { get; set; }

        /// <summary>Open, enumeration or file problems. Any entry makes the coverage of both roles unreadable.</summary>
        public List<string> Errors { get; set; } = new List<string>();
        public List<DesignCoverageGroup> Groups { get; set; } = new List<DesignCoverageGroup>();
    }

    /// <summary>
    /// COM element conversion for the S10 reader, kept COM-free so the offline tests exercise it: a value is a number only
    /// when it is a CLR numeric type; null, text and anything else are unread, never 0.
    /// </summary>
    public static class NativeConvert
    {
        public static bool TryNumber(object element, out double number)
        {
            switch (element)
            {
                case float f: number = f; return true;
                case double d: number = d; return true;
                case int i: number = i; return true;
                case short s: number = s; return true;
                case long l: number = l; return true;
                case byte b: number = b; return true;
                case decimal m: number = (double)m; return true;
                default: number = double.NaN; return false;
            }
        }

        /// <summary>A one-dimensional native array, element by element, with its lower bound; unread elements are listed.</summary>
        public static ResultSeries Series(object value)
        {
            if (!(value is System.Array array) || array.Rank != 1)
            {
                return value == null ? ResultSeries.Absent("null") : ResultSeries.Unreadable("not a one-dimensional array (" + value.GetType().Name + ")");
            }

            int lowerBound = array.GetLowerBound(0);
            double[] values = new double[array.Length];
            List<int> unread = new List<int>();
            for (int i = 0; i < values.Length; i++)
            {
                if (TryNumber(array.GetValue(lowerBound + i), out double number))
                {
                    values[i] = number;
                }
                else
                {
                    values[i] = double.NaN;
                    unread.Add(lowerBound + i);
                }
            }

            return ResultSeries.Of(values, lowerBound, unread);
        }

        /// <summary>A two-dimensional native table with its bounds; every cell keeps its reader state.</summary>
        public static NativeTable Table(object value)
        {
            if (!(value is System.Array array) || array.Rank != 2)
            {
                return new NativeTable { Kind = value == null ? FieldKind.Absent : FieldKind.Unreadable, Detail = value == null ? "null" : "not a two-dimensional array" };
            }

            NativeTable result = new NativeTable
            {
                Kind = FieldKind.Finite,
                LowerBounds = new[] { array.GetLowerBound(0), array.GetLowerBound(1) },
                Lengths = new[] { array.GetLength(0), array.GetLength(1) },
            };

            for (int r = 0; r < result.Lengths[0]; r++)
            {
                List<NativeField> row = new List<NativeField>();
                for (int c = 0; c < result.Lengths[1]; c++)
                {
                    row.Add(Field(array.GetValue(result.LowerBounds[0] + r, result.LowerBounds[1] + c)));
                }

                result.Cells.Add(row);
            }

            return result;
        }

        public static NativeField Field(object value)
        {
            return TryNumber(value, out double number) ? NativeField.Of(number) : value == null ? NativeField.Absent("null") : NativeField.Unreadable("not a number (" + value.GetType().Name + ")");
        }
    }
}
