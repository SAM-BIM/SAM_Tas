// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SAM.Analytical.Tas.GenOpt.Tests.Helpers
{
    /// <summary>
    /// Runs a helper the generator writes into the TasGenExecute script against stand-in TBD types (a building of zones
    /// with sizing types), so its decision is tested without Tas. Compiled at C# 7.0, as TasGenExecute compiles scripts.
    /// The stand-in <c>SizingType</c> carries Interop.TBD's values (asserted against <see cref="TasZoneSizing"/>).
    /// </summary>
    public static class StandInTbdProbe
    {
        private const string StandIn = @"
namespace TBD
{
    public enum SizingType { tbdNoSizing = 0, tbdDesignSizingOnly = 1, tbdSizing = 2, tbdMixed = 3, tbdLoadPerArea = 4 }
    public class zone { public string name; public int sizeHeating; public int sizeCooling; }
    public class Building
    {
        public List<zone> Zones = new List<zone>();
        public zone GetZone(int i) { return i < Zones.Count ? Zones[i] : null; }
    }
}
";

        /// <summary>
        /// Calls <paramref name="method"/> (a helper taking the building, extracted from <paramref name="script"/>) on a
        /// building whose zones have the given (heating, cooling) sizing types.
        /// </summary>
        public static void Run(string script, string method, IEnumerable<(int Heating, int Cooling)> zones)
        {
            int start = script.IndexOf("void " + method + "(", StringComparison.Ordinal);
            if (start < 0)
            {
                throw new InvalidOperationException("The script has no " + method + ".");
            }

            int end = script.IndexOf("\n}\n", start, StringComparison.Ordinal) + 2;
            string source = "using System; using System.Collections.Generic; using System.Linq;\n" + StandIn
                + "public static class Probe\n{\npublic static " + script.Substring(start, end - start) + "\n}";

            Assembly assembly = Load(source);
            object building = Activator.CreateInstance(assembly.GetType("TBD.Building"));
            System.Collections.IList list = (System.Collections.IList)building.GetType().GetField("Zones").GetValue(building);
            int i = 0;
            foreach ((int heating, int cooling) in zones)
            {
                object zone = Activator.CreateInstance(assembly.GetType("TBD.zone"));
                zone.GetType().GetField("name").SetValue(zone, "Zone " + (++i));
                zone.GetType().GetField("sizeHeating").SetValue(zone, heating);
                zone.GetType().GetField("sizeCooling").SetValue(zone, cooling);
                list.Add(zone);
            }

            try
            {
                assembly.GetType("Probe").GetMethod(method).Invoke(null, new[] { building });
            }
            catch (TargetInvocationException exception)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            }
        }

        private static Assembly Load(string source)
        {
            string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location);
            IEnumerable<MetadataReference> references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
                .Where(x => x.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && string.Equals(Path.GetDirectoryName(x), runtime, StringComparison.OrdinalIgnoreCase))
                .Select(x => (MetadataReference)MetadataReference.CreateFromFile(x));
            CSharpCompilation compilation = CSharpCompilation.Create(
                "StandInTbdProbe" + Guid.NewGuid().ToString("N"),
                new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp7)) },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (MemoryStream stream = new MemoryStream())
            {
                Microsoft.CodeAnalysis.Emit.EmitResult result = compilation.Emit(stream);
                if (!result.Success)
                {
                    throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error)));
                }

                return Assembly.Load(stream.ToArray());
            }
        }
    }
}
