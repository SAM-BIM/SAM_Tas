// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Math;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace SAM.Analytical.Tas.GenOpt
{
    /// <summary>
    /// Evaluates one optimisation point by running Tas's own <c>TasGenExecute.exe</c> directly, following the Gate T
    /// protocol (TASGENEXECUTE_PROTOCOL.md §2.3). Java, GenOpt, cmd.exe and the Tas Manager registry are not involved.
    /// <list type="bullet">
    /// <item><c>args[0]</c> is the run's project snapshot (<see cref="NativeGenOptWorkspace.ProjectDirectory"/>).</item>
    /// <item>The process working folder is a fresh evaluation folder, <c>evaluations\NNNN</c>, or
    /// <c>evaluations\NNNN-retry</c> for the kernel's single retry. It receives <c>Variables.txt</c>; <c>Output.txt</c>
    /// and <c>Error.txt</c> are read from it.</item>
    /// <item>The evaluation fails (<see cref="ObjectiveEvaluation.Failure"/>) when the process exits non-zero,
    /// <c>Error.txt</c> has content, or an objective is missing or not parseable. The SAM.Math kernel owns the
    /// retry-once-then-stop rule; this class never retries.</item>
    /// <item>Cancellation is cooperative and happens between evaluations: a running TasGenExecute is never killed. The
    /// kernel checks the token before starting the next simulation.</item>
    /// </list>
    /// Evaluations run one at a time (Phase 1).
    /// </summary>
    public sealed class TasGenExecuteObjectiveEvaluator : IObjectiveEvaluator
    {
        public const string VariablesFileName = "Variables.txt";
        public const string OutputFileName = "Output.txt";
        public const string ErrorFileName = "Error.txt";

        private static readonly Regex NumberPattern = new Regex(@"^[+-]?(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?$", RegexOptions.CultureInvariant);

        private readonly string executablePath;
        private readonly string projectDirectory;
        private readonly string evaluationsDirectory;
        private readonly ReadOnlyCollection<NumberParameter> parameters;
        private readonly ReadOnlyCollection<Objective> objectives;

        /// <param name="executablePath">TasGenExecute.exe (or a protocol-compatible stand-in).</param>
        /// <param name="projectDirectory">The project snapshot, passed as the only argument.</param>
        /// <param name="evaluationsDirectory">Parent of the per-evaluation working folders.</param>
        /// <param name="parameters">Parameters in kernel order; their Min/Max/Step text goes into Variables.txt.</param>
        /// <param name="objectives">Objectives in kernel output order; the first is minimised.</param>
        public TasGenExecuteObjectiveEvaluator(string executablePath, string projectDirectory, string evaluationsDirectory, IEnumerable<NumberParameter> parameters, IEnumerable<Objective> objectives)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                throw new ArgumentNullException(nameof(executablePath));
            }

            if (string.IsNullOrWhiteSpace(projectDirectory))
            {
                throw new ArgumentNullException(nameof(projectDirectory));
            }

            if (string.IsNullOrWhiteSpace(evaluationsDirectory))
            {
                throw new ArgumentNullException(nameof(evaluationsDirectory));
            }

            this.executablePath = executablePath;
            this.projectDirectory = projectDirectory;
            this.evaluationsDirectory = evaluationsDirectory;
            this.parameters = new ReadOnlyCollection<NumberParameter>((parameters ?? throw new ArgumentNullException(nameof(parameters))).ToList());
            this.objectives = new ReadOnlyCollection<Objective>((objectives ?? throw new ArgumentNullException(nameof(objectives))).ToList());
            if (this.parameters.Count == 0 || this.objectives.Count == 0)
            {
                throw new ArgumentException("At least one parameter and one objective are required.");
            }
        }

        /// <summary>
        /// Runs one evaluation to completion. The cancellation token is deliberately not observed here: an evaluation
        /// that has started always finishes normally, and the kernel stops before the next one.
        /// </summary>
        public ObjectiveEvaluation Evaluate(ObjectiveEvaluationRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return Evaluate(request.Simulation, request.Attempt, request.Coordinates);
        }

        /// <summary>
        /// Runs one evaluation outside an optimisation run, for example "Test one simulation": the same protocol as the
        /// kernel's evaluations (folder <c>NNNN</c> for <paramref name="simulation"/>, Variables.txt, classification).
        /// </summary>
        public ObjectiveEvaluation Evaluate(int simulation, IReadOnlyList<double> coordinates)
        {
            if (coordinates == null)
            {
                throw new ArgumentNullException(nameof(coordinates));
            }

            return Evaluate(simulation, 1, coordinates);
        }

        private ObjectiveEvaluation Evaluate(int simulation, int attempt, IReadOnlyList<double> coordinates)
        {
            if (coordinates.Count != parameters.Count)
            {
                return ObjectiveEvaluation.Failure("Expected " + parameters.Count.ToString(CultureInfo.InvariantCulture) + " coordinates, got " + coordinates.Count.ToString(CultureInfo.InvariantCulture) + ".");
            }

            string evaluationDirectory = EvaluationDirectory(evaluationsDirectory, simulation, attempt);
            if (Directory.Exists(evaluationDirectory))
            {
                return ObjectiveEvaluation.Failure("The evaluation folder already exists: '" + evaluationDirectory + "'.");
            }

            Directory.CreateDirectory(evaluationDirectory);
            System.IO.File.WriteAllText(Path.Combine(evaluationDirectory, VariablesFileName), VariablesText(parameters, coordinates));

            int exitCode;
            using (Process process = Process.Start(StartInfo(executablePath, projectDirectory, evaluationDirectory)))
            {
                process.WaitForExit();
                exitCode = process.ExitCode;
            }

            return Classify(evaluationDirectory, exitCode, objectives);
        }

        /// <summary>
        /// The working folder of one attempt: <c>NNNN</c> for the first attempt and <c>NNNN-retry</c> for the kernel's
        /// single retry (same simulation number, fresh folder).
        /// </summary>
        public static string EvaluationDirectory(string evaluationsDirectory, int simulation, int attempt)
        {
            string name = simulation.ToString("D4", CultureInfo.InvariantCulture);
            if (attempt > 1)
            {
                name += attempt == 2 ? "-retry" : "-retry" + (attempt - 1).ToString(CultureInfo.InvariantCulture);
            }

            return Path.Combine(evaluationsDirectory, name);
        }

        /// <summary>
        /// Direct invocation: the executable itself (no cmd.exe, no shell), the quoted project snapshot as the only
        /// argument, and the evaluation folder as the working directory.
        /// </summary>
        public static ProcessStartInfo StartInfo(string executablePath, string projectDirectory, string evaluationDirectory)
        {
            return new ProcessStartInfo(executablePath)
            {
                Arguments = "\"" + projectDirectory.TrimEnd('\\', '/') + "\"",
                WorkingDirectory = evaluationDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
        }

        /// <summary>
        /// Variables.txt in the form TasGenExecute reads (the form GenOpt's template produced for it):
        /// <c>Name,&lt;value&gt;,Min,Max,Step,System.Double</c>, one line per parameter, joined with '\n'. The value is
        /// the kernel coordinate as invariant round-trip text. Min, Max and Step are the GenOpt-format writer's text for the
        /// parameter's own values.
        /// </summary>
        public static string VariablesText(IReadOnlyList<NumberParameter> parameters, IReadOnlyList<double> coordinates)
        {
            List<string> lines = new List<string>();
            for (int i = 0; i < parameters.Count; i++)
            {
                NumberParameter parameter = parameters[i];
                lines.Add(string.Join(",", new[]
                {
                    parameter.Name,
                    coordinates[i].ToString("R", CultureInfo.InvariantCulture),
                    GenOptNumber.WriterText(parameter.Min),
                    GenOptNumber.WriterText(parameter.Max),
                    GenOptNumber.WriterText(parameter.Step),
                    typeof(double).FullName,
                }));
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Classifies a finished evaluation. It fails on a non-zero exit code, on Error.txt with any non-whitespace
        /// content, or on an objective that is missing or not parseable. Otherwise it succeeds, with one output per
        /// objective in order.
        /// </summary>
        public static ObjectiveEvaluation Classify(string evaluationDirectory, int exitCode, IReadOnlyList<Objective> objectives)
        {
            if (exitCode != 0)
            {
                return ObjectiveEvaluation.Failure("TasGenExecute exited with code " + exitCode.ToString(CultureInfo.InvariantCulture) + " (0x" + exitCode.ToString("X8", CultureInfo.InvariantCulture) + ").");
            }

            string errorPath = Path.Combine(evaluationDirectory, ErrorFileName);
            if (System.IO.File.Exists(errorPath))
            {
                string error = System.IO.File.ReadAllText(errorPath);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    return ObjectiveEvaluation.Failure("TasGenExecute reported an error in Error.txt: " + Shorten(error));
                }
            }

            string outputPath = Path.Combine(evaluationDirectory, OutputFileName);
            if (!System.IO.File.Exists(outputPath))
            {
                return ObjectiveEvaluation.Failure("TasGenExecute wrote no Output.txt.");
            }

            string[] lines = System.IO.File.ReadAllLines(outputPath);
            double[] values = new double[objectives.Count];
            for (int i = 0; i < objectives.Count; i++)
            {
                string message;
                if (!TryReadObjective(lines, objectives[i].Delimiter, out values[i], out message))
                {
                    return ObjectiveEvaluation.Failure("Objective '" + objectives[i].Name + "': " + message);
                }
            }

            return ObjectiveEvaluation.Success(values);
        }

        /// <summary>
        /// Reads one objective from Output.txt (TASGENEXECUTE_PROTOCOL.md §2.3). Take the last line containing the
        /// delimiter, then the text after the delimiter's last occurrence in that line. The file is appended to, so
        /// the last line is the newest.
        /// </summary>
        public static bool TryReadObjective(IEnumerable<string> lines, string delimiter, out double value, out string message)
        {
            value = double.NaN;
            string line = lines?.LastOrDefault(x => x != null && x.IndexOf(delimiter, StringComparison.Ordinal) >= 0);
            if (line == null)
            {
                message = "no '" + delimiter + "' line in Output.txt.";
                return false;
            }

            string text = line.Substring(line.LastIndexOf(delimiter, StringComparison.Ordinal) + delimiter.Length).Trim();
            return TryParseValue(text, out value, out message);
        }

        /// <summary>
        /// The numeric contract: invariant culture with a '.' decimal point and an optional exponent, or NaN/Infinity
        /// as .NET writes them. Comma-decimal text is refused. TasGenExecute writes with the current culture, and
        /// GenOpt would silently cut "7392,29" at the comma.
        /// </summary>
        public static bool TryParseValue(string text, out double value, out string message)
        {
            value = double.NaN;
            if (string.IsNullOrEmpty(text))
            {
                message = "the value is empty.";
                return false;
            }

            if (text.IndexOf(',') >= 0)
            {
                message = "'" + text + "' uses a comma decimal separator; TasGenExecute must run with a '.' decimal culture.";
                return false;
            }

            if (text == "NaN" || text == "Infinity" || text == "-Infinity")
            {
                value = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
                message = null;
                return true;
            }

            if (!NumberPattern.IsMatch(text) || !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                value = double.NaN;
                message = "'" + text + "' is not a number.";
                return false;
            }

            message = null;
            return true;
        }

        private static string Shorten(string text)
        {
            string trimmed = text.Trim().Replace("\r", string.Empty).Replace('\n', ' ');
            return trimmed.Length <= 300 ? trimmed : trimmed.Substring(0, 300) + "…";
        }
    }
}
