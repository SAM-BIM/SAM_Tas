// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical.Tas.GenOpt.Tests.Helpers;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace SAM.Analytical.Tas.GenOpt.Tests
{
    /// <summary>
    /// PR4: SAM.Core.Optimisation.dll ships later (PR8, SAM_Deploy). Until then an installation may have the new
    /// SAM.Analytical.Tas.GenOpt.dll without it, so the existing route (GenOptDocument, Query, RunNative) must not load
    /// it. These tests load the built assemblies in a separate load context that refuses SAM.Core.Optimisation.
    /// </summary>
    [TestFixture]
    public class OptimisationDeploymentTests
    {
        private const string OptimisationAssemblyName = "SAM.Core.Optimisation";

        /// <summary>Loads the test folder's assemblies, except SAM.Core.Optimisation, which it refuses.</summary>
        private sealed class WithoutOptimisationLoadContext : AssemblyLoadContext
        {
            public WithoutOptimisationLoadContext()
                : base("WithoutSAM.Core.Optimisation", true)
            {
            }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                if (string.Equals(assemblyName.Name, OptimisationAssemblyName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new FileNotFoundException("Refused by the test load context.", assemblyName.Name + ".dll");
                }

                string path = Path.Combine(AppContext.BaseDirectory, assemblyName.Name + ".dll");
                return System.IO.File.Exists(path) ? LoadFromAssemblyPath(path) : null;
            }
        }

        private static Assembly GenOpt(AssemblyLoadContext assemblyLoadContext) => assemblyLoadContext.LoadFromAssemblyPath(typeof(GenOptDocument).Assembly.Location);

        private static object Invoke(Type type, string name, params object[] arguments)
        {
            try
            {
                return type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null).Invoke(null, arguments);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException;
            }
        }

        [Test]
        public void ExistingRoute_RunsWithoutSAMCoreOptimisation()
        {
            WithoutOptimisationLoadContext assemblyLoadContext = new WithoutOptimisationLoadContext();
            try
            {
                Assembly assembly = GenOpt(assemblyLoadContext);
                Type query = assembly.GetType("SAM.Analytical.Tas.GenOpt.Query", true);

                // A host may run a type's static initialiser as early as it likes (beforefieldinit; .NET Framework often
                // does it when a calling method is compiled). Run Query's and Convert's now: neither may need the DLL.
                Assert.DoesNotThrow(() => System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(query.TypeHandle));
                Assert.DoesNotThrow(() => System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(assembly.GetType("SAM.Analytical.Tas.GenOpt.Convert", true).TypeHandle));
                Assert.That(Invoke(query, "TasGenOptExecutePath"), Does.EndWith("TasGenExecute.exe"));

                using (TestFolder folder = new TestFolder())
                {
                    dynamic genOptDocument = Activator.CreateInstance(assembly.GetType("SAM.Analytical.Tas.GenOpt.GenOptDocument", true), folder.Folder("ws"));
                    genOptDocument.AddParameter("x", 0.5, 0.0, 1.0, 0.1);
                    genOptDocument.AddObjective("Result");
                    genOptDocument.AddScript(TestFolder.StubScript(center: new[] { 0.3 }, weight: new[] { 1.0 }));

                    dynamic run = genOptDocument.RunNative(folder.Folder("runs"), TestFolder.StubExecutable);
                    Assert.That(((object)run.Result.Outcome).ToString(), Is.EqualTo("Success"));
                    Assert.That((int)run.Result.Simulations, Is.GreaterThan(0));
                }

                Assert.That(assemblyLoadContext.Assemblies, Has.None.Matches<Assembly>(x => x.GetName().Name == OptimisationAssemblyName));
            }
            finally
            {
                assemblyLoadContext.Unload();
            }
        }

        [Test]
        public void TheNewApi_IsWhatNeedsSAMCoreOptimisation()
        {
            // Control: the load context really refuses the DLL, so the test above proves something.
            WithoutOptimisationLoadContext assemblyLoadContext = new WithoutOptimisationLoadContext();
            try
            {
                Type query = GenOpt(assemblyLoadContext).GetType("SAM.Analytical.Tas.GenOpt.Query", true);

                Assert.Throws<FileNotFoundException>(() => Invoke(query, "TasOptimisationCapabilities"));
            }
            finally
            {
                assemblyLoadContext.Unload();
            }
        }
    }
}
