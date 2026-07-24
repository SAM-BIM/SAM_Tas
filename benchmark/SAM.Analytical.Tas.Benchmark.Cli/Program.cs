// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using SAM.Analytical.Benchmark;

namespace SAM.Analytical.Tas.Benchmark
{
    /// <summary>
    /// <c>benchmark-tas</c>: the B2 TAS benchmark producer. Loads a SAM AnalyticalModel, runs the
    /// native TAS route (shared gbXML → T3D → TBD → simulate/size via SAM_Tas's own
    /// <see cref="WorkflowCalculator"/>), reads the engine-neutral SAM results, and emits a B1a
    /// schema-v1 benchmark document (<c>route = Native-TAS</c>) to the SAME canonical units and shape
    /// as the OpenStudio producer (B1b), so the two are directly comparable. The two provenance
    /// hashes are computed with the B1a helpers exactly as the schema requires: <c>sourceFileHash</c>
    /// over the raw model bytes and <c>canonicalModelHash</c> over the neutral SAM model BEFORE any
    /// TAS translation.
    /// <para>
    /// Argument parsing, invariant-culture, exception mapping and exit codes are delegated to the
    /// shared benchmark CLI host (<see cref="BenchmarkCliHost"/>): <c>0</c> success, <c>2</c> usage,
    /// <c>3</c> input/IO/serialization, <c>4</c> validation, <c>5</c> producer failure.
    /// </para>
    /// <para>
    /// Runs only on a licensed EDSL Tas laptop (the TAS COM servers must be registered). The
    /// simulation is a blocking in-process COM call, so the hard timeout abandons the worker thread
    /// on process exit rather than gracefully cancelling it (see <see cref="RunWithTimeout"/>).
    /// </para>
    /// </summary>
    public static class Program
    {
        private static readonly Regex CommitPattern = new Regex("^[0-9a-f]{7,64}$", RegexOptions.CultureInvariant);

        private static readonly string[] RequiredOptions = { "model", "weather", "tbd", "out" };

        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromHours(1);

        [STAThread]
        public static int Main(string[] args)
        {
            return Run(args, Console.Out, Console.Error);
        }

        /// <summary>
        /// Testable entry point: the shared host drives parsing, culture, help and exception mapping;
        /// <see cref="Execute"/> carries the producer logic. Writers are injectable so CLI tests can
        /// capture the output without touching the process <see cref="Console"/>.
        /// </summary>
        internal static int Run(string[] args, TextWriter standardOutput, TextWriter standardError)
        {
            return BenchmarkCliHost.Run(
                args,
                Usage,
                RequiredOptions,
                (arguments, _) => Execute(arguments, standardOutput),
                standardOutput,
                standardError);
        }

