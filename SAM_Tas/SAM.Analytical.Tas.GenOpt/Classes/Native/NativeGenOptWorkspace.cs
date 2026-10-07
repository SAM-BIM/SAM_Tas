// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SAM.Analytical.Tas.GenOpt
{
    /// <summary>
    /// The isolated folders of one native optimisation run. A unique run folder holds:
    /// <list type="bullet">
    /// <item><c>project</c>: the project snapshot that TasGenExecute receives as <c>args[0]</c>. It is created once, at
    /// the start of the run, and the native code never writes to it again.</item>
    /// <item><c>evaluations</c>: one fresh working folder per evaluation attempt, created by
    /// <see cref="TasGenExecuteObjectiveEvaluator"/>.</item>
    /// </list>
    /// <para>
    /// The snapshot is an allow-list, not a copy of the source folder. It holds <c>Script.txt</c>, written from the
    /// document exactly as the Java route writes it, plus the source folder's top-level Tas files of the types
    /// TasGenExecute itself discovers. Those types are TasGenComm's <c>TasFiles.TasExtension</c>: T3D, TBD, TPD, TSD
    /// and TWD. Runtime artifacts of earlier runs, such as Variables.txt, Output.txt, Error.txt, GenOpt files and
    /// <c>tmp-genopt-run-*</c> folders, are never carried over.
    /// </para>
    /// <para>
    /// Runs never share folders, so separate runs can coexist (Gate T T7). No registry value is touched.
    /// </para>
    /// </summary>
    public sealed class NativeGenOptWorkspace
    {
        public const string ScriptFileName = "Script.txt";
        public const string ProjectFolderName = "project";
        public const string EvaluationsFolderName = "evaluations";

        /// <summary>The Tas file types TasGenExecute discovers in its workspace (TasGenComm TasFiles.TasExtension).</summary>
        public static readonly ReadOnlyCollection<string> TasFileExtensions = Array.AsReadOnly(new[] { ".t3d", ".tbd", ".tpd", ".tsd", ".twd" });

        private NativeGenOptWorkspace(string runDirectory, IList<string> projectFiles)
        {
            RunDirectory = runDirectory;
            ProjectDirectory = Path.Combine(runDirectory, ProjectFolderName);
            EvaluationsDirectory = Path.Combine(runDirectory, EvaluationsFolderName);
            ProjectFiles = new ReadOnlyCollection<string>(projectFiles);
        }

        public string RunDirectory { get; }

        /// <summary>The snapshot passed to TasGenExecute as its only argument.</summary>
        public string ProjectDirectory { get; }

        /// <summary>Parent of the per-evaluation working folders.</summary>
        public string EvaluationsDirectory { get; }

        /// <summary>File names in the snapshot (Script.txt first).</summary>
        public IReadOnlyList<string> ProjectFiles { get; }

        /// <summary>
        /// Creates a new run folder under <paramref name="runsDirectory"/> and fills the project snapshot.
        /// </summary>
        /// <param name="sourceDirectory">The optimisation workspace holding the Tas files (GenOptDocument.Directory).</param>
        /// <param name="scriptText">The TasGenExecute C# script.</param>
        /// <param name="runsDirectory">Parent of the run folders. Keep it short: Tas COM refuses very long paths.</param>
        public static NativeGenOptWorkspace Create(string sourceDirectory, string scriptText, string runsDirectory)
        {
            if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
            {
                throw new DirectoryNotFoundException("The optimisation workspace does not exist: '" + sourceDirectory + "'.");
            }

            if (string.IsNullOrWhiteSpace(scriptText))
            {
                throw new GenOptCompatibilityException("The TasGenExecute script is empty.");
            }

            if (string.IsNullOrWhiteSpace(runsDirectory))
            {
                throw new ArgumentNullException(nameof(runsDirectory));
            }

            string runDirectory = Path.Combine(
                Path.GetFullPath(runsDirectory),
                DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            if (Directory.Exists(runDirectory))
            {
                throw new IOException("The run folder already exists: '" + runDirectory + "'.");
            }

            List<string> projectFiles = new List<string> { ScriptFileName };
            NativeGenOptWorkspace result = new NativeGenOptWorkspace(runDirectory, projectFiles);
            Directory.CreateDirectory(result.ProjectDirectory);
            Directory.CreateDirectory(result.EvaluationsDirectory);

            new ScriptFile(scriptText).Save(Path.Combine(result.ProjectDirectory, ScriptFileName));

            foreach (string path in Directory.GetFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                if (!IsTasFile(path))
                {
                    continue;
                }

                string name = Path.GetFileName(path);
                System.IO.File.Copy(path, Path.Combine(result.ProjectDirectory, name), false);
                projectFiles.Add(name);
            }

            return result;
        }

        /// <summary>True for the Tas file types TasGenExecute discovers (case-insensitive extension match).</summary>
        public static bool IsTasFile(string path)
        {
            string extension = Path.GetExtension(path);
            return !string.IsNullOrEmpty(extension) && TasFileExtensions.Contains(extension.ToLowerInvariant());
        }
    }
}
