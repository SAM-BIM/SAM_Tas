// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Optimisation;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Tas.GenOpt
{
    public static partial class Convert
    {
        /// <summary>
        /// Builds the <see cref="GenOptDocument"/> that runs an Optimisation Definition on Tas through the existing,
        /// accepted native route (<see cref="GenOptDocument.RunNative"/>). The definition is checked against
        /// <see cref="Query.TasOptimisationCapabilities"/> first; a definition with any error throws
        /// <see cref="TasOptimisationDefinitionException"/> and nothing is built.
        /// <para>
        /// The mapping reproduces what the SAM_UI form built before (PR5), value for value, so the run is unchanged:
        /// </para>
        /// <list type="bullet">
        /// <item>Each design variable becomes a <see cref="NumberParameter"/> (name, start, minimum, maximum, step), in
        /// definition order. Golden section ignores start and step, but they are passed through as given so
        /// Variables.txt keeps its bytes; when absent, start is the minimum and step is 0.</item>
        /// <item>The objective output goes first (the native optimiser minimises the first output); the recorded outputs
        /// follow in definition order. Each reads the script's <c>name::value</c> line.</item>
        /// <item>Golden section: the tolerance is AbsDiffFunction. Hooke–Jeeves: step reduction factor, initial step
        /// exponent, step exponent increment and step reductions are MeshSizeDivider, InitialMeshSizeExponent,
        /// MeshSizeExponentIncrement and NumberOfStepReduction. A setting the definition omits keeps the SAM_Tas
        /// default.</item>
        /// <item>Maximum simulations is MaxIte. MaxEqualResults and the other OptimizationSettings keep their SAM_Tas
        /// defaults.</item>
        /// </list>
        /// <para>The objects are added in the order the SAM_UI form and the Grasshopper component add them: script,
        /// objectives, parameters.</para>
        /// </summary>
        /// <param name="optimisationDefinition">The definition to run.</param>
        /// <param name="directory">The Tas project folder (the workspace).</param>
        /// <param name="scriptText">The text of the Tas script.</param>
        public static GenOptDocument ToGenOptDocument(this OptimisationDefinition optimisationDefinition, string directory, string scriptText)
        {
            if (optimisationDefinition == null)
            {
                throw new ArgumentNullException(nameof(optimisationDefinition));
            }

            List<OptimisationDiagnostic> diagnostics = optimisationDefinition.Diagnostics(Query.TasOptimisationCapabilities());
            if (!diagnostics.IsRunnable())
            {
                throw new TasOptimisationDefinitionException(diagnostics);
            }

            GenOptDocument result = new GenOptDocument(directory)
            {
                Algorithm = ToGenOpt(optimisationDefinition.Method),
                OptimizationSettings = ToGenOpt(optimisationDefinition.Stopping),
            };

            result.AddScript(scriptText);

            foreach (string name in ObjectiveNames(optimisationDefinition))
            {
                result.AddObjective(new Objective(name));
            }

            foreach (DesignVariable designVariable in optimisationDefinition.Variables)
            {
                result.AddParameter(new NumberParameter()
                {
                    Name = designVariable.Name,
                    Initial = designVariable.Start ?? designVariable.Minimum,
                    Min = designVariable.Minimum,
                    Max = designVariable.Maximum,
                    Step = designVariable.Step ?? 0,
                });
            }

            return result;
        }

        /// <summary>
        /// <see cref="ToGenOptDocument(OptimisationDefinition, string, string)"/> for the local settings: the workspace is
        /// <see cref="TasExecutionSettings.ProjectFolder"/> and the script is read from
        /// <see cref="TasExecutionSettings.ScriptPath"/>. Run the result with
        /// <c>RunNative(settings.RunsFolder, settings.TasGenExecutePath, …)</c>.
        /// </summary>
        public static GenOptDocument ToGenOptDocument(this OptimisationDefinition optimisationDefinition, TasExecutionSettings tasExecutionSettings)
        {
            if (optimisationDefinition == null)
            {
                throw new ArgumentNullException(nameof(optimisationDefinition));
            }

            if (tasExecutionSettings == null)
            {
                throw new ArgumentNullException(nameof(tasExecutionSettings));
            }

            if (string.IsNullOrWhiteSpace(tasExecutionSettings.ScriptPath) || !System.IO.File.Exists(tasExecutionSettings.ScriptPath))
            {
                throw new System.IO.FileNotFoundException("The Tas script was not found.", tasExecutionSettings.ScriptPath);
            }

            return optimisationDefinition.ToGenOptDocument(tasExecutionSettings.ProjectFolder, System.IO.File.ReadAllText(tasExecutionSettings.ScriptPath));
        }

        /// <summary>The output names in the order the optimiser receives them: the objective first, then the recorded outputs.</summary>
        private static List<string> ObjectiveNames(OptimisationDefinition optimisationDefinition)
        {
            List<string> result = new List<string> { optimisationDefinition.Objective.Output };
            foreach (OptimisationOutput optimisationOutput in optimisationDefinition.RecordedOutputs())
            {
                result.Add(optimisationOutput.Name);
            }

            return result;
        }

        private static Algorithm ToGenOpt(OptimisationMethod optimisationMethod)
        {
            if (optimisationMethod is GoldenSectionMethod goldenSectionMethod)
            {
                GoldenSectionAlgorithm result = new GoldenSectionAlgorithm();
                if (goldenSectionMethod.Tolerance != null)
                {
                    result.AbsDiffFunction = goldenSectionMethod.Tolerance.Value;
                }

                return result;
            }

            if (optimisationMethod is HookeJeevesMethod hookeJeevesMethod)
            {
                GPSHookeJeevesAlgorithm result = new GPSHookeJeevesAlgorithm();
                if (hookeJeevesMethod.StepReductionFactor != null)
                {
                    result.MeshSizeDivider = hookeJeevesMethod.StepReductionFactor.Value;
                }

                if (hookeJeevesMethod.InitialStepExponent != null)
                {
                    result.InitialMeshSizeExponent = hookeJeevesMethod.InitialStepExponent.Value;
                }

                if (hookeJeevesMethod.StepExponentIncrement != null)
                {
                    result.MeshSizeExponentIncrement = hookeJeevesMethod.StepExponentIncrement.Value;
                }

                if (hookeJeevesMethod.StepReductions != null)
                {
                    result.NumberOfStepReduction = hookeJeevesMethod.StepReductions.Value;
                }

                return result;
            }

            // Unreachable for a runnable definition: the capabilities list only these two methods.
            throw new NotSupportedException("The optimisation method " + optimisationMethod?.Algorithm.ToString() + " is not supported by the Tas engine.");
        }

        private static OptimizationSettings ToGenOpt(StoppingCriteria stoppingCriteria)
        {
            OptimizationSettings result = new OptimizationSettings();
            if (stoppingCriteria?.MaximumSimulations != null)
            {
                result.MaxIterations = stoppingCriteria.MaximumSimulations.Value;
            }

            return result;
        }
    }
}
