// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Text.Json.Nodes;
using SAM.Core;
using SAM.Core.Tas;
using SAM.Weather;
using System.Collections.Generic;
using System.Diagnostics;
using TBD;

namespace SAM.Analytical.Tas
{
    public class WorkflowCalculator : IJSAMObject
    {
        public WorkflowCalculator()
        {

        }

        public WorkflowCalculator(JsonObject jObject)
        {
            FromJsonObject(jObject);
        }

        public WorkflowCalculator(WorkflowSettings workflowSettings)
        {
            WorkflowSettings = workflowSettings == null ? null : new WorkflowSettings(workflowSettings);
        }

        public event WorkflowCalculatorStartedEventHandler Ended;
        public event WorkflowCalculatorStartedEventHandler Started;
        public event WorkflowCalculatorStepsCountedEventHandler StepsCounted;
        public event WorkflowCalculatorUpdatingEventHandler Updating;

        private readonly Stopwatch stepStopwatch = new Stopwatch();
        private string currentStepName;
        private readonly List<KeyValuePair<string, double>> timings = new List<KeyValuePair<string, double>>();

        public IReadOnlyList<KeyValuePair<string, double>> Timings => timings;

        public WorkflowSettings WorkflowSettings { get; set; }

        /// <summary>
        /// Optional cooperative cancellation. Defaults to <see cref="System.Threading.CancellationToken.None"/>
        /// so existing callers (e.g. the benchmark CLI) are unaffected. It is observed once per stage in
        /// <see cref="Step"/>, so a cancel aborts before the next step begins; it does NOT interrupt the
        /// in-flight, uninterruptible TAS COM simulate/sizing call within a step.
        /// </summary>
        public System.Threading.CancellationToken CancellationToken { get; set; } = System.Threading.CancellationToken.None;

        private void Step(string description)
        {
            // Finalize first so the stage that just completed keeps its timing.
            FinalizeCurrentStep();

            // Already cancelled: stop without announcing a stage that will not run.
            CancellationToken.ThrowIfCancellationRequested();

            // Raise Updating BEFORE deciding to proceed. A WinForms listener pumps the message queue here
            // (ProgressForm.Update -> Application.DoEvents), so this is where a queued Cancel click is actually
            // delivered. Checking the token only before this call would observe a stale state and let this
            // stage - potentially an expensive or file-writing one - start anyway.
            Updating?.Invoke(this, new WorkflowCalculatorUpdatingEventArgs(description));

            // Observe a cancel delivered by that pump, before this stage does any work. Cancelling can still
            // leave partially written .t3d/.tbd/.tsd files behind - a later clean rerun should set
            // _removeTBD_ true.
            CancellationToken.ThrowIfCancellationRequested();

            currentStepName = description;
            stepStopwatch.Restart();
        }

        private void FinalizeCurrentStep()
        {
            if (currentStepName == null)
                return;

            stepStopwatch.Stop();
            timings.Add(new KeyValuePair<string, double>(currentStepName, stepStopwatch.Elapsed.TotalMilliseconds));
            currentStepName = null;
        }

