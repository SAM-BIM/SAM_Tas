using System.Text.Json.Nodes;
using SAM.Core;
using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public class ConstructionCalculationData : IConstructionCalculationData
    {
        public string ConstructionName { get; set; }
        public HashSet<string> ConstructionNames { get; set; }
        public double ThermalTransmittance { get; set; }
        public HeatFlowDirection HeatFlowDirection { get; set; }
        public bool External { get; set; }
        public Range<double> ThicknessRange { get; set; } = new Range<double>(0.001, 1);

        public ConstructionCalculationData()
        {

        }
        
        public ConstructionCalculationData(JsonObject jObject)
        {
            FromJsonObject(jObject);
        }

        public ConstructionCalculationData(ConstructionCalculationData constructionCalculationData)
        {
            if(constructionCalculationData != null)
            {
                ConstructionName = constructionCalculationData.ConstructionName;
                ConstructionNames = constructionCalculationData.ConstructionNames == null ? null : new HashSet<string>(constructionCalculationData.ConstructionNames);
                ThermalTransmittance = constructionCalculationData.ThermalTransmittance;
                HeatFlowDirection = constructionCalculationData.HeatFlowDirection;
                External = constructionCalculationData.External;
                ThicknessRange = constructionCalculationData.ThicknessRange == null ? null : new Range<double>(constructionCalculationData.ThicknessRange);
            }
        }

        public ConstructionCalculationData(string constrcutionName, IEnumerable<string> constructionNames, double thermalTransmittance, HeatFlowDirection heatFlowDirection, bool external)
        {
            ConstructionName = constrcutionName;

            if(constructionNames != null)
            {
                this.ConstructionNames = new HashSet<string>();
                foreach(string constructionName in constructionNames)
                {
                    this.ConstructionNames.Add(constructionName);
                }
            }

            ThermalTransmittance = thermalTransmittance;
            HeatFlowDirection = heatFlowDirection;
            External = external;
        }

        public bool FromJsonObject(JsonObject jObject)
        {
            if(jObject == null)
            {
                return false;
            }

            if (jObject.ContainsKey("ConstructionName"))
            {
                ConstructionName = jObject["ConstructionName"]?.GetValue<string>() ?? null;
            }

            if (jObject.ContainsKey("ConstructionNames"))
            {
                JsonArray jArray = jObject["ConstructionNames"] as JsonArray;
                if(jArray != null)
                {
                    ConstructionNames = new HashSet<string>();
                    foreach(string constructionName in jArray)
                    {
                        ConstructionNames.Add(constructionName);
                    }
                }
            }

            if (jObject.ContainsKey("ThermalTransmittance"))
            {
                ThermalTransmittance = jObject["ThermalTransmittance"]?.GetValue<double>() ?? default(double);
            }

            if (jObject.ContainsKey("HeatFlowDirection"))
            {
                HeatFlowDirection = Core.Query.Enum<HeatFlowDirection>(jObject["HeatFlowDirection"]?.GetValue<string>() ?? null);
            }

            if (jObject.ContainsKey("External"))
            {
                External = jObject["External"]?.GetValue<bool>() ?? default(bool);
            }

            return true;
        }

        public JsonObject ToJsonObject()
        {
            JsonObject jObject = new JsonObject();
            jObject.Add("_type", Core.Query.FullTypeName(this));

            if(ConstructionName != null)
            {
                jObject.Add("ConstructionName", ConstructionName);
            }

            if(ConstructionNames != null)
            {
                JsonArray jArray = new JsonArray();
                foreach(string constructionName in ConstructionNames)
                {
                    jArray.Add(constructionName);
                }
                
                jObject.Add("ConstructionNames", jArray);
            }

            if(!double.IsNaN(ThermalTransmittance))
            {
                jObject.Add("ThermalTransmittance", ThermalTransmittance);
            }

            if (HeatFlowDirection != HeatFlowDirection.Undefined)
            {
                jObject.Add("HeatFlowDirection", HeatFlowDirection.ToString());
            }

            jObject.Add("External", External);

            if(ThicknessRange != null)
            {
                jObject.Add("ThicknessRange", ThicknessRange.ToJsonObject());
            }

            return jObject;
        }
    }
}
