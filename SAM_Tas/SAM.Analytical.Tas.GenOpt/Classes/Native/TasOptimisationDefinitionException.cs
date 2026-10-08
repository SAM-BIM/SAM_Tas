// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Optimisation;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Tas.GenOpt
{
    /// <summary>
    /// An Optimisation Definition the Tas engine cannot run: it has at least one error against
    /// <see cref="Query.TasOptimisationCapabilities"/>. <see cref="Diagnostics"/> holds every finding; the message lists
    /// the errors. Thrown before any folder is created or any process is started.
    /// </summary>
    public class TasOptimisationDefinitionException : GenOptCompatibilityException
    {
        public TasOptimisationDefinitionException(IEnumerable<OptimisationDiagnostic> diagnostics)
            : base(Text(diagnostics))
        {
            Diagnostics = (diagnostics ?? Enumerable.Empty<OptimisationDiagnostic>()).Where(x => x != null).ToList().AsReadOnly();
        }

        /// <summary>Every finding (errors, warnings and information), in the order SAM.Core.Optimisation reported them.</summary>
        public IReadOnlyList<OptimisationDiagnostic> Diagnostics { get; }

        private static string Text(IEnumerable<OptimisationDiagnostic> diagnostics)
        {
            List<string> errors = (diagnostics ?? Enumerable.Empty<OptimisationDiagnostic>()).Where(x => x != null && x.Severity == DiagnosticSeverity.Error).Select(x => x.Message).ToList();
            if (errors.Count == 0)
            {
                return "The optimisation definition cannot run with the Tas engine.";
            }

            return "The optimisation definition cannot run with the Tas engine: " + string.Join(" ", errors);
        }
    }
}
