// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.InputEquivalence;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

// COM-free: compiled into the licensed harness AND linked into the offline read-back tests.
namespace SAM.Analytical.Tas.DirectT3D.Validation.ReadBack
{
    /// <summary>What the export step froze, ran and wrote (<c>export.json</c>).</summary>
    public sealed class ExportManifest
    {
        public string Schema { get; set; } = "s08-readback-export/1";
        public string Label { get; set; }
        public int ProcessId { get; set; }
        public string StartedUtc { get; set; }
        public string EndedUtc { get; set; }
        public string Machine { get; set; }
        public string Runtime { get; set; }
        public string TasExecutable { get; set; }
        public string TasVersion { get; set; }
        public string SourceModelPath { get; set; }
        public string SourceModelSha256 { get; set; }
        public string SourceWeatherPath { get; set; }
        public string SourceWeatherSha256 { get; set; }
        public string FrozenModelSha256 { get; set; }
        public string FrozenModelSha256After { get; set; }
        public string FrozenWeatherSha256 { get; set; }
        public string FrozenWeatherSha256After { get; set; }
        public string WeatherSourceKind { get; set; }
        public int SourceWeatherHours { get; set; } = -1;
        public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();
        public List<string> Steps { get; set; } = new List<string>();
        public List<string> Notes { get; set; } = new List<string>();
        public List<string> Timings { get; set; } = new List<string>();
        public bool ResultReturned { get; set; }
        public string Refusal { get; set; }
        public Dictionary<string, string> Assemblies { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> Outputs { get; set; } = new Dictionary<string, string>();

        /// <summary>Set only on a negative-test derivative: what was changed on the disposable copy, and the original's hashes.</summary>
        public string Mutation { get; set; }
        public Dictionary<string, string> OriginalOutputs { get; set; }
    }

    /// <summary>
    /// <c>readback-compare</c>: the COM-free comparison of one run folder (frozen <c>input.json</c> and <c>weather.json</c>,
    /// <c>export.json</c>, the fresh-process <c>snapshot.json</c>) under the committed calibration rules.
    /// </summary>
    public static class ReadBackCompare
    {
        public const string ReportFile = "report.json";
        public const string ReportTextFile = "report.txt";
        public const string ElementsFile = "series-elements.csv.gz";

        public static ReadBackReport Run(string runDirectory, string rulesPath, string fixtureId, string purpose, GateRuleSet ruleSet, string outDirectory = null)
        {
            outDirectory = outDirectory ?? runDirectory;
            Directory.CreateDirectory(outDirectory);
            List<string> problems = new List<string>();

            string path_Manifest = Path.Combine(runDirectory, ReadBackFiles.ExportManifest);
            string path_Snapshot = Path.Combine(runDirectory, ReadBackFiles.Snapshot);
            string path_Model = Path.Combine(runDirectory, ReadBackFiles.FrozenModel);
            string path_Weather = Path.Combine(runDirectory, ReadBackFiles.FrozenWeather);

            ExportManifest manifest = File.Exists(path_Manifest) ? SnapshotJson.Read<ExportManifest>(path_Manifest) : null;
            NativeSnapshot snapshot = File.Exists(path_Snapshot) ? SnapshotJson.Read<NativeSnapshot>(path_Snapshot) : null;
            if (manifest == null) problems.Add("no export manifest");
            if (snapshot == null) problems.Add("no native snapshot");
            if (!File.Exists(path_Model)) problems.Add("no frozen model");

            if (manifest != null && snapshot != null)
            {
                problems.AddRange(snapshot.Errors.Select(x => "reader: " + x));
                problems.AddRange(Checks(manifest, snapshot, path_Model, path_Weather));
            }

            ReadBackReport report;
            if (problems.Count > 0)
            {
                report = ReadBackReport.InfrastructureFailure(fixtureId, problems);
            }
            else
            {
                AnalyticalModel model = Core.Convert.ToSAM<AnalyticalModel>(path_Model)?.FirstOrDefault();
                Weather.WeatherData weather = File.Exists(path_Weather) ? Core.Convert.ToSAM<Weather.WeatherData>(path_Weather)?.FirstOrDefault() : null;
                CalibrationRules rules = rulesPath == null ? CalibrationRules.None() : CalibrationRules.Load(rulesPath);
                ExpectedModel expected = ReadBackExpected.Build(model, weather, fixtureId);
                ReadBackComparison comparison = ReadBackComparison.Run(expected, snapshot, rules, ruleSet);
                ReadBackReport.WriteElements(comparison, Path.Combine(outDirectory, ElementsFile));
                report = ReadBackReport.Create(comparison, purpose, ElementsFile);
            }

            report.Inputs["export.json"] = SnapshotJson.Sha256(path_Manifest);
            report.Inputs["snapshot.json"] = SnapshotJson.Sha256(path_Snapshot);
            report.Inputs["input.json"] = SnapshotJson.Sha256(path_Model);
            report.Inputs["weather.json"] = SnapshotJson.Sha256(path_Weather);
            report.Inputs["exportProcess"] = manifest?.ProcessId.ToString(CultureInfo.InvariantCulture);
            report.Inputs["readerProcess"] = snapshot?.Reader?.ProcessId.ToString(CultureInfo.InvariantCulture);
            report.Inputs["mutation"] = manifest?.Mutation;

            SnapshotJson.Write(report, Path.Combine(outDirectory, ReportFile));
            File.WriteAllText(Path.Combine(outDirectory, ReportTextFile), report.ToText());
            return report;
        }

