// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Math;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Tas.GenOpt
{
    /// <summary>The outcome of "Test one simulation" (<see cref="TasModelRunner.Test"/>).</summary>
    public sealed class TasModelTest
    {
        internal TasModelTest(NativeGenOptWorkspace workspace, IReadOnlyList<double> coordinates, ObjectiveEvaluation evaluation, string evaluationDirectory, TimeSpan duration)
        {
            Workspace = workspace;
            Coordinates = coordinates;
            Evaluation = evaluation;
            EvaluationDirectory = evaluationDirectory;
            Duration = duration;
        }

        public NativeGenOptWorkspace Workspace { get; }

        /// <summary>The point evaluated, in variable order.</summary>
        public IReadOnlyList<double> Coordinates { get; }

        /// <summary>Success with the outputs (objective first), or failure with the reason.</summary>
        public ObjectiveEvaluation Evaluation { get; }

        /// <summary>The evaluation folder (Variables.txt, Output.txt, Error.txt, tas-model-trace.txt).</summary>
        public string EvaluationDirectory { get; }

        /// <summary>The wall time of the evaluation: the estimate of one simulation.</summary>
        public TimeSpan Duration { get; }
    }
}
