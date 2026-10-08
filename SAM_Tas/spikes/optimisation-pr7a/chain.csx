// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
//
// Native Optimisation PR7a spike: the whole tas-model chain in ONE TasGenExecute script (C# 7.0, the level
// TasGenExecute's compiler accepts). Not compiled by any project. Double-brace placeholders are filled in by
// Invoke-Pr7aEvaluation.ps1, the way PR7b's generator would write model item names into its blocks.
//
// Variables (all optional except Mode):
//   Mode                1 = building simulation only, 2 = plant simulation only (existing TSD), 3 = both
//   HeatingSetpoint     internal condition {{IC}}: thermostat lower limit (ticLL), degC (value or 24-hour profile)
//   CoolingSetpoint     internal condition {{IC}}: thermostat upper limit (ticUL), degC (value or 24-hour profile)
//   GlazingG            construction {{GLAZING}}: total solar energy transmittance (g), solved on transparent
//                       layer {{PANE_INDEX}} (0-based among the transparent layers)
//   ControllerSetpoint  plant room {{PLANTROOM}}, controller {{CONTROLLER}}: Setpoint
//   FixBuildingPath     1 = before a plant-only simulation, point the TSD's buildingPath at model.tbd (finding F1)
//   SaveTpd             0 = do not save the evaluation's TPD (the Systems Demo saves it)
//   ReadTsd             1 = also read the building measures in a plant-only evaluation
// Outputs: Result (= the {{OBJECTIVE}} output), HeatingDemand, CoolingDemand (kWh), Overheating (h, worst
// occupied room, resultant temperature > {{THRESHOLD}} degC), PlantEnergy (kWh), PlantCost (GBP), PlantCO2 (kg),
// the values read back after each write, and the time of each stage (s). A trace is written to pr7a-trace.txt.

using System.Linq;
using System.Runtime.InteropServices;

var trace = new List<string>();
var outputs = new Dictionary<string, double>();
var total = System.Diagnostics.Stopwatch.StartNew();
var stage = System.Diagnostics.Stopwatch.StartNew();
string cwd = Directory.GetCurrentDirectory();

double Lap(string name)
{
    double seconds = stage.Elapsed.TotalSeconds;
    outputs["t" + name] = seconds;
    trace.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "stage {0}: {1:0.000} s", name, seconds));
    stage.Restart();
    return seconds;
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

// Waits until no process holds the file (the TSD is still being written when simulate returns).
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

int mode = (int)Variable("Mode");
bool building = (mode & 1) != 0;
bool plant = (mode & 2) != 0;
if (mode < 1 || mode > 3) throw new Exception("Mode must be 1, 2 or 3.");
double heatingSetpoint = Variable("HeatingSetpoint");
double coolingSetpoint = Variable("CoolingSetpoint");
double glazingG = Variable("GlazingG");
double controllerSetpoint = Variable("ControllerSetpoint");

// ---- 1. Copy the workspace files into the evaluation folder (the workspace snapshot is never written).
string tbdPath = Path.Combine(cwd, "model.tbd");
string tsdPath = Path.Combine(cwd, "model.tsd");
string tpdPath = Path.Combine(cwd, "model.tpd");
File.Copy(TasFiles.getFiles(TasGenComm.TasFiles.TasExtension.TBD)["{{TBD}}"].FullPath, tbdPath, true);
if (!building)
{
    File.Copy(TasFiles.getFiles(TasGenComm.TasFiles.TasExtension.TSD)["{{TSD}}"].FullPath, tsdPath, true);
}

if (plant)
{
    File.Copy(TasFiles.getFiles(TasGenComm.TasFiles.TasExtension.TPD)["{{TPD}}"].FullPath, tpdPath, true);
}

Lap("Copy");

