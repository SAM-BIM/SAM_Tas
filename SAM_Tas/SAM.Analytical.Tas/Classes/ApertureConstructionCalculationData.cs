using System.Text.Json.Nodes;
using SAM.Core;
using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public class ApertureConstructionCalculationData : IApertureConstructionCalculationData
    {
        public ApertureType ApertureType { get; set; } = ApertureType.Undefined;
        public string ApertureConstructionName { get; set; }
        public HashSet<string> ApertureConstructionNames { get; set; }
        public double PaneThermalTransmittance { get; set; }
        public double FrameThermalTransmittance { get; set; }
        public HeatFlowDirection HeatFlowDirection { get; set; }
        public bool External { get; set; }
        public Range<double> ThicknessRange { get; set; } = new Range<double>(0.001, 1);

        public ApertureConstructionCalculationData()
        {

        }
        
        public ApertureConstructionCalculationData(JsonObject jObject)
        {
            FromJsonObject(jObject);
        }

        public ApertureConstructionCalculationData(ApertureConstructionCalculationData apertureConstructionCalculationData)
        {
            if(apertureConstructionCalculationData != null)
            {
                ApertureType = apertureConstructionCalculationData.ApertureType;
                ApertureConstructionName = apertureConstructionCalculationData.ApertureConstructionName;
                ApertureConstructionNames = apertureConstructionCalculationData.ApertureConstructionNames == null ? null : new HashSet<string>(apertureConstructionCalculationData.ApertureConstructionNames);
                PaneThermalTransmittance = apertureConstructionCalculationData.PaneThermalTransmittance;
                FrameThermalTransmittance = apertureConstructionCalculationData.FrameThermalTransmittance;
                HeatFlowDirection = apertureConstructionCalculationData.HeatFlowDirection;
                External = apertureConstructionCalculationData.External;
                ThicknessRange = apertureConstructionCalculationData.ThicknessRange == null ? null : new Range<double>(apertureConstructionCalculationData.ThicknessRange);
            }
        }

        public ApertureConstructionCalculationData(ApertureType apertureType, string apertureConstrcutionName, IEnumerable<string> apertureConstructionNames, double paneThermalTransmittance, double frameThermalTransmittance, HeatFlowDirection heatFlowDirection, bool external)
        {
            ApertureType = apertureType;
            
            ApertureConstructionName = apertureConstrcutionName;

            if(apertureConstructionNames != null)
            {
                this.ApertureConstructionNames = new HashSet<string>();
                foreach(string constructionName in apertureConstructionNames)
                {
                    this.ApertureConstructionNames.Add(constructionName);
                }
            }

            PaneThermalTransmittance = paneThermalTransmittance;
            FrameThermalTransmittance= frameThermalTransmittance;
            HeatFlowDirection = heatFlowDirection;
            External = external;
        }

        public bool FromJsonObject(JsonObject jObject)
        {
            if(jObject == null)
            {
                return false;
            }

            if (jObject.ContainsKey("ApertureType"))
            {
                ApertureType = Core.Query.Enum<ApertureType>(jObject["ApertureType"]?.GetValue<string>() ?? null);
            }

            if (jObject.ContainsKey("ApertureConstructionName"))
            {
                ApertureConstructionName = jObject["ApertureConstructionName"]?.GetValue<string>() ?? null;
            }

            if (jObject.ContainsKey("ApertureConstructionNames"))
            {
                JsonArray jArray = jObject["ApertureConstructionNames"] as JsonArray;
                if(jArray != null)
                {
                    ApertureConstructionNames = new HashSet<string>();
                    foreach(string apertureConstructionName in jArray)
                    {
                        ApertureConstructionNames.Add(apertureConstructionName);
                    }
                }
            }

            if (jObject.ContainsKey("PaneThermalTransmittance"))
            {
                PaneThermalTransmittance = jObject["PaneThermalTransmittance"]?.GetValue<double>() ?? default(double);
            }

            if (jObject.ContainsKey("FrameThermalTransmittance"))
            {
                FrameThermalTransmittance = jObject["FrameThermalTransmittance"]?.GetValue<double>() ?? default(double);
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

            if(ApertureConstructionName != null)
            {
                jObject.Add("ApertureConstructionName", ApertureConstructionName);
            }

            if(ApertureType != ApertureType.Undefined)
            {
                jObject.Add("ApertureType", ApertureType.ToString());
            }

            if(ApertureConstructionNames != null)
            {
                JsonArray jArray = new JsonArray();
                foreach(string apertureConstructionName in ApertureConstructionNames)
                {
                    jArray.Add(apertureConstructionName);
                }
                
                jObject.Add("ApertureConstructionNames", jArray);
            }

            if(!double.IsNaN(PaneThermalTransmittance))
            {
                jObject.Add("PaneThermalTransmittance", PaneThermalTransmittance);
            }

            if (!double.IsNaN(FrameThermalTransmittance))
            {
                jObject.Add("FrameThermalTransmittance", FrameThermalTransmittance);
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
