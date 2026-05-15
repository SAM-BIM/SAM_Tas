using System.Text.Json.Nodes;
using SAM.Core;
using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public class TSDConversionSettings : IJSAMObject
    {
        public HashSet<SpaceDataType> SpaceDataTypes { get; set; } = null;

        public HashSet<PanelDataType> PanelDataTypes { get; set; } = null;

        public HashSet<string> SpaceNames { get; set; } = null;

        public HashSet<string> ZoneNames { get; set; } = null;

        public bool ConvertWeaterData { get; set; } = true;

        public bool ConvertZones { get; set; } = false;

        public TSDConversionSettings()
        {

        }

        public TSDConversionSettings(JsonObject jObject)
        {
            FromJsonObject(jObject);
        }

        public TSDConversionSettings(TSDConversionSettings tSDConversionSettings)
        {
            if(tSDConversionSettings != null)
            {
                SpaceDataTypes = tSDConversionSettings.SpaceDataTypes == null ? null : new HashSet<SpaceDataType>(tSDConversionSettings.SpaceDataTypes);
                PanelDataTypes = tSDConversionSettings.PanelDataTypes == null ? null : new HashSet<PanelDataType>(tSDConversionSettings.PanelDataTypes);
                ConvertWeaterData = tSDConversionSettings.ConvertWeaterData;
                ConvertZones = tSDConversionSettings.ConvertZones;
                SpaceNames = tSDConversionSettings.SpaceNames == null ? null : new HashSet<string>(tSDConversionSettings.SpaceNames);
                ZoneNames = tSDConversionSettings.ZoneNames == null ? null : new HashSet<string>(tSDConversionSettings.ZoneNames);
            }
        }

        public bool FromJsonObject(JsonObject jObject)
        {
            if(jObject == null)
            {
                return false;
            }

            if(jObject.ContainsKey("SpaceDataTypes"))
            {
                JsonArray jArray = jObject["SpaceDataTypes"] as JsonArray;
                if(jArray != null)
                {
                    SpaceDataTypes = new HashSet<SpaceDataType>();
                    foreach(string @string in jArray)
                    {
                        if(Core.Query.TryGetEnum(@string, out SpaceDataType spaceDataType))
                        {
                            SpaceDataTypes.Add(spaceDataType);
                        }
                    }
                }
            }

            if (jObject.ContainsKey("PanelDataTypes"))
            {
                JsonArray jArray = jObject["PanelDataTypes"] as JsonArray;
                if (jArray != null)
                {
                    PanelDataTypes = new HashSet<PanelDataType>();
                    foreach (string @string in jArray)
                    {
                        if (Core.Query.TryGetEnum(@string, out PanelDataType panelDataType))
                        {
                            PanelDataTypes.Add(panelDataType);
                        }
                    }
                }
            }

            if (jObject.ContainsKey("SpaceNames"))
            {
                JsonArray jArray = jObject["SpaceNames"] as JsonArray;
                if (jArray != null)
                {
                    SpaceNames = new HashSet<string>();
                    foreach (string @string in jArray)
                    {
                        SpaceNames.Add(@string);
                    }
                }
            }

            if (jObject.ContainsKey("ZoneNames"))
            {
                JsonArray jArray = jObject["ZoneNames"] as JsonArray;
                if (jArray != null)
                {
                    ZoneNames = new HashSet<string>();
                    foreach (string @string in jArray)
                    {
                        ZoneNames.Add(@string);
                    }
                }
            }

            if (jObject.ContainsKey("ConvertWeaterData"))
            {
                ConvertWeaterData = jObject["ConvertWeaterData"]?.GetValue<bool>() ?? default(bool);
            }

            if (jObject.ContainsKey("ConvertZones"))
            {
                ConvertZones = jObject["ConvertZones"]?.GetValue<bool>() ?? default(bool);
            }

            return true;
        }

        public JsonObject ToJsonObject()
        {
            JsonObject jObject = new JsonObject();
            jObject.Add("_type", Core.Query.FullTypeName(this));

            if (SpaceDataTypes != null)
            {
                JsonArray jArray = new JsonArray();
                foreach (SpaceDataType spaceDataType in SpaceDataTypes)
                {
                    jArray.Add(spaceDataType.ToString());
                }

                jObject.Add("SpaceDataTypes", jArray);
            }

            if (PanelDataTypes != null)
            {
                JsonArray jArray = new JsonArray();
                foreach (PanelDataType panelDataType in PanelDataTypes)
                {
                    jArray.Add(panelDataType.ToString());
                }

                jObject.Add("PanelDataTypes", jArray);
            }

            if (SpaceNames != null)
            {
                JsonArray jArray = new JsonArray();
                foreach (string spaceName in SpaceNames)
                {
                    if(spaceName == null)
                    {
                        continue;
                    }

                    jArray.Add(spaceName);
                }

                jObject.Add("SpaceNames", jArray);
            }

            if (ZoneNames != null)
            {
                JsonArray jArray = new JsonArray();
                foreach (string zoneName in ZoneNames)
                {
                    if (zoneName == null)
                    {
                        continue;
                    }

                    jArray.Add(zoneName);
                }

                jObject.Add("ZoneNames", jArray);
            }

            jObject.Add("ConvertWeaterData", ConvertWeaterData);

            jObject.Add("ConvertZones", ConvertZones);

            return jObject;
        }
    }
}