        private static int Execute(BenchmarkArguments arguments, TextWriter standardOutput)
        {
            // Path validation throws mapped exceptions (input/IO): a missing input is FileNotFound,
            // a bad output directory is DirectoryNotFound. The TBD is written by the run, so only its
            // directory needs to exist — validate it like an output path.
            string modelPath = BenchmarkCliPaths.ValidateInputFile(arguments.RequireOption("model"));
            string weatherPath = BenchmarkCliPaths.ValidateInputFile(arguments.RequireOption("weather"));
            string tbdPath = BenchmarkCliPaths.ValidateOutputFile(arguments.RequireOption("tbd"));
            string outputPath = BenchmarkCliPaths.ValidateOutputFile(arguments.RequireOption("out"));
            TimeSpan timeout = arguments.GetTimeout() ?? DefaultTimeout;

            // Provenance hashes (B1a helpers, exactly per SCHEMA.md "Canonical model hashing").
            string sourceFileHash = BenchmarkHash.ComputeSha256(File.ReadAllBytes(modelPath));

            AnalyticalModel model = Producer.LoadModel(modelPath);

            // The canonical hash covers the neutral loaded model BEFORE any engine-specific mutation.
            string neutralJson = model.ToJsonObject().ToJsonString();
            string canonicalModelHash = BenchmarkCanonicalJson.ComputeSha256(neutralJson);

            TasBenchmarkContext context = new TasBenchmarkContext
            {
                SourceModelName = string.IsNullOrWhiteSpace(model.Name) ? "model" : model.Name,
                SourceModelGuid = model.Guid.ToString("N"),
                SourceFileHash = sourceFileHash,
                CanonicalModelHash = canonicalModelHash,
                CanonicalizationVersion = BenchmarkCanonicalization.CurrentVersion,
                SamCommit = ResolveCommit(arguments.GetOption("sam-commit"), "SAM_COMMIT", typeof(AnalyticalModel), allowLocalGit: false),
                RunnerCommit = ResolveCommit(arguments.GetOption("runner-commit"), "RUNNER_COMMIT", typeof(Program), allowLocalGit: true),
                EngineName = "Tas",
                EngineVersion = Query.TasVersion(arguments.GetOption("engine-version")),
                SdkVersion = null,
                WeatherIdentity = Path.GetFileNameWithoutExtension(weatherPath),
                WeatherHash = BenchmarkHash.ComputeSha256(File.ReadAllBytes(weatherPath)),
                // Sizing (design loads) relies on the design days embedded in the source model.
                DesignDaySource = DesignDaySource.EmbeddedModel,
                RunTimestampUtc = DateTimeOffset.UtcNow,
            };

            if (string.IsNullOrEmpty(context.EngineVersion))
            {
                context.EngineVersion = null;
                context.Notes.Add("TAS engine version was unavailable (EDSL exposes no version in code); pass --engine-version to record it.");
            }

            // The native TAS route consumes a shared gbXML. Reuse the caller's --gbxml when present;
            // otherwise export it from the SAM model (the same gbXML both engines can consume).
            string gbxmlPath = EnsureGbXml(arguments.GetOption("gbxml"), tbdPath, model, context);

            WorkflowSettings settings = new WorkflowSettings
            {
                Path_TBD = tbdPath,
                Path_gbXML = gbxmlPath,
                WeatherData = LoadWeather(weatherPath, context),
                Simulate = true,
                Sizing = true,
                AddIZAMs = true,
                UnmetHours = true,
                UpdateZones = true,
                SimulateFrom = 1,
                SimulateTo = 365,
            };

            Stopwatch stopwatch = Stopwatch.StartNew();
            AnalyticalModel calculated = RunWithTimeout(() => new WorkflowCalculator(settings).Calculate(model), timeout, out bool timedOut, out Exception runError);
            stopwatch.Stop();

            AnalyticalModel resultModel = calculated ?? model;
            bool success = !timedOut && runError == null && calculated != null;
            context.State = success ? RunState.Success : RunState.Failure;
            context.DurationSeconds = stopwatch.Elapsed.TotalSeconds;

            if (success)
            {
                string tsdPath = Path.Combine(Path.GetDirectoryName(tbdPath) ?? string.Empty, Path.GetFileNameWithoutExtension(tbdPath) + ".tsd");

                // The workflow attaches per-space results but NOT the model-level annual energy —
                // read that explicitly (Wh/W; ToBenchmark converts to kWh/kW).
                context.ModelResult = SAM.Analytical.Tas.Convert.ToSAM_AnalyticalModelSimulationResult(tsdPath, resultModel);
                List<SpaceSimulationResult> spaceResults = resultModel.GetResults<SpaceSimulationResult>();
                context.SpaceResults = spaceResults;
                // Design loads (from sizing) live on the same per-space results.
                context.SpaceDesignLoadResults = spaceResults;
            }
            else
            {
                if (timedOut)
                {
                    context.Notes.Add("The TAS run exceeded the " + timeout.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + "s timeout; measurements are unavailable.");
                }
                else
                {
                    context.Notes.Add("The TAS run did not complete successfully; measurements are unavailable.");
                    if (runError != null)
                    {
                        context.Warnings.Add(runError.Message);
                    }
                }
            }

            BenchmarkDocument document = resultModel.ToBenchmark(context);
            return Producer.Emit(document, outputPath, success, standardOutput);
        }

        /// <summary>
        /// Resolves the gbXML the TAS route imports. Uses the caller's <c>--gbxml</c> when the file
        /// exists; otherwise exports one from the SAM model (via <c>SAM.Analytical.gbXML</c>) to the
        /// given path — defaulting to the TBD path with a <c>.xml</c> extension when none was given.
        /// </summary>
        private static string EnsureGbXml(string gbxmlOption, string tbdPath, AnalyticalModel model, TasBenchmarkContext context)
        {
            string gbxmlPath = string.IsNullOrWhiteSpace(gbxmlOption)
                ? Path.ChangeExtension(tbdPath, ".xml")
                : Path.GetFullPath(gbxmlOption);

            if (File.Exists(gbxmlPath))
            {
                return gbxmlPath;
            }

            gbXMLSerializer.gbXML gbxml = SAM.Analytical.gbXML.Convert.TogbXML(model);
            if (gbxml == null || !SAM.Core.gbXML.Create.gbXML(gbxml, gbxmlPath))
            {
                throw new InvalidOperationException("Failed to export a shared gbXML from the model to: " + gbxmlPath);
            }

            context.Notes.Add("Shared gbXML was exported from the source model (no --gbxml supplied).");
            return gbxmlPath;
        }

