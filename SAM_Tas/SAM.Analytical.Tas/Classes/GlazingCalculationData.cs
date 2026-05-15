// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Text.Json.Nodes;
using SAM.Core;

namespace SAM.Analytical.Tas
{
    public class GlazingCalculationData : IJSAMObject
    {
        public System.Guid ConstructionGuid { get; set; } = System.Guid.Empty;
        public double TotalSolarEnergyTransmittance { get; set; } = double.NaN;
        public Range<double> TotalSolarEnergyTransmittanceRange { get; set; } = new Range<double>(-0.02, 0);
        public double LightTransmittance { get; set; } = double.NaN;
        public Range<double> LightTransmittanceRange { get; set; } = new Range<double>(-0.02, 0);
        public Range<double> ThicknessRange { get; set; } = new Range<double>(0.001, 1);

        public GlazingCalculationData()
        {

        }
        
        public GlazingCalculationData(JsonObject jObject)
        {
            FromJsonObject(jObject);
        }

        public GlazingCalculationData(GlazingCalculationData glazingCalculationData)
        {
            if(glazingCalculationData != null)
            {
                ConstructionGuid = glazingCalculationData.ConstructionGuid;
                TotalSolarEnergyTransmittance = glazingCalculationData.TotalSolarEnergyTransmittance;
                TotalSolarEnergyTransmittanceRange = glazingCalculationData.TotalSolarEnergyTransmittanceRange == null ? null : new Range<double>(glazingCalculationData.TotalSolarEnergyTransmittanceRange);
                LightTransmittance = glazingCalculationData.LightTransmittance;
                LightTransmittanceRange = glazingCalculationData.LightTransmittanceRange == null ? null : new Range<double>(glazingCalculationData.LightTransmittanceRange);
                ThicknessRange = glazingCalculationData.ThicknessRange == null ? null : new Range<double>(glazingCalculationData.ThicknessRange);
            }
        }

        public GlazingCalculationData(System.Guid constructionGuid, double totalSolarEnergyTransmittance, Range<double> totalSolarEnergyTransmittanceRange, double lightTransmittance, Range<double> lightTransmittanceRange, Range<double> thicknessRange)
        {
            ConstructionGuid = constructionGuid;
            TotalSolarEnergyTransmittance = totalSolarEnergyTransmittance;
            TotalSolarEnergyTransmittanceRange = totalSolarEnergyTransmittanceRange == null ? null : new Range<double>(totalSolarEnergyTransmittanceRange);
            LightTransmittance = lightTransmittance;
            LightTransmittanceRange = lightTransmittanceRange == null ? null : new Range<double>(lightTransmittanceRange);
            ThicknessRange = thicknessRange == null ? null : new Range<double>(thicknessRange);
        }

        public double Factor
        {
            get
            {
                double? value = Query.Factor(TotalSolarEnergyTransmittance, LightTransmittance);
                if (value == null || !value.HasValue)
                {
                    return double.NaN;
                }

                return value.Value;
            }
        }

        public bool FromJsonObject(JsonObject jObject)
        {
            if(jObject == null)
            {
                return false;
            }

            if(jObject.ContainsKey("ConstructionGuid"))
            {
                ConstructionGuid = System.Guid.Parse(jObject["ConstructionGuid"]?.GetValue<string>() ?? null);
            }

            if (jObject.ContainsKey("TotalSolarEnergyTransmittance"))
            {
                TotalSolarEnergyTransmittance = jObject["TotalSolarEnergyTransmittance"]?.GetValue<double>() ?? default(double);
            }

            if (jObject.ContainsKey("TotalSolarEnergyTransmittanceRange"))
            {
                TotalSolarEnergyTransmittanceRange = new Range<double>(jObject["TotalSolarEnergyTransmittanceRange"] as JsonObject);
            }

            if (jObject.ContainsKey("LightTransmittance"))
            {
                LightTransmittance = jObject["LightTransmittance"]?.GetValue<double>() ?? default(double);
            }

            if (jObject.ContainsKey("LightTransmittanceRange"))
            {
                LightTransmittanceRange = new Range<double>(jObject["LightTransmittanceRange"] as JsonObject);
            }

            if (jObject.ContainsKey("ThicknessRange"))
            {
                ThicknessRange = new Range<double>(jObject["ThicknessRange"] as JsonObject);
            }

            return true;
        }

        public JsonObject ToJsonObject()
        {
            JsonObject jObject = new JsonObject();
            jObject.Add("_type", Core.Query.FullTypeName(this));

            if(ConstructionGuid != System.Guid.Empty)
            {
                jObject.Add("ConstructionGuid", ConstructionGuid.ToString());
            }

            if(!double.IsNaN(TotalSolarEnergyTransmittance))
            {
                jObject.Add("TotalSolarEnergyTransmittance", TotalSolarEnergyTransmittance);
            }

            if(TotalSolarEnergyTransmittanceRange != null)
            {
                jObject.Add("TotalSolarEnergyTransmittanceRange", TotalSolarEnergyTransmittanceRange.ToJsonObject());
            }

            if (!double.IsNaN(LightTransmittance))
            {
                jObject.Add("LightTransmittance", LightTransmittance);
            }

            if (LightTransmittanceRange != null)
            {
                jObject.Add("LightTransmittanceRange", LightTransmittanceRange.ToJsonObject());
            }

            if (ThicknessRange != null)
            {
                jObject.Add("ThicknessRange", ThicknessRange.ToJsonObject());
            }

            return jObject;
        }
    }
}
