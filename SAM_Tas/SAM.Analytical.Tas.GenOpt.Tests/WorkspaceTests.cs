// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical.Tas.GenOpt.Tests.Helpers;
using System.IO;
using System.Linq;

namespace SAM.Analytical.Tas.GenOpt.Tests
{
    /// <summary>The project snapshot (allow-list) and run isolation.</summary>
    [TestFixture]
    public class WorkspaceTests
    {
        private static string SourceWorkspace(TestFolder folder)
        {
            string source = folder.Folder("source");
            foreach (string name in new[] { "Model.tbd", "Model.TPD", "Model.t3d", "Model.tsd", "Weather.twd" })
            {
                System.IO.File.WriteAllText(Path.Combine(source, name), name);
            }

            // Stale runtime artifacts, GenOpt files and unrelated files that must not reach the snapshot.
            foreach (string name in new[] { "Variables.txt", "Output.txt", "Error.txt", "Info.txt", "Config.ini", "Command.txt", "config.txt", "Template.txt", "GenOpt.bat", "OutputListingAll.txt", "TasOutputs.txt", "Script.txt", "Construction.tcd", "notes.docx" })
            {
                System.IO.File.WriteAllText(Path.Combine(source, name), "stale " + name);
            }

            string run = Path.Combine(source, "tmp-genopt-run-1");
            Directory.CreateDirectory(run);
            System.IO.File.WriteAllText(Path.Combine(run, "Copy.tbd"), "nested");
            return source;
        }

        [Test]
        public void Snapshot_HoldsOnlyTheScriptAndTheTasFileTypesTasGenExecuteDiscovers()
        {
            using (TestFolder folder = new TestFolder())
            {
                string source = SourceWorkspace(folder);

                NativeGenOptWorkspace workspace = NativeGenOptWorkspace.Create(source, "the document script", folder.Folder("runs"));

                string[] files = Directory.GetFiles(workspace.ProjectDirectory).Select(Path.GetFileName).OrderBy(x => x).ToArray();
                Assert.That(files, Is.EqualTo(new[] { "Model.t3d", "Model.tbd", "Model.TPD", "Model.tsd", "Script.txt", "Weather.twd" }.OrderBy(x => x).ToArray()));
                Assert.That(Directory.GetDirectories(workspace.ProjectDirectory), Is.Empty);
                Assert.That(workspace.ProjectFiles[0], Is.EqualTo("Script.txt"));
                Assert.That(System.IO.File.ReadAllText(Path.Combine(workspace.ProjectDirectory, "Script.txt")), Is.EqualTo("the document script"));
                Assert.That(System.IO.File.ReadAllText(Path.Combine(workspace.ProjectDirectory, "Model.tbd")), Is.EqualTo("Model.tbd"));
                Assert.That(Directory.GetFileSystemEntries(workspace.EvaluationsDirectory), Is.Empty);
            }
        }

        [Test]
        public void Snapshot_LeavesTheSourceWorkspaceUntouched()
        {
            using (TestFolder folder = new TestFolder())
            {
                string source = SourceWorkspace(folder);
                string[] before = Directory.GetFileSystemEntries(source, "*", SearchOption.AllDirectories).OrderBy(x => x).ToArray();

                NativeGenOptWorkspace.Create(source, "script", folder.Folder("runs"));

                Assert.That(Directory.GetFileSystemEntries(source, "*", SearchOption.AllDirectories).OrderBy(x => x).ToArray(), Is.EqualTo(before));
                Assert.That(System.IO.File.ReadAllText(Path.Combine(source, "Script.txt")), Is.EqualTo("stale Script.txt"));
            }
        }

        [Test]
        public void TwoRuns_GetDisjointFolders()
        {
            using (TestFolder folder = new TestFolder())
            {
                string source = SourceWorkspace(folder);
                string runs = folder.Folder("runs");

                NativeGenOptWorkspace first = NativeGenOptWorkspace.Create(source, "a", runs);
                NativeGenOptWorkspace second = NativeGenOptWorkspace.Create(source, "b", runs);

                Assert.That(first.RunDirectory, Is.Not.EqualTo(second.RunDirectory));
                Assert.That(System.IO.File.ReadAllText(Path.Combine(first.ProjectDirectory, "Script.txt")), Is.EqualTo("a"));
                Assert.That(System.IO.File.ReadAllText(Path.Combine(second.ProjectDirectory, "Script.txt")), Is.EqualTo("b"));
            }
        }

        [TestCase("a.TBD", true)]
        [TestCase("a.tsd", true)]
        [TestCase("a.Twd", true)]
        [TestCase("a.tcd", false)]
        [TestCase("a.tbd.bak", false)]
        [TestCase("tbd", false)]
        public void IsTasFile_MatchesTasGenCommExtensions(string name, bool expected)
        {
            Assert.That(NativeGenOptWorkspace.IsTasFile(name), Is.EqualTo(expected));
        }

        [Test]
        public void Snapshot_MissingWorkspaceOrScript_IsRefused()
        {
            using (TestFolder folder = new TestFolder())
            {
                Assert.Throws<DirectoryNotFoundException>(() => NativeGenOptWorkspace.Create(folder.Combine("nope"), "s", folder.Path));
                Assert.Throws<GenOptCompatibilityException>(() => NativeGenOptWorkspace.Create(folder.Path, " ", folder.Path));
            }
        }
    }
}
