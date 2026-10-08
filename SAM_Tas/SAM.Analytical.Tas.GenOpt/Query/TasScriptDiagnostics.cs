// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Optimisation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SAM.Analytical.Tas.GenOpt
{
    public static partial class Query
    {
        /// <summary>
        /// Warnings for names the Tas script does not appear to use (L6, execution): OPT501 for a design variable the
        /// script never reads (<c>Variables["name"]</c>), OPT502 for an output it never writes
        /// (<c>ScriptOutput.SetValue("name", …)</c>). The scan is literal (<see cref="TasScriptCatalogue"/>), so these are
        /// warnings, never errors: a name built at run time is not found, and the run is not blocked.
        /// </summary>
        /// <param name="optimisationDefinition">The definition whose names are checked.</param>
        /// <param name="scriptText">The text of the Tas script.</param>
        public static List<OptimisationDiagnostic> TasScriptDiagnostics(this OptimisationDefinition optimisationDefinition, string scriptText)
        {
            List<OptimisationDiagnostic> result = new List<OptimisationDiagnostic>();
            if (optimisationDefinition == null)
            {
                return result;
            }

            OptimisationCatalogue catalogue = TasScriptCatalogue(scriptText);
            HashSet<string> names_Variable = new HashSet<string>(catalogue.Variables.Select(x => x.Name), StringComparer.Ordinal);
            HashSet<string> names_Output = new HashSet<string>(catalogue.Outputs.Select(x => x.Name), StringComparer.Ordinal);

            List<DesignVariable> variables = optimisationDefinition.Variables ?? new List<DesignVariable>();
            for (int i = 0; i < variables.Count; i++)
            {
                string name = variables[i]?.Name;
                if (string.IsNullOrWhiteSpace(name) || names_Variable.Contains(name))
                {
                    continue;
                }

                result.Add(new OptimisationDiagnostic(
                    DiagnosticSeverity.Warning,
                    "OPT501",
                    string.Format(CultureInfo.InvariantCulture, "$.variables[{0}].name", i),
                    "The Tas script never reads design variable \"" + name + "\".",
                    Hint(name, catalogue.Variables, "Check that the name matches the one the script reads with Variables[\"…\"]")));
            }

            List<OptimisationOutput> outputs = optimisationDefinition.Outputs ?? new List<OptimisationOutput>();
            for (int i = 0; i < outputs.Count; i++)
            {
                string name = outputs[i]?.Name;
                if (string.IsNullOrWhiteSpace(name) || names_Output.Contains(name))
                {
                    continue;
                }

                result.Add(new OptimisationDiagnostic(
                    DiagnosticSeverity.Warning,
                    "OPT502",
                    string.Format(CultureInfo.InvariantCulture, "$.outputs[{0}].name", i),
                    "The Tas script never writes output \"" + name + "\".",
                    Hint(name, catalogue.Outputs, "Check that the name matches the one the script writes with ScriptOutput.SetValue(\"…\", value)")));
            }

            return result;
        }

        private static string Hint(string name, IReadOnlyList<OptimisationCatalogueEntry> entries, string text)
        {
            OptimisationCatalogueEntry entry = entries.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (entry != null)
            {
                return text + "; names are case-sensitive and the script uses \"" + entry.Name + "\".";
            }

            if (entries.Count == 0)
            {
                return text + ".";
            }

            return text + "; the script uses " + string.Join(", ", entries.Select(x => "\"" + x.Name + "\"")) + ".";
        }
    }
}
