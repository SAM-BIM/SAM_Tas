// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
//
// Native Optimisation PR7a-2 spike: swap a whole glazing system in ONE TasGenExecute script (C# 7.0, TBD interop
// only; TCD is not referenced in TasGenExecute). Not compiled by any project. Double-brace placeholders are filled in
// by Invoke-Pr7aEvaluation.ps1 (PR7a's driver), the way PR7b's generator would write its catalogue into the block.
//
// Before the run (outside TasGenExecute) the candidate systems were written into the workspace TBD as UNUSED
// constructions with SAM_Tas' Modify.UpdateConstructions(building, apertureConstructions, materialLibrary), each
// under a unique name ("Windows: <name> <last 6 of Guid> -pane" / "-frame"), and their g, U and light transmittance
// were calculated by SAM_Tas' ThermalTransmittanceCalculator (Tas TCD). The table below is that catalogue.
//
// Variables (one of the two glazing variables, or neither for the baseline):
//   GlazingOption  (B) choice: 1..n, the option of the table (PR6 "options", numbered 1 to n)
//   GlazingG       (A) snap: a continuous g target; the option whose g is closest is applied (ties: the U-value
//                  closest to the current system's, then the lower option number)
//   SwapFrame      0 = keep the frames (default 1: a whole system is pane + frame, as SAM's Set glazing applies it)
// Target: every building element whose construction is exactly "{{GLAZING}}" (the current glazing), and the frame
// element paired with each by name ("<base>-pane" -> "<base>-frame", with or without the space SAM_Tas writes).
// Outputs: Result (= {{OBJECTIVE}}), HeatingDemand, CoolingDemand (kWh), Overheating (h, worst occupied zone,
// resultant > {{THRESHOLD}} degC), SolarGain and GlazingConduction (kWh, all zones), GlazingOptionApplied,
// GlazingGApplied / GlazingUApplied / GlazingLightApplied (read back from the TBD after the swap), element counts,
// stage times. A trace is written to pr7a2-trace.txt.

using System.Linq;
using System.Runtime.InteropServices;

class GlazingOption
{
    public GlazingOption(string label, string pane, string frame, double g, double u, double light)
    {
        Label = label; Pane = pane; Frame = frame; G = g; U = u; Light = light;
    }

    public string Label; public string Pane; public string Frame; public double G; public double U; public double Light;
}

var trace = new List<string>();
var outputs = new Dictionary<string, double>();
var total = System.Diagnostics.Stopwatch.StartNew();
var stage = System.Diagnostics.Stopwatch.StartNew();
string cwd = Directory.GetCurrentDirectory();
var invariant = System.Globalization.CultureInfo.InvariantCulture;

void Lap(string name)
{
    outputs["t" + name] = stage.Elapsed.TotalSeconds;
    trace.Add(string.Format(invariant, "stage {0}: {1:0.000} s", name, stage.Elapsed.TotalSeconds));
    stage.Restart();
}

double Variable(string name)
{
    TasVariable variable;
    return Variables.TryGetValue(name, out variable) ? variable.VariableValue : double.NaN;
}

float[] Floats(object values)
{
    return ((System.Collections.IEnumerable)values).Cast<object>().Select(x => System.Convert.ToSingle(x)).ToArray();
}

double WaitToUnlock(string path)
{
    var watch = System.Diagnostics.Stopwatch.StartNew();
    while (watch.Elapsed.TotalSeconds < 120)
    {
        try
        {
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            return watch.Elapsed.TotalSeconds;
        }
        catch (IOException)
        {
            System.Threading.Thread.Sleep(100);
        }
    }

    throw new Exception("File still locked after 120 s: " + Path.GetFileName(path));
}

// The catalogue: label, TBD pane construction, TBD frame construction ("" = the system has no frame), g, U, light.
var options = new List<GlazingOption>
{
{{OPTIONS}}
};

double optionVariable = Variable("GlazingOption");
double gTarget = Variable("GlazingG");
bool swapFrame = Variable("SwapFrame") != 0;

// ---- 1. Copy the workspace TBD into the evaluation folder (the workspace snapshot is never written).
string tbdPath = Path.Combine(cwd, "model.tbd");
string tsdPath = Path.Combine(cwd, "model.tsd");
File.Copy(TasFiles.getFiles(TasGenComm.TasFiles.TasExtension.TBD)["{{TBD}}"].FullPath, tbdPath, true);
Lap("Copy");

