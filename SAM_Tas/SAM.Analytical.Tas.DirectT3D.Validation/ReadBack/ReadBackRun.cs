// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SAM.Analytical.Tas.DirectT3D.Validation.ReadBack
{
    /// <summary>
    /// The S08 PR-B read-back modes. Each mode is one step and is meant to run in its own process (see
    /// <c>Run-S08-ReadBack.ps1</c>): the export writes the persisted files and exits, the read reopens them in a fresh
    /// process, and the compare is COM-free. Exit codes: 0 only for an actual PASS (see <see cref="ReadBackExit"/>).
    /// </summary>
    public static class ReadBackRun
    {
        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;

        public const string FrozenModelFile = ReadBackFiles.FrozenModel;
        public const string FrozenWeatherFile = ReadBackFiles.FrozenWeather;
        public const string ExportManifestFile = ReadBackFiles.ExportManifest;
        public const string SnapshotFile = ReadBackFiles.Snapshot;
        public const string TbdFile = ReadBackFiles.Tbd;
        public const string T3dFile = ReadBackFiles.T3d;

        public static int Run(string[] args)
        {
            string mode = args[0];
            Dictionary<string, string> options = Options(args);
            try
            {
                switch (mode)
                {
                    case "readback-models":
                        return Models(Require(options, "out"));

                    case "readback-fixtures":
                        return Fixtures(Require(options, "out"));

                    case "readback-export":
                        return Export(Require(options, "model"), Require(options, "out"), options.TryGetValue("weather", out string weather) ? weather : null, options.ContainsKey("sizing"), options.ContainsKey("simulate"), options.TryGetValue("label", out string label) ? label : null);

                    case "readback-read":
                        return Read(Require(options, "tbd"), options.TryGetValue("t3d", out string t3d) ? t3d : null, Require(options, "out"));

                    case "readback-tsd":
                        {
                            TsdSnapshot tsd = TsdReadBack.Read(Require(options, "tsd"));
                            SnapshotJson.Write(tsd, Require(options, "out"));
                            Console.WriteLine("tsd: pid={0} zones={1} errors={2} days {3}..{4}", tsd.ProcessId, tsd.Zones.Count, tsd.Errors.Count, tsd.FirstDay, tsd.LastDay);
                            foreach (string error in tsd.Errors) Console.WriteLine("  error: " + error);
                            return tsd.Errors.Count == 0 ? 0 : ReadBackExit.Infrastructure;
                        }

                    case "readback-calibrate":
                        return Calibrate(Require(options, "runs"), options.TryGetValue("align-tsd", out string alignTsd) ? alignTsd : null, Require(options, "out"));

                    case "readback-diff":
                        {
                            SnapshotDiffResult diff = SnapshotDiff.Compare(Require(options, "a"), Require(options, "b"), options.TryGetValue("purpose", out string why) ? why : null);
                            SnapshotJson.Write(diff, Require(options, "out"));
                            Console.WriteLine("diff: {0} leaf value(s) compared, {1} differ", diff.Compared, diff.DifferenceCount);
                            foreach (KeyValuePair<string, int> pattern in diff.ByPattern.OrderByDescending(x => x.Value).Take(40)) Console.WriteLine("  {0,6} {1}", pattern.Value, pattern.Key);
                            return 0;
                        }

                    case "readback-negative":
                        return Negative(Require(options, "run"), Require(options, "case"), Require(options, "out"));

                    case "readback-compare":
                        {
                            string ruleSet = options.TryGetValue("rule-set", out string r) ? r : "interim";
                            if (ruleSet != "interim" && ruleSet != "provisional" && ruleSet != "approved")
                            {
                                throw new UsageException("--rule-set interim|provisional|approved");
                            }

                            // approved: OD-G1..G3 as the owner approved them on 2026-10-21 (S08 PR-C), with T-VENT-INACTIVE.
                            InputEquivalence.GateRuleSet gateRuleSet = ruleSet == "approved" ? InputEquivalence.GateRuleSet.Approved : ruleSet == "provisional" ? InputEquivalence.GateRuleSet.ProvisionalProposed : InputEquivalence.GateRuleSet.Interim;
                            ReadBackReport report = ReadBackCompare.Run(Require(options, "run"), options.TryGetValue("rules", out string rules) ? rules : DefaultRules(), Require(options, "fixture-id"), options.TryGetValue("purpose", out string purpose) ? purpose : "calibration (not an equivalence fixture)", gateRuleSet, options.TryGetValue("out", out string output) ? output : null);
                            Console.Write(report.ToText());
                            return report.ExitCode;
                        }

                    default:
                        return ReadBackExit.Usage;
                }
            }
            catch (UsageException exception)
            {
                Console.WriteLine("usage: " + exception.Message);
                return ReadBackExit.Usage;
            }
        }

        // ---- readback-models: the disposable calibration inputs, written once with hashes --------------------------------

        private static int Models(string directory)
        {
            Directory.CreateDirectory(directory);
            List<string> lines = new List<string>();
            foreach (string name in CalibrationModels.Names)
            {
                string path = Path.Combine(directory, name + ".json");
                Core.Convert.ToFile(CalibrationModels.Create(name), path, Core.SAMFileType.Json);
                lines.Add(SnapshotJson.Sha256(path) + "  " + Path.GetFileName(path));
            }

            string path_Weather = Path.Combine(directory, "calib-weather.json");
            Core.Convert.ToFile(CalibrationModels.CreateWeather(), path_Weather, Core.SAMFileType.Json);
            lines.Add(SnapshotJson.Sha256(path_Weather) + "  " + Path.GetFileName(path_Weather));

            File.WriteAllLines(Path.Combine(directory, "SHA256SUMS.txt"), lines);
            foreach (string line in lines) Console.WriteLine(line);
            return 0;
        }

        // ---- readback-fixtures: the S08 PR-C equivalence fixtures, authored once; the committed files are the fixtures ---

        private static int Fixtures(string directory)
        {
            if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Console.WriteLine("refused: the fixture directory exists and is not empty: " + directory);
                return ReadBackExit.Usage;
            }

            Directory.CreateDirectory(directory);
            List<string> lines = new List<string>();
            foreach (string name in AcceptanceFixtures.Names)
            {
                string path = Path.Combine(directory, name + ".json");
                Core.Convert.ToFile(AcceptanceFixtures.Create(name), path, Core.SAMFileType.Json);
                lines.Add(SnapshotJson.Sha256(path) + "  " + Path.GetFileName(path));
            }

            File.WriteAllLines(Path.Combine(directory, "SHA256SUMS.txt"), lines);
            foreach (string line in lines) Console.WriteLine(line);
            return 0;
        }

        // ---- readback-export: freeze, hash, run the contract route, record what actually ran ----------------------------


        private static int Export(string path_Model, string directory, string path_Weather, bool sizing, bool simulate, string label)
        {
            // A fresh, disposable run directory is the only place RemoveExistingTBD may act: refuse to reuse one.
            if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Console.WriteLine("refused: the run directory exists and is not empty: " + directory);
                return ReadBackExit.Usage;
            }

            if (!File.Exists(path_Model))
            {
                Console.WriteLine("missing model " + path_Model);
                return ReadBackExit.Usage;
            }

            Directory.CreateDirectory(directory);
            ExportManifest manifest = new ExportManifest
            {
                Label = label,
                ProcessId = Process.GetCurrentProcess().Id,
                StartedUtc = DateTime.UtcNow.ToString("o", ci),
                Machine = Environment.MachineName,
                Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                SourceModelPath = Path.GetFullPath(path_Model),
                SourceModelSha256 = SnapshotJson.Sha256(path_Model)
            };

            TasIdentity(manifest);

            AnalyticalModel model = Core.Convert.ToSAM<AnalyticalModel>(path_Model)?.FirstOrDefault();
            if (model == null)
            {
                Console.WriteLine("cannot read model " + path_Model);
                return ReadBackExit.Usage;
            }

            // The effective weather is frozen beside the model; an 8784-hour EPW is refused before anything converts it,
            // so a leap year is never silently cut to 365 days (S07 section 7; owner leap-year policy).
            Weather.WeatherData weatherData = null;
            if (path_Weather != null)
            {
                if (!File.Exists(path_Weather))
                {
                    Console.WriteLine("missing weather " + path_Weather);
                    return ReadBackExit.Usage;
                }

                manifest.SourceWeatherPath = Path.GetFullPath(path_Weather);
                manifest.SourceWeatherSha256 = SnapshotJson.Sha256(path_Weather);
                if (path_Weather.EndsWith(".epw", StringComparison.OrdinalIgnoreCase))
                {
                    manifest.WeatherSourceKind = "epw";
                    manifest.SourceWeatherHours = ReadBackCompare.EpwHourCount(path_Weather);
                    manifest.Refusal = ReadBackCompare.EpwRefusal(manifest.SourceWeatherHours);
                    if (manifest.Refusal != null)
                    {
                        SnapshotJson.Write(manifest, Path.Combine(directory, ExportManifestFile));
                        Console.WriteLine(manifest.Refusal);
                        return manifest.SourceWeatherHours == 8784 ? ReadBackExit.BlockedOwner : ReadBackExit.BlockedTechnical;
                    }

                    weatherData = Weather.Convert.ToSAM(path_Weather);
                }
                else
                {
                    manifest.WeatherSourceKind = "sam-json";
                    weatherData = Core.Convert.ToSAM<Weather.WeatherData>(path_Weather)?.FirstOrDefault();
                }

                if (weatherData == null)
                {
                    Console.WriteLine("no weather in " + path_Weather);
                    return ReadBackExit.Usage;
                }
            }

            string path_Frozen = Path.Combine(directory, FrozenModelFile);
            Core.Convert.ToFile(model, path_Frozen, Core.SAMFileType.Json);
            File.SetAttributes(path_Frozen, FileAttributes.ReadOnly);
            manifest.FrozenModelSha256 = SnapshotJson.Sha256(path_Frozen);

            string path_FrozenWeather = null;
            if (weatherData != null)
            {
                path_FrozenWeather = Path.Combine(directory, FrozenWeatherFile);
                Core.Convert.ToFile(weatherData, path_FrozenWeather, Core.SAMFileType.Json);
                File.SetAttributes(path_FrozenWeather, FileAttributes.ReadOnly);
                manifest.FrozenWeatherSha256 = SnapshotJson.Sha256(path_FrozenWeather);
            }

            // The run converts a FRESH load of the frozen files, never the object it was serialised from.
            AnalyticalModel model_Run = Core.Convert.ToSAM<AnalyticalModel>(path_Frozen)?.FirstOrDefault();
            Weather.WeatherData weather_Run = path_FrozenWeather == null ? null : Core.Convert.ToSAM<Weather.WeatherData>(path_FrozenWeather)?.FirstOrDefault();

            WorkflowSettings settings = new WorkflowSettings
            {
                Path_TBD = Path.Combine(directory, TbdFile),
                T3DRoute = T3DRoute.Direct,
                UseWidths = false,
                Sizing = sizing,
                Simulate = simulate,
                SimulateFrom = 1,
                SimulateTo = 365,
                AddIZAMs = false,
                RemoveIZAMs = false,
                RemoveMechanicalVentilationGains = false,
                UpdateZones = true,
                UnmetHours = false,
                RemoveExistingTBD = true,
                UpdateWindowPositionType = false,
                WeatherData = weather_Run
            };

            foreach (PropertyInfo property in typeof(WorkflowSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                object value = property.GetValue(settings);
                manifest.Settings[property.Name] = value == null ? "(null)" : value is Weather.WeatherData w ? "WeatherData '" + w.Name + "' (frozen " + FrozenWeatherFile + ")" : value is System.Collections.ICollection c ? c.GetType().Name + "[" + c.Count + "]" : System.Convert.ToString(value, ci);
            }

            WorkflowCalculator calculator = new WorkflowCalculator(settings);
            calculator.Updating += (sender, e) => manifest.Steps.Add(e.Description);
            AnalyticalModel result = calculator.Calculate(model_Run);
            manifest.ResultReturned = result != null;
            manifest.Notes.AddRange(calculator.Notes);
            manifest.Timings.AddRange(calculator.Timings.Select(x => x.Key + " = " + x.Value.ToString("F1", ci) + " ms"));

            // The frozen inputs must be byte-identical after the run.
            manifest.FrozenModelSha256After = SnapshotJson.Sha256(path_Frozen);
            manifest.FrozenWeatherSha256After = path_FrozenWeather == null ? null : SnapshotJson.Sha256(path_FrozenWeather);

            foreach (string file in Directory.EnumerateFiles(directory).OrderBy(x => x, StringComparer.Ordinal))
            {
                manifest.Outputs[Path.GetFileName(file)] = SnapshotJson.Sha256(file);
            }

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies().Where(x => !x.IsDynamic && !string.IsNullOrEmpty(x.Location)).OrderBy(x => x.GetName().Name, StringComparer.Ordinal))
            {
                string name = assembly.GetName().Name;
                if (name.StartsWith("SAM", StringComparison.Ordinal) || name.StartsWith("Interop", StringComparison.Ordinal) || name.StartsWith("direct-t3d", StringComparison.Ordinal))
                {
                    manifest.Assemblies[name] = SnapshotJson.Sha256(assembly.Location) + " " + assembly.Location;
                }
            }

            manifest.EndedUtc = DateTime.UtcNow.ToString("o", ci);
            SnapshotJson.Write(manifest, Path.Combine(directory, ExportManifestFile));

            Console.WriteLine("export: result={0} steps={1} notes={2} frozen-unchanged={3}", manifest.ResultReturned, manifest.Steps.Count, manifest.Notes.Count, manifest.FrozenModelSha256 == manifest.FrozenModelSha256After && manifest.FrozenWeatherSha256 == manifest.FrozenWeatherSha256After);
            if (manifest.FrozenModelSha256 != manifest.FrozenModelSha256After || manifest.FrozenWeatherSha256 != manifest.FrozenWeatherSha256After)
            {
                Console.WriteLine("infrastructure: a frozen input changed during the run");
                return ReadBackExit.Infrastructure;
            }

            if (!manifest.ResultReturned || !File.Exists(settings.Path_TBD))
            {
                return ReadBackExit.Infrastructure;
            }

            return 0;
        }

        private static void TasIdentity(ExportManifest manifest)
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(@"TBD.Document\CLSID"))
                {
                    string clsid = key?.GetValue(null) as string;
                    using (Microsoft.Win32.RegistryKey server = clsid == null ? null : Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(@"CLSID\" + clsid + @"\LocalServer32"))
                    {
                        string path = (server?.GetValue(null) as string)?.Trim('"');
                        manifest.TasExecutable = path;
                        if (path != null && File.Exists(path))
                        {
                            FileVersionInfo version = FileVersionInfo.GetVersionInfo(path);
                            manifest.TasVersion = version.FileVersion + " (" + File.GetLastWriteTimeUtc(path).ToString("yyyy-MM-dd", ci) + ")";
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                manifest.TasVersion = "unreadable: " + exception.GetType().Name;
            }
        }

        // ---- readback-read: the independent reopen, in a fresh process ---------------------------------------------------

        private static int Read(string path_TBD, string path_T3D, string path_Out)
        {
            NativeSnapshot snapshot = TbdReadBack.Read(path_TBD, path_T3D);
            SnapshotJson.Write(snapshot, path_Out);
            Console.WriteLine("read: pid={0} zones={1} elements={2} errors={3} t3d={4}", snapshot.Reader.ProcessId, snapshot.Zones.Count, snapshot.BuildingElements.Count, snapshot.Errors.Count, snapshot.T3D?.Kind);
            foreach (string error in snapshot.Errors) Console.WriteLine("  error: " + error);
            return snapshot.Errors.Count == 0 ? 0 : ReadBackExit.Infrastructure;
        }

        // ---- readback-calibrate: measure the conventions on the synthetic runs, write findings and a candidate rule table ---

        private static int Calibrate(string runs, string path_AlignTsd, string directory)
        {
            Directory.CreateDirectory(directory);
            List<CalibrationRun> list = new List<CalibrationRun>();
            foreach (string item in runs.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                string[] parts = item.Split(new[] { '=' }, 2);
                list.Add(Calibration.Load(parts[0], parts[1]));
            }

            TsdSnapshot align = path_AlignTsd == null ? null : SnapshotJson.Read<TsdSnapshot>(path_AlignTsd);
            List<CalibrationFinding> findings = Calibration.Analyse(list, align);
            SnapshotJson.Write(findings, Path.Combine(directory, "calibration-findings.json"));
            SnapshotJson.Write(Calibration.Candidate(findings, string.Empty), Path.Combine(directory, "calibration-rules.candidate.json"));
            foreach (CalibrationFinding finding in findings)
            {
                Console.WriteLine("{0,-28} {1,-10} {2}", finding.Id, finding.Consistent ? "verified" : "UNRESOLVED", finding.Answer ?? finding.Note);
            }

            return 0;
        }

        // ---- readback-negative: one deliberate change on a DISPOSABLE copy, originals hash-checked before and after ---------

        private static int Negative(string runDirectory, string mutation, string directory)
        {
            if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Console.WriteLine("refused: the negative-test directory exists and is not empty: " + directory);
                return ReadBackExit.Usage;
            }

            Directory.CreateDirectory(directory);
            string[] files = { FrozenModelFile, FrozenWeatherFile, ExportManifestFile, TbdFile, T3dFile };
            Dictionary<string, string> before = files.Where(x => File.Exists(Path.Combine(runDirectory, x))).ToDictionary(x => x, x => SnapshotJson.Sha256(Path.Combine(runDirectory, x)));
            foreach (string file in before.Keys)
            {
                File.Copy(Path.Combine(runDirectory, file), Path.Combine(directory, file));
                File.SetAttributes(Path.Combine(directory, file), FileAttributes.Normal);
            }

            string applied = TbdMutation.Apply(Path.Combine(directory, TbdFile), mutation);
            Dictionary<string, string> after = files.Where(x => File.Exists(Path.Combine(runDirectory, x))).ToDictionary(x => x, x => SnapshotJson.Sha256(Path.Combine(runDirectory, x)));
            bool originalsUnchanged = before.Count == after.Count && before.All(x => after[x.Key] == x.Value);

            ExportManifest manifest = SnapshotJson.Read<ExportManifest>(Path.Combine(directory, ExportManifestFile));
            manifest.OriginalOutputs = new Dictionary<string, string>(manifest.Outputs);
            manifest.Outputs[TbdFile] = SnapshotJson.Sha256(Path.Combine(directory, TbdFile));
            manifest.Mutation = mutation + ": " + applied + "; originals unchanged " + originalsUnchanged;
            File.SetAttributes(Path.Combine(directory, FrozenModelFile), FileAttributes.ReadOnly);
            if (File.Exists(Path.Combine(directory, FrozenWeatherFile))) File.SetAttributes(Path.Combine(directory, FrozenWeatherFile), FileAttributes.ReadOnly);
            SnapshotJson.Write(manifest, Path.Combine(directory, ExportManifestFile));

            Console.WriteLine("negative {0}: {1}; originals unchanged: {2}", mutation, applied, originalsUnchanged);
            return originalsUnchanged && applied != null && !applied.StartsWith("not applied", StringComparison.Ordinal) ? 0 : ReadBackExit.Infrastructure;
        }

        // The committed rule table copied beside the executable (see the csproj); a run records its path and sha256.
        private static string DefaultRules()
        {
            return Path.Combine(AppContext.BaseDirectory, "calibration-rules.json");
        }

        // ---- options -----------------------------------------------------------------------------------------------------

        private sealed class UsageException : Exception
        {
            public UsageException(string message) : base(message) { }
        }

        private static Dictionary<string, string> Options(string[] args)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 1; i < args.Length; i++)
            {
                if (!args[i].StartsWith("--", StringComparison.Ordinal))
                {
                    throw new UsageException("unexpected argument " + args[i]);
                }

                string key = args[i].Substring(2);
                string value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[++i] : "true";
                result[key] = value;
            }

            return result;
        }

        private static string Require(Dictionary<string, string> options, string key)
        {
            if (!options.TryGetValue(key, out string value) || string.IsNullOrWhiteSpace(value))
            {
                throw new UsageException("--" + key + " is required");
            }

            return value;
        }
    }
}
