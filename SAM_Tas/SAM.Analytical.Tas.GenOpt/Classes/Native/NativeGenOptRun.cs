// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Math;
using System.Collections.Generic;

namespace SAM.Analytical.Tas.GenOpt
{
    /// <summary>The outcome of <see cref="GenOptDocument.RunNative"/>: the kernel result and where the run's files are.</summary>
    public sealed class NativeGenOptRun
    {
        internal NativeGenOptRun(NativeGenOptWorkspace workspace, IReadOnlyList<string> parameterNames, IReadOnlyList<string> objectiveNames, OptimisationResult result)
        {
            Workspace = workspace;
            ParameterNames = parameterNames;
            ObjectiveNames = objectiveNames;
            Result = result;
        }

        public NativeGenOptWorkspace Workspace { get; }

        /// <summary>Parameter names in coordinate order.</summary>
        public IReadOnlyList<string> ParameterNames { get; }

        /// <summary>Objective names in output order; the first is the one minimised.</summary>
        public IReadOnlyList<string> ObjectiveNames { get; }

        /// <summary>The SAM.Math kernel's structured result and trace (OutputListingAll/Main rows).</summary>
        public OptimisationResult Result { get; }
    }
}
