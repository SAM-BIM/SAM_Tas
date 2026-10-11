// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

// Native Optimisation PR7b-2 licensed acceptance driver (spike; not production code, not in SAM_Tas.sln).
//   sam <file.sam>                                   model summary: spaces, heating/cooling, apertures internal/external
//   tpd <file.tpd>                                   plant rooms, controllers, TSD data (opened read-only; pass a COPY)
//   catalogue <folder> [pool] [permissive]           TasModelInventory + catalogue (as PR7b)
//   test <folder> <definition.json> <v1,..> <runs> [pool] [permissive]
//   run  <folder> <definition.json> <runs> [pool] [permissive]
//   readers <evaluation folder> <kind,kind,...>      SAM_Tas readers on the evaluation's model.tsd vs its Output.txt
//          kinds in output order: heating | cooling | oh<threshold> | - (skip)
//   plant <evaluation folder> <work folder> <room|controller=value;...> <kind,...>
//          copies the evaluation's model.tbd/tsd/tpd to <work>, writes the controllers, simulates every plant room with the
//          generated script's call, SAVES, then reads the saved TPD with SAM_Tas SystemEnergyCentreResults and compares
//          with Output.txt. kinds: energy | cost | co2 | -
// "pool" = SAM default library + My glazing systems (TCD); "permissive" = no Ug/light limits.

