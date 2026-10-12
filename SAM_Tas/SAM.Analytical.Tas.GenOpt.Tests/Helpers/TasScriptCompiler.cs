// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SAM.Analytical.Tas.GenOpt.Tests.Helpers
{
    /// <summary>
    /// Compiles a script the way TasGenExecute does (PR7a question 7): Roslyn scripting at C# 7.0 exactly, the imports
    /// System, System.IO, System.Collections.Generic and TasGenComm, the globals object TasGenComm.ScriptArgs, and the Tas
    /// interops (TBD, TSD, TPD; the repository's build-only copies). TasGenComm is the stand-in
    /// (<c>TasGenCommStandIn.cs</c>). Nothing is run, except a generated helper loaded on its own (<see cref="Load"/>).
    /// </summary>
    public static class TasScriptCompiler
    {
        public static readonly string[] Imports = { "System", "System.IO", "System.Collections.Generic", "TasGenComm" };

        /// <summary>The compiler errors (empty when the script compiles).</summary>
        public static List<string> Errors(string script, LanguageVersion languageVersion = LanguageVersion.CSharp7)
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(script, new CSharpParseOptions(languageVersion, kind: SourceCodeKind.Script));
            CSharpCompilation compilation = CSharpCompilation.CreateScriptCompilation(
                "TasGenExecuteScript",
                syntaxTree,
                References(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, usings: Imports),
                null,
                typeof(object),
                typeof(TasGenComm.ScriptArgs));

            return compilation.GetDiagnostics()
                .Where(x => x.Severity == DiagnosticSeverity.Error)
                .Select(x => x.Id + " " + x.GetMessage() + " at line " + (x.Location.GetLineSpan().StartLinePosition.Line + 1))
                .ToList();
        }

        /// <summary>
        /// Compiles C# source (a class, not a script) at C# 7.0 against the same references and loads it, so that a test can
        /// run a helper the generator writes into the script.
        /// </summary>
        public static System.Reflection.Assembly Load(string source)
        {
            CSharpCompilation compilation = CSharpCompilation.Create(
                "TasScriptProbe" + Guid.NewGuid().ToString("N"),
                new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp7)) },
                References(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (MemoryStream stream = new MemoryStream())
            {
                Microsoft.CodeAnalysis.Emit.EmitResult result = compilation.Emit(stream);
                if (!result.Success)
                {
                    throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error)));
                }

                return System.Reflection.Assembly.Load(stream.ToArray());
            }
        }

        private static IEnumerable<MetadataReference> References()
        {
            // The runtime's own assemblies only (TasGenExecute's default script references; Microsoft.CSharp included),
            // not the test's dependencies (SAM, Interop.TCD, ...), which a TasGenExecute script cannot see.
            Dictionary<string, string> paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location);
            string platform = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            foreach (string path in platform.Split(Path.PathSeparator).Where(x => x.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && string.Equals(Path.GetDirectoryName(x), runtime, StringComparison.OrdinalIgnoreCase)))
            {
                paths[Path.GetFileName(path)] = path;
            }

            string interops = Path.Combine(TasModelFixtures.RepositoryRoot, "references_buildonly");
            foreach (string name in new[] { "Interop.TBD.dll", "Interop.TSD.dll", "Interop.TPD.dll" })
            {
                paths[name] = Path.Combine(interops, name);
            }

            string standIn = typeof(TasGenComm.ScriptArgs).Assembly.Location;
            paths[Path.GetFileName(standIn)] = standIn;
            return paths.Values.Select(x => (MetadataReference)MetadataReference.CreateFromFile(x)).ToList();
        }
    }
}
