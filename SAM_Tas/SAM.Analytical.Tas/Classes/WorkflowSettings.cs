// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Text.Json.Nodes;
using SAM.Core;
using SAM.Core.Tas;
using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public class WorkflowSettings : IJSAMObject
    {
        public string Path_TBD { get; set; } = null;

        public string Path_gbXML { get; set; } = null;

        public Weather.WeatherData WeatherData { get; set; } = null;

        public List<DesignDay> DesignDays_Heating { get; set; } = null;

        public List<DesignDay> DesignDays_Cooling { get; set; } = null;

        public List<SurfaceOutputSpec> SurfaceOutputSpecs { get; set; } = null;

        public bool UnmetHours { get; set; } = true;

        public bool Simulate { get; set; } = true;

        public bool Sizing { get; set; } = true;

        public bool UpdateZones { get; set; } = true;

        public bool UseWidths { get; set; } = false;

        public bool AddIZAMs { get; set; } = true;

        public int SimulateFrom { get; set; } = 1;

        public int SimulateTo { get; set; } = 1;

        public bool RemoveExistingTBD { get; set; } = false;

        public bool UpdateWindowPositionType { get; set; } = false;

        public WorkflowSettings()
        {

        }

        public WorkflowSettings(JsonObject jObject)
        {
            FromJsonObject(jObject);
        }

        public WorkflowSettings(WorkflowSettings workflowSettings)
        {
            if(workflowSettings != null)
            {
                Path_TBD = workflowSettings.Path_TBD;
                Path_gbXML = workflowSettings.Path_gbXML;
                WeatherData = workflowSettings.WeatherData;
                DesignDays_Heating = workflowSettings.DesignDays_Heating;
                DesignDays_Cooling = workflowSettings.DesignDays_Cooling;
                SurfaceOutputSpecs = workflowSettings.SurfaceOutputSpecs;
                UnmetHours = workflowSettings.UnmetHours;
                Simulate = workflowSettings.Simulate;
                Sizing = workflowSettings.Sizing;
                UpdateZones = workflowSettings.UpdateZones;
                UseWidths = workflowSettings.UseWidths;
                AddIZAMs = workflowSettings.AddIZAMs;
                SimulateFrom = workflowSettings.SimulateFrom;
                SimulateTo = workflowSettings.SimulateTo;

                RemoveExistingTBD = workflowSettings.RemoveExistingTBD;

                UpdateWindowPositionType = workflowSettings.UpdateWindowPositionType;
            }
        }

        public bool FromJsonObject(JsonObject jObject)
        {
            if(jObject == null)
            {
                return false;
            }

            if(jObject.ContainsKey("Path_TBD"))
            {
                Path_TBD = jObject["Path_TBD"]?.GetValue<string>() ?? null;
            }

            if (jObject.ContainsKey("Path_gbXML"))
            {
                Path_gbXML = jObject["Path_gbXML"]?.GetValue<string>() ?? null;
            }

            if (jObject.ContainsKey("WeatherData"))
            {
                WeatherData = new Weather.WeatherData(jObject["WeatherData"] as JsonObject);
            }

            if (jObject.ContainsKey("DesignDays_Heating"))
            {
                JsonArray jArray = jObject["DesignDays_Heating"] as JsonArray;
                if(jArray != null)
                {
                    DesignDays_Heating = new List<DesignDay>();
                    foreach(JsonNode jsonNode_DesignDay in jArray)
                    {
                        if (!(jsonNode_DesignDay is JsonObject jObject_DesignDay))
                        {
                            continue;
                        }

                        DesignDays_Heating.Add(new DesignDay(jObject_DesignDay));
                    }
                }
            }

            if (jObject.ContainsKey("DesignDays_Cooling"))
            {
                JsonArray jArray = jObject["DesignDays_Cooling"] as JsonArray;
                if (jArray != null)
                {
                    DesignDays_Cooling = new List<DesignDay>();
                    foreach (JsonNode jsonNode_DesignDay in jArray)
                    {
                        if (!(jsonNode_DesignDay is JsonObject jObject_DesignDay))
                        {
                            continue;
                        }

                        DesignDays_Cooling.Add(new DesignDay(jObject_DesignDay));
                    }
                }
            }

            if (jObject.ContainsKey("SurfaceOutputSpecs"))
            {
                JsonArray jArray = jObject["SurfaceOutputSpecs"] as JsonArray;
                if (jArray != null)
                {
                    SurfaceOutputSpecs = new List<SurfaceOutputSpec>();
                    foreach (JsonNode jsonNode_SurfaceOutputSpec in jArray)
                    {
                        if (!(jsonNode_SurfaceOutputSpec is JsonObject jObject_SurfaceOutputSpec))
                        {
                            continue;
                        }

                        SurfaceOutputSpecs.Add(new SurfaceOutputSpec(jObject_SurfaceOutputSpec));
                    }
                }
            }


            if (jObject.ContainsKey("UnmetHours"))
            {
                UnmetHours = jObject["UnmetHours"]?.GetValue<bool>() ?? default(bool);
            }

            if (jObject.ContainsKey("Simulate"))
            {
                Simulate = jObject["Simulate"]?.GetValue<bool>() ?? default(bool);
            }

            if (jObject.ContainsKey("Sizing"))
            {
                Sizing = jObject["Sizing"]?.GetValue<bool>() ?? default(bool);
            }

            if (jObject.ContainsKey("UpdateZones"))
            {
                UpdateZones = jObject["UpdateZones"]?.GetValue<bool>() ?? default(bool);
            }

            if (jObject.ContainsKey("UseWidths"))
            {
                UseWidths = jObject["UseWidths"]?.GetValue<bool>() ?? default(bool);
            }

            if (jObject.ContainsKey("AddIZAMs"))
            {
                AddIZAMs = jObject["AddIZAMs"]?.GetValue<bool>() ?? default(bool);
            }

            if (jObject.ContainsKey("SimulateFrom"))
            {
                SimulateFrom = jObject["SimulateFrom"]?.GetValue<int>() ?? default(int);
            }

            if (jObject.ContainsKey("SimulateTo"))
            {
                SimulateTo = jObject["SimulateTo"]?.GetValue<int>() ?? default(int);
            }

            if (jObject.ContainsKey("RemoveExistingTBD"))
            {
                RemoveExistingTBD = jObject["RemoveExistingTBD"]?.GetValue<bool>() ?? default(bool);
            }

            if (jObject.ContainsKey("UpdateWindowPositionType"))
            {
                UpdateWindowPositionType = jObject["UpdateWindowPositionType"]?.GetValue<bool>() ?? default(bool);
            }

            return true;
        }

        public JsonObject ToJsonObject()
        {
            JsonObject jObject = new JsonObject();
            jObject.Add("_type", Core.Query.FullTypeName(this));
            
            if(Path_TBD != null)
            {
                jObject.Add("Path_TBD", Path_TBD);
            }

            if (Path_gbXML != null)
            {
                jObject.Add("Path_gbXML", Path_gbXML);
            }

            if (WeatherData != null)
            {
                jObject.Add("WeatherData", WeatherData.ToJsonObject());
            }

            if (DesignDays_Heating != null)
            {
                JsonArray jArray = new JsonArray();
                foreach(DesignDay designDay in DesignDays_Heating)
                {
                    jArray.Add(designDay.ToJsonObject());
                }

                jObject.Add("DesignDays_Heating", jArray);
            }

            if (DesignDays_Cooling != null)
            {
                JsonArray jArray = new JsonArray();
                foreach (DesignDay designDay in DesignDays_Cooling)
                {
                    jArray.Add(designDay.ToJsonObject());
                }

                jObject.Add("DesignDays_Cooling", jArray);
            }

            if (SurfaceOutputSpecs != null)
            {
                JsonArray jArray = new JsonArray();
                foreach (SurfaceOutputSpec surfaceOutputSpec in SurfaceOutputSpecs)
                {
                    jArray.Add(surfaceOutputSpec.ToJsonObject());
                }

                jObject.Add("SurfaceOutputSpecs", jArray);
            }

            jObject.Add("UnmetHours", UnmetHours);
            jObject.Add("Simulate", Simulate);
            jObject.Add("Sizing", Sizing);
            jObject.Add("UpdateZones", UpdateZones);
            jObject.Add("UseWidths", UseWidths);
            jObject.Add("AddIZAMs", AddIZAMs);

            jObject.Add("SimulateFrom", SimulateFrom);
            jObject.Add("SimulateTo", SimulateTo);

            jObject.Add("RemoveExistingTBD", RemoveExistingTBD);

            jObject.Add("UpdateWindowPositionType", UpdateWindowPositionType);

            return jObject;
        }
    }
}
