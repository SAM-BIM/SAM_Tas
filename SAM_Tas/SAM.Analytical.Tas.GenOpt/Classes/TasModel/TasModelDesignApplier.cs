// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace SAM.Analytical.Tas.GenOpt
{
    /// <summary>
    /// "Apply best design" for the Tas files (PR9): writes the best point of a "tas-model" run into the project's TBD
    /// (zone setpoints, glazing choice) and TPD (plant controller setpoints) with the same rules as the generated script
    /// (<see cref="Create.TasScript"/>), so the files hold exactly the design that was evaluated.
    /// <para>The original files are never opened for writing:</para>
    /// <list type="number">
    /// <item>The project's Tas files must still have the hashes the run recorded (<see cref="TasModelRunner.SourceHashes"/>):
    /// otherwise they are not the model that was optimised, and nothing is done.</item>
    /// <item>The files to change are copied into a staging folder and a backup folder of this application's own work
    /// folder (<c>&lt;project&gt;\SAM_ApplyBestDesign\&lt;time&gt;-&lt;id&gt;</c>).</item>
    /// <item>The staging copies are written (licensed Tas, COM) and read back with <see cref="Query.TasModelInventory"/>:
    /// every changed item must read back as the best value, and every other internal condition, glazing assignment and
    /// controller exactly as before.</item>
    /// <item>The project files to replace are then held so that no other program can open them, and every Tas file of
    /// the project is checked again against the run's hashes: a file saved or simulated by another program meanwhile
    /// refuses everything, and that program's file is kept. A file another program has open is refused too.</item>
    /// <item>Only then are the project files replaced from staging, one by one, with the operating system's replacement
    /// (<see cref="System.IO.File.Replace(string, string, string)"/>). If one cannot be replaced, the ones no longer
    /// original are restored from the backup; the exception says what happened.</item>
    /// </list>
    /// <para>What this does not guarantee: the files are replaced one after another, not as one transaction. If the
    /// process or the computer stops during the replacement, the TBD may be the best design and the TPD still the
    /// original; the note <see cref="ReplacingNoteName"/> then stays in the work folder, with the backup and the hashes to
    /// restore by hand, and the next optimisation or Apply sees files that are not the run's. Nothing is recovered
    /// automatically.</para>
    /// <para>Changes whose item already holds the best value are reported unchanged and need no file; when no change
    /// needs one, nothing is written at all. One Tas document is open at a time.</para>
    /// </summary>
    public sealed class TasModelDesignApplier
    {
        /// <summary>The folder, under the Tas project, that holds each application's staging copies and backup.</summary>
        public const string WorkFolderName = "SAM_ApplyBestDesign";

        /// <summary>
        /// The note in an application's work folder while the project files are being replaced; it stays only if the
        /// replacement was interrupted (or a file could not be restored) and says how to restore the originals.
        /// </summary>
        public const string ReplacingNoteName = "REPLACING-PROJECT-FILES.txt";

        private readonly string projectFolder;
        private readonly TasModelInventory inventory;
        private readonly IReadOnlyDictionary<string, string> sourceHashes;

        /// <param name="projectFolder">The Tas project folder the optimisation ran on.</param>
        /// <param name="tasModelInventory">The inventory the run was checked against (<see cref="TasModelRunner.Inventory"/>).</param>
        /// <param name="sourceHashes">The hashes of the files the run evaluated (<see cref="TasModelRunner.SourceHashes"/>).</param>
        public TasModelDesignApplier(string projectFolder, TasModelInventory tasModelInventory, IReadOnlyDictionary<string, string> sourceHashes)
        {
            if (string.IsNullOrWhiteSpace(projectFolder) || !Directory.Exists(projectFolder))
            {
                throw new DirectoryNotFoundException("The Tas project folder does not exist: '" + projectFolder + "'.");
            }

            // Tas opens files in its own server process: always full paths.
            this.projectFolder = Path.GetFullPath(projectFolder);
            inventory = tasModelInventory ?? throw new ArgumentNullException(nameof(tasModelInventory));
            this.sourceHashes = sourceHashes;
        }

        /// <summary>Writes the building changes into a TBD (a staging copy): <see cref="WriteTbd"/> by default (licensed Tas). Replaceable for tests.</summary>
        public Func<string, IReadOnlyList<TasModelDesignChange>, List<TasModelAppliedValue>> TbdWriter { get; set; } = WriteTbd;

        /// <summary>Writes the controller changes into a TPD (a staging copy): <see cref="WriteTpd"/> by default (licensed Tas). Replaceable for tests.</summary>
        public Func<string, IReadOnlyList<TasModelDesignChange>, List<TasModelAppliedValue>> TpdWriter { get; set; } = WriteTpd;

        /// <summary>Reads the staging folder back: <see cref="Query.TasModelInventory(string)"/> by default (licensed Tas). Replaceable for tests.</summary>
        public Func<string, TasModelInventory> InventoryReader { get; set; } = Query.TasModelInventory;

        /// <summary>
        /// Replaces a project file with its staged copy (source, destination): <see cref="System.IO.File.Replace(string, string, string)"/>
        /// by default, the operating system's replacement (the staged copy, in the same folder tree, takes the project file's
        /// place; it is refused while another program has the file open). Replaceable for tests.
        /// </summary>
        public Action<string, string> FileReplacer { get; set; } = (source, destination) => System.IO.File.Replace(source, destination, null);

        /// <summary>
        /// Writes <paramref name="changes"/> (<see cref="Query.TasModelDesignChanges"/>) into the project.
        /// </summary>
        /// <exception cref="TasModelApplyException">Nothing was applied; <see cref="TasModelApplyException.ProjectChanged"/>
        /// is true only if a project file could not be restored after a failed replacement.</exception>
        public TasModelApplyResult Apply(IReadOnlyList<TasModelDesignChange> changes)
        {
            if (changes == null || changes.Count == 0 || changes.Any(x => x == null))
            {
                throw new ArgumentException("There are no changes to apply.", nameof(changes));
            }

            // 1. The project is the model that was optimised.
            if (sourceHashes == null || sourceHashes.Count == 0)
            {
                throw new TasModelApplyException("There is no record of the Tas files the optimisation used, so they cannot be checked. Nothing was changed: run the optimisation again.");
            }

            Dictionary<string, string> current = Query.TasFileHashes(projectFolder);
            List<string> differences = Differences(sourceHashes, current);
            if (differences.Count > 0)
            {
                throw new TasModelApplyException("The Tas files changed after the optimisation ran (" + string.Join("; ", differences) + "), so the best design may not suit them. Nothing was changed: run the optimisation again on the current model.");
            }

            // 2. What needs writing, and where.
            List<TasModelDesignChange> building = changes.Where(x => x.IsBuilding && !IsUnchanged(x, inventory)).ToList();
            List<TasModelDesignChange> plant = changes.Where(x => x.IsController && !IsUnchanged(x, inventory)).ToList();
            Dictionary<TasModelDesignChange, TasModelAppliedValue> values = new Dictionary<TasModelDesignChange, TasModelAppliedValue>();
            foreach (TasModelDesignChange change in changes.Where(x => IsUnchanged(x, inventory)))
            {
                string text = CurrentText(change, inventory);
                values[change] = new TasModelAppliedValue(change, text, text, false, "already the best value");
            }

            if (building.Count == 0 && plant.Count == 0)
            {
                return new TasModelApplyResult(projectFolder, null, null, null, changes.Select(x => values[x]));
            }

            List<string> files = new List<string>();
            if (building.Count > 0)
            {
                files.Add(ProjectFile(".tbd", inventory.TbdFileName));
            }

            if (plant.Count > 0)
            {
                files.Add(ProjectFile(".tpd", inventory.TpdFileName));
            }

            // 3. The work folder: staging copies to write, and the originals' backup.
            string workFolder = Path.Combine(projectFolder, WorkFolderName, DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            string staging = Path.Combine(workFolder, "staging");
            string backup = Path.Combine(workFolder, "original");
            try
            {
                Directory.CreateDirectory(staging);
                Directory.CreateDirectory(backup);
                foreach (string file in files)
                {
                    System.IO.File.Copy(Path.Combine(projectFolder, file), Path.Combine(staging, file), false);
                    System.IO.File.Copy(Path.Combine(projectFolder, file), Path.Combine(backup, file), false);
                    if (Query.FileHash(Path.Combine(staging, file)) != current[file] || Query.FileHash(Path.Combine(backup, file)) != current[file])
                    {
                        throw new IOException("The copy of " + file + " differs from the project file.");
                    }
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                throw new TasModelApplyException("The Tas files could not be copied for writing (" + exception.Message + "). Nothing in the project was changed.", false, workFolder, exception);
            }

            // 4. Write the staging copies.
            try
            {
                if (building.Count > 0)
                {
                    foreach (TasModelAppliedValue value in TbdWriter(Path.Combine(staging, files[0]), building) ?? new List<TasModelAppliedValue>())
                    {
                        values[value.Change] = value;
                    }
                }

                if (plant.Count > 0)
                {
                    foreach (TasModelAppliedValue value in TpdWriter(Path.Combine(staging, files[files.Count - 1]), plant) ?? new List<TasModelAppliedValue>())
                    {
                        values[value.Change] = value;
                    }
                }
            }
            catch (Exception exception) when (!(exception is TasModelApplyException))
            {
                throw new TasModelApplyException("Tas could not write the best design: " + Innermost(exception).Message + " Nothing in the project was changed; the attempt is kept in " + workFolder + ".", false, workFolder, exception);
            }

            TasModelDesignChange missing = changes.FirstOrDefault(x => !values.ContainsKey(x));
            if (missing != null)
            {
                throw new TasModelApplyException("Tas did not report writing “" + missing.VariableName + "”. Nothing in the project was changed; the attempt is kept in " + workFolder + ".", false, workFolder);
            }

            // 5. Read the staging copies back.
            List<string> problems;
            try
            {
                problems = Verify(inventory, InventoryReader(staging), building, plant);
            }
            catch (Exception exception)
            {
                throw new TasModelApplyException("The written Tas files could not be read back: " + Innermost(exception).Message + " Nothing in the project was changed; the attempt is kept in " + workFolder + ".", false, workFolder, exception);
            }

            if (problems.Count > 0)
            {
                throw new TasModelApplyException("The best design did not read back as written: " + string.Join(" ", problems) + " Nothing in the project was changed; the attempt is kept in " + workFolder + ".", false, workFolder);
            }

            Dictionary<string, string> staged = files.ToDictionary(x => x, x => Query.FileHash(Path.Combine(staging, x)), StringComparer.OrdinalIgnoreCase);

            // 6. Hold the project files to replace, so that no other program can open them until they are replaced, and
            // check again, under that hold, that every Tas file is still the run's: Tas (or anyone) may have saved or
            // simulated the project while the staging copies were written and read back.
            List<FileStream> holds = new List<FileStream>();
            string note = Path.Combine(workFolder, ReplacingNoteName);
            List<string> replaced = new List<string>();
            try
            {
                foreach (string file in files)
                {
                    try
                    {
                        holds.Add(new FileStream(Path.Combine(projectFolder, file), FileMode.Open, FileAccess.Read, FileShare.Delete));
                    }
                    catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                    {
                        throw new TasModelApplyException(file + " is open in another program (" + Innermost(exception).Message + "), so it cannot be replaced safely. Close it (in Tas too) and apply again. Nothing in the project was changed; the attempt is kept in " + workFolder + ".", false, workFolder, exception);
                    }
                }

                List<string> changed = Differences(sourceHashes, LiveHashes(holds));
                if (changed.Count > 0)
                {
                    throw new TasModelApplyException("The Tas files changed while the best design was being written (" + string.Join("; ", changed) + "): another program saved or simulated them. Nothing in the project was changed; the attempt is kept in " + workFolder + ". Run the optimisation again on the current model.", false, workFolder);
                }

                // 7. Replace the project files from staging (an OS replacement per file); restore every one replaced so far
                // if one fails. Until the replacement ends, a note in the work folder says how to recover by hand if the
                // process or the computer stops part-way.
                System.IO.File.WriteAllText(note, ReplacingNote(files, current, staged, backup));
                foreach (string file in files)
                {
                    try
                    {
                        FileReplacer(Path.Combine(staging, file), Path.Combine(projectFolder, file));
                        if (Query.FileHash(Path.Combine(projectFolder, file)) != staged[file])
                        {
                            throw new IOException(file + " does not hold the written copy after it was replaced.");
                        }

                        replaced.Add(file);
                    }
                    catch (Exception exception)
                    {
                        holds.ForEach(x => x.Dispose());
                        replaced.Add(file);
                        List<string> notRestored = Restore(replaced, backup, current);
                        if (notRestored.Count == 0)
                        {
                            DeleteNote(note);
                            throw new TasModelApplyException("Replacing " + file + " failed (" + Innermost(exception).Message + "), so every project file was restored from the backup. Nothing in the project was changed.", false, workFolder, exception);
                        }

                        throw new TasModelApplyException("Replacing " + file + " failed (" + Innermost(exception).Message + ") and " + string.Join(", ", notRestored) + " could not be restored. Copy " + (notRestored.Count == 1 ? "it" : "them") + " back from " + backup + " before using the model.", true, workFolder, exception);
                    }
                }
            }
            finally
            {
                holds.ForEach(x => x.Dispose());
            }

            DeleteNote(note);

            // The staging copies are the project files now; the backup stays.
            try
            {
                Directory.Delete(staging, true);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
            }

            return new TasModelApplyResult(projectFolder, workFolder, backup, files, changes.Select(x => values[x]));
        }

        /// <summary>
        /// True when the run's model already holds the change's best value, so nothing is written for it: glazing option 1
        /// (the current glazing), a setpoint equal to the TBD's (compared as the float the TBD stores), a controller setpoint
        /// equal to the TPD's.
        /// </summary>
        public static bool IsUnchanged(TasModelDesignChange change, TasModelInventory tasModelInventory)
        {
            if (change == null)
            {
                return false;
            }

            if (change.IsGlazing)
            {
                return change.GlazingOption != null && change.GlazingOption.IsCurrent;
            }

            if (change.IsSetpoint)
            {
                TasSetpointProfile profile = SetpointProfile(tasModelInventory, change);
                return profile?.Setpoint != null && (float)profile.Setpoint.Value == (float)change.Value;
            }

            if (change.IsController)
            {
                TasPlantControllerInfo controller = Controller(tasModelInventory, change);
                return controller != null && controller.Setpoint == change.Value;
            }

            return false;
        }

        /// <summary>The item's current value in the run's model, as invariant text (a glazing construction's name).</summary>
        public static string CurrentText(TasModelDesignChange change, TasModelInventory tasModelInventory)
        {
            if (change == null)
            {
                return null;
            }

            if (change.IsGlazing)
            {
                return change.GlazingConstruction;
            }

            if (change.IsSetpoint)
            {
                double? setpoint = SetpointProfile(tasModelInventory, change)?.Setpoint;
                return setpoint == null ? null : Text((float)setpoint.Value);
            }

            if (change.IsController)
            {
                TasPlantControllerInfo controller = Controller(tasModelInventory, change);
                return controller == null ? null : controller.Setpoint.ToString("R", CultureInfo.InvariantCulture);
            }

            return null;
        }

        /// <summary>
        /// Writes the building changes into a TBD (licensed Tas, COM; a staging copy, never the user's file): first the
        /// glazing systems the chosen options need (<see cref="TasModelRunner.WriteGlazingSystems"/>, unique names), then
        /// each setpoint and glazing assignment as the generated script does, read back, and saved.
        /// </summary>
        public static List<TasModelAppliedValue> WriteTbd(string tbdPath, IReadOnlyList<TasModelDesignChange> changes)
        {
            List<TasGlazingOption> systems = (changes ?? new List<TasModelDesignChange>())
                .Where(x => x.IsGlazing && x.GlazingOption != null && !x.GlazingOption.IsCurrent)
                .Select(x => x.GlazingOption)
                .GroupBy(x => x.PaneConstruction, StringComparer.Ordinal)
                .Select(x => x.First())
                .ToList();
            if (systems.Count > 0)
            {
                TasModelRunner.WriteGlazingSystems(tbdPath, systems);
            }

            List<TasModelAppliedValue> result = new List<TasModelAppliedValue>();
            TBD.TBDDocument document = new TBD.TBDDocument();
            try
            {
                document.open(tbdPath);
                TBD.Building building = document.Building;
                foreach (TasModelDesignChange change in changes)
                {
                    if (change.IsSetpoint)
                    {
                        result.Add(SetSetpoint(building, change));
                    }
                    else if (change.IsGlazing)
                    {
                        result.Add(SetGlazing(building, change));
                    }
                    else
                    {
                        throw new InvalidOperationException("“" + change.VariableName + "” is not a TBD change.");
                    }
                }

                document.save();
            }
            finally
            {
                document.close();
                Marshal.FinalReleaseComObject(document);
            }

            return result;
        }

        /// <summary>
        /// Writes the controller setpoints into a TPD (licensed Tas, COM; a staging copy, never the user's file): exact
        /// plant room and controller names, the value as a double, read back, saved. A fault while TPD closes after the save
        /// is ignored (PR7a F2): the file is read back afterwards anyway.
        /// </summary>
        public static List<TasModelAppliedValue> WriteTpd(string tpdPath, IReadOnlyList<TasModelDesignChange> changes)
        {
            List<TasModelAppliedValue> result = new List<TasModelAppliedValue>();
            TPD.TPDDoc document = new TPD.TPDDoc();
            try
            {
                document.Open(tpdPath);
                TPD.EnergyCentre energyCentre = document.EnergyCentre;
                foreach (TasModelDesignChange change in changes)
                {
                    if (!change.IsController)
                    {
                        throw new InvalidOperationException("“" + change.VariableName + "” is not a TPD change.");
                    }

                    result.Add(SetController(energyCentre, change));
                }

                document.Save();
            }
            finally
            {
                Query.IgnoreServerFault(() => document.Close());
                Marshal.FinalReleaseComObject(document);
            }

            return result;
        }

        private static TasModelAppliedValue SetSetpoint(TBD.Building building, TasModelDesignChange change)
        {
            string name = change.InternalCondition;
            List<TBD.InternalCondition> matches = new List<TBD.InternalCondition>();
            for (int i = 0; ; i++)
            {
                TBD.InternalCondition candidate = building.GetIC(i);
                if (candidate == null)
                {
                    break;
                }

                if (string.Equals(candidate.name, name, StringComparison.Ordinal))
                {
                    matches.Add(candidate);
                }
            }

            string what = change.IsHeating ? "Heating" : "Cooling";
            if (matches.Count != 1)
            {
                throw new InvalidOperationException("Internal condition “" + name + "” was found " + matches.Count + " times in the TBD.");
            }

            TBD.Thermostat thermostat = matches[0].GetThermostat() ?? throw new InvalidOperationException("Internal condition “" + name + "” has no thermostat.");
            TBD.profile profile = thermostat.GetProfile((int)(change.IsHeating ? TBD.Profiles.ticLL : TBD.Profiles.ticUL));
            if (profile.factor != 1)
            {
                throw new InvalidOperationException(what + " setpoint of “" + name + "” has factor " + profile.factor.ToString(CultureInfo.InvariantCulture) + "; only factor 1 is supported.");
            }

            float value = (float)change.Value;
            if (profile.type == TBD.ProfileTypes.ticValueProfile)
            {
                float before = profile.value;
                profile.value = value;
                return new TasModelAppliedValue(change, Text(before), Text(profile.value), true, "value profile");
            }

            if (profile.type == TBD.ProfileTypes.ticHourlyProfile)
            {
                float[] hours = new float[24];
                for (int h = 1; h <= 24; h++)
                {
                    hours[h - 1] = profile.hourlyValues[h];
                }

                float before = change.IsHeating ? hours.Max() : hours.Min();
                int changed = 0;
                int first = 0;
                for (int h = 1; h <= 24; h++)
                {
                    if (hours[h - 1] == before)
                    {
                        profile.hourlyValues[h] = value;
                        changed++;
                        first = first == 0 ? h : first;
                    }
                }

                return new TasModelAppliedValue(change, Text(before), Text(profile.hourlyValues[first]), true, changed.ToString(CultureInfo.InvariantCulture) + " of 24 hours");
            }

            throw new InvalidOperationException(what + " setpoint of “" + name + "” is a " + profile.type + "; only value and 24-hour profiles are supported.");
        }

        private static TasModelAppliedValue SetGlazing(TBD.Building building, TasModelDesignChange change)
        {
            string target = change.GlazingConstruction;
            TasGlazingOption option = change.GlazingOption ?? throw new InvalidOperationException("“" + change.VariableName + "” has no glazing option.");

            List<TBD.buildingElement> elements = new List<TBD.buildingElement>();
            for (int i = 0; ; i++)
            {
                TBD.buildingElement element = building.GetBuildingElement(i);
                if (element == null)
                {
                    break;
                }

                TBD.Construction construction = element.GetConstruction();
                if (construction != null && string.Equals(construction.name, target, StringComparison.Ordinal))
                {
                    elements.Add(element);
                }
            }

            if (elements.Count == 0)
            {
                throw new InvalidOperationException("No building element uses the glazing construction “" + target + "”.");
            }

            TBD.Construction pane = building.GetConstructionByName(option.PaneConstruction);
            if (pane == null || pane.type != TBD.ConstructionTypes.tcdTransparentConstruction)
            {
                throw new InvalidOperationException("Glazing option " + option.Number + " pane construction “" + option.PaneConstruction + "” is not in the TBD.");
            }

            foreach (TBD.buildingElement element in elements)
            {
                element.AssignConstruction(pane);
            }

            if (elements.Any(x => !string.Equals(x.GetConstruction()?.name, option.PaneConstruction, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("Glazing option " + option.Number + " was not assigned.");
            }

            float[] glazing = Query.Floats(pane.GetGlazingValues());
            float[] u = Query.Floats(pane.GetUValue());
            string detail = string.Format(CultureInfo.InvariantCulture, "{0} elements; g {1}, U {2}, light {3}", elements.Count, Text(glazing[5]), Text(u[6]), Text(glazing[0]));
            return new TasModelAppliedValue(change, target, option.PaneConstruction, true, detail);
        }

        private static TasModelAppliedValue SetController(TPD.EnergyCentre energyCentre, TasModelDesignChange change)
        {
            TPD.PlantRoom plantRoom = null;
            for (int i = 1; i <= energyCentre.GetPlantRoomCount(); i++)
            {
                TPD.PlantRoom candidate = energyCentre.GetPlantRoom(i);
                if (!string.Equals(candidate.Name, change.PlantRoom, StringComparison.Ordinal))
                {
                    continue;
                }

                if (plantRoom != null)
                {
                    throw new InvalidOperationException("Plant room “" + change.PlantRoom + "” was found more than once in the TPD.");
                }

                plantRoom = candidate;
            }

            if (plantRoom == null)
            {
                throw new InvalidOperationException("Plant room “" + change.PlantRoom + "” is not in the TPD.");
            }

            TPD.PlantController controller = null;
            for (int i = 1; i <= plantRoom.GetControllerCount(); i++)
            {
                TPD.PlantController candidate = plantRoom.GetController(i);
                if (!string.Equals(candidate.Name, change.Controller, StringComparison.Ordinal))
                {
                    continue;
                }

                if (controller != null)
                {
                    throw new InvalidOperationException("Controller “" + change.Controller + "” was found more than once in “" + change.PlantRoom + "”.");
                }

                controller = candidate;
            }

            if (controller == null)
            {
                throw new InvalidOperationException("Controller “" + change.Controller + "” is not in “" + change.PlantRoom + "”.");
            }

            double before = controller.Setpoint;
            controller.Setpoint = change.Value;
            return new TasModelAppliedValue(change, before.ToString("R", CultureInfo.InvariantCulture), controller.Setpoint.ToString("R", CultureInfo.InvariantCulture), true, controller.SensorType.ToString());
        }

        /// <summary>
        /// What the staging copies read back as, against what was expected: the changed items at their best values, and
        /// every other internal condition, glazing assignment and controller exactly as in the run's model.
        /// </summary>
        internal static List<string> Verify(TasModelInventory before, TasModelInventory after, IReadOnlyList<TasModelDesignChange> building, IReadOnlyList<TasModelDesignChange> plant)
        {
            List<string> result = new List<string>();
            if (after == null)
            {
                result.Add("Nothing could be read.");
                return result;
            }

            if (building.Count > 0)
            {
                VerifyInternalConditions(before, after, building, result);
                VerifyGlazing(before, after, building, result);
            }

            if (plant.Count > 0)
            {
                VerifyControllers(before, after, plant, result);
            }

            return result;
        }

        private static void VerifyInternalConditions(TasModelInventory before, TasModelInventory after, IReadOnlyList<TasModelDesignChange> changes, List<string> result)
        {
            if (!before.InternalConditions.Select(x => x.Name).SequenceEqual(after.InternalConditions.Select(x => x.Name), StringComparer.Ordinal))
            {
                result.Add("The TBD's internal conditions are not the ones it had.");
                return;
            }

            for (int i = 0; i < before.InternalConditions.Count; i++)
            {
                TasInternalConditionInfo info_Before = before.InternalConditions[i];
                TasInternalConditionInfo info_After = after.InternalConditions[i];
                foreach (bool heating in new[] { true, false })
                {
                    TasSetpointProfile profile_Before = heating ? info_Before.Heating : info_Before.Cooling;
                    TasSetpointProfile profile_After = heating ? info_After.Heating : info_After.Cooling;
                    TasModelDesignChange change = changes.FirstOrDefault(x => x.IsSetpoint && x.IsHeating == heating && x.InternalCondition == info_Before.Name);
                    string what = (heating ? "Heating" : "Cooling") + " setpoint of “" + info_Before.Name + "”";
                    if (change == null)
                    {
                        if (!SameProfile(profile_Before, profile_After))
                        {
                            result.Add(what + " changed although it is not part of the design.");
                        }

                        continue;
                    }

                    if (!SameProfile(Expected(profile_Before, (float)change.Value, heating), profile_After))
                    {
                        result.Add(what + " reads " + (profile_After?.Setpoint == null ? "nothing" : Text((float)profile_After.Setpoint.Value)) + ", not the best value " + Text((float)change.Value) + ".");
                    }
                }
            }
        }

        /// <summary>The profile the script's block makes of <paramref name="profile"/> for <paramref name="value"/>.</summary>
        private static TasSetpointProfile Expected(TasSetpointProfile profile, float value, bool heating)
        {
            if (profile == null || profile.Setpoint == null)
            {
                return null;
            }

            if (profile.Type == TasSetpointProfileType.Value)
            {
                return new TasSetpointProfile(TasSetpointProfileType.Value, profile.Factor, value);
            }

            if (profile.Type == TasSetpointProfileType.Hourly && profile.Hours != null && profile.Hours.Count == 24)
            {
                float setpoint = (float)profile.Setpoint.Value;
                float[] hours = profile.Hours.Select(x => x == setpoint ? value : x).ToArray();
                float setpoint_New = heating ? hours.Max() : hours.Min();
                return new TasSetpointProfile(TasSetpointProfileType.Hourly, profile.Factor, setpoint_New, hours.Count(x => x == setpoint_New), hours);
            }

            return null;
        }

        private static bool SameProfile(TasSetpointProfile x, TasSetpointProfile y)
        {
            if (x == null || y == null)
            {
                return x == null && y == null;
            }

            if (x.Type != y.Type || x.Factor != y.Factor || x.Setpoint.HasValue != y.Setpoint.HasValue)
            {
                return false;
            }

            if (x.Setpoint.HasValue && (float)x.Setpoint.Value != (float)y.Setpoint.Value)
            {
                return false;
            }

            if (x.Type == TasSetpointProfileType.Hourly)
            {
                return x.Hours != null && y.Hours != null && x.Hours.SequenceEqual(y.Hours);
            }

            return true;
        }

        private static void VerifyGlazing(TasModelInventory before, TasModelInventory after, IReadOnlyList<TasModelDesignChange> changes, List<string> result)
        {
            // Element → glazing construction, as it was, with the chosen options assigned.
            Dictionary<string, string> expected = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (TasGlazingConstructionInfo info in before.GlazingConstructions)
            {
                foreach (string element in info.Elements)
                {
                    expected[element] = info.Name;
                }
            }

            foreach (TasModelDesignChange change in changes.Where(x => x.IsGlazing && x.GlazingOption != null && !x.GlazingOption.IsCurrent))
            {
                foreach (string element in expected.Where(x => x.Value == change.GlazingConstruction).Select(x => x.Key).ToList())
                {
                    expected[element] = change.GlazingOption.PaneConstruction;
                }
            }

            Dictionary<string, string> actual = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (TasGlazingConstructionInfo info in after.GlazingConstructions)
            {
                foreach (string element in info.Elements)
                {
                    actual[element] = info.Name;
                }
            }

            foreach (string element in expected.Keys.Union(actual.Keys).OrderBy(x => x, StringComparer.Ordinal))
            {
                expected.TryGetValue(element, out string construction_Expected);
                actual.TryGetValue(element, out string construction_Actual);
                if (!string.Equals(construction_Expected, construction_Actual, StringComparison.Ordinal))
                {
                    result.Add("Building element “" + element + "” uses " + (construction_Actual == null ? "no glazing" : "“" + construction_Actual + "”") + ", not " + (construction_Expected == null ? "no glazing" : "“" + construction_Expected + "”") + ".");
                }
            }

            foreach (TasModelDesignChange change in changes.Where(x => x.IsGlazing && x.GlazingOption != null && !x.GlazingOption.IsCurrent))
            {
                TasGlazingOption option = change.GlazingOption;
                TasGlazingConstructionInfo info = after.GlazingConstructions.FirstOrDefault(x => x.Name == option.PaneConstruction);
                if (info == null)
                {
                    continue;
                }

                if (!Close(info.G, option.G, GlazingGTolerance) || !Close(info.U, option.U, GlazingUTolerance) || !Close(info.Light, option.Light, GlazingLightTolerance))
                {
                    result.Add(string.Format(CultureInfo.InvariantCulture, "“{0}” reads g {1}, U {2}, light {3}, not the option's g {4}, U {5}, light {6}.", option.PaneConstruction, info.G, info.U, info.Light, option.G, option.U, option.Light));
                }

                // The pane, layer by layer, is the system's: the materials the SAM model gets for it (SAM_UI refuses a
                // model material of the same name that differs) are the ones evaluated.
                if (info.PaneLayers != null && option.System?.ApertureConstruction != null)
                {
                    List<string> layers = Query.TasMaterialLayerDifferences(Query.TasMaterialLayers(option.System.ApertureConstruction.PaneConstructionLayers, option.System.MaterialLibrary), info.PaneLayers);
                    if (layers.Count > 0)
                    {
                        result.Add("“" + option.PaneConstruction + "” is not the system's pane: " + string.Join("; ", layers) + ".");
                    }
                }
            }
        }

        private static void VerifyControllers(TasModelInventory before, TasModelInventory after, IReadOnlyList<TasModelDesignChange> changes, List<string> result)
        {
            if (!before.PlantRooms.Select(x => x.Name).SequenceEqual(after.PlantRooms.Select(x => x.Name), StringComparer.Ordinal))
            {
                result.Add("The TPD's plant rooms are not the ones it had.");
                return;
            }

            for (int i = 0; i < before.PlantRooms.Count; i++)
            {
                TasPlantRoomInfo room_Before = before.PlantRooms[i];
                TasPlantRoomInfo room_After = after.PlantRooms[i];
                if (!room_Before.Controllers.Select(x => x.Name).SequenceEqual(room_After.Controllers.Select(x => x.Name), StringComparer.Ordinal))
                {
                    result.Add("The controllers of “" + room_Before.Name + "” are not the ones it had.");
                    continue;
                }

                for (int j = 0; j < room_Before.Controllers.Count; j++)
                {
                    TasPlantControllerInfo controller_Before = room_Before.Controllers[j];
                    TasPlantControllerInfo controller_After = room_After.Controllers[j];
                    TasModelDesignChange change = changes.FirstOrDefault(x => x.PlantRoom == room_Before.Name && x.Controller == controller_Before.Name);
                    double expected = change == null ? controller_Before.Setpoint : change.Value;
                    if (controller_After.Setpoint != expected)
                    {
                        result.Add(string.Format(CultureInfo.InvariantCulture, "Controller “{0}” reads {1}, not {2}{3}.", controller_Before.Name, controller_After.Setpoint.ToString("R", CultureInfo.InvariantCulture), expected.ToString("R", CultureInfo.InvariantCulture), change == null ? " (it is not part of the design)" : string.Empty));
                    }
                }
            }
        }

        private static TasSetpointProfile SetpointProfile(TasModelInventory tasModelInventory, TasModelDesignChange change)
        {
            List<TasInternalConditionInfo> matches = tasModelInventory?.InternalConditions.Where(x => x.Name == change.InternalCondition).ToList();
            if (matches == null || matches.Count != 1)
            {
                return null;
            }

            return change.IsHeating ? matches[0].Heating : matches[0].Cooling;
        }

        private static TasPlantControllerInfo Controller(TasModelInventory tasModelInventory, TasModelDesignChange change)
        {
            List<TasPlantRoomInfo> rooms = tasModelInventory?.PlantRooms.Where(x => x.Name == change.PlantRoom).ToList();
            if (rooms == null || rooms.Count != 1)
            {
                return null;
            }

            List<TasPlantControllerInfo> controllers = rooms[0].Controllers.Where(x => x.Name == change.Controller).ToList();
            return controllers.Count == 1 ? controllers[0] : null;
        }

        private string ProjectFile(string extension, string expected)
        {
            string path;
            try
            {
                path = Query.SingleFile(projectFolder, extension);
            }
            catch (InvalidOperationException exception)
            {
                throw new TasModelApplyException(exception.Message + " Nothing was changed.");
            }

            if (path == null || !string.Equals(Path.GetFileName(path), expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new TasModelApplyException("The project's " + extension.TrimStart('.').ToUpperInvariant() + " is " + (path == null ? "missing" : Path.GetFileName(path)) + ", but the optimisation used " + (expected ?? "none") + ". Nothing was changed.");
            }

            return Path.GetFileName(path);
        }

        private static List<string> Differences(IReadOnlyDictionary<string, string> expected, IReadOnlyDictionary<string, string> actual)
        {
            List<string> result = new List<string>();
            foreach (KeyValuePair<string, string> pair in expected.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!actual.TryGetValue(pair.Key, out string hash))
                {
                    result.Add(pair.Key + " is missing");
                }
                else if (!string.Equals(hash, pair.Value, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(pair.Key + " changed");
                }
            }

            foreach (string name in actual.Keys.Where(x => !expected.Keys.Contains(x, StringComparer.OrdinalIgnoreCase)).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(name + " is new");
            }

            return result;
        }

        /// <summary>
        /// The hashes of the project's Tas files now: the held ones read through their holds (no one else can open them),
        /// the others as usual.
        /// </summary>
        private Dictionary<string, string> LiveHashes(IEnumerable<FileStream> holds)
        {
            Dictionary<string, FileStream> held = holds.ToDictionary(x => Path.GetFileName(x.Name), StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in Directory.GetFiles(projectFolder, "*", SearchOption.TopDirectoryOnly).Where(NativeGenOptWorkspace.IsTasFile))
            {
                string name = Path.GetFileName(path);
                if (held.TryGetValue(name, out FileStream stream))
                {
                    stream.Position = 0;
                    result[name] = Query.FileHash(stream);
                }
                else
                {
                    result[name] = Query.FileHash(path);
                }
            }

            return result;
        }

        private string ReplacingNote(IEnumerable<string> files, IReadOnlyDictionary<string, string> originals, IReadOnlyDictionary<string, string> staged, string backup)
        {
            List<string> lines = new List<string>
            {
                "SAM \"Apply best design\" was replacing these Tas files of " + projectFolder + " with the best design.",
                "If this note is still here, the replacement did not finish: a file may be the original, the best design or incomplete.",
                "Before using the model, copy the originals back from " + backup + " (SHA-256 below), then run the optimisation again.",
                string.Empty,
            };

            foreach (string file in files)
            {
                lines.Add(file + "  original " + originals[file] + "  best design " + staged[file]);
            }

            return string.Join(Environment.NewLine, lines) + Environment.NewLine;
        }

        private static void DeleteNote(string path)
        {
            try
            {
                System.IO.File.Delete(path);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
            }
        }

        private List<string> Restore(IEnumerable<string> files, string backup, IReadOnlyDictionary<string, string> hashes)
        {
            List<string> result = new List<string>();
            foreach (string file in files)
            {
                try
                {
                    // A file whose replacement never happened is still the original: leave it alone.
                    string path = Path.Combine(projectFolder, file);
                    if (System.IO.File.Exists(path) && Query.FileHash(path) == hashes[file])
                    {
                        continue;
                    }

                    System.IO.File.Copy(Path.Combine(backup, file), Path.Combine(projectFolder, file), true);
                    if (Query.FileHash(Path.Combine(projectFolder, file)) != hashes[file])
                    {
                        result.Add(file);
                    }
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    result.Add(file);
                }
            }

            return result;
        }

        /// <summary>
        /// How far a glazing value read back from the TBD may be from the option's, per quantity. The option's g, U and light
        /// are the pool's, which SAM_Tas' glazing calculation reads from TCD rounded to <see cref="global::SAM.Core.Tolerance.MacroDistance"/>
        /// (<c>Analytical.Tas.Query.GlazingValues</c>, <c>Analytical.Tas.Query.ThermalTransmittance</c>), while the TBD's are
        /// read unrounded: one rounding step each (the licensed acceptance: a model system listed as g 0.4, light 0.804 reads
        /// back 0.40016…, 0.80356…). The steps are the same today and named apart so one can change alone. Equal values are
        /// not the same glazing: the pane is also read back layer by layer (<see cref="Query.TasMaterialLayerDifferences"/>).
        /// </summary>
        internal const double GlazingGTolerance = global::SAM.Core.Tolerance.MacroDistance;

        /// <inheritdoc cref="GlazingGTolerance"/>
        internal const double GlazingUTolerance = global::SAM.Core.Tolerance.MacroDistance;

        /// <inheritdoc cref="GlazingGTolerance"/>
        internal const double GlazingLightTolerance = global::SAM.Core.Tolerance.MacroDistance;

        private static bool Close(double x, double y, double tolerance)
        {
            return System.Math.Abs(x - y) <= tolerance;
        }

        /// <summary>A TBD float as invariant text that reads back as the same float.</summary>
        private static string Text(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static Exception Innermost(Exception exception)
        {
            Exception result = exception;
            while ((result is AggregateException || result is System.Reflection.TargetInvocationException) && result.InnerException != null)
            {
                result = result.InnerException;
            }

            return result;
        }
    }
}
