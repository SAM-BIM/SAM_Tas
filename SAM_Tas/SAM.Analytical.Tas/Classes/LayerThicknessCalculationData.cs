// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Text.Json.Nodes;
using SAM.Core;

namespace SAM.Analytical.Tas
{
    public class LayerThicknessCalculationData: IConstructionCalculationData
    {
        public string ConstructionName { get; set; }
        public int LayerIndex { get; set; }
        public double ThermalTransmittance { get; set; }
        public HeatFlowDirection HeatFlowDirection { get; set; }
        public bool External { get; set; }
        public Range<double> ThicknessRange { get; set; } = new Range<double>(0.001, 1);

        public LayerThicknessCalculationData()
        {

        }
        
        public LayerThicknessCalculationData(JsonObject jObject)
        {
            FromJsonObject(jObject);
        }

        public LayerThicknessCalculationData(LayerThicknessCalculationData layerThicknessCalculationData)
        {
            if(layerThicknessCalculationData != null)
            {
                ConstructionName = layerThicknessCalculationData.ConstructionName;
                LayerIndex = layerThicknessCalculationData.LayerIndex;
                ThermalTransmittance = layerThicknessCalculationData.ThermalTransmittance;
                HeatFlowDirection = layerThicknessCalculationData.HeatFlowDirection;
                External = layerThicknessCalculationData.External;
                ThicknessRange = layerThicknessCalculationData.ThicknessRange == null ? null : new Range<double>(layerThicknessCalculationData.ThicknessRange);
            }
        }

        public LayerThicknessCalculationData(string constructionName, int layerIndex, double thermalTransmittance, HeatFlowDirection heatFlowDirection, bool external)
        {
            ConstructionName = constructionName;
            LayerIndex = layerIndex;
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

            if(jObject.ContainsKey("ConstructionName"))
            {
                ConstructionName = jObject["ConstructionName"]?.GetValue<string>() ?? null;
            }

            if (jObject.ContainsKey("LayerIndex"))
            {
                LayerIndex = jObject["LayerIndex"]?.GetValue<int>() ?? default(int);
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

            jObject.Add("LayerIndex", LayerIndex);

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
