// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Optimisation;
using SAM.Math;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Tas.GenOpt
{
    public static partial class Convert
    {
        /// <summary>
        /// Maps a "tas-model" definition onto the SAM.Math kernel. The definition is checked against
        /// <see cref="Query.TasModelCapabilities"/> first; any error throws <see cref="TasOptimisationDefinitionException"/>.
        /// <list type="bullet">
        /// <item>Golden section and Hooke–Jeeves exactly as the "tas-script" route maps them (PR4,
        /// <see cref="ToGenOptDocument(OptimisationDefinition, string, string)"/>): the GenOpt algorithm and settings
        /// objects, then <see cref="ToSAM_Optimiser"/>; each variable a parameter (start or minimum, minimum, maximum,
        /// step or 0).</item>
        /// <item>Try every option (SAM#190): <see cref="TryEveryOption"/> with the simulation limit (2000 when not
        /// given) and one parameter (name, 1, 1, n, 1) for the choice; option k is the target's option k.</item>
        /// </list>
        /// The kernel and Variables.txt see the variables as <c>V1..Vn</c> and the outputs as <c>Y1..Ym</c> (objective
        /// first), the names the generated script uses (<see cref="Create.TasScript"/>), so no label can break the
        /// comma-separated Variables.txt or the <c>name::value</c> Output.txt.
        /// </summary>
        public static TasModelKernel ToSAM_TasModelKernel(this OptimisationDefinition optimisationDefinition)
        {
            if (optimisationDefinition == null)
            {
                throw new ArgumentNullException(nameof(optimisationDefinition));
            }

            List<OptimisationDiagnostic> diagnostics = optimisationDefinition.Diagnostics(Query.TasModelCapabilities());
            if (!diagnostics.IsRunnable())
            {
                throw new TasOptimisationDefinitionException(diagnostics);
            }

            List<NumberParameter> parameters = new List<NumberParameter>();
            for (int i = 0; i < optimisationDefinition.Variables.Count; i++)
            {
                DesignVariable designVariable = optimisationDefinition.Variables[i];
                if (designVariable.Type == DesignVariableType.Discrete)
                {
                    int count = designVariable.Target?.Options?.Count > 0 ? designVariable.Target.Options.Count : (int)designVariable.Maximum;
                    parameters.Add(new NumberParameter() { Name = Create.TasScriptVariableName(i), Initial = 1, Min = 1, Max = count, Step = 1 });
                }
                else
                {
                    parameters.Add(new NumberParameter()
                    {
                        Name = Create.TasScriptVariableName(i),
                        Initial = designVariable.Start ?? designVariable.Minimum,
                        Min = designVariable.Minimum,
                        Max = designVariable.Maximum,
                        Step = designVariable.Step ?? 0,
                    });
                }
            }

            List<OptimisationOutput> outputs = Create.TasScriptOutputs(optimisationDefinition);
            List<Objective> objectives = Enumerable.Range(0, outputs.Count).Select(x => new Objective(Create.TasScriptOutputName(x))).ToList();

            Optimiser optimiser;
            if (optimisationDefinition.Method is TryEveryOptionMethod)
            {
                optimiser = new TryEveryOption { MaximumSimulations = optimisationDefinition.Stopping?.MaximumSimulations ?? new OptimizationSettings().MaxIterations };
            }
            else
            {
                optimiser = ToGenOpt(optimisationDefinition.Method).ToSAM_Optimiser(ToGenOpt(optimisationDefinition.Stopping), parameters.Count);
            }

            OptimisationProblem problem = new OptimisationProblem(parameters.ConvertAll(ToSAM_OptimisationParameter), objectives.Count);
            return new TasModelKernel(
                optimiser,
                problem,
                parameters,
                objectives,
                optimisationDefinition.Variables.ConvertAll(x => x.Name),
                outputs.ConvertAll(x => x.Name));
        }
    }
}