// ---- 2. Building: edit the TBD, then simulate TBD -> TSD.
if (building)
{
    TBD.TBDDocument tbdDocument = new TBD.TBDDocument();
    tbdDocument.open(tbdPath);
    TBD.Building tbdBuilding = tbdDocument.Building;

    if (!double.IsNaN(heatingSetpoint) || !double.IsNaN(coolingSetpoint))
    {
        // Exactly one internal condition with this exact (ordinal, untrimmed) name.
        var matches = new List<TBD.InternalCondition>();
        for (int i = 0; ; i++)
        {
            TBD.InternalCondition candidate = tbdBuilding.GetIC(i);
            if (candidate == null) break;
            if (string.Equals(candidate.name, "{{IC}}", StringComparison.Ordinal)) matches.Add(candidate);
        }

        if (matches.Count != 1) throw new Exception("Internal condition \"{{IC}}\" found " + matches.Count + " times.");

        TBD.Thermostat thermostat = matches[0].GetThermostat();
        foreach (var target in new[] { Tuple.Create("Heating", TBD.Profiles.ticLL, heatingSetpoint), Tuple.Create("Cooling", TBD.Profiles.ticUL, coolingSetpoint) })
        {
            if (double.IsNaN(target.Item3)) continue;
            TBD.profile profile = thermostat.GetProfile((int)target.Item2);
            if (profile.factor != 1)
                throw new Exception(target.Item1 + " setpoint of \"{{IC}}\" has factor " + profile.factor + "; only factor 1 is supported.");

            // The setpoint is the conditioned value: a value profile's value (its setback applies outside its
            // schedule), or, for a 24-hour profile, the highest (heating) / lowest (cooling) hour; the other hours
            // are the setback and are kept.
            float before;
            if (profile.type == TBD.ProfileTypes.ticValueProfile)
            {
                before = profile.value;
                profile.value = (float)target.Item3;
                outputs[target.Item1 + "SetpointRead"] = profile.value;
            }
            else if (profile.type == TBD.ProfileTypes.ticHourlyProfile)
            {
                float[] hours = new float[24];
                for (int h = 1; h <= 24; h++) hours[h - 1] = profile.hourlyValues[h];
                before = target.Item2 == TBD.Profiles.ticLL ? hours.Max() : hours.Min();
                int changed = 0;
                for (int h = 1; h <= 24; h++)
                {
                    if (hours[h - 1] == before) { profile.hourlyValues[h] = (float)target.Item3; changed++; }
                }

                outputs[target.Item1 + "SetpointRead"] = target.Item2 == TBD.Profiles.ticLL ? Enumerable.Range(1, 24).Max(h => profile.hourlyValues[h]) : Enumerable.Range(1, 24).Min(h => profile.hourlyValues[h]);
                outputs[target.Item1 + "SetpointHours"] = changed;
                trace.Add(target.Item1 + " hourly profile now [" + string.Join(",", Enumerable.Range(1, 24).Select(h => profile.hourlyValues[h])) + "]");
            }
            else
            {
                throw new Exception(target.Item1 + " setpoint of \"{{IC}}\" is a " + profile.type + "; only value and 24-hour profiles are supported.");
            }

            outputs[target.Item1 + "SetpointBefore"] = before;
            trace.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "IC \"{{IC}}\" {0} setpoint ({1}) {2} -> {3} (setback {4}, schedule {5})",
                target.Item1, profile.type, before, target.Item3, profile.setbackValue, profile.schedule == null ? "-" : profile.schedule.name));
        }

        outputs["LowerLimitRead"] = matches[0].GetLowerLimit();
        outputs["UpperLimitRead"] = matches[0].GetUpperLimit();
    }

    if (!double.IsNaN(glazingG))
    {
        TBD.Construction construction = tbdBuilding.GetConstructionByName("{{GLAZING}}");
        if (construction == null || construction.type != TBD.ConstructionTypes.tcdTransparentConstruction)
            throw new Exception("Glazing construction \"{{GLAZING}}\" not found.");

        // The chosen transparent layer (the solar-control pane: the widest g span, see the record) has its solar
        // transmittance solved by bisection so that GetGlazingValues()[5] (g) equals the target.
        var layers = new List<TBD.material>();
        for (int j = 1; j < 50; j++)
        {
            TBD.material layer;
            try { layer = construction.materials(j); } catch { break; }
            if (layer == null) break;
            if (layer.type == (int)TBD.MaterialTypes.tcdTransparentLayer) layers.Add(layer);
        }

        TBD.material pane = layers[{{PANE_INDEX}}];
        double gBefore = Floats(construction.GetGlazingValues())[5];
        float tauBefore = pane.solarTransmittance;
        double low = 0, high = 1 - Math.Max(pane.externalSolarReflectance, pane.internalSolarReflectance), g = double.NaN;
        int iterations = 0;
        for (; iterations < 40; iterations++)
        {
            double mid = (low + high) / 2;
            pane.solarTransmittance = (float)mid;
            g = Floats(construction.GetGlazingValues())[5];
            if (Math.Abs(g - glazingG) <= 1e-5) break;
            if (g < glazingG) low = mid; else high = mid;
        }

        if (Math.Abs(g - glazingG) > 1e-4) throw new Exception("g = " + glazingG + " is out of reach for \"{{GLAZING}}\" (reached " + g + ").");
        outputs["GlazingGBefore"] = gBefore;
        outputs["GlazingGRead"] = g;
        outputs["GlazingTau"] = pane.solarTransmittance;
        outputs["GlazingIterations"] = iterations;
        trace.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "glazing \"{{GLAZING}}\" pane \"{0}\" tau {1} -> {2}; g {3} -> {4} in {5} steps",
            pane.name, tauBefore, pane.solarTransmittance, gBefore, g, iterations));
    }

    Lap("TbdEdit");

    tbdDocument.save();
    tbdDocument.simulate(1, 365, 0, 1, 0, 0, tsdPath, 1, 0);
    Lap("BuildingSimulation");
    outputs["tTsdUnlock"] = WaitToUnlock(tsdPath);
    tbdDocument.save();
    tbdDocument.close();
    Marshal.FinalReleaseComObject(tbdDocument);
    Lap("TbdClose");
}