        private void WriteTimingsCsv(string directory, string fileName)
        {
            FinalizeCurrentStep();
            if (timings.Count == 0 || string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
                return;

            try
            {
                string path = System.IO.Path.Combine(directory, fileName + ".timing.csv");
                List<string> lines = new List<string> { "step,milliseconds" };
                double total = 0.0;
                foreach (KeyValuePair<string, double> entry in timings)
                {
                    string safeName = entry.Key?.Replace("\"", "\"\"") ?? string.Empty;
                    lines.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "\"{0}\",{1:F1}", safeName, entry.Value));
                    total += entry.Value;
                }
                lines.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "\"TOTAL\",{0:F1}", total));
                System.IO.File.WriteAllLines(path, lines);
            }
            catch
            {
                // Timing instrumentation must never break the workflow.
            }
        }
        
        public AnalyticalModel Calculate(AnalyticalModel analyticalModel)
        {
            Started?.Invoke(this, new System.EventArgs());

            if (analyticalModel == null || WorkflowSettings == null)
            {
                return null;
            }

            AnalyticalModel result = new AnalyticalModel(analyticalModel);

            string directory = System.IO.Path.GetDirectoryName(WorkflowSettings.Path_TBD);
            string fileName = System.IO.Path.GetFileNameWithoutExtension(WorkflowSettings.Path_TBD);

            string path_TSD = System.IO.Path.Combine(directory, string.Format("{0}.{1}", fileName, "tsd"));

            analyticalModel.TryGetValue(Analytical.AnalyticalModelParameter.WeatherData, out WeatherData weatherData);
            if (WorkflowSettings?.WeatherData != null)
            {
                weatherData = WorkflowSettings.WeatherData;
            }

            analyticalModel.TryGetValue(Analytical.AnalyticalModelParameter.HeatingDesignDays, out SAMCollection<DesignDay> heatingDesignDays);
            if (WorkflowSettings?.DesignDays_Heating != null)
            {
                heatingDesignDays = new SAMCollection<DesignDay>(WorkflowSettings.DesignDays_Heating);
            }

            analyticalModel.TryGetValue(Analytical.AnalyticalModelParameter.CoolingDesignDays, out SAMCollection<DesignDay> coolingDesignDays);
            if (WorkflowSettings?.DesignDays_Cooling != null)
            {
                coolingDesignDays = new SAMCollection<DesignDay>(WorkflowSettings.DesignDays_Cooling);
            }

            int count = 6;
            if (!string.IsNullOrWhiteSpace(WorkflowSettings.Path_gbXML))
            {
                count = count + 9;
            }

            if (WorkflowSettings.UpdateZones)
            {
                count++;
            }

            if (WorkflowSettings.AddIZAMs)
            {
                count++;
            }

            if (WorkflowSettings.DesignDays_Cooling != null || WorkflowSettings.DesignDays_Heating != null)
            {
                count++;
            }

            if (WorkflowSettings.SurfaceOutputSpecs != null && WorkflowSettings.SurfaceOutputSpecs.Count > 0)
            {
                count = count + 2;
            }

            if (WorkflowSettings.Simulate)
            {
                count = count + 2;
            }

            if (WorkflowSettings.UnmetHours)
            {
                count++;
            }

            if (!WorkflowSettings.Sizing)
            {
                count--;
            }

            StepsCounted?.Invoke(this, new WorkflowCalculatorStepsCountedEventArgs(count));

            AdjacencyCluster adjacencyCluster = null;

            bool hasWeatherData = false;

            Core.Tas.Modify.SetProjectDirectory(directory);

            if (!string.IsNullOrWhiteSpace(WorkflowSettings.Path_gbXML))
            {
                string path_T3D = System.IO.Path.Combine(directory, string.Format("{0}.{1}", fileName, "t3d"));
                if (System.IO.File.Exists(path_T3D))
                {
                    System.IO.File.Delete(path_T3D);
                }

                if (WorkflowSettings.RemoveExistingTBD)
                {
                    if (System.IO.File.Exists(WorkflowSettings.Path_TBD))
                    {
                        System.IO.File.Delete(WorkflowSettings.Path_TBD);
                    }
                }

                // Only worth opening the T3D to read its GUID if a previous run left one on disk.
                // The unconditional delete above means this is currently always skipped (~8.8 s on a real model),
                // but the block stays in place so that making the delete conditional later restores GUID preservation for free.
                string guid = null;
                if (System.IO.File.Exists(path_T3D))
                {
                    Step("Extracting GUID");
                    using (SAMT3DDocument sAMT3DDocument = new SAMT3DDocument(path_T3D))
                    {
                        TAS3D.T3DDocument t3DDocument = sAMT3DDocument.T3DDocument;
                        guid = t3DDocument.Building.GUID;
                        sAMT3DDocument.Save();
                    }
                }

                float latitude = float.NaN;
                float longitude = float.NaN;
                float timeZone = float.NaN;
                Step("Opening TBD file");
                using (SAMTBDDocument sAMTBDDocument = new SAMTBDDocument(WorkflowSettings.Path_TBD))
                {
                    TBDDocument tBDDocument = sAMTBDDocument.TBDDocument;

                    Step("Updating Weather Data");
                    if (weatherData != null)
                    {
                        Weather.Tas.Modify.UpdateWeatherData(tBDDocument, weatherData, result.AdjacencyCluster.BuildingHeight());
                    }

                    if (!string.IsNullOrWhiteSpace(guid))
                    {
                        tBDDocument.Building.GUID = guid;
                    }

                    Step("Updating HDD and CDD Day Types");

                    Calendar calendar = tBDDocument.Building.GetCalendar();

                    List<dayType> dayTypes = Query.DayTypes(calendar);
                    if (dayTypes.Find(x => x.name == "HDD") == null)
                    {
                        dayType dayType = calendar.AddDayType();
                        dayType.name = "HDD";
                    }

                    if (dayTypes.Find(x => x.name == "CDD") == null)
                    {
                        dayType dayType = calendar.AddDayType();
                        dayType.name = "CDD";
                    }

                    latitude = tBDDocument.Building.latitude;
                    longitude = tBDDocument.Building.longitude;
                    timeZone = tBDDocument.Building.timeZone;

                    sAMTBDDocument.Save();
                }

                Step("Opening T3D file");
                using (SAMT3DDocument sAMT3DDocument = new SAMT3DDocument(path_T3D))
                {
                    TAS3D.T3DDocument t3DDocument = sAMT3DDocument.T3DDocument;

                    Step("Importing gbXML");
                    t3DDocument.TogbXML(WorkflowSettings.Path_gbXML, true, true, true);

                    //sets the window position to wall 2026.04.22



                    Step("Updating T3D file");
                    t3DDocument.SetUseBEWidths(WorkflowSettings.UseWidths);
                    result = Query.UpdateT3D(result, t3DDocument, WorkflowSettings.UpdateWindowPositionType);

                    t3DDocument.Building.latitude = float.IsNaN(latitude) ? t3DDocument.Building.latitude : latitude;
                    t3DDocument.Building.longitude = float.IsNaN(longitude) ? t3DDocument.Building.longitude : longitude;
                    t3DDocument.Building.timeZone = float.IsNaN(timeZone) ? t3DDocument.Building.timeZone : timeZone;

                    sAMT3DDocument.Save();

                    Step("T3D to TBD -> Shading");
                    Convert.ToTBD(t3DDocument, WorkflowSettings.Path_TBD, 1, 365, 15, true, false);
                }
            }

            Step("Opening TBD");
            using (SAMTBDDocument sAMTBDDocument = new SAMTBDDocument(WorkflowSettings.Path_TBD))
            {
                TBDDocument tBDDocument = sAMTBDDocument.TBDDocument;

                hasWeatherData = tBDDocument?.Building.GetWeatherYear() != null;

                Step("Updating Facing External Elements");
                result = Query.UpdateFacingExternal(result, tBDDocument);

                Step("Assigning Adiabatic Constructions");
                Modify.AssignAdiabaticConstruction(tBDDocument, "Adiabatic", new string[] { "-unzoned", "-internal", "-exposed" }, false, true);

                Step("Setting Adiabatic");
                Modify.UpdateAdiabatic(tBDDocument, result, Tolerance.MacroDistance);

                Step("Updating Building Elements");
                Modify.UpdateBuildingElements(tBDDocument, result);

                adjacencyCluster = result.AdjacencyCluster;
                Step("Updating Ids");
                Modify.UpdateIds(adjacencyCluster, tBDDocument.Building);

                Step("Updating Thermal Parameters");
                Modify.UpdateThermalParameters(adjacencyCluster, tBDDocument.Building);
                result = new AnalyticalModel(result, adjacencyCluster);

                if (WorkflowSettings.UpdateZones)
                {
                    Step("Updating Zones");
                    Modify.UpdateZones(tBDDocument.Building, result, true);
                }

                if (coolingDesignDays != null || heatingDesignDays != null)
                {
                    Step("Adding Design Days");
                    Modify.AddDesignDays(tBDDocument, coolingDesignDays, heatingDesignDays, 30);
                }

                Step("Creating Zone Groups");
                Modify.AddDefaultZoneGroups(tBDDocument?.Building, adjacencyCluster);

                if (!string.IsNullOrWhiteSpace(WorkflowSettings.Path_gbXML))
                {
                    Step("Updating Aperture Types");
                    Modify.SetApertureTypes(tBDDocument.Building, adjacencyCluster, Tolerance.MacroDistance);
                }

                if (WorkflowSettings.AddIZAMs)
                {
                    Step("Add IZAMs");
                    Modify.UpdateIZAMs(tBDDocument, adjacencyCluster);
                }

                sAMTBDDocument.Save();
            }

            if (coolingDesignDays != null || heatingDesignDays != null)
            {
                if (adjacencyCluster == null)
                {
                    adjacencyCluster = result.AdjacencyCluster;
                }

                if (adjacencyCluster != null)
                {
                    if (coolingDesignDays != null)
                    {
                        for (int i = 0; i < coolingDesignDays.Count; i++)
                        {
                            adjacencyCluster.AddObject(new DesignDay(coolingDesignDays[i], LoadType.Cooling));
                        }
                    }

                    if (heatingDesignDays != null)
                    {
                        for (int i = 0; i < heatingDesignDays.Count; i++)
                        {
                            adjacencyCluster.AddObject(new DesignDay(heatingDesignDays[i], LoadType.Heating));
                        }
                    }
                }
            }

            result = new AnalyticalModel(result, adjacencyCluster);

            if (!hasWeatherData)
            {
                WriteTimingsCsv(directory, fileName);
                return result;
            }

            if (WorkflowSettings.Sizing)
            {
                Step("Sizing");
                Query.Sizing(WorkflowSettings.Path_TBD, new SizingSettings() { ExcludeOutdoorAir = false, ExcludePositiveInternalGains = true }, result);
            }

            bool hasSurfaceOutputSpecs = WorkflowSettings.SurfaceOutputSpecs != null && WorkflowSettings.SurfaceOutputSpecs.Count > 0;

            if (hasSurfaceOutputSpecs && !WorkflowSettings.Simulate)
            {
                Step("Opening TBD");
                using (SAMTBDDocument sAMTBDDocument = new SAMTBDDocument(WorkflowSettings.Path_TBD))
                {
                    TBDDocument tBDDocument = sAMTBDDocument.TBDDocument;
                    Step("Updating Surface Output Specs");
                    Core.Tas.Modify.UpdateSurfaceOutputSpecs(tBDDocument, WorkflowSettings.SurfaceOutputSpecs);
                    Step("Assigning Surface Output Specs");
                    Core.Tas.Modify.AssignSurfaceOutputSpecs(tBDDocument, WorkflowSettings.SurfaceOutputSpecs[0].Name);
                    sAMTBDDocument.Save();
                }
            }

            if (WorkflowSettings.Simulate)
            {
                Step("Simulating Model");
                Modify.Simulate(WorkflowSettings.Path_TBD, path_TSD, WorkflowSettings.SimulateFrom, WorkflowSettings.SimulateTo, SizingType.Undefined, hasSurfaceOutputSpecs ? WorkflowSettings.SurfaceOutputSpecs : null);
            }

            if (!WorkflowSettings.Simulate)
            {
                WriteTimingsCsv(directory, fileName);
                return result;
            }

            Step("Adding Results");
            adjacencyCluster = result.AdjacencyCluster;
            List<Result> results = Modify.AddResults(path_TSD, adjacencyCluster);

            if (WorkflowSettings.UnmetHours)
            {
                Step("Calculating Unmet Hours");
                List<Result> results_UnmetHours = Query.UnmetHours(path_TSD, WorkflowSettings.Path_TBD, 0.5);
                if (results_UnmetHours != null && results_UnmetHours.Count > 0)
                {
                    foreach (Result result_UnmetHours in results_UnmetHours)
                    {
                        if (result_UnmetHours is AdjacencyClusterSimulationResult)
                        {
                            adjacencyCluster.AddObject(result_UnmetHours);
                        }
                        else if (result_UnmetHours is SpaceSimulationResult)
                        {
                            SpaceSimulationResult spaceSimulationResult = (SpaceSimulationResult)result_UnmetHours;

                            List<SpaceSimulationResult> spaceSimulationResults = Query.Results(results, spaceSimulationResult);
                            if (spaceSimulationResults == null)
                                results.Add(spaceSimulationResult);
                            else
                                spaceSimulationResults.ForEach(x => Core.Modify.Copy(spaceSimulationResult, x, Analytical.SpaceSimulationResultParameter.UnmetHourFirstIndex, Analytical.SpaceSimulationResultParameter.UnmetHours, Analytical.SpaceSimulationResultParameter.OccupiedUnmetHours));
                        }
                    }
                }
            }

            Step("Updating Design Loads");
            adjacencyCluster = Modify.UpdateDesignLoads(WorkflowSettings.Path_TBD, adjacencyCluster);

            result = new AnalyticalModel(result, adjacencyCluster);

            if(result != null)
            {
                Step("Saving Model");
                string path_Json = System.IO.Path.Combine(directory, string.Format("{0}.{1}", fileName, "json"));

                Core.Convert.ToFile(result, path_Json, SAMFileType.Json);
            }

            WriteTimingsCsv(directory, fileName);

            Ended?.Invoke(this, new System.EventArgs());

            return result;
        }

        public bool FromJsonObject(JsonObject jObject)
        {
            if (jObject == null)
            {
                return false;
            }

            if (jObject.ContainsKey("WorkflowSettings"))
            {
                WorkflowSettings = new WorkflowSettings(jObject["WorkflowSettings"] as JsonObject);
            }

            return true;
        }

        public JsonObject ToJsonObject()
        {
            JsonObject jObject = new JsonObject();
            jObject.Add("_type", Core.Query.FullTypeName(this));

            if (jObject.ContainsKey("WorkflowSettings"))
            {
                WorkflowSettings = new WorkflowSettings(jObject["WorkflowSettings"] as JsonObject);
            }

            return jObject;
        }
    }
}