using SAM.Analytical;
using SAM.Analytical.Systems;
using SAM.Analytical.Tas.GenOpt;
using SAM.Core;
using SAM.Core.Optimisation;
using SAM.Math;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using File = System.IO.File;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        switch (args[0])
        {
            case "sam": return Sam(args[1]);
            case "tpd": return Tpd(args[1]);
            case "readers": return Readers(args[1], args[2].Split(','));
            case "plant": return Plant(args[1], args[2], args[3], args[4].Split(','));
            case "zones": return Zones(args[1]);
        }

        string folder = args[1];
        bool usePool = args.Contains("pool");
        TasGlazingFilter filter = args.Contains("permissive") ? new TasGlazingFilter { UgAllowance = 100, LightAllowance = 1 } : null;
        List<TasGlazingSystem> pool = usePool ? Pool() : new List<TasGlazingSystem>();

        var watch = System.Diagnostics.Stopwatch.StartNew();
        TasModelInventory inventory = SAM.Analytical.Tas.GenOpt.Query.TasModelInventory(folder);
        Console.WriteLine("inventory read in " + watch.Elapsed.TotalSeconds.ToString("0.00") + " s");

        if (args[0] == "catalogue")
        {
            Console.WriteLine("files: " + inventory.TbdFileName + " | " + inventory.TsdFileName + " | " + inventory.TpdFileName + " costUnit=" + inventory.TpdCostUnit);
            foreach (TasInternalConditionInfo ic in inventory.InternalConditions)
            {
                Console.WriteLine("IC '" + ic.Name + "' zones=" + ic.ZoneCount + " heating=" + P(ic.Heating) + " cooling=" + P(ic.Cooling));
            }

            foreach (TasGlazingConstructionInfo g in inventory.GlazingConstructions)
            {
                Console.WriteLine("glazing '" + g.Name + "' elements=" + g.Elements.Count + " [" + string.Join("|", g.Elements) + "] g=" + R(g.G) + " U=" + R(g.U) + " light=" + R(g.Light));
                if (g.ZoneSurfaces != null)
                {
                    Console.WriteLine("   zone surfaces: " + string.Join(", ", g.ZoneSurfaces.Select(x => x.Key + "=" + x.Value)));
                }
            }

            foreach (TasPlantRoomInfo p in inventory.PlantRooms)
            {
                foreach (TasPlantControllerInfo c in p.Controllers)
                {
                    Console.WriteLine("controller '" + p.Name + "'/'" + c.Name + "' " + c.SensorType + " setpoint=" + R(c.Setpoint));
                }
            }

            Console.WriteLine("heating=" + R(inventory.HeatingDemand) + " cooling=" + R(inventory.CoolingDemand) + " plantEnergy=" + R(inventory.PlantEnergy) + " cost=" + R(inventory.PlantCost) + " co2=" + R(inventory.PlantCO2));
            foreach (TasGlazingSystem s in pool)
            {
                Console.WriteLine("pool " + s.Source + " '" + s.Name + "' " + s.ShortId + " " + s.ApertureType + " transparent=" + s.Transparent + " g=" + R(s.G) + " Ug=" + R(s.Ug) + " light=" + R(s.Light));
            }

            OptimisationCatalogue catalogue = SAM.Analytical.Tas.GenOpt.Query.TasModelCatalogue(inventory, pool, filter);
            foreach (OptimisationCatalogueEntry e in catalogue.Variables.Concat(catalogue.Outputs))
            {
                Console.WriteLine("entry '" + e.Name + "' value=" + R(e.Value) + " range=" + R(e.Minimum) + ".." + R(e.Maximum) + " " + e.Unit + " options=[" + string.Join(" | ", e.Options) + "]");
            }

            return 0;
        }

        OptimisationDefinition definition = SAM.Core.Optimisation.Create.OptimisationDefinition(File.ReadAllText(args[2]), out List<OptimisationDiagnostic> diagnostics);
        if (definition == null)
        {
            Console.WriteLine(string.Join("\n", diagnostics));
            return 1;
        }

        string runs = args[0] == "test" ? args[4] : args[3];
        TasModelRunSettings settings = new TasModelRunSettings(folder, runs) { Inventory = inventory, GlazingPool = pool, GlazingFilter = filter };
        TasModelRunner runner = new TasModelRunner(definition, settings);
        foreach (var pair in runner.GlazingOptions)
        {
            foreach (TasGlazingOption o in pair.Value)
            {
                Console.WriteLine("option " + pair.Key + " " + o.Summary + " pane '" + o.PaneConstruction + "'");
            }
        }

        if (args[0] == "test")
        {
            List<double> point = args[3].Split(',').Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToList();
            TasModelTest test = runner.Test(point);
            Console.WriteLine("test " + string.Join(",", point.Select(x => R(x))) + " ok=" + test.Evaluation.Succeeded + " " + test.Evaluation.Message + " seconds=" + test.Duration.TotalSeconds.ToString("0.00"));
            if (test.Evaluation.Succeeded)
            {
                for (int i = 0; i < test.Evaluation.Outputs.Count; i++)
                {
                    Console.WriteLine("  " + runner.Kernel.OutputNames[i] + " = " + R(test.Evaluation.Outputs[i]));
                }
            }

            Console.WriteLine("folder " + test.EvaluationDirectory);
            Dump(test.EvaluationDirectory);
            return test.Evaluation.Succeeded ? 0 : 1;
        }

        watch.Restart();
        NativeGenOptRun run = runner.Run();
        Console.WriteLine("run outcome=" + run.Result.Outcome + " in " + watch.Elapsed.TotalSeconds.ToString("0") + " s folder " + run.Workspace.RunDirectory);
        foreach (OptimisationTraceEntry entry in run.Result.Entries)
        {
            Console.WriteLine("  sim " + entry.Simulation + " " + entry.Event + " x=" + string.Join(",", entry.Coordinates.Select(x => R(x))) + " y=" + string.Join(",", entry.Outputs.Select(x => R(x))));
        }

        NativeGenOptOutcome outcome = new NativeGenOptOutcome(run, false);
        Console.WriteLine("best " + (outcome.BestEntry == null ? "-" : string.Join(",", outcome.BestEntry.Coordinates.Select(x => R(x))) + " y=" + string.Join(",", outcome.BestEntry.Outputs.Select(x => R(x)))));
        return 0;
    }

    private static int Sam(string path)
    {
        AnalyticalModel model = SAM.Core.Convert.ToSAM<AnalyticalModel>(path).OfType<AnalyticalModel>().Single();
        AdjacencyCluster cluster = model.AdjacencyCluster;
        Console.WriteLine("model '" + model.Name + "' sha256 " + SAM.Analytical.Tas.GenOpt.Query.FileHash(path));
        Console.WriteLine("spaces " + cluster.GetSpaces().Count + ", panels " + cluster.GetPanels().Count + ", apertures " + cluster.GetApertures().Count);
        foreach (Space space in cluster.GetSpaces().OrderBy(x => x.Name))
        {
            InternalCondition ic = space.InternalCondition;
            Profile heating = ic?.GetProfile(ProfileType.Heating, model.ProfileLibrary);
            Profile cooling = ic?.GetProfile(ProfileType.Cooling, model.ProfileLibrary);
            Console.WriteLine("space '" + space.Name + "' ic '" + ic?.Name + "' heating '" + heating?.Name + "' [" + Values(heating) + "] cooling '" + cooling?.Name + "' [" + Values(cooling) + "]");
        }

        int internalCount = 0;
        foreach (Panel panel in cluster.GetPanels())
        {
            List<Aperture> apertures = panel.Apertures;
            if (apertures == null || apertures.Count == 0)
            {
                continue;
            }

            List<Space> spaces = cluster.GetSpaces(panel) ?? new List<Space>();
            bool isInternal = spaces.Count > 1;
            if (isInternal)
            {
                internalCount += apertures.Count;
            }

            foreach (Aperture aperture in apertures)
            {
                Console.WriteLine((isInternal ? "INTERNAL " : "external ") + "aperture '" + aperture.Name + "' construction '" + aperture.ApertureConstruction?.Name + "' type " + aperture.ApertureType + " on panel '" + panel.Name + "' (" + panel.PanelType + ") spaces " + string.Join(" | ", spaces.Select(x => x.Name)));
            }
        }

        Console.WriteLine("internal apertures: " + internalCount);
        foreach (IGrouping<Guid, ApertureConstruction> group in cluster.GetApertureConstructions().GroupBy(x => x.Guid))
        {
            ApertureConstruction a = group.First();
            Console.WriteLine("aperture construction '" + a.Name + "' " + a.ApertureType + " apertures " + cluster.GetApertures(a).Count + " pane " + string.Join("/", (a.PaneConstructionLayers ?? new List<ConstructionLayer>()).Select(x => x.Name + " " + R(x.Thickness))));
        }

        Console.WriteLine("weather: " + (model.TryGetValue(SAM.Analytical.AnalyticalModelParameter.WeatherData, out SAM.Weather.WeatherData weather) ? weather?.Name : "-"));
        return 0;
    }

    private static string Values(Profile profile)
    {
        if (profile == null)
        {
            return "-";
        }

        double[] values = profile.GetValues();
        return values == null ? "-" : string.Join(" ", values.Take(24).Select(x => R(x))) + (values.Length > 24 ? " ...(" + values.Length + ")" : string.Empty);
    }

    private static int Tpd(string path)
    {
        TPD.TPDDoc document = new TPD.TPDDoc();
        try
        {
            document.OpenReadOnly(path);
            TPD.EnergyCentre energyCentre = document.EnergyCentre;
            Console.WriteLine("TPD " + path + " sha256 " + SAM.Analytical.Tas.GenOpt.Query.FileHash(path));
            Console.WriteLine("plant rooms " + energyCentre.GetPlantRoomCount() + ", TSD data " + energyCentre.GetTSDDataCount());
            for (int i = 1; i <= energyCentre.GetTSDDataCount(); i++)
            {
                TPD.TSDData tsdData = energyCentre.GetTSDData(i);
                Console.WriteLine("  TSD " + i + ": " + tsdData.TSDPath + " (TBD " + tsdData.TBDPath + ") exists=" + File.Exists(tsdData.TSDPath));
            }

            for (int i = 1; i <= energyCentre.GetPlantRoomCount(); i++)
            {
                TPD.PlantRoom plantRoom = energyCentre.GetPlantRoom(i);
                Console.WriteLine("  plant room " + i + " '" + plantRoom.Name + "': systems " + plantRoom.GetSystemCount() + ", components " + plantRoom.GetComponentCount() + ", controllers " + plantRoom.GetControllerCount());
                for (int j = 1; j <= plantRoom.GetControllerCount(); j++)
                {
                    TPD.PlantController c = plantRoom.GetController(j);
                    Console.WriteLine("    controller '" + c.Name + "' " + c.SensorType + " setpoint " + R(c.Setpoint));
                }
            }
        }
        finally
        {
            try { document.Close(); } catch (COMException) { }
            Marshal.FinalReleaseComObject(document);
        }

        return 0;
    }

    // zones <file.tbd>: each zone's sizing flags and capacities, and its internal conditions' thermostat lower limit
    // (opened and closed without saving; pass a COPY).
    private static int Zones(string path)
    {
        TBD.TBDDocument document = new TBD.TBDDocument();
        try
        {
            Console.WriteLine("TBD " + path + " sha256 " + SAM.Analytical.Tas.GenOpt.Query.FileHash(path));
            document.open(path);
            TBD.Building building = document.Building;
            for (int i = 0; building.GetZone(i) != null; i++)
            {
                TBD.zone zone = building.GetZone(i);
                List<string> conditions = new List<string>();
                for (int j = 0; zone.GetIC(j) != null; j++)
                {
                    TBD.InternalCondition ic = zone.GetIC(j);
                    TBD.profile lower = ic.GetThermostat()?.GetProfile((int)TBD.Profiles.ticLL);
                    conditions.Add("'" + ic.name + "' LL " + (lower == null ? "-" : lower.type + " value " + R(lower.value) + " factor " + R(lower.factor)));
                }

                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "zone '{0}': sizeHeating {1} maxHeatingLoad {2} sizeCooling {3} maxCoolingLoad {4} | {5}", zone.name, zone.sizeHeating, R(zone.maxHeatingLoad), zone.sizeCooling, R(zone.maxCoolingLoad), string.Join("; ", conditions)));
            }
        }
        finally
        {
            try { document.close(); } catch (COMException) { }
            Marshal.FinalReleaseComObject(document);
        }

        return 0;
    }

    private static Dictionary<string, double> OutputValues(string evaluation)
    {
        Dictionary<string, double> result = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (string line in File.ReadAllLines(Path.Combine(evaluation, "Output.txt")))
        {
            string[] parts = line.Split(new[] { "::" }, StringSplitOptions.None);
            if (parts.Length >= 2 && double.TryParse(parts[parts.Length - 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                result[parts[parts.Length - 2]] = value;
            }
        }

        return result;
    }

    private static int Readers(string evaluation, string[] kinds)
    {
        Dictionary<string, double> outputs = OutputValues(evaluation);
        string tsd = Path.Combine(evaluation, "model.tsd");
        Console.WriteLine("evaluation " + evaluation + " model.tsd sha256 " + SAM.Analytical.Tas.GenOpt.Query.FileHash(tsd));
        AnalyticalModelSimulationResult result = SAM.Analytical.Tas.Convert.ToSAM_AnalyticalModelSimulationResult(tsd, new AnalyticalModel(Guid.NewGuid(), "reader"));
        result.TryGetValue(AnalyticalModelSimulationResultParameter.ConsumptionHeating, out double heatingWh);
        result.TryGetValue(AnalyticalModelSimulationResultParameter.ConsumptionCooling, out double coolingWh);
        Console.WriteLine("SAM_Tas ToSAM_AnalyticalModelSimulationResult: ConsumptionHeating " + R(heatingWh) + " (labelled kWh, holds Wh), ConsumptionCooling " + R(coolingWh));

        Dictionary<double, (int Hours, string Zone)> worst = new Dictionary<double, (int, string)>();
        List<double> thresholds = kinds.Where(x => x.StartsWith("oh")).Select(x => double.Parse(x.Substring(2), CultureInfo.InvariantCulture)).ToList();
        if (thresholds.Count > 0)
        {
            TSD.TSDDocument document = new TSD.TSDDocument();
            try
            {
                document.openReadOnly(tsd);
                TSD.BuildingData buildingData = document.SimulationData.GetBuildingData();
                foreach (double t in thresholds)
                {
                    worst[t] = (0, "-");
                }

                for (int z = 1; z <= buildingData.zoneCount; z++)
                {
                    TSD.ZoneData zoneData = buildingData.GetZoneData(z);
                    Dictionary<SpaceSimulationResultParameter, object> overheating = SAM.Analytical.Tas.Query.Overheating(zoneData, 1, 365);
                    if (overheating == null || (int)overheating[SpaceSimulationResultParameter.OccupiedHours] == 0)
                    {
                        continue;
                    }

                    foreach (double t in thresholds)
                    {
                        SpaceSimulationResultParameter parameter = t == 25 ? SpaceSimulationResultParameter.OccupiedHours25 : t == 28 ? SpaceSimulationResultParameter.OccupiedHours28 : throw new NotSupportedException("SAM_Tas Query.Overheating reports 25 and 28 only");
                        int hours = (int)overheating[parameter];
                        if (hours > worst[t].Hours)
                        {
                            worst[t] = (hours, zoneData.name);
                        }
                    }
                }
            }
            finally
            {
                try { document.close(); } catch (COMException) { }
                Marshal.FinalReleaseComObject(document);
            }
        }

        bool pass = true;
        for (int i = 0; i < kinds.Length; i++)
        {
            string kind = kinds[i];
            if (kind == "-")
            {
                continue;
            }

            double script = outputs["Y" + (i + 1)];
            double reader;
            string how;
            if (kind == "heating" || kind == "cooling")
            {
                double wh = kind == "heating" ? heatingWh : coolingWh;
                reader = wh / 1000;
                how = "reader Wh " + R(wh) + " / 1000";
            }
            else
            {
                double t = double.Parse(kind.Substring(2), CultureInfo.InvariantCulture);
                reader = worst[t].Hours;
                how = "SAM_Tas Query.Overheating OccupiedHours" + t + " worst occupied zone '" + worst[t].Zone + "'";
            }

            double difference = script - reader;
            double relative = reader == 0 ? Math.Abs(difference) : Math.Abs(difference / reader);
            bool ok = kind.StartsWith("oh") ? difference == 0 : relative <= 1e-9;
            pass &= ok;
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Y{0} {1}: script {2} | reader {3} ({4}) | difference {5} relative {6} -> {7}", i + 1, kind, R(script), R(reader), how, R(difference), R(relative), ok ? "PASS" : "FAIL"));
        }

        Console.WriteLine(pass ? "READERS PASS" : "READERS FAIL");
        return pass ? 0 : 1;
    }

    private static int Plant(string evaluation, string work, string controllers, string[] kinds)
    {
        Dictionary<string, double> outputs = OutputValues(evaluation);
        Directory.CreateDirectory(work);
        foreach (string name in new[] { "model.tbd", "model.tsd", "model.tpd" })
        {
            File.Copy(Path.Combine(evaluation, name), Path.Combine(work, name), true);
        }

        string tbdPath = Path.Combine(work, "model.tbd");
        string tsdPath = Path.Combine(work, "model.tsd");
        string tpdPath = Path.Combine(work, "model.tpd");

        // As the generated script (F1): the TSD names the evaluation's TBD; point it at this copy.
        TSD.TSDDocument tsdFix = new TSD.TSDDocument();
        tsdFix.open(tsdPath);
        tsdFix.SimulationData.buildingPath = tbdPath;
        tsdFix.save();
        tsdFix.close();
        Marshal.FinalReleaseComObject(tsdFix);

        TPD.TPDDoc document = new TPD.TPDDoc();
        document.Open(tpdPath);
        TPD.EnergyCentre energyCentre = document.EnergyCentre;
        energyCentre.GetTSDData(1).FixTSDPath(tsdPath);
        foreach (string assignment in controllers.Split(';').Where(x => x.Length > 0))
        {
            string[] parts = assignment.Split('=');
            string[] names = parts[0].Split('|');
            double value = double.Parse(parts[1], CultureInfo.InvariantCulture);
            int found = 0;
            for (int i = 1; i <= energyCentre.GetPlantRoomCount(); i++)
            {
                TPD.PlantRoom plantRoom = energyCentre.GetPlantRoom(i);
                if (plantRoom.Name != names[0]) continue;
                for (int j = 1; j <= plantRoom.GetControllerCount(); j++)
                {
                    TPD.PlantController c = plantRoom.GetController(j);
                    if (c.Name != names[1]) continue;
                    c.Setpoint = value;
                    found++;
                    Console.WriteLine("controller '" + names[0] + "'/'" + names[1] + "' = " + R(c.Setpoint));
                }
            }

            if (found != 1) throw new Exception("controller " + parts[0] + " found " + found + " times");
        }

        for (int i = 1; i <= energyCentre.GetPlantRoomCount(); i++)
        {
            energyCentre.GetPlantRoom(i).SimulateEx(1, 8760, 0, energyCentre.ExternalPollutant.Value, 10.0, (int)TPD.tpdSimulationData.tpdSimulationDataLoad + (int)TPD.tpdSimulationData.tpdSimulationDataPipe, 0, 0);
        }

        document.Save();
        try { document.Close(); } catch (COMException e) { Console.WriteLine("TPD close fault ignored (F2): " + e.Message); }
        Marshal.FinalReleaseComObject(document);
        Console.WriteLine("simulated and saved " + tpdPath + " sha256 " + SAM.Analytical.Tas.GenOpt.Query.FileHash(tpdPath));

        List<SystemEnergyCentreDataType> types = new List<SystemEnergyCentreDataType> { SystemEnergyCentreDataType.Consumption, SystemEnergyCentreDataType.Cost, SystemEnergyCentreDataType.Co2 };
        List<SystemEnergyCentreResult> results = SAM.Core.Tas.TPD.Query.SystemEnergyCentreResults(tpdPath, SAM.Core.Tas.TPD.ResultPeriod.Annual, types);
        Dictionary<SystemEnergyCentreDataType, double> sums = new Dictionary<SystemEnergyCentreDataType, double>();
        foreach (SystemEnergyCentreResult result in results)
        {
            double sum = 0;
            foreach (SystemEnergyCentreValues values in result.SystemEnergyCentreValues)
            {
                sum += values[values.GetMinIndex() ?? 0];
                Console.WriteLine("  " + result.SystemEnergyCentreDataType + " item '" + values.Name + "' category '" + values.Category + "' unit '" + values.UnitName + "' value[0] " + R(values[values.GetMinIndex() ?? 0]) + " (" + values.Count + " values)");
            }

            sums[result.SystemEnergyCentreDataType] = sum;
        }

        bool pass = true;
        for (int i = 0; i < kinds.Length; i++)
        {
            if (kinds[i] == "-") continue;
            SystemEnergyCentreDataType type = kinds[i] == "energy" ? SystemEnergyCentreDataType.Consumption : kinds[i] == "cost" ? SystemEnergyCentreDataType.Cost : SystemEnergyCentreDataType.Co2;
            double script = outputs["Y" + (i + 1)];
            double reader = sums[type];
            double relative = reader == 0 ? Math.Abs(script - reader) : Math.Abs((script - reader) / reader);
            bool ok = relative <= 1e-9;
            pass &= ok;
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Y{0} {1}: script {2} | SAM_Tas SystemEnergyCentreResults sum {3} | difference {4} relative {5} -> {6}", i + 1, kinds[i], R(script), R(reader), R(script - reader), R(relative), ok ? "PASS" : "FAIL"));
        }

        Console.WriteLine(pass ? "PLANT READERS PASS" : "PLANT READERS FAIL");
        return pass ? 0 : 1;
    }

    private static void Dump(string evaluation)
    {
        foreach (string name in new[] { "Output.txt", "Error.txt", "tas-model-trace.txt" })
        {
            string path = Path.Combine(evaluation, name);
            if (File.Exists(path))
            {
                Console.WriteLine("---- " + name);
                Console.WriteLine(File.ReadAllText(path));
            }
        }
    }

    private static List<TasGlazingSystem> Pool()
    {
        List<TasGlazingSystem> result = new List<TasGlazingSystem>();
        ApertureConstructionLibrary library = SAM.Analytical.Query.DefaultApertureConstructionLibrary();
        result.AddRange(SAM.Analytical.Tas.GenOpt.Query.TasGlazingSystems(new ConstructionManager(library.GetApertureConstructions(), null, SAM.Analytical.Query.DefaultMaterialLibrary()), "Default library"));
        string user = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SAM", "User Libraries", "Glazing Systems.json");
        if (File.Exists(user))
        {
            ConstructionManager constructionManager = SAM.Core.Convert.ToSAM<ConstructionManager>(user).FirstOrDefault();
            result.AddRange(SAM.Analytical.Tas.GenOpt.Query.TasGlazingSystems(constructionManager, "My glazing systems"));
        }

        return result;
    }

    private static string P(TasSetpointProfile p) => p == null ? "-" : p.Type + "/f" + R(p.Factor) + "/" + R(p.Setpoint) + "/h" + p.ChangedHours;

    private static string R(double? v) => v == null ? "-" : v.Value.ToString("R", CultureInfo.InvariantCulture);
}
