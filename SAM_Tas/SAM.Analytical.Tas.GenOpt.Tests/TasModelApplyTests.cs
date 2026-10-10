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
    /// PR9 "Apply best design" for the Tas files: the best point → changes (pure), and the applier's protection of the
    /// project (hash check, staging, read-back, backup, replace and rollback) with stand-in writers and readers, so no Tas
    /// is needed. The licensed writers themselves are proven by the PR9 licensed acceptance (local evidence).
    /// </summary>
    [TestFixture]
    public class TasModelApplyTests
    {
        private const string Tbd = "Systems Training.tbd";
        private const string Tsd = "Systems Training.tsd";
        private const string Tpd = "Systems Training.tpd";

        [Test]
        public void A_best_point_gives_one_change_per_variable_with_the_exact_values()
        {
            OptimisationDefinition definition = TasModelFixtures.Read(TasScriptTests.Everything);
            double heating = 20.123456789012345;
            double controller = 4.968943799848584;

            List<TasModelDesignChange> changes = Query.TasModelDesignChanges(definition, new[] { heating, controller });

            Assert.That(changes.Select(x => x.VariableName), Is.EqualTo(new[] { "Heating", "Controller" }));
            Assert.That(changes[0].IsSetpoint && changes[0].IsHeating && changes[0].IsBuilding, Is.True);
            Assert.That(changes[0].InternalCondition, Is.EqualTo("Office Weekday"));
            Assert.That(changes[1].IsController && !changes[1].IsBuilding, Is.True);
            Assert.That(new[] { changes[1].PlantRoom, changes[1].Controller }, Is.EqualTo(new[] { "Plant Room", "HeatPumpController" }));
            Assert.That(BitConverter.DoubleToInt64Bits(changes[0].Value), Is.EqualTo(BitConverter.DoubleToInt64Bits(heating)), "no rounding");
            Assert.That(BitConverter.DoubleToInt64Bits(changes[1].Value), Is.EqualTo(BitConverter.DoubleToInt64Bits(controller)), "no rounding");

            // The change keeps its own copy of the binding.
            definition.Variables[0].Target.Reference["internalCondition"] = "Renamed";
            Assert.That(changes[0].InternalCondition, Is.EqualTo("Office Weekday"));
        }

        [Test]
        public void A_glazing_choice_resolves_to_the_runs_option_of_that_number()
        {
            OptimisationDefinition definition = TasModelFixtures.Read(TasScriptTests.Glazing);
            Dictionary<string, IReadOnlyList<TasGlazingOption>> options = AttachedGlazingOptions();

            TasModelDesignChange change = Query.TasModelDesignChanges(definition, new double[] { 3 }, options).Single();
            Assert.That(change.IsGlazing, Is.True);
            Assert.That(change.GlazingConstruction, Is.EqualTo(TasModelFixtures.SamGlazing));
            Assert.That(change.OptionNumber, Is.EqualTo(3));
            Assert.That(change.GlazingOption.Text, Is.EqualTo("Triple low-e"));
            Assert.That(change.GlazingOption.PaneConstruction, Is.EqualTo("Windows: Triple low-e a5191c -pane"));

            TasModelDesignChange current = Query.TasModelDesignChanges(definition, new double[] { 1 }, options).Single();
            Assert.That(current.GlazingOption.IsCurrent, Is.True);
        }

        [TestCase("count", "has 2 values but the definition has 1")]
        [TestCase("nan", "is not a number")]
        [TestCase("above", "is outside its range 1 to 3")]
        [TestCase("fraction", "is not an option number")]
        [TestCase("option", "is option 4, but there are options 1 to 3")]
        [TestCase("no-options", "options of “Glazing” are not known")]
        [TestCase("other-options", "are not the ones its definition lists")]
        [TestCase("detached", "has no glazing system to write")]
        [TestCase("no-target", "has no tas-model target")]
        [TestCase("no-reference", "does not say which glazingConstruction")]
        public void A_point_that_does_not_fit_the_definition_is_refused(string mode, string message)
        {
            OptimisationDefinition definition = TasModelFixtures.Read(TasScriptTests.Glazing);
            Dictionary<string, IReadOnlyList<TasGlazingOption>> options = AttachedGlazingOptions();
            double[] point = { 2 };
            switch (mode)
            {
                case "count":
                    point = new double[] { 2, 2 };
                    break;
                case "nan":
                    point = new[] { double.NaN };
                    break;
                case "above":
                    point = new double[] { 3.5 };
                    break;
                case "fraction":
                    point = new double[] { 2.5 };
                    break;
                case "option":
                    definition.Variables[0].Maximum = 4;
                    point = new double[] { 4 };
                    break;
                case "no-options":
                    options = null;
                    break;
                case "other-options":
                    options["Glazing"] = options["Glazing"].Reverse().ToList();
                    break;
                case "detached":
                    options["Glazing"] = new List<TasGlazingOption> { options["Glazing"][0], TasModelFixtures.Option(2, "Double B", "Windows: Double B ff1c5e -pane", 0.28, 1.7, 0.8).Detached(), options["Glazing"][2] };
                    break;
                case "no-target":
                    definition.Variables[0].Target = null;
                    break;
                case "no-reference":
                    definition.Variables[0].Target.Reference.Clear();
                    break;
            }

            ArgumentException exception = Assert.Throws<ArgumentException>(() => Query.TasModelDesignChanges(definition, point, options));
            Assert.That(exception.Message, Does.Contain(message));
        }

        [Test]
        public void Apply_writes_a_staging_copy_reads_it_back_and_replaces_the_project_file_with_a_backup()
        {
            using (TestFolder folder = new TestFolder())
            {
                string project = Project(folder);
                Dictionary<string, string> hashes = Query.TasFileHashes(project);
                List<TasModelDesignChange> changes = Changes(TasScriptTests.Setpoints, 21.25, 26.5);

                TasModelDesignApplier applier = new TasModelDesignApplier(project, Inventory(), hashes);
                List<string> calls = new List<string>();
                applier.TbdWriter = (path, list) =>
                {
                    calls.Add("TBD " + Path.GetFileName(Path.GetDirectoryName(path)) + " " + list.Count);
                    System.IO.File.AppendAllText(path, " written");
                    return list.Select(x => new TasModelAppliedValue(x, "20", ((float)x.Value).ToString("R"), true)).ToList();
                };
                applier.TpdWriter = (path, list) => throw new AssertionException("no plant change");
                applier.InventoryReader = staging =>
                {
                    Assert.That(Directory.GetFiles(staging).Select(Path.GetFileName), Is.EqualTo(new[] { Tbd }), "only the file to change is staged");
                    return Inventory(heating: 21.25f, cooling: 26.5f);
                };

                TasModelApplyResult result = applier.Apply(changes);

                Assert.That(calls, Is.EqualTo(new[] { "TBD staging 2" }));
                Assert.That(result.FilesReplaced, Is.EqualTo(new[] { Tbd }));
                Assert.That(System.IO.File.ReadAllText(Path.Combine(project, Tbd)), Is.EqualTo("tbd written"));
                Assert.That(System.IO.File.ReadAllText(Path.Combine(result.BackupFolder, Tbd)), Is.EqualTo("tbd"));
                Assert.That(result.WorkFolder, Does.StartWith(Path.Combine(Path.GetFullPath(project), TasModelDesignApplier.WorkFolderName)));
                Assert.That(Directory.Exists(Path.Combine(result.WorkFolder, "staging")), Is.False, "the staging copy is the project file now");
                Assert.That(Query.TasFileHashes(project)[Tpd], Is.EqualTo(hashes[Tpd]), "the TPD is untouched");
                Assert.That(Query.TasFileHashes(project)[Tsd], Is.EqualTo(hashes[Tsd]), "the TSD is untouched");
                Assert.That(result.Values.Select(x => x.Change.VariableName), Is.EqualTo(new[] { "Heating, office", "Cooling" }));
                Assert.That(result.Values.All(x => x.Changed && x.File == "TBD"), Is.True);
            }
        }

        [Test]
        public void Apply_refuses_files_that_changed_after_the_run_and_writes_nothing()
        {
            using (TestFolder folder = new TestFolder())
            {
                string project = Project(folder);
                Dictionary<string, string> hashes = Query.TasFileHashes(project);
                System.IO.File.WriteAllText(Path.Combine(project, Tbd), "tbd simulated again");

                TasModelDesignApplier applier = Applier(project, hashes);
                TasModelApplyException exception = Assert.Throws<TasModelApplyException>(() => applier.Apply(Changes(TasScriptTests.Setpoints, 21, 26)));

                Assert.That(exception.Message, Does.Contain("changed after the optimisation ran (" + Tbd + " changed)"));
                Assert.That(exception.ProjectChanged, Is.False);
                Assert.That(Directory.Exists(Path.Combine(project, TasModelDesignApplier.WorkFolderName)), Is.False);

                // A new or missing Tas file is a different model too; and a run that recorded nothing cannot be checked.
                System.IO.File.WriteAllText(Path.Combine(project, Tbd), "tbd");
                System.IO.File.WriteAllText(Path.Combine(project, "other.t3d"), "t3d");
                Assert.That(Assert.Throws<TasModelApplyException>(() => applier.Apply(Changes(TasScriptTests.Setpoints, 21, 26))).Message, Does.Contain("other.t3d is new"));
                Assert.That(Assert.Throws<TasModelApplyException>(() => Applier(project, null).Apply(Changes(TasScriptTests.Setpoints, 21, 26))).Message, Does.Contain("no record of the Tas files"));
            }
        }

        [TestCase("writer")]
        [TestCase("reader")]
        public void A_source_saved_by_another_program_while_the_staging_copy_is_written_is_refused_and_kept(string mode)
        {
            using (TestFolder folder = new TestFolder())
            {
                string project = Project(folder);
                Dictionary<string, string> hashes = Query.TasFileHashes(project);
                TasModelDesignApplier applier = Applier(project, hashes);
                Func<string, IReadOnlyList<TasModelDesignChange>, List<TasModelAppliedValue>> writer = applier.TbdWriter;
                Func<string, TasModelInventory> reader = applier.InventoryReader;

                // Tas (or anyone) saves the project's TBD, or simulates its TSD, during the licensed write or read-back.
                if (mode == "writer")
                {
                    applier.TbdWriter = (path, list) =>
                    {
                        System.IO.File.WriteAllText(Path.Combine(project, Tbd), "tbd saved meanwhile");
                        return writer(path, list);
                    };
                }
                else
                {
                    applier.InventoryReader = staging =>
                    {
                        System.IO.File.WriteAllText(Path.Combine(project, Tsd), "tsd simulated meanwhile");
                        return reader(staging);
                    };
                }

                TasModelApplyException exception = Assert.Throws<TasModelApplyException>(() => applier.Apply(Changes(TasScriptTests.Setpoints, 21.25, 26.5)));

                Assert.That(exception.Message, Does.Contain("changed while the best design was being written (" + (mode == "writer" ? Tbd : Tsd) + " changed)").And.Contain("Nothing in the project was changed"));
                Assert.That(exception.ProjectChanged, Is.False);
                Assert.That(System.IO.File.ReadAllText(Path.Combine(project, Tbd)), Is.EqualTo(mode == "writer" ? "tbd saved meanwhile" : "tbd"), "the other program's save is kept, neither overwritten nor restored");
                Assert.That(System.IO.File.ReadAllText(Path.Combine(project, Tsd)), Is.EqualTo(mode == "writer" ? "tsd" : "tsd simulated meanwhile"));
                Assert.That(System.IO.File.ReadAllText(Path.Combine(exception.WorkFolder, "original", Tbd)), Is.EqualTo("tbd"), "the backup is the run's file");
            }
        }

        [Test]
        public void A_project_file_open_in_another_program_at_replacement_is_refused_and_kept()
        {
            using (TestFolder folder = new TestFolder())
            {
                string project = Project(folder);
                Dictionary<string, string> hashes = Query.TasFileHashes(project);
                TasModelDesignApplier applier = Applier(project, hashes);
                Func<string, IReadOnlyList<TasModelDesignChange>, List<TasModelAppliedValue>> writer = applier.TbdWriter;
                FileStream other = null;
                try
                {
                    // Another program opens the project's TBD for writing (as Tas does with an open document) and keeps it open.
                    applier.TbdWriter = (path, list) =>
                    {
                        other = new FileStream(Path.Combine(project, Tbd), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                        return writer(path, list);
                    };

                    TasModelApplyException exception = Assert.Throws<TasModelApplyException>(() => applier.Apply(Changes(TasScriptTests.Setpoints, 21.25, 26.5)));

                    Assert.That(exception.Message, Does.Contain(Tbd + " is open in another program").And.Contain("Nothing in the project was changed"));
                    Assert.That(exception.ProjectChanged, Is.False);
                }
                finally
                {
                    other?.Dispose();
                }

                Assert.That(Query.TasFileHashes(project), Is.EqualTo(hashes));
            }
        }

        [Test]
        public void No_other_program_can_open_a_project_file_while_it_is_replaced()
        {
            using (TestFolder folder = new TestFolder())
            {
                string project = Project(folder);
                TasModelDesignApplier applier = Applier(project, Query.TasFileHashes(project));
                string attempt = null;
                string note = null;
                applier.FileReplacer = (source, destination) =>
                {
                    note = System.IO.File.ReadAllText(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(source)), TasModelDesignApplier.ReplacingNoteName));

                    // The final check and the replacement hold the file: a writer between them is refused.
                    try
                    {
                        using (new FileStream(destination, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
                        {
                            attempt = "opened";
                        }
                    }
                    catch (IOException)
                    {
                        attempt = "refused";
                    }

                    System.IO.File.Replace(source, destination, null);
                };

                TasModelApplyResult result = applier.Apply(Changes(TasScriptTests.Setpoints, 21.25, 26.5));

                Assert.That(attempt, Is.EqualTo("refused"));
                Assert.That(note, Does.Contain("did not finish").And.Contain(result.BackupFolder).And.Contain(Tbd + "  original " + Query.FileHash(Path.Combine(result.BackupFolder, Tbd))), "while replacing, the work folder says how to recover");
                Assert.That(result.FilesReplaced, Is.EqualTo(new[] { Tbd }));
                Assert.That(System.IO.File.ReadAllText(Path.Combine(project, Tbd)), Is.EqualTo("tbd written"));
                Assert.That(Directory.GetFiles(result.WorkFolder), Is.Empty, "no pending-replacement note after success");
            }
        }

        [Test]
        public void A_writer_failure_leaves_the_project_untouched()
        {
            using (TestFolder folder = new TestFolder())
            {
                string project = Project(folder);
                Dictionary<string, string> hashes = Query.TasFileHashes(project);
                TasModelDesignApplier applier = Applier(project, hashes);
                applier.TbdWriter = (path, list) => throw new InvalidOperationException("Internal condition “Office Weekday” was found 0 times in the TBD.");

                TasModelApplyException exception = Assert.Throws<TasModelApplyException>(() => applier.Apply(Changes(TasScriptTests.Setpoints, 21, 26)));

                Assert.That(exception.Message, Does.Contain("was found 0 times").And.Contain("Nothing in the project was changed"));
                Assert.That(exception.ProjectChanged, Is.False);
                Assert.That(Query.TasFileHashes(project), Is.EqualTo(hashes));
            }
        }

        [TestCase("value", "reads 21, not the best value 21.25")]
        [TestCase("other", "Cooling setpoint of “Office Weekend” changed although it is not part of the design")]
        [TestCase("ic", "internal conditions are not the ones it had")]
        public void Apply_stops_when_the_written_file_does_not_read_back_as_the_best_design(string mode, string message)
        {
            using (TestFolder folder = new TestFolder())
            {
                string project = Project(folder);
                Dictionary<string, string> hashes = Query.TasFileHashes(project);
                TasModelDesignApplier applier = Applier(project, hashes);
                applier.InventoryReader = staging => mode == "value"
                    ? Inventory(heating: 21, cooling: 26.5f)
                    : mode == "other" ? Inventory(heating: 21.25f, cooling: 26.5f, weekendCooling: 30) : new TasModelInventory(Tbd, null, null);

                TasModelApplyException exception = Assert.Throws<TasModelApplyException>(() => applier.Apply(Changes(TasScriptTests.Setpoints, 21.25, 26.5)));

                Assert.That(exception.Message, Does.Contain(message).And.Contain("Nothing in the project was changed"));
                Assert.That(Query.TasFileHashes(project), Is.EqualTo(hashes));
            }
        }

        [Test]
        public void A_failed_replacement_restores_the_files_already_replaced()
        {
            using (TestFolder folder = new TestFolder())
            {
                string project = Project(folder);
                Dictionary<string, string> hashes = Query.TasFileHashes(project);
                TasModelDesignApplier applier = Applier(project, hashes);
                applier.TpdWriter = (path, list) =>
                {
                    System.IO.File.AppendAllText(path, " written");
                    return list.Select(x => new TasModelAppliedValue(x, "3", x.Value.ToString("R"), true)).ToList();
                };
                applier.InventoryReader = staging => Inventory(heating: 21.25f, controller: 4.5);
                List<string> replaced = new List<string>();
                applier.FileReplacer = (source, destination) =>
                {
                    replaced.Add(Path.GetFileName(destination));
                    System.IO.File.Replace(source, destination, null);
                    if (destination.EndsWith(".tpd", StringComparison.OrdinalIgnoreCase))
                    {
                        // The TPD is left neither the original nor the written copy.
                        System.IO.File.WriteAllText(destination, "half");
                        throw new IOException("The process cannot access the file because it is being used by another process.");
                    }
                };

                TasModelApplyException exception = Assert.Throws<TasModelApplyException>(() => applier.Apply(Changes(TasScriptTests.Everything, 21.25, 4.5)));

                Assert.That(replaced, Is.EqualTo(new[] { Tbd, Tpd }), "the TBD first, then the TPD");
                Assert.That(exception.Message, Does.Contain("Replacing " + Tpd + " failed").And.Contain("every project file was restored from the backup"));
                Assert.That(exception.ProjectChanged, Is.False);
                Assert.That(Query.TasFileHashes(project), Is.EqualTo(hashes), "TBD and TPD restored");
                Assert.That(System.IO.File.Exists(Path.Combine(exception.WorkFolder, TasModelDesignApplier.ReplacingNoteName)), Is.False, "restored: no recovery note");
            }
        }

        [Test]
        public void A_tbd_and_a_tpd_change_are_both_written_and_the_plant_value_is_a_double()
        {
            using (TestFolder folder = new TestFolder())
            {
                string project = Project(folder);
                Dictionary<string, string> hashes = Query.TasFileHashes(project);
                double controller = 4.968943799848584;
                TasModelDesignApplier applier = Applier(project, hashes);
                List<double> written = new List<double>();
                applier.TpdWriter = (path, list) =>
                {
                    written.AddRange(list.Select(x => x.Value));
                    System.IO.File.AppendAllText(path, " written");
                    return list.Select(x => new TasModelAppliedValue(x, "3", x.Value.ToString("R"), true)).ToList();
                };
                applier.InventoryReader = staging => Inventory(heating: 21.25f, controller: controller);

                TasModelApplyResult result = applier.Apply(Changes(TasScriptTests.Everything, 21.25, controller));

                Assert.That(result.FilesReplaced, Is.EqualTo(new[] { Tbd, Tpd }));
                Assert.That(written, Is.EqualTo(new[] { controller }));
                Assert.That(result.Values[1].File, Is.EqualTo("TPD"));
                Assert.That(System.IO.File.ReadAllText(Path.Combine(project, Tpd)), Is.EqualTo("tpd written"));

                // A controller that reads back rounded (a float) is refused.
                string project2 = folder.Folder("again");
                foreach (string file in Directory.GetFiles(result.BackupFolder))
                {
                    System.IO.File.Copy(file, Path.Combine(project2, Path.GetFileName(file)));
                }

                System.IO.File.WriteAllText(Path.Combine(project2, Tsd), "tsd");
                TasModelDesignApplier applier2 = Applier(project2, Query.TasFileHashes(project2));
                applier2.TpdWriter = applier.TpdWriter;
                applier2.InventoryReader = staging => Inventory(heating: 21.25f, controller: (float)controller);
                Assert.That(Assert.Throws<TasModelApplyException>(() => applier2.Apply(Changes(TasScriptTests.Everything, 21.25, controller))).Message, Does.Contain("Controller “HeatPumpController” reads ").And.Contain(", not 4.968943799848584."));
            }
        }

        [Test]
        public void Items_that_already_hold_the_best_value_write_nothing()
        {
            using (TestFolder folder = new TestFolder())
            {
                string project = Project(folder);
                Dictionary<string, string> hashes = Query.TasFileHashes(project);
                TasModelDesignApplier applier = new TasModelDesignApplier(project, Inventory(), hashes)
                {
                    TbdWriter = (path, list) => throw new AssertionException("nothing to write"),
                    TpdWriter = (path, list) => throw new AssertionException("nothing to write"),
                };

                TasModelApplyResult result = applier.Apply(Changes(TasScriptTests.Everything, 20, 3));

                Assert.That(result.FilesReplaced, Is.Empty);
                Assert.That(result.WorkFolder, Is.Null);
                Assert.That(result.Values.Select(x => x.Changed), Is.EqualTo(new[] { false, false }));
                Assert.That(result.Values.Select(x => x.Before), Is.EqualTo(new[] { "20", "3" }));
                Assert.That(Directory.Exists(Path.Combine(project, TasModelDesignApplier.WorkFolderName)), Is.False);
            }
        }

        [Test]
        public void A_24_hour_setpoint_must_read_back_hour_by_hour_with_the_setback_kept()
        {
            using (TestFolder folder = new TestFolder())
            {
                string project = folder.Folder("project");
                System.IO.File.WriteAllText(Path.Combine(project, "000000_SAM_AnalyticalModel.tbd"), "tbd");
                float[] hours = Enumerable.Range(0, 24).Select(h => h >= 8 && h < 20 ? 23f : 150f).ToArray();
                TasModelInventory before = SamInventory(hours);
                OptimisationDefinition definition = TasModelFixtures.Read(TasScriptTests.Setpoints.Replace("Office Weekday", "Studio 1_0").Replace("\"start\": 24", "\"start\": 23").Replace("\"maximum\": 28", "\"maximum\": 26"));
                definition.Variables.RemoveAt(0);
                List<TasModelDesignChange> changes = Query.TasModelDesignChanges(definition, new[] { 25.976 });

                float[] expected = hours.Select(x => x == 23f ? (float)25.976 : x).ToArray();
                TasModelDesignApplier applier = new TasModelDesignApplier(project, before, Query.TasFileHashes(project))
                {
                    TbdWriter = (path, list) => list.Select(x => new TasModelAppliedValue(x, "23", "25.976", true, "12 of 24 hours")).ToList(),
                    InventoryReader = staging => SamInventory(expected),
                };
                Assert.That(applier.Apply(changes).Values.Single().Detail, Is.EqualTo("12 of 24 hours"));

                // The setback hours must stay: a profile written as a value everywhere is refused.
                applier.InventoryReader = staging => SamInventory(Enumerable.Repeat((float)25.976, 24).ToArray());
                Assert.That(Assert.Throws<TasModelApplyException>(() => applier.Apply(changes)).Message, Does.Contain("Cooling setpoint of “Studio 1_0” reads 25.976"));
            }
        }

        [Test]
        public void A_glazing_choice_must_move_exactly_the_elements_of_its_glazing_construction()
        {
            using (TestFolder folder = new TestFolder())
            {
                string project = folder.Folder("project");
                System.IO.File.WriteAllText(Path.Combine(project, "000000_SAM_AnalyticalModel.tbd"), "tbd");
                TasModelInventory before = new TasModelInventory("000000_SAM_AnalyticalModel.tbd", null, null, null, new[]
                {
                    new TasGlazingConstructionInfo(TasModelFixtures.SamGlazing, new[] { "GLAZING 1", "GLAZING 2" }, 0.4001609981060028, 1.2433900833129883, 0.803563117980957),
                    new TasGlazingConstructionInfo("Windows: Rooflight -pane", new[] { "ROOF 1" }, 0.5, 1.5, 0.6),
                });
                List<TasModelDesignChange> changes = Query.TasModelDesignChanges(TasModelFixtures.Read(TasScriptTests.Glazing), new double[] { 3 }, AttachedGlazingOptions());
                const string pane = "Windows: Triple low-e a5191c -pane";

                TasModelDesignApplier applier = new TasModelDesignApplier(project, before, Query.TasFileHashes(project))
                {
                    TbdWriter = (path, list) => list.Select(x => new TasModelAppliedValue(x, TasModelFixtures.SamGlazing, pane, true)).ToList(),
                    InventoryReader = staging => new TasModelInventory("000000_SAM_AnalyticalModel.tbd", null, null, null, new[]
                    {
                        new TasGlazingConstructionInfo(pane, new[] { "GLAZING 1", "GLAZING 2" }, 0.36886733770370483, 0.997646152973175, 0.7281997203826904),
                        new TasGlazingConstructionInfo("Windows: Rooflight -pane", new[] { "ROOF 1" }, 0.5, 1.5, 0.6),
                    }),
                };
                Assert.That(applier.Apply(changes).Values.Single().After, Is.EqualTo(pane));

                // The pool may hold a system's values rounded (licensed acceptance: listed g 0.4 / light 0.804, read back
                // 0.40016 / 0.80356): within 0.001, the option filter's precision, it is the option.
                applier.InventoryReader = staging => new TasModelInventory("000000_SAM_AnalyticalModel.tbd", null, null, null, new[]
                {
                    new TasGlazingConstructionInfo(pane, new[] { "GLAZING 1", "GLAZING 2" }, 0.36886733770370483 + 0.0009, 0.997646152973175 - 0.0009, 0.7281997203826904 + 0.0004),
                    new TasGlazingConstructionInfo("Windows: Rooflight -pane", new[] { "ROOF 1" }, 0.5, 1.5, 0.6),
                });
                Assert.That(applier.Apply(changes).Values.Single().After, Is.EqualTo(pane));
                applier.InventoryReader = staging => new TasModelInventory("000000_SAM_AnalyticalModel.tbd", null, null, null, new[]
                {
                    new TasGlazingConstructionInfo(pane, new[] { "GLAZING 1", "GLAZING 2" }, 0.36886733770370483 + 0.0011, 0.997646152973175, 0.7281997203826904),
                    new TasGlazingConstructionInfo("Windows: Rooflight -pane", new[] { "ROOF 1" }, 0.5, 1.5, 0.6),
                });
                Assert.That(Assert.Throws<TasModelApplyException>(() => applier.Apply(changes)).Message, Does.Contain("not the option's g 0.36886733770370483"));

                // The rooflight moved too: refused.
                applier.InventoryReader = staging => new TasModelInventory("000000_SAM_AnalyticalModel.tbd", null, null, null, new[]
                {
                    new TasGlazingConstructionInfo(pane, new[] { "GLAZING 1", "GLAZING 2", "ROOF 1" }, 0.36886733770370483, 0.997646152973175, 0.7281997203826904),
                });
                Assert.That(Assert.Throws<TasModelApplyException>(() => applier.Apply(changes)).Message, Does.Contain("Building element “ROOF 1” uses “" + pane + "”, not “Windows: Rooflight -pane”"));

                // Another system was written under the option's name: refused on its values.
                applier.InventoryReader = staging => new TasModelInventory("000000_SAM_AnalyticalModel.tbd", null, null, null, new[]
                {
                    new TasGlazingConstructionInfo(pane, new[] { "GLAZING 1", "GLAZING 2" }, 0.5, 0.997646152973175, 0.7281997203826904),
                    new TasGlazingConstructionInfo("Windows: Rooflight -pane", new[] { "ROOF 1" }, 0.5, 1.5, 0.6),
                });
                Assert.That(Assert.Throws<TasModelApplyException>(() => applier.Apply(changes)).Message, Does.Contain("not the option's g 0.36886733770370483"));
            }
        }

        [Test]
        public void The_written_pane_must_read_back_as_the_systems_layers()
        {
            using (TestFolder folder = new TestFolder())
            {
                string project = folder.Folder("project");
                System.IO.File.WriteAllText(Path.Combine(project, "000000_SAM_AnalyticalModel.tbd"), "tbd");
                TasModelInventory before = new TasModelInventory("000000_SAM_AnalyticalModel.tbd", null, null, null, new[]
                {
                    new TasGlazingConstructionInfo(TasModelFixtures.SamGlazing, new[] { "GLAZING 1" }, 0.4001609981060028, 1.2433900833129883, 0.803563117980957),
                });
                Dictionary<string, IReadOnlyList<TasGlazingOption>> options = AttachedGlazingOptions();
                TasGlazingSystem triple = options["Glazing"][2].System;
                triple.ApertureConstruction = new ApertureConstruction(triple.Guid, triple.Name, ApertureType.Window, new[] { new ConstructionLayer("Low-e 6mm", 0.006) }, null);
                triple.MaterialLibrary.Add(Analytical.Create.TransparentMaterial("Low-e 6mm", string.Empty, "Low-e 6mm", string.Empty, 1, 0.006, 9999, 0.4, 0.7, 0.3, 0.3, 0.2, 0.2, 0.84, 0.04, false));
                List<TasModelDesignChange> changes = Query.TasModelDesignChanges(TasModelFixtures.Read(TasScriptTests.Glazing), new double[] { 3 }, options);
                const string pane = "Windows: Triple low-e a5191c -pane";
                List<TasMaterialLayer> layers = Query.TasMaterialLayers(triple.ApertureConstruction.PaneConstructionLayers, triple.MaterialLibrary);

                TasModelDesignApplier applier = new TasModelDesignApplier(project, before, Query.TasFileHashes(project))
                {
                    TbdWriter = (path, list) => list.Select(x => new TasModelAppliedValue(x, TasModelFixtures.SamGlazing, pane, true)).ToList(),
                    InventoryReader = staging => new TasModelInventory("000000_SAM_AnalyticalModel.tbd", null, null, null, new[]
                    {
                        new TasGlazingConstructionInfo(pane, new[] { "GLAZING 1" }, triple.G, triple.Ug, triple.Light, layers, null, null),
                    }),
                };
                Assert.That(applier.Apply(changes).Values.Single().After, Is.EqualTo(pane));

                // The TBD already held a material of that name with other physics, and the writer kept it: refused.
                List<TasMaterialLayer> other = new List<TasMaterialLayer> { new TasMaterialLayer("Low-e 6mm", 0.006f, TasMaterialKind.Transparent, layers[0].Properties.Select(x => x.Key == TasMaterialLayer.InternalEmissivity ? new KeyValuePair<string, float>(x.Key, 0.84f) : x)) };
                applier.InventoryReader = staging => new TasModelInventory("000000_SAM_AnalyticalModel.tbd", null, null, null, new[]
                {
                    new TasGlazingConstructionInfo(pane, new[] { "GLAZING 1" }, triple.G, triple.Ug, triple.Light, other, null, null),
                });
                Assert.That(Assert.Throws<TasModelApplyException>(() => applier.Apply(changes)).Message, Does.Contain("“" + pane + "” is not the system's pane: layer 1 “Low-e 6mm” internal emissivity 0.84, not 0.04"));
            }
        }

        [Test]
        public void A_run_records_the_hashes_of_the_files_it_evaluated()
        {
            using (TestFolder folder = new TestFolder())
            {
                string project = Project(folder);
                Environment.SetEnvironmentVariable("SAM_TAS_GENOPT_STUB_SPEC", "{\"kind\":\"quadratic\",\"center\":[4.5],\"offset\":7000,\"outputs\":[\"Y1\",\"Y2\",\"Y3\"]}");
                try
                {
                    TasModelRunSettings settings = new TasModelRunSettings(project, folder.Combine("runs"), TestFolder.StubExecutable) { Inventory = TasModelFixtures.Demo() };
                    TasModelRunner runner = new TasModelRunner(TasModelFixtures.Read(TasScriptTests.Controller), settings);
                    Assert.That(runner.SourceHashes, Is.Null);

                    runner.Test();

                    Assert.That(runner.SourceHashes, Is.EqualTo(Query.TasFileHashes(project)));
                    Assert.That(runner.SourceHashes.Keys, Is.EquivalentTo(new[] { Tbd, Tsd, Tpd }), "Script.txt is not a Tas file");
                }
                finally
                {
                    Environment.SetEnvironmentVariable("SAM_TAS_GENOPT_STUB_SPEC", null);
                }
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

        private static List<TasModelDesignChange> Changes(string json, params double[] point)
        {
            return Query.TasModelDesignChanges(TasModelFixtures.Read(json), point);
        }

        /// <summary>A project folder with small stand-in Tas files (only the stand-in writers open them).</summary>
        private static string Project(TestFolder folder)
        {
            string project = folder.Folder("project");
            System.IO.File.WriteAllText(Path.Combine(project, Tbd), "tbd");
            System.IO.File.WriteAllText(Path.Combine(project, Tsd), "tsd");
            System.IO.File.WriteAllText(Path.Combine(project, Tpd), "tpd");
            return project;
        }

        /// <summary>The Demo inventory (as <see cref="TasModelFixtures.Demo"/>), with the given setpoints.</summary>
        private static TasModelInventory Inventory(float heating = 20, float cooling = 24, float weekendCooling = 150, double controller = 3)
        {
            return new TasModelInventory(
                Tbd,
                Tsd,
                Tpd,
                new[]
                {
                    new TasInternalConditionInfo("Office Weekday", "Office", 17, new TasSetpointProfile(TasSetpointProfileType.Value, 1, heating), new TasSetpointProfile(TasSetpointProfileType.Value, 1, cooling)),
                    new TasInternalConditionInfo("Office Weekend", "Office", 17, new TasSetpointProfile(TasSetpointProfileType.Value, 1, 12), new TasSetpointProfile(TasSetpointProfileType.Value, 1, weekendCooling)),
                    new TasInternalConditionInfo("Steady State Heating ", null, 26, new TasSetpointProfile(TasSetpointProfileType.Value, 1, 21), null),
                },
                new[]
                {
                    new TasGlazingConstructionInfo(TasModelFixtures.DemoGlazing, new[] { "Lower Window-pane", "Upper Window-pane", "Curtain Wall-pane" }, 0.33669999241828918, 1.0549999475479126, 0.64200001955032349),
                },
                new[]
                {
                    new TasPlantRoomInfo("Plant Room", new[]
                    {
                        new TasPlantControllerInfo("HeatPumpController", "tpdTempSensor", controller),
                        new TasPlantControllerInfo("Controller 2", "tpdLoadSensor", 0.5),
                    }),
                });
        }

        private static TasModelInventory SamInventory(float[] cooling)
        {
            float setpoint = cooling.Min();
            return new TasModelInventory("000000_SAM_AnalyticalModel.tbd", null, null, new[]
            {
                new TasInternalConditionInfo("Studio 1_0", "S54_Rehearsal - Studio 1_0", 1, new TasSetpointProfile(TasSetpointProfileType.Value, 1, -50), new TasSetpointProfile(TasSetpointProfileType.Hourly, 1, setpoint, cooling.Count(x => x == setpoint), cooling)),
            });
        }

        /// <summary>The glazing case's options with SAM window systems attached (as the runner resolves them from a SAM UI pool).</summary>
        private static Dictionary<string, IReadOnlyList<TasGlazingOption>> AttachedGlazingOptions()
        {
            List<TasGlazingSystem> pool = TasModelFixtures.Pool(attach: true);
            TasGlazingSystem doubleB = pool.Single(x => x.Name == "Double B");
            TasGlazingSystem triple = pool.Single(x => x.Name == "Triple low-e");
            return new Dictionary<string, IReadOnlyList<TasGlazingOption>>
            {
                {
                    "Glazing",
                    new List<TasGlazingOption>
                    {
                        TasModelFixtures.Option(1, TasModelFixtures.SamGlazing, TasModelFixtures.SamGlazing, 0.4001609981060028, 1.2433900833129883, 0.803563117980957),
                        new TasGlazingOption(2, "Double B", Query.TasGlazingPaneConstruction(doubleB), doubleB.G, doubleB.Ug, doubleB.Light, doubleB.Source, doubleB),
                        new TasGlazingOption(3, "Triple low-e", Query.TasGlazingPaneConstruction(triple), triple.G, triple.Ug, triple.Light, triple.Source, triple),
                    }
                },
            };
        }
    }

    internal static class TasGlazingOptionTestExtensions
    {
        /// <summary>The option with a pool system that has no SAM aperture construction (values known, nothing to write).</summary>
        public static TasGlazingOption Detached(this TasGlazingOption option)
        {
            TasGlazingSystem system = TasModelFixtures.System(option.Text, "Test", option.G, option.U, option.Light, "aaaaaaaa-0000-0000-0000-0000000000dd");
            return new TasGlazingOption(option.Number, option.Text, option.PaneConstruction, option.G, option.U, option.Light, option.Source, system);
        }
    }
}