// ---- 3. Building results from the TSD (always after a building simulation; on request otherwise).
if (building || Variable("ReadTsd") == 1)
{
    TSD.TSDDocument tsdDocument = new TSD.TSDDocument();
    tsdDocument.openReadOnly(tsdPath);
    TSD.BuildingData buildingData = tsdDocument.SimulationData.GetBuildingData();

    // W each hour, summed over the year = Wh; reported in kWh.
    float[] heatingProfile = Floats(buildingData.GetAnnualBuildingResult((int)TSD.tsdBuildingArray.heatingProfile));
    float[] coolingProfile = Floats(buildingData.GetAnnualBuildingResult((int)TSD.tsdBuildingArray.coolingProfile));
    double heating = heatingProfile.Sum(x => (double)x);
    double cooling = coolingProfile.Sum(x => (double)x);
    outputs["HeatingHours"] = heatingProfile.Length;
    outputs["HeatingDemand"] = heating / 1000;
    outputs["CoolingDemand"] = cooling / 1000;

    // Occupied hours (occupant sensible gain > 0) with resultant temperature > threshold, per zone; the worst
    // occupied zone counts. Read day by day as SAM.Analytical.Tas.Query.ZoneResultSeries does.
    double threshold = {{THRESHOLD}};
    int worst = 0;
    string worstZone = "-";
    double zoneHeating = 0;
    int occupiedZones = 0;
    for (int z = 1; z <= buildingData.zoneCount; z++)
    {
        TSD.ZoneData zoneData = buildingData.GetZoneData(z);
        int occupied = 0, over = 0;
        for (int day = 1; day <= 365; day++)
        {
            // The daily arrays are 1-based SAFEARRAYs (GetValue(0) throws): enumerate them, as SAM_Tas does.
            float[] gain = Floats(zoneData.GetDailyZoneResult(day, (short)TSD.tsdZoneArray.occupantSensibleGain));
            float[] resultant = Floats(zoneData.GetDailyZoneResult(day, (short)TSD.tsdZoneArray.resultantTemp));
            float[] load = Floats(zoneData.GetDailyZoneResult(day, (short)TSD.tsdZoneArray.heatingLoad));
            for (int h = 0; h < 24; h++)
            {
                zoneHeating += load[h];
                if (gain[h] > 0)
                {
                    occupied++;
                    if (resultant[h] > threshold) over++;
                }
            }
        }

        if (occupied > 0)
        {
            occupiedZones++;
            if (over > worst) { worst = over; worstZone = zoneData.name; }
        }

        trace.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "zone \"{0}\": occupied {1} h, over {2} degC {3} h", zoneData.name, occupied, threshold, over));
    }

    outputs["Overheating"] = worst;
    outputs["OccupiedZones"] = occupiedZones;
    outputs["ZoneHeatingSum"] = zoneHeating / 1000;
    trace.Add("worst occupied zone: \"" + worstZone + "\" " + worst + " h");
    tsdDocument.close();
    Marshal.FinalReleaseComObject(tsdDocument);
    Lap("TsdRead");
}

