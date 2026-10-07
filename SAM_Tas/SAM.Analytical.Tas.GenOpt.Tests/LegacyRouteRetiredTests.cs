// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace SAM.Analytical.Tas.GenOpt.Tests
{
    /// <summary>
    /// PR6: the legacy Java GenOpt execution route (<c>GenOptDocument.Run()</c>: GenOpt.bat, java, genopt.jar,
    /// <c>cmd /c</c>, the Tas Manager project registry) is gone from SAM.Analytical.Tas.GenOpt, so it cannot be invoked.
    /// The built assembly is read by reflection and as metadata; the only process it starts is TasGenExecute, from
    /// <see cref="TasGenExecuteObjectiveEvaluator"/>.
    /// </summary>
    [TestFixture]
    public class LegacyRouteRetiredTests
    {
        private static readonly Assembly assembly = typeof(GenOptDocument).Assembly;

        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        // ------------------------------------------------------------------ the API

        [Test]
        public void GenOptDocument_CanOnlyBeRunNatively()
        {
            List<string> runs = typeof(GenOptDocument).GetMethods(All).Select(x => x.Name).Where(x => x.StartsWith("Run", StringComparison.Ordinal)).Distinct().ToList();

            Assert.That(runs, Is.EqualTo(new[] { nameof(GenOptDocument.RunNative) }));
        }

        [TestCase("ExecutableFile")]
        [TestCase("SimulationConfigFile")]
        [TestCase("Command")]
        [TestCase("WriteInputFileExtension")]
        [TestCase("ErrorMessage")]
        [TestCase("NumberFormat")]
        public void GenOptDocument_HasNoJavaLaunchSetting(string property)
        {
            Assert.That(typeof(GenOptDocument).GetProperty(property, All), Is.Null);
        }

        [TestCase("ExecutableFile")]
        [TestCase("SimulationConfigFile")]
        [TestCase("SimulationStart")]
        [TestCase("SimulationError")]
        [TestCase("IO")]
        [TestCase("NumberFormat")]
        public void TheJavaLaunchTypesAreGone(string name)
        {
            Assert.That(assembly.GetType("SAM.Analytical.Tas.GenOpt." + name), Is.Null);
        }

        [Test]
        public void NoJavaPathOrCommandBuilderIsLeft()
        {
            Assert.That(typeof(Query).GetMethod("TasGenOptJavaPath", All), Is.Null);
            Assert.That(typeof(Create).GetMethod("Command", All), Is.Null);

            // What the native route itself needs from the installation stays.
            Assert.That(Query.TasGenOptExecutePath(), Does.EndWith("TasGenExecute.exe"));
        }

        // ------------------------------------------------------------------ the built assembly

        [Test]
        public void OnlyTheEvaluatorStartsAProcess()
        {
            List<string> starters = Callers(typeof(Process).GetMethods().Where(x => x.Name == nameof(Process.Start)).Cast<MethodBase>().ToList());

            Assert.That(starters, Is.EqualTo(new[] { typeof(TasGenExecuteObjectiveEvaluator).FullName }));
        }

        [Test]
        public void NothingWritesTheTasManagerRegistry()
        {
            Metadata(out HashSet<string> memberReferences, out HashSet<string> typeReferences, out _);

            Assert.That(memberReferences, Does.Not.Contain("SAM.Core.Tas.Modify::SetProjectDirectory"));
            Assert.That(typeReferences, Does.Not.Contain("Microsoft.Win32.Registry"));
            Assert.That(typeReferences, Does.Not.Contain("Microsoft.Win32.RegistryKey"));
        }

        [TestCase("java.exe")]
        [TestCase("javaw")]
        [TestCase("java -classpath")]
        [TestCase(".jar")]
        [TestCase("genopt.GenOpt")]
        [TestCase("GenOpt.bat")]
        [TestCase("cmd /c")]
        [TestCase("cmd.exe")]
        [TestCase("start  /WAIT")]
        [TestCase(@"SOFTWARE\EDSL")]
        public void HoldsNoJavaLaunchText(string text)
        {
            Metadata(out _, out _, out List<string> userStrings);

            Assert.That(userStrings.Where(x => x.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0), Is.Empty);
        }

        // ------------------------------------------------------------------ helpers

        private static void Metadata(out HashSet<string> memberReferences, out HashSet<string> typeReferences, out List<string> userStrings)
        {
            memberReferences = new HashSet<string>(StringComparer.Ordinal);
            typeReferences = new HashSet<string>(StringComparer.Ordinal);
            userStrings = new List<string>();

            using (FileStream fileStream = System.IO.File.OpenRead(assembly.Location))
            using (PEReader peReader = new PEReader(fileStream))
            {
                MetadataReader reader = peReader.GetMetadataReader();

                foreach (TypeReferenceHandle handle in reader.TypeReferences)
                {
                    TypeReference typeReference = reader.GetTypeReference(handle);
                    typeReferences.Add(reader.GetString(typeReference.Namespace) + "." + reader.GetString(typeReference.Name));
                }

                foreach (MemberReferenceHandle handle in reader.MemberReferences)
                {
                    MemberReference memberReference = reader.GetMemberReference(handle);
                    if (memberReference.Parent.Kind == HandleKind.TypeReference)
                    {
                        TypeReference parent = reader.GetTypeReference((TypeReferenceHandle)memberReference.Parent);
                        memberReferences.Add(reader.GetString(parent.Namespace) + "." + reader.GetString(parent.Name) + "::" + reader.GetString(memberReference.Name));
                    }
                }

                UserStringHandle userStringHandle = MetadataTokens.UserStringHandle(1);
                while (!userStringHandle.IsNil)
                {
                    userStrings.Add(reader.GetUserString(userStringHandle));
                    userStringHandle = reader.GetNextHandle(userStringHandle);
                }
            }
        }

        // The outermost declared type of every method body in the assembly that calls one of targets (compiler-generated
        // closures and state machines count for the type that declares them). call, callvirt and newobj are followed by a
        // four-byte method token; a byte pattern that only looks like one does not resolve to a target.
        private static List<string> Callers(ICollection<MethodBase> targets)
        {
            SortedSet<string> result = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Type type in assembly.GetTypes())
            {
                IEnumerable<MethodBase> methods = type.GetMethods(All).Cast<MethodBase>().Concat(type.GetConstructors(All));
                foreach (MethodBase method in methods)
                {
                    byte[] il = method.GetMethodBody()?.GetILAsByteArray();
                    if (il == null)
                    {
                        continue;
                    }

                    for (int i = 0; i + 4 < il.Length; i++)
                    {
                        if (il[i] != 0x28 && il[i] != 0x6F && il[i] != 0x73)
                        {
                            continue;
                        }

                        MethodBase called;
                        try
                        {
                            called = type.Module.ResolveMethod(BitConverter.ToInt32(il, i + 1), type.IsGenericType ? type.GetGenericArguments() : null, method.IsGenericMethod ? method.GetGenericArguments() : null);
                        }
                        catch (Exception)
                        {
                            continue;
                        }

                        if (targets.Contains(called))
                        {
                            Type outer = type;
                            while (outer.DeclaringType != null)
                            {
                                outer = outer.DeclaringType;
                            }

                            result.Add(outer.FullName);
                        }
                    }
                }
            }

            return result.ToList();
        }
    }
}
