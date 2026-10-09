// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Optimisation;
using System;
using System.Collections.Generic;
using System.IO;

namespace SAM.Analytical.Tas.GenOpt.Tests.Helpers
{
    /// <summary>
    /// Inventories shaped like the two PR7a/PR7a-2 models (no Tas needed): the Systems Demo ("Systems Training") and a
    /// SAM-generated Part O model. Names, profile shapes, the glazing and the baseline results are the ones the licensed
    /// spikes read; the other conditions are illustrative edge cases (no cooling, no heating, factor 2, unsupported).
    /// The glazing pool has the PR7a-2 values under generic names (EDSL database names are not committed).
    /// </summary>
    public static class TasModelFixtures
    {
        public const string DemoGlazing = "Suncool Example";
        public const string SamGlazing = "Windows: SIM_EXT_GLZ -pane";

        public static TasModelInventory Demo()
        {
            return new TasModelInventory(
                "Systems Training.tbd",
                "Systems Training.tsd",
                "Systems Training.tpd",
                new[]
                {
                    new TasInternalConditionInfo("Office Weekday", "Office", 17, new TasSetpointProfile(TasSetpointProfileType.Value, 1, 20), new TasSetpointProfile(TasSetpointProfileType.Value, 1, 24)),
                    new TasInternalConditionInfo("Office Weekend", "Office", 17, new TasSetpointProfile(TasSetpointProfileType.Value, 1, 12), new TasSetpointProfile(TasSetpointProfileType.Value, 1, 150)),
                    new TasInternalConditionInfo("Steady State Heating ", null, 26, new TasSetpointProfile(TasSetpointProfileType.Value, 1, 21), null),
                },
                new[]
                {
                    new TasGlazingConstructionInfo(DemoGlazing, new[] { "Lower Window-pane", "Upper Window-pane", "Curtain Wall-pane" }, 0.33669999241828918, 1.0549999475479126, 0.64200001955032349),
                },
                new[]
                {
                    new TasPlantRoomInfo("Plant Room", new[]
                    {
                        new TasPlantControllerInfo("HeatPumpController", "tpdTempSensor", 3),
                        new TasPlantControllerInfo("Controller 2", "tpdLoadSensor", 0.5),
                    }),
                })
            {
                TpdCostUnit = "£",
                HeatingDemand = 15227.8406637096,
                CoolingDemand = 2978.53252598965,
                PlantEnergy = 27387.79,
                PlantCost = 7363.17,
                PlantCO2 = 3932.89,
            };
        }

        public static TasModelInventory SamModel()
        {
            return new TasModelInventory(
                "000000_SAM_AnalyticalModel.tbd",
                "000000_SAM_AnalyticalModel.tsd",
                null,
                new[]
                {
                    new TasInternalConditionInfo("Kitchen", "Residential Kitchen - Kitchen", 1, new TasSetpointProfile(TasSetpointProfileType.Hourly, 1, 21, 12), new TasSetpointProfile(TasSetpointProfileType.Hourly, 1, 23, 12)),
                    new TasInternalConditionInfo("Kitchen - HDD", null, 1, new TasSetpointProfile(TasSetpointProfileType.Value, 1, 21), null),
                    new TasInternalConditionInfo("Store", "Store - Store", 1, new TasSetpointProfile(TasSetpointProfileType.Value, 1, -50), new TasSetpointProfile(TasSetpointProfileType.Value, 1, 150)),
                    new TasInternalConditionInfo("Hall", "Hall - Hall", 1, new TasSetpointProfile(TasSetpointProfileType.Value, 2, 21), new TasSetpointProfile(TasSetpointProfileType.Unsupported, 1, null)),
                },
                new[]
                {
                    new TasGlazingConstructionInfo(SamGlazing, new[] { "GLAZING 1", "GLAZING 2" }, 0.4001609981060028, 1.2433900833129883, 0.803563117980957),
                });
        }