        /// <summary>The hourly records of an EPW (every non-blank line after its 8 header lines).</summary>
        public static int EpwHourCount(string path)
        {
            return File.ReadLines(path).Skip(8).Count(x => !string.IsNullOrWhiteSpace(x));
        }

        /// <summary>
        /// The owner leap-year policy for annual weather (PR-B launch): only an 8760-hour year is carried; an 8784-hour year is
        /// UNSUPPORTED-MAPPING (BLOCKED, owner decision) and is refused before conversion, never truncated. Null when carried.
        /// </summary>
        public static string EpwRefusal(int hours)
        {
            if (hours == 8760)
            {
                return null;
            }

            return hours == 8784
                ? "UNSUPPORTED-MAPPING: the EPW has 8784 hourly records (a leap year); only 8760 are carried and the year is never truncated."
                : "INPUT-INVALID: the EPW has " + hours + " hourly records; only an 8760-hour year is defined.";
        }

        /// <summary>The preconditions a comparison rests on; any failure is infrastructure, never an engineering verdict.</summary>
        public static IEnumerable<string> Checks(ExportManifest manifest, NativeSnapshot snapshot, string path_Model, string path_Weather)
        {
            if (snapshot.Schema != NativeSnapshot.CurrentSchema)
            {
                yield return "snapshot schema " + snapshot.Schema;
            }

            if (manifest.FrozenModelSha256 == null || manifest.FrozenModelSha256 != manifest.FrozenModelSha256After || SnapshotJson.Sha256(path_Model) != manifest.FrozenModelSha256)
            {
                yield return "the frozen model is not byte-identical to what the export froze";
            }

            if (manifest.FrozenWeatherSha256 != manifest.FrozenWeatherSha256After || SnapshotJson.Sha256(path_Weather) != manifest.FrozenWeatherSha256)
            {
                yield return "the frozen weather is not byte-identical to what the export froze";
            }

            // Fresh process and ordering: proven only by valid evidence. A missing or unparseable process id or time proves
            // nothing, so it fails the precondition rather than being skipped.
            ReaderInfo reader = snapshot.Reader;
            if (manifest.ProcessId <= 0 || reader == null || reader.ProcessId <= 0)
            {
                yield return "the export or reader process id is missing (export " + manifest.ProcessId + ", reader " + (reader?.ProcessId.ToString(CultureInfo.InvariantCulture) ?? "none") + "): a fresh read is not proven";
            }
            else if (reader.ProcessId == manifest.ProcessId)
            {
                yield return "the reader did not run in a fresh process (same process id as the export)";
            }

            bool endedValid = TryUtc(manifest.EndedUtc, out DateTime ended);
            bool readValid = TryUtc(reader?.StartedUtc, out DateTime read);
            if (!endedValid)
            {
                yield return "the export end time is missing or invalid ('" + manifest.EndedUtc + "'): the reader is not proven to have run after the export";
            }

            if (!readValid)
            {
                yield return "the reader start time is missing or invalid ('" + reader?.StartedUtc + "'): the reader is not proven to have run after the export";
            }

            if (endedValid && readValid && read < ended)
            {
                yield return "the reader started before the export ended";
            }

            // Required native files: the TBD always; the T3D when the export wrote one. Each must be the file the export wrote
            // and unchanged across the read. An optional file the export did not write is not a problem.
            foreach (string problem in RequiredFile("TBD", manifest.Outputs.TryGetValue(ReadBackFiles.Tbd, out string tbd) ? tbd : null, true, reader?.TbdSha256Before, reader?.TbdSha256After))
            {
                yield return problem;
            }

            bool t3dRequired = manifest.Outputs.TryGetValue(ReadBackFiles.T3d, out string t3d);
            foreach (string problem in RequiredFile("T3D", t3d, t3dRequired, reader?.T3dSha256Before, reader?.T3dSha256After))
            {
                yield return problem;
            }
        }

        private static bool TryUtc(string value, out DateTime result)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out result) && result.Kind == DateTimeKind.Utc;
        }

        private static IEnumerable<string> RequiredFile(string name, string written, bool required, string before, string after)
        {
            if (!required)
            {
                if (before != null && before != after)
                {
                    yield return "the " + name + " changed while it was read (sha256 before " + before + ", after " + (after ?? "none") + ")";
                }

                yield break;
            }

            if (string.IsNullOrEmpty(written))
            {
                yield return "the export recorded no sha256 for the " + name + " it wrote: the " + name + " the reader opened is unverified";
                yield break;
            }

            if (before == null)
            {
                yield return "the reader recorded no sha256 for the " + name + " the export wrote: the " + name + " read is unverified";
                yield break;
            }

            if (written != before)
            {
                yield return "the " + name + " the reader opened is not the one the export wrote (sha256)";
            }

            if (after == null)
            {
                yield return "the reader recorded no sha256 for the " + name + " after the read: an unchanged " + name + " is unverified";
            }
            else if (after != before)
            {
                yield return "the " + name + " changed while it was read (sha256 before " + before + ", after " + after + ")";
            }
        }
    }

    public static class ReadBackFiles
    {
        public const string FrozenModel = "input.json";
        public const string FrozenWeather = "weather.json";
        public const string ExportManifest = "export.json";
        public const string Snapshot = "snapshot.json";
        public const string Tbd = "model.tbd";
        public const string T3d = "model.t3d";
    }
}
