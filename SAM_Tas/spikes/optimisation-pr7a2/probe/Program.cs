// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

// Native Optimisation PR7a-2 spike probe (glazing; not production code, not in SAM_Tas.sln).
//
//   Pr7a2Probe pool <out.tsv> <sources>
//       builds the glazing pool as the SAM UI Glazing window does (model, default library, "My glazing systems"),
//       calculates every system with SAM_Tas' ThermalTransmittanceCalculator.CalculateGlazing (Tas TCD) and, in a
//       second TCD run, reads the unrounded TCD values of the same "-pane"/"-frame" constructions.
//   Pr7a2Probe write <tbd> <sources> --guids <g1,g2,...> [--rename] [--save]
//       writes the chosen systems into the TBD (a COPY) with SAM_Tas' Modify.UpdateConstructions(building,
//       apertureConstructions, materialLibrary) - the code Modify.AddUnusedConstructions calls - and compares the
//       g-value the TBD reports for each "-pane" construction with the TCD's, layer by layer when they differ.
//   Pr7a2Probe constructions <tbd>
//       lists every construction past the GetConstruction(i) null gaps, with g, U and the elements using it.
//
//   <sources>: any of --model <file.sam>  --library  --user <Glazing Systems.json>  --loaded <SAM_UI TCD cache .json>
//
// Always run it on COPIES of the Tas files, from PowerShell (TCD needs an STA thread and a quoted path).

