// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Math;
using System.Collections.Generic;

namespace SAM.Analytical.Tas.GenOpt
{
    /// <summary>
    /// A "tas-model" definition mapped onto the SAM.Math kernel (<see cref="Convert.ToSAM_TasModelKernel"/>): the
    /// optimiser, the problem, and what the evaluator writes and reads (Variables.txt parameters <c>V1..Vn</c>,
    /// Output.txt objectives <c>Y1..Ym</c>, objective first).
    /// </summary>
    public sealed class TasModelKernel
    {
        internal TasModelKernel(Optimiser optimiser, OptimisationProblem problem, List<NumberParameter> parameters, List<Objective> objectives, List<string> variableNames, List<string> outputNames)
        {
            Optimiser = optimiser;
            Problem = problem;
            Parameters = parameters.AsReadOnly();
            Objectives = objectives.AsReadOnly();
            VariableNames = variableNames.AsReadOnly();
            OutputNames = outputNames.AsReadOnly();
        }

        public Optimiser Optimiser { get; }

        public OptimisationProblem Problem { get; }

        /// <summary>The Variables.txt parameters, <c>V1..Vn</c> in definition order.</summary>
        public IReadOnlyList<NumberParameter> Parameters { get; }

        /// <summary>The Output.txt objectives, <c>Y1..Ym</c>, objective first.</summary>
        public IReadOnlyList<Objective> Objectives { get; }

        /// <summary>The definition's variable names, in coordinate order.</summary>
        public IReadOnlyList<string> VariableNames { get; }

        /// <summary>The definition's output names, in output order (objective first).</summary>
        public IReadOnlyList<string> OutputNames { get; }
    }
}
