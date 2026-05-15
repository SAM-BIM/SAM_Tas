using System.Text.Json.Nodes;
using SAM.Core;

namespace SAM.Analytical.Tas.TPD
{
    public class ComponentConversionSettings : IJSAMObject
    {
        public int StartHour { get; set; } = 0;
        public int EndHour { get; set; } = 8759;
        public bool IncludeComponentResults { get; set; } = true;
        public bool IncludeControllerResults { get; set; } = false;

        public ComponentConversionSettings() 
        { 
        }

        public ComponentConversionSettings(JsonObject jObject)
        {
            FromJsonObject(jObject);
        }

        public ComponentConversionSettings(ComponentConversionSettings componentConversionSettings)
        {
            if (componentConversionSettings != null)
            {
                StartHour = componentConversionSettings.StartHour;
                EndHour = componentConversionSettings.EndHour;
                IncludeComponentResults = componentConversionSettings.IncludeComponentResults;
                IncludeControllerResults = componentConversionSettings.IncludeControllerResults;
            }
        }

        public bool FromJsonObject(JsonObject jObject)
        {
            if(jObject == null)
            {
                return false;
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

            return true;
        }

        public JsonObject ToJsonObject()
        {
            JsonObject result = new JsonObject();
            result.Add("_type", Core.Query.FullTypeName(this));

            result.Add("StartHour", StartHour);
            result.Add("EndHour", EndHour);

            result.Add("IncludeComponentResults", IncludeComponentResults);

            result.Add("IncludeControllerResults", IncludeControllerResults);

            return result;
        }
    }
}
