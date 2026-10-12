// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical.Tas.GenOpt.Tests.Helpers;
using SAM.Core.Optimisation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SAM.Analytical.Tas.GenOpt.Tests
{
    /// <summary>
    /// Energy Simulation with Sizing (the dialog's default) leaves <c>&lt;name&gt;_Uncapped.tbd</c> and
    /// <c>&lt;name&gt;_HDDCDD.tbd</c> beside the project's <c>&lt;name&gt;.tbd</c> (SAM_Tas <c>Query.Sizing</c>). The
    /// optimisation identifies the project's TBD, leaves the copies out of the inventory, the run snapshot, the run's
    /// hashes and "Apply best design" alike, never touches them, and refuses a folder whose TBD it cannot identify.
    /// No Tas is needed: every refusal comes before a Tas document is opened, and Apply uses stand-in writers.
    /// </summary>
    [TestFixture]
    public class SizingCopyTests
    {
        private const string Tbd = "Systems Training.tbd";
        private const string Tsd = "Systems Training.tsd";
        private const string Tpd = "Systems Training.tpd";
        private const string T3d = "Systems Training.t3d";
        private const string Uncapped = "Systems Training_Uncapped.tbd";
        private const string Hddcdd = "Systems Training_HDDCDD.tbd";

        /// <summary>A default Energy Simulation folder: the project's files and the two sizing copies, plus non-Tas files.</summary>
        private static string SizedProject(TestFolder folder)
        {
            string project = folder.Folder("project");
            foreach (string name in new[] { Tbd, Tsd, Tpd, T3d })
            {
                System.IO.File.WriteAllText(Path.Combine(project, name), Path.GetExtension(name).TrimStart('.'));
            }

            System.IO.File.WriteAllText(Path.Combine(project, Uncapped), "uncapped copy");
            System.IO.File.WriteAllText(Path.Combine(project, Hddcdd), "hddcdd copy");
            System.IO.File.WriteAllText(Path.Combine(project, "Systems Training.sam"), "model");
            return project;
        }

        private static Dictionary<string, (string Text, DateTime Written)> SizingCopiesNow(string project)
        {
            return new[] { Uncapped, Hddcdd }.ToDictionary(x => x, x => (System.IO.File.ReadAllText(Path.Combine(project, x)), System.IO.File.GetLastWriteTimeUtc(Path.Combine(project, x))));
        }

        [Test]
        public void The_project_files_and_the_sizing_copies_are_told_apart()
        {
            using (TestFolder folder = new TestFolder())
            {
                TasProjectFiles files = Query.TasProjectFiles(SizedProject(folder));

                Assert.That(files.Files, Is.EqualTo(new[] { T3d, Tbd, Tpd, Tsd }));
                Assert.That(files.SizingCopies, Is.EqualTo(new[] { Hddcdd, Uncapped }));
                Assert.That(files.UnmatchedSizingNames, Is.Empty);
            }
        }

        [TestCase("Model.TBD", "model_hddcdd.tbd", true)]
        [TestCase("Model.tbd", "Model_UNCAPPED.TBD", true)]
        [TestCase("Model.tbd", "Model_Uncapped.tbd", true)]
        [TestCase("Model.tbd", "Model_HDDCDD_Uncapped.tbd", false)] // the primary would be Model_HDDCDD.tbd, which is absent
        [TestCase("Model.tbd", "Other_HDDCDD.tbd", false)]
        [TestCase("Model.tsd", "Model_HDDCDD.tsd", false)] // Sizing copies only the TBD
        [TestCase("Model.tbd", "_HDDCDD.tbd", false)]
        [TestCase("Model.tbd", "Model HDDCDD.tbd", false)]
        public void Only_a_sizing_named_TBD_beside_its_primary_is_a_sizing_copy(string primary, string other, bool copy)
        {
            using (TestFolder folder = new TestFolder())
            {
                string project = folder.Folder("project");
                System.IO.File.WriteAllText(Path.Combine(project, primary), "primary");
                System.IO.File.WriteAllText(Path.Combine(project, other), "other");

                TasProjectFiles files = Query.TasProjectFiles(project);

                if (copy)
                {
                    Assert.That(files.SizingCopies, Is.EqualTo(new[] { other }));
                    Assert.That(files.Files, Is.EqualTo(new[] { primary }));
                }
                else
                {
                    Assert.That(files.SizingCopies, Is.Empty);
                    Assert.That(files.Files, Does.Contain(other));
                }
            }
        }

        [Test]
        public void A_chain_of_copies_is_recognised_while_each_primary_is_present()
        {
            using (TestFolder folder = new TestFolder())
            {
                string project = folder.Folder("project");
                foreach (string name in new[] { "A.tbd", "A_Uncapped.tbd", "A_Uncapped_HDDCDD.tbd" })
                {
                    System.IO.File.WriteAllText(Path.Combine(project, name), name);
                }

                TasProjectFiles files = Query.TasProjectFiles(project);

                Assert.That(files.Files, Is.EqualTo(new[] { "A.tbd" }));
                Assert.That(files.SizingCopies, Is.EqualTo(new[] { "A_Uncapped.tbd", "A_Uncapped_HDDCDD.tbd" }));
            }
        }

        [Test]
        public void The_run_snapshot_leaves_the_sizing_copies_out_and_untouched()
        {
            using (TestFolder folder = new TestFolder())
            {
                string project = SizedProject(folder);
                var before = SizingCopiesNow(project);

                NativeGenOptWorkspace workspace = NativeGenOptWorkspace.Create(project, "script", folder.Folder("runs"));

                Assert.That(Directory.GetFiles(workspace.ProjectDirectory).Select(Path.GetFileName).OrderBy(x => x, StringComparer.OrdinalIgnoreCase), Is.EqualTo(new[] { "Script.txt", T3d, Tbd, Tpd, Tsd }));
                Assert.That(workspace.ProjectFiles, Is.EqualTo(new[] { "Script.txt", T3d, Tbd, Tpd, Tsd }));
                Assert.That(SizingCopiesNow(project), Is.EqualTo(before));
            }
        }

        [Test]
        public void The_hashes_leave_the_sizing_copies_out_so_they_match_the_snapshots()
        {
            using (TestFolder folder = new TestFolder())
            {
                string project = SizedProject(folder);
                NativeGenOptWorkspace workspace = NativeGenOptWorkspace.Create(project, "script", folder.Folder("runs"));

                Dictionary<string, string> hashes = Query.TasFileHashes(project);

                Assert.That(hashes.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase), Is.EqualTo(new[] { T3d, Tbd, Tpd, Tsd }));
                Assert.That(Query.TasFileHashes(workspace.ProjectDirectory), Is.EqualTo(hashes), "the run records what Apply compares");

                // A sizing copy is not the model; the project's TBD is.
                System.IO.File.WriteAllText(Path.Combine(project, Hddcdd), "hddcdd copy, sized again");
                Assert.That(Query.TasFileHashes(project), Is.EqualTo(hashes));
                System.IO.File.WriteAllText(Path.Combine(project, Tbd), "tbd simulated again");
                Assert.That(Query.TasFileHashes(project)[Tbd], Is.Not.EqualTo(hashes[Tbd]));
            }
        }

        [Test]
        public void The_inventory_refuses_a_sizing_named_TBD_whose_primary_is_missing()
        {
            using (TestFolder folder = new TestFolder())
            {
                string project = SizedProject(folder);
                System.IO.File.Delete(Path.Combine(project, Tbd));

                // Both copies now lack their primary: neither may be taken for the project's TBD.
                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Query.TasModelInventory(project));

                Assert.That(exception.Message, Does.Contain(Hddcdd + ", " + Uncapped));
                Assert.That(exception.Message, Does.Contain("sizing copies of " + Tbd + ", " + Tbd));
                Assert.That(exception.Message, Does.Contain("cannot be identified"));
            }
        }

        [Test]
        public void The_inventory_refuses_two_project_TBDs_and_does_not_count_the_sizing_copies()
        {
            using (TestFolder folder = new TestFolder())
            {
                string project = SizedProject(folder);
                System.IO.File.WriteAllText(Path.Combine(project, "Other.tbd"), "another model");

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Query.TasModelInventory(project));

                Assert.That(exception.Message, Does.Contain("holds 2 .tbd files (Other.tbd, " + Tbd + "; sizing copies " + Hddcdd + ", " + Uncapped + " not counted)"));
            }
        }

        [Test]
        public void A_second_TSD_named_like_a_sizing_copy_is_still_ambiguous()
        {
            using (TestFolder folder = new TestFolder())
            {
                string project = SizedProject(folder);
                System.IO.File.WriteAllText(Path.Combine(project, "Systems Training_HDDCDD.tsd"), "a simulated sizing copy");

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Query.TasModelInventory(project));

                Assert.That(exception.Message, Does.Contain("holds 2 .tsd files"));
            }
        }

        [Test]
        public void Apply_on_a_sized_project_changes_only_the_projects_TBD_and_keeps_the_sizing_copies()
        {
            using (TestFolder folder = new TestFolder())
            {
                string project = SizedProject(folder);
                NativeGenOptWorkspace workspace = NativeGenOptWorkspace.Create(project, "script", folder.Folder("runs"));
                Dictionary<string, string> runHashes = Query.TasFileHashes(workspace.ProjectDirectory);
                var before = SizingCopiesNow(project);

                TasModelDesignApplier applier = Applier(project, runHashes);
                applier.InventoryReader = staging =>
                {
                    Assert.That(Directory.GetFiles(staging).Select(Path.GetFileName), Is.EqualTo(new[] { Tbd }), "only the project's TBD is staged");
                    return Inventory(heating: 21.25f, cooling: 26.5f);
                };

                TasModelApplyResult result = applier.Apply(Query.TasModelDesignChanges(TasModelFixtures.Read(TasScriptTests.Setpoints), new[] { 21.25, 26.5 }));

                Assert.That(result.FilesReplaced, Is.EqualTo(new[] { Tbd }));
                Assert.That(System.IO.File.ReadAllText(Path.Combine(project, Tbd)), Is.EqualTo("tbd written"));
                Assert.That(Directory.GetFiles(result.BackupFolder).Select(Path.GetFileName), Is.EqualTo(new[] { Tbd }), "the sizing copies are not backed up: they are not changed");
                Assert.That(SizingCopiesNow(project), Is.EqualTo(before));
            }
        }

        [Test]
        public void Apply_refuses_when_the_projects_TBD_can_no_longer_be_identified()
        {
            using (TestFolder folder = new TestFolder())
            {
                string project = SizedProject(folder);
                Dictionary<string, string> runHashes = Query.TasFileHashes(project);
                System.IO.File.WriteAllText(Path.Combine(project, "Other.tbd"), "another model");

                TasModelApplyException exception = Assert.Throws<TasModelApplyException>(() => Applier(project, runHashes).Apply(Query.TasModelDesignChanges(TasModelFixtures.Read(TasScriptTests.Setpoints), new[] { 21.25, 26.5 })));

                Assert.That(exception.Message, Does.Contain("Other.tbd is new"));
                Assert.That(exception.ProjectChanged, Is.False);
                Assert.That(Directory.Exists(Path.Combine(project, TasModelDesignApplier.WorkFolderName)), Is.False);
            }
        }

        private static TasModelDesignApplier Applier(string project, IReadOnlyDictionary<string, string> hashes)
        {
            return new TasModelDesignApplier(project, Inventory(), hashes)
            {
                TbdWriter = (path, list) =>
                {
                    System.IO.File.AppendAllText(path, " written");
                    return list.Select(x => new TasModelAppliedValue(x, "20", ((float)x.Value).ToString("R"), true)).ToList();
                },
                TpdWriter = (path, list) => throw new AssertionException("no plant change"),
                InventoryReader = staging => Inventory(heating: 21.25f, cooling: 26.5f),
            };
        }

        /// <summary>The Demo inventory's internal conditions with the given setpoints (enough for the setpoint changes).</summary>
        private static TasModelInventory Inventory(float heating = 20, float cooling = 24)
        {
            return new TasModelInventory(
                Tbd,
                Tsd,
                Tpd,
                new[]
                {
                    new TasInternalConditionInfo("Office Weekday", "Office", 17, new TasSetpointProfile(TasSetpointProfileType.Value, 1, heating), new TasSetpointProfile(TasSetpointProfileType.Value, 1, cooling)),
                    new TasInternalConditionInfo("Office Weekend", "Office", 17, new TasSetpointProfile(TasSetpointProfileType.Value, 1, 12), new TasSetpointProfile(TasSetpointProfileType.Value, 1, 150)),
                },
                new TasGlazingConstructionInfo[0],
                new TasPlantRoomInfo[0]).Sized();
        }
    }
}