// ---- 4. Plant: point the TPD at this TSD, set the controller, simulate, read the annual result sets.
if (plant)
{
    // TSDData.FixTSDPath throws RPC_E_SERVERFAULT when the TBD named inside the TSD (SimulationData.buildingPath,
    // absolute) does not exist, e.g. a TSD simulated in another folder or on another computer. A building simulation
    // in this script already points it at model.tbd; otherwise re-point it (FixBuildingPath = 1).
    if (!building && Variable("FixBuildingPath") == 1)
    {
        TSD.TSDDocument tsdFix = new TSD.TSDDocument();
        tsdFix.open(tsdPath);
        trace.Add("TSD buildingPath was \"" + Path.GetFileName(tsdFix.SimulationData.buildingPath) + "\" (exists: " + File.Exists(tsdFix.SimulationData.buildingPath) + ")");
        tsdFix.SimulationData.buildingPath = tbdPath;
        tsdFix.save();
        tsdFix.close();
        Marshal.FinalReleaseComObject(tsdFix);
        Lap("TsdBuildingPath");
    }

    TPD.TPDDoc tpdDocument = new TPD.TPDDoc();
    tpdDocument.Open(tpdPath);
    TPD.EnergyCentre energyCentre = tpdDocument.EnergyCentre;
    energyCentre.GetTSDData(1).FixTSDPath(tsdPath);

    TPD.PlantRoom plantRoom = null;
    for (int i = 1; i <= energyCentre.GetPlantRoomCount(); i++)
    {
        TPD.PlantRoom candidate = energyCentre.GetPlantRoom(i);
        if (string.Equals(candidate.Name, "{{PLANTROOM}}", StringComparison.Ordinal))
        {
            if (plantRoom != null) throw new Exception("Plant room \"{{PLANTROOM}}\" found more than once.");
            plantRoom = candidate;
        }
    }

    if (plantRoom == null) throw new Exception("Plant room \"{{PLANTROOM}}\" not found.");

    if (!double.IsNaN(controllerSetpoint))
    {
        TPD.PlantController controller = null;
        for (int i = 1; i <= plantRoom.GetControllerCount(); i++)
        {
            TPD.PlantController candidate = plantRoom.GetController(i);
            if (string.Equals(candidate.Name, "{{CONTROLLER}}", StringComparison.Ordinal))
            {
                if (controller != null) throw new Exception("Controller \"{{CONTROLLER}}\" found more than once.");
                controller = candidate;
            }
        }

        if (controller == null) throw new Exception("Controller \"{{CONTROLLER}}\" not found in \"{{PLANTROOM}}\".");
        outputs["ControllerSetpointBefore"] = controller.Setpoint;
        controller.Setpoint = (float)controllerSetpoint;
        outputs["ControllerSetpointRead"] = controller.Setpoint;
        trace.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "controller \"{{CONTROLLER}}\" ({0}, {1}) setpoint {2} -> {3}",
            controller.ControlType, controller.SensorType, outputs["ControllerSetpointBefore"], controller.Setpoint));
    }

    Lap("TpdEdit");
    plantRoom.SimulateEx(1, 8760, 0, energyCentre.ExternalPollutant.Value, 10.0, (int)TPD.tpdSimulationData.tpdSimulationDataLoad + (int)TPD.tpdSimulationData.tpdSimulationDataPipe, 0, 0);
    Lap("PlantSimulation");

    TPD.WrResultSet resultSet = (TPD.WrResultSet)energyCentre.GetResultSet(TPD.tpdResultsPeriod.tpdResultsPeriodAnnual, 0, 0, 0, null);
    foreach (var vector in new[] { Tuple.Create("PlantEnergy", TPD.tpdResultVectorType.tpdConsumption), Tuple.Create("PlantCost", TPD.tpdResultVectorType.tpdCost), Tuple.Create("PlantCO2", TPD.tpdResultVectorType.tpdCo2) })
    {
        double sum = 0;
        int size = resultSet.GetVectorSize(vector.Item2);
        for (int i = 1; i <= size; i++)
        {
            TPD.WrResultItem item = (TPD.WrResultItem)resultSet.GetResultItem(vector.Item2, i);
            Array values = (Array)item.GetValues();
            sum += (double)values.GetValue(0);
            trace.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}[{1}] category \"{2}\" fuel \"{3}\" unit \"{4}\" = {5}",
                vector.Item1, i, item.Category, item.GetFuelSource() == null ? "" : item.GetFuelSource().Name, item.GetUnitString(), values.GetValue(0)));
        }

        outputs[vector.Item1] = sum;
    }

    resultSet.Dispose();
    Lap("PlantRead");
    // The Systems Demo saves the TPD, then releases it (no Close). TPD.exe intermittently crashed inside Save in
    // this spike, after the results were read; SaveTpd = 0 skips the save (the evaluation copy is discarded anyway).
    if (Variable("SaveTpd") != 0) tpdDocument.Save();
    Marshal.FinalReleaseComObject(tpdDocument);
    Lap("TpdClose");
}

outputs["tTotal"] = total.Elapsed.TotalSeconds;
File.WriteAllLines(Path.Combine(cwd, "pr7a-trace.txt"), trace);

double objective;
ScriptOutput.SetValue("Result", outputs.TryGetValue("{{OBJECTIVE}}", out objective) ? objective : double.NaN);
foreach (var pair in outputs) ScriptOutput.SetValue(pair.Key, pair.Value);