        /// <summary>The PR7a-2 pool (values as calculated; generic names), in source order.</summary>
        public static List<TasGlazingSystem> Pool(bool attach = false)
        {
            List<TasGlazingSystem> result = new List<TasGlazingSystem>
            {
                System("Model glazing", "Model", 0.4001609981060028, 1.2433900833129883, 0.803563117980957, "aaaaaaaa-0000-0000-0000-000000b9d885"),
                System("Library glazing 7mm air", "Default library", 0.40626540780067444, 2.237656354904175, 0.803563117980957, "aaaaaaaa-0000-0000-0000-000000230343"),
                System("Library glazing sky", "Default library", 0.5478138327598572, 1.6901262998580933, 0.6099563241004944, "aaaaaaaa-0000-0000-0000-000000e95599"),
                System("Library single", "Default library", 0.7917975187301636, 5.555555820465088, 0.8700000047683716, "aaaaaaaa-0000-0000-0000-00000055cd3f"),
                System("Triple low-e", "My glazing systems", 0.36886733770370483, 0.997646152973175, 0.7281997203826904, "aaaaaaaa-0000-0000-0000-000000a5191c"),
                System("Solar control A", "Loaded file", 0.11761055886745453, 2.342884063720703, 0.07330752164125443, "aaaaaaaa-0000-0000-0000-00000036191d"),
                System("Double B", "Loaded file", 0.28368183970451355, 1.6993681192398071, 0.7965357303619385, "aaaaaaaa-0000-0000-0000-000000ff1c5e"),
                System("Double C", "Loaded file", 0.49532291293144226, 2.8311753273010254, 0.4378443956375122, "aaaaaaaa-0000-0000-0000-00000045ded9"),
            };

            return attach ? result.ConvertAll(Attach) : result;
        }

        /// <summary>
        /// The system with a SAM window system of its Guid and name attached (no layers), as a pool from SAM UI has it,
        /// so its TBD pane name can be derived. Nothing is written: the tests replace the runner's glazing writer.
        /// </summary>
        public static TasGlazingSystem Attach(TasGlazingSystem tasGlazingSystem)
        {
            tasGlazingSystem.ApertureConstruction = new ApertureConstruction(tasGlazingSystem.Guid, tasGlazingSystem.Name, ApertureType.Window);
            tasGlazingSystem.MaterialLibrary = new global::SAM.Core.MaterialLibrary("Test");
            return tasGlazingSystem;
        }

        public static TasGlazingSystem System(string name, string source, double g, double ug, double light, string guid, bool transparent = true, string apertureType = "Window")
        {
            return new TasGlazingSystem(Guid.Parse(guid), name, source, apertureType, transparent, g, ug, light);
        }

        /// <summary>A glazing option as the runner resolves it, with an explicit TBD pane name (no SAM aperture construction).</summary>
        public static TasGlazingOption Option(int number, string text, string pane, double g, double u, double light)
        {
            return new TasGlazingOption(number, text, pane, g, u, light, number == 1 ? "Model (current)" : "Test", null);
        }

        /// <summary>The SAM_Tas repository folder (the one holding SAM_Tas.sln and references_buildonly).</summary>
        public static string RepositoryRoot
        {
            get
            {
                DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
                while (directory != null && !global::System.IO.File.Exists(Path.Combine(directory.FullName, "SAM_Tas.sln")))
                {
                    directory = directory.Parent;
                }

                return directory?.FullName ?? throw new DirectoryNotFoundException("SAM_Tas.sln not found above " + AppContext.BaseDirectory);
            }
        }

        /// <summary>The SAM.Core.Optimisation fixtures, read from the sibling SAM checkout (as the golden traces are).</summary>
        public static string FixtureText(string fileName)
        {
            return global::System.IO.File.ReadAllText(Path.Combine(Path.GetDirectoryName(GoldenTrace.Directory_Golden), "Optimisation", fileName));
        }

        public static OptimisationDefinition Read(string text, out List<OptimisationDiagnostic> diagnostics)
        {
            return global::SAM.Core.Optimisation.Create.OptimisationDefinition(text, out diagnostics);
        }

        public static OptimisationDefinition Read(string text)
        {
            OptimisationDefinition result = Read(text, out List<OptimisationDiagnostic> diagnostics);
            if (result == null)
            {
                throw new InvalidOperationException(string.Join("\n", diagnostics));
            }

            return result;
        }
    }
}