// ---- 2. Swap the glazing system.
TBD.TBDDocument tbdDocument = new TBD.TBDDocument();
tbdDocument.open(tbdPath);
TBD.Building building = tbdDocument.Building;

var elements = new List<TBD.buildingElement>();
for (int i = 0; ; i++)
{
    TBD.buildingElement element = building.GetBuildingElement(i);
    if (element == null) break;
    elements.Add(element);
}

var panes = elements.Where(x => x.GetConstruction() != null && string.Equals(x.GetConstruction().name, "{{GLAZING}}", StringComparison.Ordinal)).ToList();
if (panes.Count == 0) throw new Exception("No building element uses the glazing construction \"{{GLAZING}}\".");
TBD.Construction current = panes[0].GetConstruction();
float[] currentValues = Floats(current.GetGlazingValues());
float[] currentU = Floats(current.GetUValue());
outputs["GlazingGBefore"] = currentValues[5];
outputs["GlazingUBefore"] = currentU[6];

// Frame elements paired by name: "<base>-pane" -> "<base>-frame" (the Systems Demo writes "-pane", SAM_Tas " -pane").
var frames = new List<TBD.buildingElement>();
int unpaired = 0;
foreach (TBD.buildingElement pane in panes)
{
    string name = pane.name;
    TBD.buildingElement frame = null;
    if (name.EndsWith("-pane", StringComparison.Ordinal))
    {
        string frameName = name.Substring(0, name.Length - "-pane".Length) + "-frame";
        var matches = elements.Where(x => string.Equals(x.name, frameName, StringComparison.Ordinal)).ToList();
        if (matches.Count > 1) throw new Exception("Frame element \"" + frameName + "\" found " + matches.Count + " times.");
        frame = matches.FirstOrDefault();
    }

    if (frame == null) unpaired++; else frames.Add(frame);
    trace.Add("pane element \"" + name + "\" (" + (TBD.BuildingElementType)pane.BEType + ") frame element " + (frame == null ? "none" : "\"" + frame.name + "\" uses \"" + frame.GetConstruction().name + "\""));
}

int chosen = 0;
if (!double.IsNaN(optionVariable))
{
    // (B) choice: the option numbered 1..n.
    chosen = (int)Math.Round(optionVariable);
    if (chosen < 1 || chosen > options.Count || Math.Abs(optionVariable - chosen) > 1e-9)
        throw new Exception("GlazingOption " + optionVariable + " is not an option number 1.." + options.Count + ".");
}
else if (!double.IsNaN(gTarget))
{
    // (A) snap: the closest g; ties by the U-value closest to the current system, then the lower option number.
    double best = double.MaxValue, bestU = double.MaxValue;
    for (int k = 1; k <= options.Count; k++)
    {
        double distance = Math.Round(Math.Abs(options[k - 1].G - gTarget), 9);
        double distanceU = Math.Abs(options[k - 1].U - currentU[6]);
        if (distance < best || (distance == best && distanceU < bestU)) { best = distance; bestU = distanceU; chosen = k; }
    }

    outputs["GlazingGTarget"] = gTarget;
}

if (chosen > 0)
{
    GlazingOption option = options[chosen - 1];
    TBD.Construction pane = building.GetConstructionByName(option.Pane);
    if (pane == null || pane.type != TBD.ConstructionTypes.tcdTransparentConstruction)
        throw new Exception("Glazing option " + chosen + " pane construction \"" + option.Pane + "\" not found in the TBD.");
    TBD.Construction frame = null;
    if (swapFrame && option.Frame.Length > 0)
    {
        frame = building.GetConstructionByName(option.Frame);
        if (frame == null) throw new Exception("Glazing option " + chosen + " frame construction \"" + option.Frame + "\" not found in the TBD.");
    }

    foreach (TBD.buildingElement element in panes) element.AssignConstruction(pane);
    if (frame != null) foreach (TBD.buildingElement element in frames) element.AssignConstruction(frame);

    // Read back through the elements.
    if (panes.Any(x => !string.Equals(x.GetConstruction().name, option.Pane, StringComparison.Ordinal)))
        throw new Exception("Pane construction not assigned.");
    if (frame != null && frames.Any(x => !string.Equals(x.GetConstruction().name, option.Frame, StringComparison.Ordinal)))
        throw new Exception("Frame construction not assigned.");

    float[] values = Floats(panes[0].GetConstruction().GetGlazingValues());
    outputs["GlazingOptionApplied"] = chosen;
    outputs["GlazingGApplied"] = values[5];
    outputs["GlazingLightApplied"] = values[0];
    outputs["GlazingUApplied"] = Floats(panes[0].GetConstruction().GetUValue())[6];
    outputs["GlazingGCatalogue"] = option.G;
    outputs["FrameElementsChanged"] = frame == null ? 0 : frames.Count;
    trace.Add(string.Format(invariant, "option {0} \"{1}\": pane \"{2}\" on {3} elements, frame \"{4}\" on {5} elements; g {6} (catalogue {7}), U {8}, light {9}",
        chosen, option.Label, option.Pane, panes.Count, frame == null ? "-" : option.Frame, frame == null ? 0 : frames.Count, values[5], option.G, outputs["GlazingUApplied"], values[0]));
}
else
{
    outputs["GlazingOptionApplied"] = 0;
    outputs["GlazingGApplied"] = currentValues[5];
    outputs["GlazingUApplied"] = currentU[6];
    outputs["GlazingLightApplied"] = currentValues[0];
    trace.Add("baseline: no glazing variable");
}

