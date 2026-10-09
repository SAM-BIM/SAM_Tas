// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Optimisation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SAM.Analytical.Tas.GenOpt
{
    public static partial class Create
    {
        /// <summary>The name TasGenExecute's own examples give the objective; the script writes it too.</summary>
        public const string TasScriptResultName = "Result";

        /// <summary>The name the generated script reads design variable <paramref name="index"/> (0-based) from Variables.txt.</summary>
        public static string TasScriptVariableName(int index)
        {
            return "V" + (index + 1).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The name the generated script writes output <paramref name="index"/> under (0-based, objective first, then the
        /// recorded outputs in definition order: the order the native optimiser receives them).
        /// </summary>
        public static string TasScriptOutputName(int index)
        {
            return "Y" + (index + 1).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Generates the TasGenExecute script of a "tas-model" definition from the blocks proven on licensed Tas in PR7a
        /// (<c>NATIVE_OPTIMISATION_PR7A_SPIKE.md</c>) and PR7a-2 (<c>NATIVE_OPTIMISATION_PR7A2_GLAZING.md</c>). Nobody
        /// writes or edits it; a run writes it again from the definition.
        /// <para>Code rules (TasGenExecute compiles C# 7.0 with Roslyn scripting 2.4): C# 7.0 only, <c>using System.Linq;</c>
        /// written out, only the BCL and the TBD/TSD/TPD interops (no TCD, no <c>#r</c>), model item names as escaped
        /// string literals (the script is ASCII), the TSD's 1-based daily arrays enumerated, never indexed.</para>
        /// <para>Chain (PR7a's order, which left no lock and no process behind):</para>
        /// <list type="number">
        /// <item>Copy the workspace TBD/TSD/TPD into the evaluation folder (the snapshot is never written).</item>
        /// <item>Building, when a variable targets the TBD (or there is no TSD to read): open the TBD, write every
        /// setpoint (value profile: its value; 24-hour profile: the hours at the setpoint; factor 1 only; exact names) and
        /// glazing choice (the option's pane construction assigned to every element using the target glazing; frames
        /// kept), read back, save, simulate days 1–365, save, close, release.</item>
        /// <item>TSD, when an output measures it: open read-only, read only the arrays needed (annual heating/cooling
        /// profiles; zone occupant sensible gain and resultant temperature for overheating), close, release.</item>
        /// <item>Plant, when a variable or output needs it: without a building simulation, re-point the TSD's building
        /// path at the evaluation's TBD first (F1); open the TPD, FixTSDPath, write every controller setpoint as a double,
        /// simulate every plant room as the Systems Demo does, read every annual result, dispose the result set, release
        /// the TPD (no Save: the evaluation copy is discarded; F2's shutdown crash happens after the results are read).</item>
        /// <item>Write <c>Result</c> and <c>Y1..Ym</c> (the outputs, objective first), then the read-back values.</item>
        /// </list>
        /// </summary>
        /// <param name="optimisationDefinition">A definition runnable on <see cref="Query.TasModelCapabilities"/>.</param>
        /// <param name="tasModelInventory">The project's files (names of the TBD/TSD/TPD in the workspace).</param>
        /// <param name="glazingOptions">For every glazing choice variable (by variable name), its options in the
        /// definition's order (<see cref="TasModelRunner"/> resolves them).</param>
        public static string TasScript(OptimisationDefinition optimisationDefinition, TasModelInventory tasModelInventory, IDictionary<string, IReadOnlyList<TasGlazingOption>> glazingOptions = null)
        {
            if (optimisationDefinition == null)
            {
                throw new ArgumentNullException(nameof(optimisationDefinition));
            }

            if (tasModelInventory == null)
            {
                throw new ArgumentNullException(nameof(tasModelInventory));
            }

            return new TasScriptWriter(optimisationDefinition, tasModelInventory, glazingOptions).Write();
        }

        /// <summary>The output definitions in the order the generated script writes them (objective first).</summary>
        public static List<OptimisationOutput> TasScriptOutputs(OptimisationDefinition optimisationDefinition)
        {
            List<OptimisationOutput> result = new List<OptimisationOutput>();
            OptimisationOutput objective = optimisationDefinition?.Output(optimisationDefinition.Objective?.Output);
            if (objective != null)
            {
                result.Add(objective);
            }

            result.AddRange(optimisationDefinition?.RecordedOutputs() ?? new List<OptimisationOutput>());
            return result;
        }

        private sealed class TasScriptWriter
        {
            private readonly OptimisationDefinition definition;
            private readonly TasModelInventory inventory;
            private readonly IDictionary<string, IReadOnlyList<TasGlazingOption>> glazingOptions;
            private readonly StringBuilder text = new StringBuilder();
            private readonly List<OptimisationOutput> outputs;
            private readonly List<double> thresholds = new List<double>();

            private readonly bool building;
            private readonly bool tsd;
            private readonly bool plant;
            private readonly bool heatingDemand;
            private readonly bool coolingDemand;

            internal TasScriptWriter(OptimisationDefinition definition, TasModelInventory inventory, IDictionary<string, IReadOnlyList<TasGlazingOption>> glazingOptions)
            {
                this.definition = definition;
                this.inventory = inventory;
                this.glazingOptions = glazingOptions ?? new Dictionary<string, IReadOnlyList<TasGlazingOption>>();
                outputs = TasScriptOutputs(definition);

                foreach (DesignVariable designVariable in definition.Variables)
                {
                    string kind = designVariable?.Target?.Kind;
                    if (!TasModelKind.IsBuildingTarget(kind) && kind != TasModelKind.ControllerSetpoint)
                    {
                        throw new ArgumentException("Design variable “" + designVariable?.Name + "” has no tas-model target (" + (kind ?? "none") + ").", nameof(definition));
                    }
                }

                foreach (OptimisationOutput output in outputs)
                {
                    string kind = output?.Measure?.Kind;
                    if (!TasModelKind.IsBuildingMeasure(kind) && !TasModelKind.IsPlantKind(kind))
                    {
                        throw new ArgumentException("Output “" + output?.Name + "” has no tas-model measure (" + (kind ?? "none") + ").", nameof(definition));
                    }

                    if (kind == TasModelKind.OverheatingHours)
                    {
                        double threshold = Threshold(output.Measure);
                        if (!thresholds.Contains(threshold))
                        {
                            thresholds.Add(threshold);
                        }
                    }
                }

                heatingDemand = outputs.Any(x => x.Measure.Kind == TasModelKind.AnnualHeatingDemand);
                coolingDemand = outputs.Any(x => x.Measure.Kind == TasModelKind.AnnualCoolingDemand);
                tsd = outputs.Any(x => TasModelKind.IsBuildingMeasure(x.Measure.Kind));
                plant = definition.Variables.Any(x => TasModelKind.IsPlantKind(x.Target.Kind)) || outputs.Any(x => TasModelKind.IsPlantKind(x.Measure.Kind));
                building = definition.Variables.Any(x => TasModelKind.IsBuildingTarget(x.Target.Kind)) || ((tsd || plant) && string.IsNullOrEmpty(inventory.TsdFileName));

                if ((building || plant) && string.IsNullOrEmpty(inventory.TbdFileName))
                {
                    throw new ArgumentException("The project has no TBD: the tas-model engine needs one to simulate the building or to point the plant at its results.", nameof(inventory));
                }

                if (plant && string.IsNullOrEmpty(inventory.TpdFileName))
                {
                    throw new ArgumentException("The project has no TPD, but the definition needs the plant simulation.", nameof(inventory));
                }

                if (tsd && !building && string.IsNullOrEmpty(inventory.TsdFileName))
                {
                    throw new ArgumentException("The project has no TSD to read.", nameof(inventory));
                }
            }

            internal string Write()
            {
                Header();
                Helpers();
                Copy();
                if (building)
                {
                    Building();
                }

                if (tsd)
                {
                    Tsd();
                }

                if (plant)
                {
                    Plant();
                }

                Outputs();
                return text.ToString();
            }

            private void Header()
            {
                Line("// Generated by SAM_Tas (SAM.Analytical.Tas.GenOpt, Create.TasScript) for the \"tas-model\" engine.");
                Line("// Do not edit: every run writes it again from the optimisation definition.");
                Line("// Definition: " + Comment(definition.Name));
                for (int i = 0; i < definition.Variables.Count; i++)
                {
                    DesignVariable designVariable = definition.Variables[i];
                    Line("// " + TasScriptVariableName(i) + " = " + Comment(designVariable.Name) + ": " + Comment(Describe(designVariable.Target)));
                }

                for (int i = 0; i < outputs.Count; i++)
                {
                    Line("// " + TasScriptOutputName(i) + " = " + Comment(outputs[i].Name) + (i == 0 ? " (objective)" : string.Empty) + ": " + Comment(Describe(outputs[i].Measure)));
                }

                Line("// Chain: " + string.Join(", ", new[] { "copy", building ? "building simulation" : null, tsd ? "building results" : null, plant ? "plant simulation" : null }.Where(x => x != null)) + ".");
                Line();
                Line("using System.Linq;");
                Line("using System.Runtime.InteropServices;");
                Line();
                Line("var outputs = new Dictionary<string, double>();");
                Line("var trace = new List<string>();");
                Line("var total = System.Diagnostics.Stopwatch.StartNew();");
                Line("var stage = System.Diagnostics.Stopwatch.StartNew();");
                Line("var invariant = System.Globalization.CultureInfo.InvariantCulture;");
                Line("string cwd = Directory.GetCurrentDirectory();");
                Line();
            }

            private void Helpers()
            {
                Line("void Lap(string name)");
                Line("{");
                Line("    outputs[\"t.\" + name] = stage.Elapsed.TotalSeconds;");
                Line("    stage.Restart();");
                Line("}");
                Line();
                if (definition.Variables.Count > 0)
                {
                    Line("double Variable(string name)");
                    Line("{");
                    Line("    TasVariable variable;");
                    Line("    if (!Variables.TryGetValue(name, out variable)) throw new Exception(\"Variable \" + name + \" is missing from Variables.txt.\");");
                    Line("    return variable.VariableValue;");
                    Line("}");
                    Line();
                }

                Line("// COM arrays (the TSD's daily arrays are 1-based SAFEARRAYs): enumerate them, never index them.");
                Line("float[] Floats(object values)");
                Line("{");
                Line("    return ((System.Collections.IEnumerable)values).Cast<object>().Select(x => System.Convert.ToSingle(x)).ToArray();");
                Line("}");
                Line();
                if (building)
                {
                    Line("// The TSD is still being written when simulate returns.");
                    Line("void WaitToUnlock(string path)");
                    Line("{");
                    Line("    var watch = System.Diagnostics.Stopwatch.StartNew();");
                    Line("    while (watch.Elapsed.TotalSeconds < 120)");
                    Line("    {");
                    Line("        try");
                    Line("        {");
                    Line("            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }");
                    Line("            return;");
                    Line("        }");
                    Line("        catch (IOException)");
                    Line("        {");
                    Line("            System.Threading.Thread.Sleep(100);");
                    Line("        }");
                    Line("    }");
                    Line();
                    Line("    throw new Exception(\"File still locked after 120 s: \" + Path.GetFileName(path));");
                    Line("}");
                    Line();
                }

                if (definition.Variables.Any(x => x.Target.Kind == TasModelKind.HeatingSetpoint || x.Target.Kind == TasModelKind.CoolingSetpoint))
                {
                    Line("// Zone setpoint of one internal condition (exact name): heating = thermostat lower limit (ticLL), cooling = upper");
                    Line("// limit (ticUL). A value profile's value changes (setback and schedule kept); in a 24-hour profile the hours at the");
                    Line("// setpoint (heating: highest, cooling: lowest) change and the other hours are kept. Factor 1 only.");
                    Line("void SetSetpoint(TBD.Building building, string internalConditionName, bool heating, double value, string output)");
                    Line("{");
                    Line("    var matches = new List<TBD.InternalCondition>();");
                    Line("    for (int i = 0; ; i++)");
                    Line("    {");
                    Line("        TBD.InternalCondition candidate = building.GetIC(i);");
                    Line("        if (candidate == null) break;");
                    Line("        if (string.Equals(candidate.name, internalConditionName, StringComparison.Ordinal)) matches.Add(candidate);");
                    Line("    }");
                    Line();
                    Line("    if (matches.Count != 1) throw new Exception(\"Internal condition \\\"\" + internalConditionName + \"\\\" found \" + matches.Count + \" times.\");");
                    Line("    string what = heating ? \"Heating\" : \"Cooling\";");
                    Line("    TBD.profile profile = matches[0].GetThermostat().GetProfile((int)(heating ? TBD.Profiles.ticLL : TBD.Profiles.ticUL));");
                    Line("    if (profile.factor != 1) throw new Exception(what + \" setpoint of \\\"\" + internalConditionName + \"\\\" has factor \" + profile.factor + \"; only factor 1 is supported.\");");
                    Line("    float before;");
                    Line("    if (profile.type == TBD.ProfileTypes.ticValueProfile)");
                    Line("    {");
                    Line("        before = profile.value;");
                    Line("        profile.value = (float)value;");
                    Line("        outputs[output + \".Read\"] = profile.value;");
                    Line("    }");
                    Line("    else if (profile.type == TBD.ProfileTypes.ticHourlyProfile)");
                    Line("    {");
                    Line("        float[] hours = new float[24];");
                    Line("        for (int h = 1; h <= 24; h++) hours[h - 1] = profile.hourlyValues[h];");
                    Line("        before = heating ? hours.Max() : hours.Min();");
                    Line("        int changed = 0;");
                    Line("        for (int h = 1; h <= 24; h++)");
                    Line("        {");
                    Line("            if (hours[h - 1] == before) { profile.hourlyValues[h] = (float)value; changed++; }");
                    Line("        }");
                    Line();
                    Line("        outputs[output + \".Read\"] = heating ? Enumerable.Range(1, 24).Max(h => profile.hourlyValues[h]) : Enumerable.Range(1, 24).Min(h => profile.hourlyValues[h]);");
                    Line("        outputs[output + \".Hours\"] = changed;");
                    Line("    }");
                    Line("    else");
                    Line("    {");
                    Line("        throw new Exception(what + \" setpoint of \\\"\" + internalConditionName + \"\\\" is a \" + profile.type + \"; only value and 24-hour profiles are supported.\");");
                    Line("    }");
                    Line();
                    Line("    outputs[output + \".Before\"] = before;");
                    Line("    trace.Add(string.Format(invariant, \"{0} {1} setpoint of \\\"{2}\\\" ({3}) {4} -> {5}\", output, what, internalConditionName, profile.type, before, value));");
                    Line("}");
                    Line();
                }

                if (definition.Variables.Any(x => x.Target.Kind == TasModelKind.GlazingChoice))
                {
                    Line("// Glazing choice: every building element using the target glazing construction (exact name) is given the option's");
                    Line("// pane construction (written into the TBD under a unique name before the run); the frames are kept.");
                    Line("void SetGlazing(TBD.Building building, string glazingConstruction, double value, string[] panes, string[] labels, string output)");
                    Line("{");
                    Line("    int option = (int)Math.Round(value);");
                    Line("    if (option < 1 || option > panes.Length || Math.Abs(value - option) > 1e-9)");
                    Line("        throw new Exception(output + \" = \" + value.ToString(invariant) + \" is not an option number 1..\" + panes.Length + \".\");");
                    Line();
                    Line("    var elements = new List<TBD.buildingElement>();");
                    Line("    for (int i = 0; ; i++)");
                    Line("    {");
                    Line("        TBD.buildingElement element = building.GetBuildingElement(i);");
                    Line("        if (element == null) break;");
                    Line("        TBD.Construction construction = element.GetConstruction();");
                    Line("        if (construction != null && string.Equals(construction.name, glazingConstruction, StringComparison.Ordinal)) elements.Add(element);");
                    Line("    }");
                    Line();
                    Line("    if (elements.Count == 0) throw new Exception(\"No building element uses the glazing construction \\\"\" + glazingConstruction + \"\\\".\");");
                    Line("    string paneName = panes[option - 1];");
                    Line("    TBD.Construction pane = building.GetConstructionByName(paneName);");
                    Line("    if (pane == null || pane.type != TBD.ConstructionTypes.tcdTransparentConstruction)");
                    Line("        throw new Exception(\"Glazing option \" + option + \" pane construction \\\"\" + paneName + \"\\\" not found in the TBD.\");");
                    Line("    if (!string.Equals(paneName, glazingConstruction, StringComparison.Ordinal))");
                    Line("    {");
                    Line("        foreach (TBD.buildingElement element in elements) element.AssignConstruction(pane);");
                    Line("    }");
                    Line();
                    Line("    if (elements.Any(x => !string.Equals(x.GetConstruction().name, paneName, StringComparison.Ordinal)))");
                    Line("        throw new Exception(\"Glazing option \" + option + \" was not assigned.\");");
                    Line("    float[] values = Floats(elements[0].GetConstruction().GetGlazingValues());");
                    Line("    float[] u = Floats(elements[0].GetConstruction().GetUValue());");
                    Line("    outputs[output + \".Option\"] = option;");
                    Line("    outputs[output + \".g\"] = values[5];");
                    Line("    outputs[output + \".U\"] = u[6];");
                    Line("    outputs[output + \".Light\"] = values[0];");
                    Line("    outputs[output + \".Elements\"] = elements.Count;");
                    Line("    trace.Add(string.Format(invariant, \"{0} glazing option {1} \\\"{2}\\\": pane \\\"{3}\\\" on {4} elements; g {5}, U {6}, light {7}\", output, option, labels[option - 1], paneName, elements.Count, values[5], u[6], values[0]));");
                    Line("}");
                    Line();
                }

                if (definition.Variables.Any(x => x.Target.Kind == TasModelKind.ControllerSetpoint))
                {
                    Line("// Controller setpoint (exact plant room and controller names), written as a double.");
                    Line("void SetController(TPD.EnergyCentre energyCentre, string plantRoomName, string controllerName, double value, string output)");
                    Line("{");
                    Line("    TPD.PlantRoom plantRoom = null;");
                    Line("    for (int i = 1; i <= energyCentre.GetPlantRoomCount(); i++)");
                    Line("    {");
                    Line("        TPD.PlantRoom candidate = energyCentre.GetPlantRoom(i);");
                    Line("        if (!string.Equals(candidate.Name, plantRoomName, StringComparison.Ordinal)) continue;");
                    Line("        if (plantRoom != null) throw new Exception(\"Plant room \\\"\" + plantRoomName + \"\\\" found more than once.\");");
                    Line("        plantRoom = candidate;");
                    Line("    }");
                    Line();
                    Line("    if (plantRoom == null) throw new Exception(\"Plant room \\\"\" + plantRoomName + \"\\\" not found.\");");
                    Line("    TPD.PlantController controller = null;");
                    Line("    for (int i = 1; i <= plantRoom.GetControllerCount(); i++)");
                    Line("    {");
                    Line("        TPD.PlantController candidate = plantRoom.GetController(i);");
                    Line("        if (!string.Equals(candidate.Name, controllerName, StringComparison.Ordinal)) continue;");
                    Line("        if (controller != null) throw new Exception(\"Controller \\\"\" + controllerName + \"\\\" found more than once in \\\"\" + plantRoomName + \"\\\".\");");
                    Line("        controller = candidate;");
                    Line("    }");
                    Line();
                    Line("    if (controller == null) throw new Exception(\"Controller \\\"\" + controllerName + \"\\\" not found in \\\"\" + plantRoomName + \"\\\".\");");
                    Line("    double before = controller.Setpoint;");
                    Line("    controller.Setpoint = value;");
                    Line("    outputs[output + \".Before\"] = before;");
                    Line("    outputs[output + \".Read\"] = controller.Setpoint;");
                    Line("    trace.Add(string.Format(invariant, \"{0} controller \\\"{1}\\\" ({2}) setpoint {3} -> {4}\", output, controllerName, controller.SensorType, before, controller.Setpoint));");
                    Line("}");
                    Line();
                }

                if (plant)
                {
                    Line("// Annual TPD result: the first value of every item of the type, summed.");
                    Line("double ResultSum(TPD.WrResultSet resultSet, TPD.tpdResultVectorType type)");
                    Line("{");
                    Line("    double sum = 0;");
                    Line("    int size = resultSet.GetVectorSize(type);");
                    Line("    for (int i = 1; i <= size; i++)");
                    Line("    {");
                    Line("        TPD.WrResultItem item = (TPD.WrResultItem)resultSet.GetResultItem(type, i);");
                    Line("        Array values = (Array)item.GetValues();");
                    Line("        sum += (double)values.GetValue(0);");
                    Line("    }");
                    Line();
                    Line("    return sum;");
                    Line("}");
                    Line();
                }
            }

            private void Copy()
            {
                Line("// ---- 1. Copy the workspace files into the evaluation folder (the workspace snapshot is never written).");
                Line("string tbdPath = Path.Combine(cwd, \"model.tbd\");");
                Line("string tsdPath = Path.Combine(cwd, \"model.tsd\");");
                if (plant)
                {
                    Line("string tpdPath = Path.Combine(cwd, \"model.tpd\");");
                }

                if (building || plant)
                {
                    Line("File.Copy(TasFiles.getFiles(TasGenComm.TasFiles.TasExtension.TBD)[" + Literal(inventory.TbdFileName) + "].FullPath, tbdPath, true);");
                }

                if (!building)
                {
                    Line("File.Copy(TasFiles.getFiles(TasGenComm.TasFiles.TasExtension.TSD)[" + Literal(inventory.TsdFileName) + "].FullPath, tsdPath, true);");
                }

                if (plant)
                {
                    Line("File.Copy(TasFiles.getFiles(TasGenComm.TasFiles.TasExtension.TPD)[" + Literal(inventory.TpdFileName) + "].FullPath, tpdPath, true);");
                }

                Line("Lap(\"Copy\");");
                Line();
            }

            private void Building()
            {
                Line("// ---- 2. Building: edit the TBD, then simulate TBD -> TSD (as SAM_Tas Modify.Simulate).");
                Line("TBD.TBDDocument tbdDocument = new TBD.TBDDocument();");
                Line("tbdDocument.open(tbdPath);");
                Line("TBD.Building tbdBuilding = tbdDocument.Building;");
                for (int i = 0; i < definition.Variables.Count; i++)
                {
                    DesignVariable designVariable = definition.Variables[i];
                    string name = TasScriptVariableName(i);
                    string kind = designVariable.Target.Kind;
                    if (kind == TasModelKind.HeatingSetpoint || kind == TasModelKind.CoolingSetpoint)
                    {
                        Line("SetSetpoint(tbdBuilding, " + Literal(ReferenceValue(designVariable.Target, TasModelKind.InternalConditionKey)) + ", " + (kind == TasModelKind.HeatingSetpoint ? "true" : "false") + ", Variable(\"" + name + "\"), \"" + name + "\");");
                    }
                    else if (kind == TasModelKind.GlazingChoice)
                    {
                        IReadOnlyList<TasGlazingOption> options = Options(designVariable);
                        Line("SetGlazing(tbdBuilding, " + Literal(ReferenceValue(designVariable.Target, TasModelKind.GlazingConstructionKey)) + ", Variable(\"" + name + "\"),");
                        Line("    new[] { " + string.Join(", ", options.Select(x => Literal(x.PaneConstruction))) + " },");
                        Line("    new[] { " + string.Join(", ", options.Select(x => Literal(x.Text))) + " },");
                        Line("    \"" + name + "\");");
                    }
                }

                Line("Lap(\"TbdEdit\");");
                Line("tbdDocument.save();");
                Line("tbdDocument.simulate(1, 365, 0, 1, 0, 0, tsdPath, 1, 0);");
                Line("Lap(\"BuildingSimulation\");");
                Line("WaitToUnlock(tsdPath);");
                Line("tbdDocument.save();");
                Line("tbdDocument.close();");
                Line("Marshal.FinalReleaseComObject(tbdDocument);");
                Line("Lap(\"TbdClose\");");
                Line();
            }

            private void Tsd()
            {
                Line("// ---- 3. Building results from the TSD (only the arrays the outputs need).");
                Line("TSD.TSDDocument tsdDocument = new TSD.TSDDocument();");
                Line("tsdDocument.openReadOnly(tsdPath);");
                Line("TSD.BuildingData buildingData = tsdDocument.SimulationData.GetBuildingData();");
                if (heatingDemand)
                {
                    Line("// W each hour, summed over the year = Wh; in kWh.");
                    Line("double heatingDemand = Floats(buildingData.GetAnnualBuildingResult((int)TSD.tsdBuildingArray.heatingProfile)).Sum(x => (double)x) / 1000;");
                }

                if (coolingDemand)
                {
                    Line("double coolingDemand = Floats(buildingData.GetAnnualBuildingResult((int)TSD.tsdBuildingArray.coolingProfile)).Sum(x => (double)x) / 1000;");
                }

                if (thresholds.Count > 0)
                {
                    Line("// Overheating: occupied hours (occupant sensible gain > 0) with resultant temperature above the threshold, per");
                    Line("// zone; the worst occupied zone counts (the same rule as SAM_Tas Query.Overheating).");
                    Line("double[] thresholds = new double[] { " + string.Join(", ", thresholds.Select(Number)) + " };");
                    Line("int[] overheating = new int[thresholds.Length];");
                    Line("for (int z = 1; z <= buildingData.zoneCount; z++)");
                    Line("{");
                    Line("    TSD.ZoneData zoneData = buildingData.GetZoneData(z);");
                    Line("    int occupied = 0;");
                    Line("    int[] over = new int[thresholds.Length];");
                    Line("    for (int day = 1; day <= 365; day++)");
                    Line("    {");
                    Line("        float[] gain = Floats(zoneData.GetDailyZoneResult(day, (short)TSD.tsdZoneArray.occupantSensibleGain));");
                    Line("        float[] resultant = Floats(zoneData.GetDailyZoneResult(day, (short)TSD.tsdZoneArray.resultantTemp));");
                    Line("        for (int h = 0; h < 24; h++)");
                    Line("        {");
                    Line("            if (gain[h] <= 0) continue;");
                    Line("            occupied++;");
                    Line("            for (int t = 0; t < thresholds.Length; t++)");
                    Line("            {");
                    Line("                if (resultant[h] > thresholds[t]) over[t]++;");
                    Line("            }");
                    Line("        }");
                    Line("    }");
                    Line();
                    Line("    if (occupied == 0) continue;");
                    Line("    for (int t = 0; t < thresholds.Length; t++)");
                    Line("    {");
                    Line("        if (over[t] > overheating[t])");
                    Line("        {");
                    Line("            overheating[t] = over[t];");
                    Line("            trace.Add(string.Format(invariant, \"over {0} degC: zone \\\"{1}\\\" {2} h of {3} occupied\", thresholds[t], zoneData.name, over[t], occupied));");
                    Line("        }");
                    Line("    }");
                    Line("}");
                    Line();
                }

                Line("tsdDocument.close();");
                Line("Marshal.FinalReleaseComObject(tsdDocument);");
                Line("Lap(\"TsdRead\");");
                Line();
            }

            private void Plant()
            {
                Line("// ---- 4. Plant: point the TPD at this TSD, set the controllers, simulate, read the annual results.");
                if (!building)
                {
                    Line("// FixTSDPath fails when the TBD named inside the TSD (SimulationData.buildingPath, absolute) does not exist");
                    Line("// (PR7a F1): point it at the evaluation's TBD first.");
                    Line("TSD.TSDDocument tsdFix = new TSD.TSDDocument();");
                    Line("tsdFix.open(tsdPath);");
                    Line("tsdFix.SimulationData.buildingPath = tbdPath;");
                    Line("tsdFix.save();");
                    Line("tsdFix.close();");
                    Line("Marshal.FinalReleaseComObject(tsdFix);");
                    Line("Lap(\"TsdBuildingPath\");");
                }

                Line("TPD.TPDDoc tpdDocument = new TPD.TPDDoc();");
                Line("tpdDocument.Open(tpdPath);");
                Line("TPD.EnergyCentre energyCentre = tpdDocument.EnergyCentre;");
                Line("if (energyCentre.GetTSDDataCount() != 1) throw new Exception(\"The TPD uses \" + energyCentre.GetTSDDataCount() + \" TSD files; the tas-model engine supports one.\");");
                Line("energyCentre.GetTSDData(1).FixTSDPath(tsdPath);");
                for (int i = 0; i < definition.Variables.Count; i++)
                {
                    DesignVariable designVariable = definition.Variables[i];
                    if (designVariable.Target.Kind != TasModelKind.ControllerSetpoint)
                    {
                        continue;
                    }

                    string name = TasScriptVariableName(i);
                    Line("SetController(energyCentre, " + Literal(ReferenceValue(designVariable.Target, TasModelKind.PlantRoomKey)) + ", " + Literal(ReferenceValue(designVariable.Target, TasModelKind.ControllerKey)) + ", Variable(\"" + name + "\"), \"" + name + "\");");
                }

                Line("Lap(\"TpdEdit\");");
                Line("// The Systems Demo's simulation call, for every plant room.");
                Line("for (int i = 1; i <= energyCentre.GetPlantRoomCount(); i++)");
                Line("{");
                Line("    energyCentre.GetPlantRoom(i).SimulateEx(1, 8760, 0, energyCentre.ExternalPollutant.Value, 10.0, (int)TPD.tpdSimulationData.tpdSimulationDataLoad + (int)TPD.tpdSimulationData.tpdSimulationDataPipe, 0, 0);");
                Line("}");
                Line();
                Line("Lap(\"PlantSimulation\");");
                Line("// Read every result before the TPD is released (PR7a F2: TPD.exe may crash at shutdown, after this).");
                Line("TPD.WrResultSet resultSet = (TPD.WrResultSet)energyCentre.GetResultSet(TPD.tpdResultsPeriod.tpdResultsPeriodAnnual, 0, 0, 0, null);");
                if (outputs.Any(x => x.Measure.Kind == TasModelKind.AnnualPlantEnergy))
                {
                    Line("double plantEnergy = ResultSum(resultSet, TPD.tpdResultVectorType.tpdConsumption);");
                }

                if (outputs.Any(x => x.Measure.Kind == TasModelKind.AnnualPlantCost))
                {
                    Line("double plantCost = ResultSum(resultSet, TPD.tpdResultVectorType.tpdCost);");
                }

                if (outputs.Any(x => x.Measure.Kind == TasModelKind.AnnualPlantCO2))
                {
                    Line("double plantCO2 = ResultSum(resultSet, TPD.tpdResultVectorType.tpdCo2);");
                }

                Line("resultSet.Dispose();");
                Line("Lap(\"PlantRead\");");
                Line("Marshal.FinalReleaseComObject(tpdDocument);");
                Line("Lap(\"TpdClose\");");
                Line();
            }

            private void Outputs()
            {
                Line("// ---- 5. Outputs: the objective as Result and Y1, then the recorded outputs, then the read-back values.");
                Line("outputs[\"t.Total\"] = total.Elapsed.TotalSeconds;");
                Line("File.WriteAllLines(Path.Combine(cwd, \"tas-model-trace.txt\"), trace);");
                for (int i = 0; i < outputs.Count; i++)
                {
                    string value = Expression(outputs[i].Measure);
                    if (i == 0)
                    {
                        Line("ScriptOutput.SetValue(\"" + TasScriptResultName + "\", " + value + ");");
                    }

                    Line("ScriptOutput.SetValue(\"" + TasScriptOutputName(i) + "\", " + value + ");");
                }

                Line("foreach (var pair in outputs) ScriptOutput.SetValue(pair.Key, pair.Value);");
            }

            private string Expression(OptimisationMeasure measure)
            {
                switch (measure.Kind)
                {
                    case TasModelKind.AnnualHeatingDemand:
                        return "heatingDemand";
                    case TasModelKind.AnnualCoolingDemand:
                        return "coolingDemand";
                    case TasModelKind.OverheatingHours:
                        return "overheating[" + thresholds.IndexOf(Threshold(measure)).ToString(CultureInfo.InvariantCulture) + "]";
                    case TasModelKind.AnnualPlantEnergy:
                        return "plantEnergy";
                    case TasModelKind.AnnualPlantCost:
                        return "plantCost";
                    case TasModelKind.AnnualPlantCO2:
                        return "plantCO2";
                    default:
                        throw new ArgumentException("Unknown measure " + measure.Kind + ".");
                }
            }

            private IReadOnlyList<TasGlazingOption> Options(DesignVariable designVariable)
            {
                if (!glazingOptions.TryGetValue(designVariable.Name, out IReadOnlyList<TasGlazingOption> options) || options == null || options.Count < 2)
                {
                    throw new ArgumentException("The glazing options of “" + designVariable.Name + "” are missing.", "glazingOptions");
                }

                if (options.Count != designVariable.Target.Options.Count)
                {
                    throw new ArgumentException("“" + designVariable.Name + "” lists " + designVariable.Target.Options.Count + " options but " + options.Count + " were resolved.", "glazingOptions");
                }

                TasGlazingOption missing = options.FirstOrDefault(x => string.IsNullOrEmpty(x?.PaneConstruction));
                if (missing != null)
                {
                    throw new ArgumentException("Glazing option “" + missing.Text + "” of “" + designVariable.Name + "” has no TBD pane construction (its system is not attached).", "glazingOptions");
                }

                return options;
            }

            private static double Threshold(OptimisationMeasure measure)
            {
                return measure.Parameters != null && measure.Parameters.TryGetValue(TasModelKind.ThresholdParameter, out double value) ? value : TasModelKind.DefaultThreshold;
            }

            private static string ReferenceValue(OptimisationBinding binding, string key)
            {
                if (binding.Reference == null || !binding.Reference.TryGetValue(key, out string value) || string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException("The binding " + binding.Kind + " does not say which " + key + ".");
                }

                return value;
            }

            private static string Describe(OptimisationBinding binding)
            {
                string result = binding.Kind;
                if (binding.Reference != null && binding.Reference.Count > 0)
                {
                    result += " { " + string.Join(", ", binding.Reference.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key + ": \"" + x.Value + "\"")) + " }";
                }

                if (binding.Parameters != null && binding.Parameters.Count > 0)
                {
                    result += " (" + string.Join(", ", binding.Parameters.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key + " " + Number(x.Value))) + ")";
                }

                if (binding is OptimisationTarget target && target.Options != null && target.Options.Count > 0)
                {
                    result += " options [" + string.Join(" | ", target.Options) + "]";
                }

                return result;
            }

            private void Line(string line = "")
            {
                text.Append(line).Append('\n');
            }

            /// <summary>A C# string literal of <paramref name="value"/> that is pure ASCII (every other character as \uXXXX).</summary>
            internal static string Literal(string value)
            {
                StringBuilder result = new StringBuilder("\"");
                foreach (char c in value ?? string.Empty)
                {
                    switch (c)
                    {
                        case '"':
                            result.Append("\\\"");
                            break;
                        case '\\':
                            result.Append("\\\\");
                            break;
                        default:
                            if (c < 0x20 || c > 0x7E)
                            {
                                result.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                result.Append(c);
                            }

                            break;
                    }
                }

                return result.Append('"').ToString();
            }

            /// <summary>Text for a // comment: one line, ASCII (other characters as '?').</summary>
            private static string Comment(string value)
            {
                StringBuilder result = new StringBuilder();
                foreach (char c in value ?? string.Empty)
                {
                    result.Append(c < 0x20 || c > 0x7E ? (c == '\r' || c == '\n' || c == '\t' ? ' ' : '?') : c);
                }

                return result.ToString();
            }

            private static string Number(double value)
            {
                return value.ToString("R", CultureInfo.InvariantCulture);
            }
        }
    }
}