using SAM.Analytical;
using SAM.Analytical.Tas;
using SAM.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Pr7a2Probe
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            if (args.Length < 2)
            {
                Console.Error.WriteLine("usage: pool <out.tsv> <sources> | write <tbd> <sources> --guids <g,...> [--rename] [--save] | constructions <tbd>");
                return 2;
            }

            switch (args[0])
            {
                case "pool":
                    Pool.Run(args[1], Sources.Parse(args));
                    return 0;
                case "write":
                    Write.Run(args[1], Sources.Parse(args), Option(args, "--guids").Split(',').Select(Guid.Parse).ToList(), args.Contains("--rename"), args.Contains("--save"));
                    return 0;
                case "constructions":
                    Tbd.List(args[1]);
                    return 0;
                default:
                    Console.Error.WriteLine("unknown command " + args[0]);
                    return 2;
            }
        }

        internal static string Option(string[] args, string name)
        {
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        internal static string R(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        internal static float[] Floats(object @object)
        {
            if (@object is IEnumerable enumerable)
            {
                return enumerable.Cast<object>().Select(x => System.Convert.ToSingle(x, CultureInfo.InvariantCulture)).ToArray();
            }

            return null;
        }

        internal static string Join(float[] values)
        {
            return values == null ? "-" : "[" + string.Join(",", values.Select(x => R(x))) + "]";
        }

        internal static void Release(object @object)
        {
            if (@object != null && Marshal.IsComObject(@object))
            {
                Marshal.FinalReleaseComObject(@object);
            }
        }
    }

    /// <summary>One source of the pool, built exactly as SAM_UI's GlazingSource factories build it.</summary>
    internal sealed class Source
    {
        public Source(string kind, ConstructionManager constructionManager)
        {
            Kind = kind;
            ConstructionManager = constructionManager;
        }

        public string Kind { get; }

        public ConstructionManager ConstructionManager { get; }
    }

    internal static class Sources
    {
        // Pool order = SAM_UI GlazingSource.Rank: model, default library, "My glazing systems"; the first source of a Guid wins.
        public static List<Source> Parse(string[] args)
        {
            List<Source> result = new List<Source>();

            string model = Program.Option(args, "--model");
            if (model != null)
            {
                AnalyticalModel analyticalModel = SAM.Core.Convert.ToSAM<AnalyticalModel>(model).FirstOrDefault();
                MaterialLibrary materialLibrary = analyticalModel?.MaterialLibrary ?? new MaterialLibrary("Default MaterialLibrary");
                List<ApertureConstruction> apertureConstructions = new List<ApertureConstruction>();
                HashSet<Guid> guids = new HashSet<Guid>();
                foreach (ApertureConstruction apertureConstruction in analyticalModel?.AdjacencyCluster?.GetApertureConstructions() ?? new List<ApertureConstruction>())
                {
                    if (apertureConstruction != null && guids.Add(apertureConstruction.Guid))
                    {
                        apertureConstructions.Add(apertureConstruction);
                    }
                }

                result.Add(new Source("Model", new ConstructionManager(apertureConstructions, null, materialLibrary)));
            }

            if (args.Contains("--library"))
            {
                ApertureConstructionLibrary apertureConstructionLibrary = SAM.Analytical.Query.DefaultApertureConstructionLibrary();
                MaterialLibrary materialLibrary = SAM.Analytical.Query.DefaultMaterialLibrary();
                result.Add(new Source("Library", new ConstructionManager(apertureConstructionLibrary?.GetApertureConstructions(), null, materialLibrary)));
            }

            string user = Program.Option(args, "--user");
            if (user != null)
            {
                ConstructionManager constructionManager = File.Exists(user) ? SAM.Core.Convert.ToSAM<ConstructionManager>(user).FirstOrDefault() : null;
                result.Add(new Source("User", constructionManager ?? new ConstructionManager()));
            }

            // A Tas construction database as "Load more glazing..." reads it: SAM_UI caches the SAM_Tas conversion of a
            // .tcd as JSON; its transparent constructions become window systems (pane layers only, no frame; the
            // additional heat transfer carried to pane and frame), keeping the construction's Guid.
            string loaded = Program.Option(args, "--loaded");
            if (loaded != null)
            {
                ConstructionManager constructionManager = SAM.Core.Convert.ToSAM<ConstructionManager>(loaded).FirstOrDefault() ?? new ConstructionManager();
                MaterialLibrary materialLibrary = constructionManager.MaterialLibrary;
                List<ApertureConstruction> apertureConstructions = new List<ApertureConstruction>();
                foreach (Construction construction in constructionManager.Constructions ?? new List<Construction>())
                {
                    if (construction == null || !construction.Transparent(materialLibrary))
                    {
                        continue;
                    }

                    ApertureConstruction apertureConstruction = new ApertureConstruction(construction.Guid, construction.Name, ApertureType.Window, construction.ConstructionLayers, null);
                    if (construction.TryGetValue(SAM.Analytical.ConstructionParameter.Description, out string description) && description != null)
                    {
                        apertureConstruction.SetValue(ApertureConstructionParameter.Description, description);
                    }

                    if (construction.TryGetValue(SAM.Analytical.Tas.ConstructionParameter.AdditionalHeatTransfer, out double additionalHeatTransfer) && !double.IsNaN(additionalHeatTransfer) && additionalHeatTransfer != 0)
                    {
                        apertureConstruction.SetValue(ApertureConstructionParameter.PaneAdditionalHeatTransfer, additionalHeatTransfer);
                        apertureConstruction.SetValue(ApertureConstructionParameter.FrameAdditionalHeatTransfer, additionalHeatTransfer);
                    }

                    apertureConstructions.Add(apertureConstruction);
                }

                result.Add(new Source("Loaded", new ConstructionManager(apertureConstructions, null, materialLibrary)));
            }

            return result;
        }

        /// <summary>Every window system of the pool, de-duplicated by Guid (first source wins), with its source.</summary>
        public static List<Tuple<Source, ApertureConstruction>> Candidates(List<Source> sources)
        {
            List<Tuple<Source, ApertureConstruction>> result = new List<Tuple<Source, ApertureConstruction>>();
            HashSet<Guid> guids = new HashSet<Guid>();
            foreach (Source source in sources)
            {
                foreach (ApertureConstruction apertureConstruction in source.ConstructionManager?.ApertureConstructions ?? new List<ApertureConstruction>())
                {
                    if (apertureConstruction != null && apertureConstruction.ApertureType == ApertureType.Window && guids.Add(apertureConstruction.Guid))
                    {
                        result.Add(Tuple.Create(source, apertureConstruction));
                    }
                }
            }

            return result;
        }

        /// <summary>The TBD construction names SAM_Tas' Modify.UpdateConstructions(building, apertureConstructions, ...) writes.</summary>
        public static string TbdBaseName(ApertureConstruction apertureConstruction)
        {
            return SAM.Analytical.Tas.Query.Name(apertureConstruction.UniqueName(), true, true, false, false);
        }
    }

    internal static class Layers
    {
        // The same properties on a TBD or a TCD material (the two interops expose the same names).
        public static string Describe(dynamic material, float constructionWidth)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "name='{0}' type={1} width={2} materialWidth={3} tau={4} rhoE={5} rhoI={6} epsE={7} epsI={8} LT={9} LRE={10} LRI={11} k={12} conv={13} visc={14} rho={15} cp={16} blind={17}",
                (string)material.name, (int)material.type, Program.R(material.width), Program.R(constructionWidth), Program.R(material.solarTransmittance),
                Program.R(material.externalSolarReflectance), Program.R(material.internalSolarReflectance), Program.R(material.externalEmissivity),
                Program.R(material.internalEmissivity), Program.R(material.lightTransmittance), Program.R(material.externalLightReflectance),
                Program.R(material.internalLightReflectance), Program.R(material.conductivity), Program.R(material.convectionCoefficient),
                Program.R(material.dynamicViscosity), Program.R(material.density), Program.R(material.specificHeat), System.Convert.ToInt32(material.isBlind));
        }

        public static List<string> Tbd(TBD.Construction construction)
        {
            List<string> result = new List<string>();
            for (int j = 1; j < 50; j++)
            {
                TBD.material material;
                try
                {
                    material = construction.materials(j);
                }
                catch
                {
                    break;
                }

                if (material == null)
                {
                    break;
                }

                result.Add(Describe(material, construction.materialWidth[j]));
            }

            return result;
        }

        public static List<string> Tcd(TCD.Construction construction)
        {
            List<string> result = new List<string>();
            for (int j = 1; j < 50; j++)
            {
                TCD.material material;
                try
                {
                    material = construction.materials(j);
                }
                catch
                {
                    break;
                }

                if (material == null)
                {
                    break;
                }

                result.Add(Describe(material, construction.materialWidth[j]));
            }

            return result;
        }
    }

    /// <summary>The raw TCD values of one system's "-pane" / "-frame" constructions (what CalculateGlazing reads, unrounded).</summary>
    internal sealed class TcdValues
    {
        public string PaneName;
        public float[] PaneGlazing;
        public float[] PaneU;
        public float PaneAdditionalHeatTransfer;
        public List<string> PaneLayers;
        public string FrameName;
        public float[] FrameU;
        public List<string> FrameLayers;

        public static Dictionary<Guid, TcdValues> Calculate(Source source, IEnumerable<ApertureConstruction> apertureConstructions)
        {
            Dictionary<Guid, TcdValues> result = new Dictionary<Guid, TcdValues>();
            SAM.Analytical.Tas.Modify.Run(new Action<TCD.Document>(document =>
            {
                foreach (ApertureConstruction apertureConstruction in apertureConstructions)
                {
                    List<TCD.Construction> constructions = apertureConstruction.ToTCD_Constructions(document, source.ConstructionManager);
                    if (constructions == null)
                    {
                        continue;
                    }

                    TcdValues values = new TcdValues();
                    TCD.Construction pane = constructions.Find(x => x.name.EndsWith("-pane"));
                    if (pane != null)
                    {
                        values.PaneName = pane.name;
                        values.PaneGlazing = pane.type == TCD.ConstructionTypes.tcdTransparentConstruction ? Program.Floats(pane.GetGlazingValues()) : null;
                        values.PaneU = Program.Floats(pane.GetUValue());
                        values.PaneAdditionalHeatTransfer = pane.additionalHeatTransfer;
                        values.PaneLayers = Layers.Tcd(pane);
                    }

                    TCD.Construction frame = constructions.Find(x => x.name.EndsWith("-frame"));
                    if (frame != null)
                    {
                        values.FrameName = frame.name;
                        values.FrameU = Program.Floats(frame.GetUValue());
                        values.FrameLayers = Layers.Tcd(frame);
                    }

                    result[apertureConstruction.Guid] = values;
                }
            }));

            return result;
        }
    }

    internal static class Pool
    {
        public static void Run(string output, List<Source> sources)
        {
            List<Tuple<Source, ApertureConstruction>> candidates = Sources.Candidates(sources);
            List<string> lines = new List<string>
            {
                string.Join("\t", "source", "guid", "name", "tbdPane", "transparent", "hasFrame", "g", "lightTransmittance", "Ug", "Uf", "tcdG", "tcdLT", "tcdU6", "tcdGlazing", "tcdPaneU", "tcdFrameU", "paneBuildUp", "frameBuildUp"),
            };

            foreach (Source source in sources)
            {
                List<ApertureConstruction> apertureConstructions = candidates.Where(x => x.Item1 == source).Select(x => x.Item2).ToList();
                Stopwatch stopwatch = Stopwatch.StartNew();
                List<GlazingCalculationResult> results = new ThermalTransmittanceCalculator(source.ConstructionManager).CalculateGlazing(apertureConstructions.Select(x => x.Guid));
                double seconds = stopwatch.Elapsed.TotalSeconds;
                stopwatch.Restart();
                Dictionary<Guid, TcdValues> raw = TcdValues.Calculate(source, apertureConstructions);
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "source {0}: {1} window systems, CalculateGlazing {2:0.000} s ({3} results), raw TCD run {4:0.000} s",
                    source.Kind, apertureConstructions.Count, seconds, results?.Count ?? 0, stopwatch.Elapsed.TotalSeconds));

                foreach (ApertureConstruction apertureConstruction in apertureConstructions)
                {
                    GlazingCalculationResult result = results?.Find(x => x.Reference == apertureConstruction.Guid.ToString());
                    raw.TryGetValue(apertureConstruction.Guid, out TcdValues values);
                    double uf = result is ApertureGlazingCalculationResult apertureGlazingCalculationResult ? apertureGlazingCalculationResult.FrameThermalTransmittance : double.NaN;
                    lines.Add(string.Join("\t",
                        source.Kind,
                        apertureConstruction.Guid,
                        apertureConstruction.Name,
                        SAM.Analytical.Query.PaneApertureConstructionUniqueName(Sources.TbdBaseName(apertureConstruction)),
                        apertureConstruction.Transparent(source.ConstructionManager.MaterialLibrary),
                        apertureConstruction.HasFrameConstructionLayers(),
                        result == null ? "-" : Program.R(result.TotalSolarEnergyTransmittance),
                        result == null ? "-" : Program.R(result.LightTransmittance),
                        result == null ? "-" : Program.R(result.ThermalTransmittance),
                        Program.R(uf),
                        values?.PaneGlazing == null ? "-" : Program.R(values.PaneGlazing[5]),
                        values?.PaneGlazing == null ? "-" : Program.R(values.PaneGlazing[0]),
                        values?.PaneU == null || values.PaneU.Length < 7 ? "-" : Program.R(values.PaneU[6]),
                        Program.Join(values?.PaneGlazing),
                        Program.Join(values?.PaneU),
                        Program.Join(values?.FrameU),
                        BuildUp(apertureConstruction.PaneConstructionLayers),
                        BuildUp(apertureConstruction.FrameConstructionLayers)));
                }
            }

            File.WriteAllLines(output, lines);
            Console.WriteLine("wrote " + (lines.Count - 1) + " rows to " + output);
        }

        private static string BuildUp(List<ConstructionLayer> constructionLayers)
        {
            return constructionLayers == null || constructionLayers.Count == 0 ? "-" : string.Join(" / ", constructionLayers.Select(x => string.Format(CultureInfo.InvariantCulture, "{0:0.#} {1}", x.Thickness * 1000, x.Name)));
        }
    }

    internal static class Tbd
    {
        /// <summary>Every construction of the building: GetConstruction(i) has null gaps, so scan past them.</summary>
        public static List<Tuple<int, TBD.Construction>> Constructions(TBD.Building building)
        {
            List<Tuple<int, TBD.Construction>> result = new List<Tuple<int, TBD.Construction>>();
            int nulls = 0;
            for (int i = 0; nulls < 500; i++)
            {
                TBD.Construction construction = building.GetConstruction(i);
                if (construction == null)
                {
                    nulls++;
                    continue;
                }

                nulls = 0;
                result.Add(Tuple.Create(i, construction));
            }

            return result;
        }

        public static Dictionary<string, List<string>> Uses(TBD.Building building)
        {
            Dictionary<string, List<string>> result = new Dictionary<string, List<string>>();
            for (int i = 0; ; i++)
            {
                TBD.buildingElement element = building.GetBuildingElement(i);
                if (element == null)
                {
                    break;
                }

                string name = element.GetConstruction()?.name ?? "-";
                if (!result.TryGetValue(name, out List<string> elements))
                {
                    result[name] = elements = new List<string>();
                }

                elements.Add(element.name + " (" + (TBD.BuildingElementType)element.BEType + ")");
            }

            return result;
        }

        public static void List(string path)
        {
            TBD.TBDDocument document = new TBD.TBDDocument();
            try
            {
                document.openReadOnly(path);
                TBD.Building building = document.Building;
                Dictionary<string, List<string>> uses = Uses(building);
                foreach (Tuple<int, TBD.Construction> tuple in Constructions(building))
                {
                    TBD.Construction construction = tuple.Item2;
                    uses.TryGetValue(construction.name, out List<string> elements);
                    float[] glazing = construction.type == TBD.ConstructionTypes.tcdTransparentConstruction ? Program.Floats(construction.GetGlazingValues()) : null;
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "construction[{0}] '{1}' {2} g={3} U={4} aht={5} elements=[{6}]",
                        tuple.Item1, construction.name, construction.type, glazing == null ? "-" : Program.R(glazing[5]), Program.Join(Program.Floats(construction.GetUValue())),
                        Program.R(construction.additionalHeatTransfer), elements == null ? "" : string.Join(" | ", elements)));
                }
            }
            finally
            {
                document.close();
                Program.Release(document);
            }
        }
    }

    internal static class Write
    {
        public static void Run(string tbd, List<Source> sources, List<Guid> guids, bool rename, bool save)
        {
            List<Tuple<Source, ApertureConstruction>> candidates = Sources.Candidates(sources);
            List<Tuple<Source, ApertureConstruction>> chosen = new List<Tuple<Source, ApertureConstruction>>();
            foreach (Guid guid in guids)
            {
                Tuple<Source, ApertureConstruction> candidate = candidates.Find(x => x.Item2.Guid == guid);
                if (candidate == null)
                {
                    throw new Exception("System " + guid + " is not in the pool.");
                }

                ApertureConstruction apertureConstruction = candidate.Item2;
                if (rename)
                {
                    // A name no other system of the TBD has: the system's name plus the last 6 characters of its Guid
                    // (SAM_UI's GlazingCandidate.ShortId), so same-named systems cannot overwrite each other.
                    apertureConstruction = new ApertureConstruction(apertureConstruction.Guid, apertureConstruction, apertureConstruction.Name + " " + apertureConstruction.Guid.ToString().Substring(30));
                }

                chosen.Add(Tuple.Create(candidate.Item1, apertureConstruction));
            }

            // TCD reference values first (TCD and TBD are not open together).
            Dictionary<Guid, TcdValues> tcd = new Dictionary<Guid, TcdValues>();
            Dictionary<Guid, double> calculator = new Dictionary<Guid, double>();
            foreach (IGrouping<Source, Tuple<Source, ApertureConstruction>> group in chosen.GroupBy(x => x.Item1))
            {
                foreach (KeyValuePair<Guid, TcdValues> pair in TcdValues.Calculate(group.Key, group.Select(x => x.Item2)))
                {
                    tcd[pair.Key] = pair.Value;
                }

                foreach (GlazingCalculationResult result in new ThermalTransmittanceCalculator(group.Key.ConstructionManager).CalculateGlazing(group.Select(x => x.Item2.Guid)) ?? new List<GlazingCalculationResult>())
                {
                    calculator[Guid.Parse(result.Reference)] = result.TotalSolarEnergyTransmittance;
                }
            }

            TBD.TBDDocument document = new TBD.TBDDocument();
            try
            {
                document.open(tbd);
                TBD.Building building = document.Building;
                HashSet<string> before = new HashSet<string>(Tbd.Constructions(building).Select(x => x.Item2.name));
                Dictionary<string, List<string>> uses = Tbd.Uses(building);
                Console.WriteLine("TBD constructions before: " + before.Count);

                Stopwatch stopwatch = Stopwatch.StartNew();
                foreach (IGrouping<Source, Tuple<Source, ApertureConstruction>> group in chosen.GroupBy(x => x.Item1))
                {
                    // The code Modify.AddUnusedConstructions(building, adjacencyCluster, materialLibrary) runs for the
                    // aperture constructions held in a cluster.
                    List<ApertureConstruction> written = building.UpdateConstructions(group.Select(x => x.Item2), group.Key.ConstructionManager.MaterialLibrary);
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "UpdateConstructions ({0}): {1} of {2} systems written", group.Key.Kind, written?.Count ?? 0, group.Count()));
                }

                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "write: {0:0.000} s", stopwatch.Elapsed.TotalSeconds));

                foreach (Tuple<Source, ApertureConstruction> tuple in chosen)
                {
                    ApertureConstruction apertureConstruction = tuple.Item2;
                    string baseName = Sources.TbdBaseName(apertureConstruction);
                    string paneName = SAM.Analytical.Query.PaneApertureConstructionUniqueName(baseName);
                    string frameName = SAM.Analytical.Query.FrameApertureConstructionUniqueName(baseName);
                    TBD.Construction pane = building.GetConstructionByName(paneName);
                    TBD.Construction frame = building.GetConstructionByName(frameName);
                    tcd.TryGetValue(apertureConstruction.Guid, out TcdValues values);
                    calculator.TryGetValue(apertureConstruction.Guid, out double gCalculator);

                    float[] glazing = pane != null && pane.type == TBD.ConstructionTypes.tcdTransparentConstruction ? Program.Floats(pane.GetGlazingValues()) : null;
                    float gTbd = glazing == null ? float.NaN : glazing[5];
                    float gTcd = values?.PaneGlazing == null ? float.NaN : values.PaneGlazing[5];
                    Console.WriteLine();
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "system {0} '{1}' ({2})", apertureConstruction.Guid, apertureConstruction.Name, tuple.Item1.Kind));
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  pane  '{0}' {1}; used by [{2}]", paneName, pane == null ? "MISSING" : before.Contains(paneName) ? "REUSED (existed before)" : "created",
                        uses.TryGetValue(paneName, out List<string> paneUses) ? string.Join(" | ", paneUses) : ""));
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  frame '{0}' {1}; used by [{2}]", frameName, frame == null ? "none" : before.Contains(frameName) ? "REUSED (existed before)" : "created",
                        uses.TryGetValue(frameName, out List<string> frameUses) ? string.Join(" | ", frameUses) : ""));
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  g: calculator {0}  tcd {1}  tbd {2}  tbd-tcd {3}", Program.R(gCalculator), Program.R(gTcd), Program.R(gTbd), Program.R((double)gTbd - gTcd)));
                    Console.WriteLine("  glazing tcd " + Program.Join(values?.PaneGlazing));
                    Console.WriteLine("  glazing tbd " + Program.Join(glazing));
                    Console.WriteLine("  paneU   tcd " + Program.Join(values?.PaneU) + " aht " + (values == null ? "-" : Program.R(values.PaneAdditionalHeatTransfer)));
                    Console.WriteLine("  paneU   tbd " + (pane == null ? "-" : Program.Join(Program.Floats(pane.GetUValue())) + " aht " + Program.R(pane.additionalHeatTransfer)));
                    Console.WriteLine("  frameU  tcd " + Program.Join(values?.FrameU));
                    Console.WriteLine("  frameU  tbd " + (frame == null ? "-" : Program.Join(Program.Floats(frame.GetUValue()))));

                    List<string> layersTcd = values?.PaneLayers ?? new List<string>();
                    List<string> layersTbd = pane == null ? new List<string>() : Layers.Tbd(pane);
                    for (int i = 0; i < Math.Max(layersTcd.Count, layersTbd.Count); i++)
                    {
                        string a = i < layersTcd.Count ? layersTcd[i] : "-";
                        string b = i < layersTbd.Count ? layersTbd[i] : "-";
                        Console.WriteLine("  layer " + (i + 1) + (a == b ? " same " : " DIFF ") + "tcd " + a);
                        if (a != b)
                        {
                            Console.WriteLine("          tbd " + b);
                        }
                    }
                }

                if (save)
                {
                    document.save();
                    Console.WriteLine("saved " + tbd);
                }
            }
            finally
            {
                document.close();
                Program.Release(document);
            }
        }
    }
}
