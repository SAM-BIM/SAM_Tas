// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Optimisation;

namespace SAM.Analytical.Tas.GenOpt
{
    public static partial class Query
    {
        /// <summary>The engine id an Optimisation Definition names in <c>model.engine</c> to run on Tas through a script.</summary>
        public const string TasOptimisationEngine = "tas-script";

        /// <summary>
        /// Holds the shared instance in its own class on purpose: a static field on <see cref="Query"/> itself would load
        /// SAM.Core.Optimisation.dll whenever any Query member is first used (for example <see cref="TasGenOptExecutePath"/>),
        /// so an installation without that DLL would break routes that never touch a definition.
        /// </summary>
        private static class TasOptimisationCapabilitiesHolder
        {
            internal static readonly IOptimisationCapabilities Value = new OptimisationCapabilities(
                TasOptimisationEngine,
                "Tas",
                new[]
                {
                    new OptimisationAlgorithmCapability(OptimisationAlgorithm.GoldenSection, 1, 1),
                    new OptimisationAlgorithmCapability(OptimisationAlgorithm.HookeJeeves, 1, null),
                },
                new[] { ObjectiveSense.Minimise },
                new[] { DesignVariableType.Continuous },
                false);
        }

        /// <summary>
        /// What the Tas engine runs today (<see cref="GenOptDocument.RunNative"/>, the SAM.Math kernel): golden section on
        /// exactly one design variable, Hooke–Jeeves on one or more, minimise only, continuous variables, no constraints.
        /// SAM.Core.Optimisation validates a definition against it (<c>Diagnostics</c>, <c>IsRunnable</c>) and builds the
        /// AI text from it, so neither offers anything this engine cannot execute.
        /// </summary>
        public static IOptimisationCapabilities TasOptimisationCapabilities()
        {
            return TasOptimisationCapabilitiesHolder.Value;
        }
    }
}