outputs["PaneElements"] = panes.Count;
outputs["FrameElementsPaired"] = frames.Count;
outputs["PaneElementsUnpaired"] = unpaired;
Lap("TbdEdit");

// ---- 3. Building simulation (as SAM_Tas Modify.Simulate and PR7a's chain).
tbdDocument.save();
tbdDocument.simulate(1, 365, 0, 1, 0, 0, tsdPath, 1, 0);
Lap("BuildingSimulation");
outputs["tTsdUnlock"] = WaitToUnlock(tsdPath);
tbdDocument.save();
tbdDocument.close();
Marshal.FinalReleaseComObject(tbdDocument);
Lap("TbdClose");

// ---- 4. Building results (PR7a's TSD block, plus the zones' solar gain and glazing conduction).
TSD.TSDDocument tsdDocument = new TSD.TSDDocument();
tsdDocument.openReadOnly(tsdPath);
TSD.BuildingData buildingData = tsdDocument.SimulationData.GetBuildingData();
outputs["HeatingDemand"] = Floats(buildingData.GetAnnualBuildingResult((int)TSD.tsdBuildingArray.heatingProfile)).Sum(x => (double)x) / 1000;
outputs["CoolingDemand"] = Floats(buildingData.GetAnnualBuildingResult((int)TSD.tsdBuildingArray.coolingProfile)).Sum(x => (double)x) / 1000;

double threshold = {{THRESHOLD}};
int worst = 0;
string worstZone = "-";
double solar = 0, conduction = 0;
for (int z = 1; z <= buildingData.zoneCount; z++)
{
    TSD.ZoneData zoneData = buildingData.GetZoneData(z);
    int occupied = 0, over = 0;
    for (int day = 1; day <= 365; day++)
    {
        // 1-based SAFEARRAYs: enumerate them.
        float[] gain = Floats(zoneData.GetDailyZoneResult(day, (short)TSD.tsdZoneArray.occupantSensibleGain));
        float[] resultant = Floats(zoneData.GetDailyZoneResult(day, (short)TSD.tsdZoneArray.resultantTemp));
        float[] solarGain = Floats(zoneData.GetDailyZoneResult(day, (short)TSD.tsdZoneArray.solarGain));
        float[] glazing = Floats(zoneData.GetDailyZoneResult(day, (short)TSD.tsdZoneArray.externalConductionGlazing));
        for (int h = 0; h < 24; h++)
        {
            solar += solarGain[h];
            conduction += glazing[h];
            if (gain[h] > 0)
            {
                occupied++;
                if (resultant[h] > threshold) over++;
            }
        }
    }

    if (occupied > 0 && over > worst) { worst = over; worstZone = zoneData.name; }
}

outputs["Overheating"] = worst;
outputs["SolarGain"] = solar / 1000;
outputs["GlazingConduction"] = conduction / 1000;
trace.Add("worst occupied zone: \"" + worstZone + "\" " + worst + " h");
tsdDocument.close();
Marshal.FinalReleaseComObject(tsdDocument);
Lap("TsdRead");

outputs["tTotal"] = total.Elapsed.TotalSeconds;
File.WriteAllLines(Path.Combine(cwd, "pr7a2-trace.txt"), trace);

double objective;
ScriptOutput.SetValue("Result", outputs.TryGetValue("{{OBJECTIVE}}", out objective) ? objective : double.NaN);
foreach (var pair in outputs) ScriptOutput.SetValue(pair.Key, pair.Value);
