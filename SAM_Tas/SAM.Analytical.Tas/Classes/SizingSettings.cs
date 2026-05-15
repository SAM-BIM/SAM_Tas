// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Text.Json.Nodes;
using SAM.Core;

namespace SAM.Analytical.Tas
{
    public class SizingSettings : IJSAMObject
    {
        public bool ExcludeOutdoorAir { get; set; } = false;

        public bool ExcludePositiveInternalGains { get; set; } = false;

        public bool GenerateUncappedFile { get; set; } = true;

        public bool GenerateHDDCDDFile { get; set; } = true;

        public bool SystemSizingMethod { get; set; } = false;

        public SizingSettings()
        {

        }

        public SizingSettings(JsonObject jObject)
        {
            FromJsonObject(jObject);
        }

        public SizingSettings(bool excludeOutdoorAir, bool excludePositiveInternalGains, bool generateUncappedFile, bool generateHDDCDDFile, bool systemSizingMethod)
        {
            ExcludeOutdoorAir = excludeOutdoorAir;
            ExcludePositiveInternalGains = excludePositiveInternalGains;
            GenerateUncappedFile = generateUncappedFile;
            GenerateHDDCDDFile = generateHDDCDDFile;
            SystemSizingMethod = systemSizingMethod;
        }

        public SizingSettings(SizingSettings sizingSettings)
        {
            if(sizingSettings != null)
            {
                ExcludeOutdoorAir = sizingSettings.ExcludeOutdoorAir;
                ExcludePositiveInternalGains = sizingSettings.ExcludePositiveInternalGains;
                GenerateUncappedFile = sizingSettings.GenerateUncappedFile;
                GenerateHDDCDDFile = sizingSettings.GenerateHDDCDDFile;
                SystemSizingMethod = sizingSettings.SystemSizingMethod;
            }
        }

        public bool FromJsonObject(JsonObject jObject)
        {
            if(jObject == null)
            {
                return false;
            }

            if(jObject.ContainsKey("ExcludeOutdoorAir"))
            {
                ExcludeOutdoorAir = jObject["ExcludeOutdoorAir"]?.GetValue<bool>() ?? default(bool);
            }

            if (jObject.ContainsKey("ExcludePositiveInternalGains"))
            {
                ExcludePositiveInternalGains = jObject["ExcludePositiveInternalGains"]?.GetValue<bool>() ?? default(bool);
            }

            if (jObject.ContainsKey("GenerateUncappedFile"))
            {
                GenerateUncappedFile = jObject["GenerateUncappedFile"]?.GetValue<bool>() ?? default(bool);
            }

            if (jObject.ContainsKey("GenerateHDDCDDFile"))
            {
                GenerateHDDCDDFile = jObject["GenerateHDDCDDFile"]?.GetValue<bool>() ?? default(bool);
            }

            if (jObject.ContainsKey("SystemSizingMethod"))
            {
                SystemSizingMethod = jObject["SystemSizingMethod"]?.GetValue<bool>() ?? default(bool);
            }

            return true;
        }

        public JsonObject ToJsonObject()
        {
            JsonObject jObject = new JsonObject();
            jObject.Add("_type", Core.Query.FullTypeName(this));
            
            jObject.Add("ExcludeOutdoorAir", ExcludeOutdoorAir);
            jObject.Add("ExcludePositiveInternalGains", ExcludePositiveInternalGains);
            jObject.Add("GenerateUncappedFile", GenerateUncappedFile);
            jObject.Add("GenerateHDDCDDFile", GenerateHDDCDDFile);
            jObject.Add("SystemSizingMethod", SystemSizingMethod);

            return jObject;
        }
    }
}
