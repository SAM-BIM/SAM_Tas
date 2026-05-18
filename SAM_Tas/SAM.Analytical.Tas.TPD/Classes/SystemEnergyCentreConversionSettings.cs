// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Text.Json.Nodes;
using SAM.Core;

namespace SAM.Analytical.Tas.TPD
{
    public class SystemEnergyCentreConversionSettings : IJSAMObject
    {
        public bool Simulate { get; set; } = true;
        public int StartHour { get; set; } = 0;
        public int EndHour { get; set; } = 8759;
        public bool IncludeComponentResults { get; set; } = false;
        public bool IncludeControllerResults { get; set; } = false;
        public bool RenameAirSystemGroups { get; set; } = false;

        public SystemEnergyCentreConversionSettings() 
        { 
        }

        public SystemEnergyCentreConversionSettings(JsonObject jObject)
        {
            FromJsonObject(jObject);
        }

        public SystemEnergyCentreConversionSettings(SystemEnergyCentreConversionSettings systemEnergyCentreConversionSettings)
        {
            if (systemEnergyCentreConversionSettings != null)
            {
                Simulate = systemEnergyCentreConversionSettings.Simulate;
                StartHour = systemEnergyCentreConversionSettings.StartHour;
                EndHour = systemEnergyCentreConversionSettings.EndHour;
                IncludeComponentResults = systemEnergyCentreConversionSettings.IncludeComponentResults;
                IncludeControllerResults = systemEnergyCentreConversionSettings.IncludeControllerResults;
                RenameAirSystemGroups = systemEnergyCentreConversionSettings.RenameAirSystemGroups;
            }
        }

        public ComponentConversionSettings GetComponentConversionSettings()
        {
            return new ComponentConversionSettings() { StartHour = StartHour, EndHour = EndHour, IncludeComponentResults = IncludeComponentResults, IncludeControllerResults = IncludeControllerResults };
        }

        public bool FromJsonObject(JsonObject jObject)
        {
            if(jObject == null)
            {
                return false;
            }

            if (jObject.ContainsKey("Simulate"))
            {
                Simulate = jObject["Simulate"]?.GetValue<bool>() ?? default(bool);
            }

            if (jObject.ContainsKey("StartHour"))
            {
                StartHour = jObject["StartHour"]?.GetValue<int>() ?? default(int);
            }

            if (jObject.ContainsKey("EndHour"))
            {
                EndHour = jObject["EndHour"]?.GetValue<int>() ?? default(int);
            }

            if (jObject.ContainsKey("IncludeComponentResults"))
            {
                IncludeComponentResults = jObject["IncludeComponentResults"]?.GetValue<bool>() ?? default(bool);
            }

            if (jObject.ContainsKey("IncludeControllerResults"))
            {
                IncludeControllerResults = jObject["IncludeControllerResults"]?.GetValue<bool>() ?? default(bool);
            }

            if (jObject.ContainsKey("RenameAirSystemGroups"))
            {
                RenameAirSystemGroups = jObject["RenameAirSystemGroups"]?.GetValue<bool>() ?? default(bool);
            }

            return true;
        }

        public JsonObject ToJsonObject()
        {
            JsonObject result = new JsonObject();
            result.Add("_type", Core.Query.FullTypeName(this));

            result.Add("Simulate", Simulate);

            result.Add("StartHour", StartHour);
            result.Add("EndHour", EndHour);

            result.Add("IncludeComponentResults", IncludeComponentResults);

            result.Add("IncludeControllerResults", IncludeControllerResults);

            result.Add("RenameAirSystemGroups", RenameAirSystemGroups);

            return result;
        }
    }
}