        /// <summary>Reads an EPW weather file into a SAM <see cref="Weather.WeatherData"/>; records a note and returns null on failure (the run then produces a failure document).</summary>
        private static Weather.WeatherData LoadWeather(string weatherPath, TasBenchmarkContext context)
        {
            try
            {
                Weather.WeatherData weatherData = Weather.Convert.ToSAM(weatherPath);
                if (weatherData == null)
                {
                    context.Notes.Add("The weather file did not read into SAM weather data: " + Path.GetFileName(weatherPath));
                }

                return weatherData;
            }
            catch (Exception exception)
            {
                context.Notes.Add("Reading the weather file failed: " + exception.Message);
                return null;
            }
        }

        /// <summary>
        /// Runs <paramref name="work"/> on a dedicated STA thread (TAS COM requires STA) and waits up
        /// to <paramref name="timeout"/>. On timeout the worker is a background thread abandoned when
        /// the process exits — a hard timeout, not a graceful cancel (the blocking in-process COM
        /// call cannot be interrupted). Exceptions are captured and returned via
        /// <paramref name="error"/>.
        /// </summary>
        private static AnalyticalModel RunWithTimeout(Func<AnalyticalModel> work, TimeSpan timeout, out bool timedOut, out Exception error)
        {
            AnalyticalModel result = null;
            Exception captured = null;

            Thread thread = new Thread(() =>
            {
                try
                {
                    result = work();
                }
                catch (Exception exception)
                {
                    captured = exception;
                }
            })
            {
                IsBackground = true,
                Name = "benchmark-tas-worker",
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            bool completed = thread.Join(timeout);
            timedOut = !completed;
            error = completed ? captured : null;
            return completed ? result : null;
        }

        /// <summary>
        /// Resolves a 7-64 lowercase-hex commit: explicit argument, then environment variable, then
        /// the assembly's InformationalVersion SHA (the <c>+&lt;sha&gt;</c> suffix CI stamps), then —
        /// only for the runner — <c>git rev-parse HEAD</c> from the executable's directory. Returns
        /// null when none resolve; validation then rejects the document with a precise message.
        /// </summary>
        private static string ResolveCommit(string explicitValue, string environmentVariable, Type assemblyType, bool allowLocalGit)
        {
            string candidate = Normalize(explicitValue);
            if (candidate != null)
            {
                return candidate;
            }

            candidate = Normalize(Environment.GetEnvironmentVariable(environmentVariable));
            if (candidate != null)
            {
                return candidate;
            }

            candidate = Normalize(InformationalVersionSha(assemblyType));
            if (candidate != null)
            {
                return candidate;
            }

            if (allowLocalGit)
            {
                candidate = Normalize(GitHead(AppContext.BaseDirectory));
                if (candidate != null)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            value = value.Trim().ToLowerInvariant();
            return CommitPattern.IsMatch(value) ? value : null;
        }

        private static string InformationalVersionSha(Type assemblyType)
        {
            try
            {
                object[] attributes = assemblyType.Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false);
                if (attributes.Length == 0)
                {
                    return null;
                }

                string informationalVersion = ((System.Reflection.AssemblyInformationalVersionAttribute)attributes[0]).InformationalVersion;
                int plus = informationalVersion?.IndexOf('+') ?? -1;
                return plus >= 0 ? informationalVersion.Substring(plus + 1) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string GitHead(string directory)
        {
            try
            {
                ProcessStartInfo processStartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse HEAD",
                    WorkingDirectory = directory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                using (Process process = Process.Start(processStartInfo))
                {
                    if (process == null)
                    {
                        return null;
                    }

                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(5000);
                    return process.ExitCode == 0 ? output.Trim() : null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private const string Usage =
            "Usage: benchmark-tas --model <model.json> --weather <weather.epw> --tbd <run.tbd> --out <benchmark-TAS.json>\n" +
            "                     [--gbxml <shared.xml>] [--engine-version <version>] [--timeout-seconds <n>]\n" +
            "                     [--sam-commit <sha>] [--runner-commit <sha>]\n" +
            "\n" +
            "Runs the native TAS route (EDSL Tas laptop only). --gbxml is reused when it exists, else exported from the model.\n" +
            "Exit codes: 0 success, 2 usage error, 3 input/IO/serialization error, 4 validation failure, 5 producer failure.";
    }
}
