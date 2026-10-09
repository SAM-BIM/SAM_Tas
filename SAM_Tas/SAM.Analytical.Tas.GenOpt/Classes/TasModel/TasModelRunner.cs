// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Optimisation;
using SAM.Math;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace SAM.Analytical.Tas.GenOpt
{
    /// <summary>
    /// Runs a "tas-model" definition: the model is changed and read by a generated TasGenExecute script, driven by the
    /// SAM.Math kernel through the same evaluator and workspace as the "tas-script" route (Gate T protocol).
    /// <para>The constructor does everything that needs no process, so a definition that cannot run fails before any
    /// folder is created: it checks the definition against <see cref="Query.TasModelCapabilities"/> and the model's
    /// catalogue (names not in the model are OPT609), resolves every glazing choice to its options, maps the method
    /// onto the kernel (<see cref="Convert.ToSAM_TasModelKernel"/>) and generates the script
    /// (<see cref="Create.TasScript"/>).</para>
    /// <para><see cref="Run"/> creates the run folder (project snapshot + one folder per evaluation), writes the glazing
    /// systems the options need into the snapshot's TBD under unique names, then runs the kernel. <see cref="Test"/> runs a
    /// single evaluation ("Test one simulation"). The result rules are <see cref="NativeGenOptOutcome"/>'s.</para>
    /// </summary>
    public sealed class TasModelRunner
    {
        private readonly TasModelRunSettings settings;

        /// <param name="optimisationDefinition">The definition (engine "tas-model").</param>
        /// <param name="tasModelRunSettings">The project folder and the local settings.</param>
        public TasModelRunner(OptimisationDefinition optimisationDefinition, TasModelRunSettings tasModelRunSettings)
        {
            Definition = optimisationDefinition ?? throw new ArgumentNullException(nameof(optimisationDefinition));
            settings = tasModelRunSettings ?? throw new ArgumentNullException(nameof(tasModelRunSettings));
            if (string.IsNullOrWhiteSpace(settings.ProjectFolder) || !Directory.Exists(settings.ProjectFolder))
            {
                throw new DirectoryNotFoundException("The Tas project folder does not exist: '" + settings.ProjectFolder + "'.");
            }

            Inventory = settings.Inventory ?? Query.TasModelInventory(settings.ProjectFolder);
            List<TasGlazingSystem> pool = (settings.GlazingPool ?? Enumerable.Empty<TasGlazingSystem>()).Where(x => x != null).ToList();
            Catalogue = Query.TasModelCatalogue(Inventory, pool, settings.GlazingFilter);
            Diagnostics = optimisationDefinition.Diagnostics(Query.TasModelCapabilities(), Catalogue).AsReadOnly();
            if (!Diagnostics.IsRunnable())
            {
                throw new TasOptimisationDefinitionException(Diagnostics);
            }

            Dictionary<string, IReadOnlyList<TasGlazingOption>> glazingOptions = new Dictionary<string, IReadOnlyList<TasGlazingOption>>(StringComparer.Ordinal);
            foreach (DesignVariable designVariable in optimisationDefinition.Variables.Where(x => x.Target?.Kind == TasModelKind.GlazingChoice))
            {
                glazingOptions[designVariable.Name] = ResolveGlazingOptions(designVariable, Inventory, pool, settings.GlazingFilter);
            }

            GlazingOptions = glazingOptions;
            Kernel = optimisationDefinition.ToSAM_TasModelKernel();
            ScriptText = Create.TasScript(optimisationDefinition, Inventory, glazingOptions);
        }

        public OptimisationDefinition Definition { get; }

        public TasModelInventory Inventory { get; }

        /// <summary>The model's catalogue the definition was checked against.</summary>
        public OptimisationCatalogue Catalogue { get; }

        /// <summary>The findings of the check (no errors: the constructor throws on an error).</summary>
        public IReadOnlyList<OptimisationDiagnostic> Diagnostics { get; }

        /// <summary>Every glazing choice's options in the definition's order, by variable name.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<TasGlazingOption>> GlazingOptions { get; }

        public TasModelKernel Kernel { get; }

        /// <summary>The generated TasGenExecute script (Script.txt of every run).</summary>
        public string ScriptText { get; }

        /// <summary>
        /// Writes the glazing systems of the options into the snapshot TBD before the first evaluation:
        /// <see cref="WriteGlazingSystems"/> by default (licensed Tas, COM). Replaceable for tests.
        /// </summary>
        public Action<string, IReadOnlyList<TasGlazingOption>> GlazingWriter { get; set; } = WriteGlazingSystems;

        /// <summary>
        /// Runs the optimisation: a new run folder, the glazing systems written into its snapshot TBD, then the kernel.
        /// Evaluations run one at a time; cancellation stops before the next one.
        /// </summary>
        public NativeGenOptRun Run(IProgress<OptimisationProgress> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            TasGenExecuteObjectiveEvaluator evaluator = Prepare(out NativeGenOptWorkspace workspace);
            OptimisationResult result = Kernel.Optimiser.Run(Kernel.Problem, evaluator, progress, cancellationToken);
            return new NativeGenOptRun(workspace, Kernel.VariableNames.ToList(), Kernel.OutputNames.ToList(), result);
        }

        /// <summary>
        /// "Test one simulation": a new run folder and one evaluation at <paramref name="coordinates"/> (default: each
        /// variable's start, or its minimum; option 1, the current model, for a choice). Its duration is the estimate of
        /// one simulation.
        /// </summary>
        public TasModelTest Test(IReadOnlyList<double> coordinates = null)
        {
            List<double> point = coordinates?.ToList() ?? Kernel.Problem.Parameters.Select(x => x.Initial).ToList();
            TasGenExecuteObjectiveEvaluator evaluator = Prepare(out NativeGenOptWorkspace workspace);
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            ObjectiveEvaluation evaluation = evaluator.Evaluate(1, point);
            return new TasModelTest(workspace, point, evaluation, TasGenExecuteObjectiveEvaluator.EvaluationDirectory(workspace.EvaluationsDirectory, 1, 1), stopwatch.Elapsed);
        }

        private TasGenExecuteObjectiveEvaluator Prepare(out NativeGenOptWorkspace workspace)
        {
            string executablePath = string.IsNullOrWhiteSpace(settings.TasGenExecutePath) ? Query.TasGenOptExecutePath() : settings.TasGenExecutePath;
            if (!System.IO.File.Exists(executablePath))
            {
                throw new FileNotFoundException("TasGenExecute was not found.", executablePath);
            }

            string runsDirectory = string.IsNullOrWhiteSpace(settings.RunsFolder) ? Path.Combine(settings.ProjectFolder, "SAM_NativeGenOpt") : settings.RunsFolder;
            workspace = NativeGenOptWorkspace.Create(settings.ProjectFolder, ScriptText, runsDirectory);

            List<TasGlazingOption> systems = GlazingOptions.Values.SelectMany(x => x).Where(x => !x.IsCurrent).GroupBy(x => x.PaneConstruction, StringComparer.Ordinal).Select(x => x.First()).ToList();
            if (systems.Count > 0)
            {
                GlazingWriter(Path.Combine(workspace.ProjectDirectory, Inventory.TbdFileName), systems);
            }

            return new TasGenExecuteObjectiveEvaluator(executablePath, workspace.ProjectDirectory, workspace.EvaluationsDirectory, Kernel.Parameters, Kernel.Objectives);
        }

        /// <summary>
        /// The options of a glazing choice in the definition's order: each listed text must be one of the options the
        /// pool gives for that glazing construction (<see cref="Query.TasGlazingOptions"/>); the catalogue check already
        /// reported any other (OPT614), so a mismatch here is an error.
        /// </summary>
        private static List<TasGlazingOption> ResolveGlazingOptions(DesignVariable designVariable, TasModelInventory tasModelInventory, List<TasGlazingSystem> pool, TasGlazingFilter filter)
        {
            string glazingConstruction = designVariable.Target.Reference[TasModelKind.GlazingConstructionKey];
            TasGlazingConstructionInfo current = tasModelInventory.GlazingConstructions.FirstOrDefault(x => x.Name == glazingConstruction)
                ?? throw new InvalidOperationException("Glazing construction “" + glazingConstruction + "” is not in the model.");
            List<TasGlazingOption> available = Query.TasGlazingOptions(current, pool, filter);
            List<TasGlazingOption> result = new List<TasGlazingOption>();
            foreach (string text in designVariable.Target.Options)
            {
                TasGlazingOption option = available.FirstOrDefault(x => x.Text == text)
                    ?? throw new InvalidOperationException("Glazing option “" + text + "” of “" + designVariable.Name + "” is not available for “" + glazingConstruction + "”.");
                result.Add(new TasGlazingOption(result.Count + 1, option.Text, option.PaneConstruction, option.G, option.U, option.Light, option.Source, option.System));
            }

            return result;
        }

        /// <summary>
        /// Writes the options' glazing systems into a TBD (a run's snapshot copy, never the user's) as unused
        /// constructions, each under its unique name (<see cref="Query.TasGlazingPaneConstruction"/>), with SAM_Tas'
        /// <c>Modify.UpdateConstructions(building, apertureConstructions, materialLibrary)</c>, then saves it (licensed
        /// Tas, COM). Refuses a name the TBD already has, because that writer overwrites by name (PR7a-2).
        /// </summary>
        public static void WriteGlazingSystems(string tbdPath, IReadOnlyList<TasGlazingOption> options)
        {
            List<TasGlazingOption> systems = (options ?? new List<TasGlazingOption>()).Where(x => x != null && !x.IsCurrent).ToList();
            if (systems.Count == 0)
            {
                return;
            }

            TasGlazingOption detached = systems.FirstOrDefault(x => x.System.ApertureConstruction == null || string.IsNullOrEmpty(x.PaneConstruction));
            if (detached != null)
            {
                throw new InvalidOperationException("Glazing option “" + detached.Text + "” has no glazing system to write.");
            }

            TBD.TBDDocument document = new TBD.TBDDocument();
            try
            {
                document.open(tbdPath);
                TBD.Building building = document.Building;
                foreach (TasGlazingOption option in systems)
                {
                    if (building.GetConstructionByName(option.PaneConstruction) != null)
                    {
                        throw new InvalidOperationException("The TBD already has a construction named “" + option.PaneConstruction + "”; it would be overwritten.");
                    }
                }

                foreach (IGrouping<global::SAM.Core.MaterialLibrary, TasGlazingOption> group in systems.GroupBy(x => x.System.MaterialLibrary))
                {
                    global::SAM.Analytical.Tas.Modify.UpdateConstructions(building, group.Select(x => Query.UniqueApertureConstruction(x.System)).ToList(), group.Key);
                }

                foreach (TasGlazingOption option in systems)
                {
                    TBD.Construction construction = building.GetConstructionByName(option.PaneConstruction);
                    if (construction == null || construction.type != TBD.ConstructionTypes.tcdTransparentConstruction)
                    {
                        throw new InvalidOperationException("Glazing system “" + option.Text + "” was not written as the transparent construction “" + option.PaneConstruction + "”.");
                    }
                }

                document.save();
            }
            finally
            {
                document.close();
                Marshal.FinalReleaseComObject(document);
            }
        }
    }
}
