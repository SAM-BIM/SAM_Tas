// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Text.Json.Nodes;
using SAM.Core;

namespace SAM.Analytical.Tas
{
    public class GlazingCalculationResult : Result
    {
        private double totalSolarEnergyTransmittance;
        private double lightTransmittance;
        private double thermalTransmittance;

        public GlazingCalculationResult(JsonObject jObject)
            : base(jObject)
        {
            FromJsonObject(jObject);
        }
        

        public GlazingCalculationResult(GlazingCalculationResult solarTransmittanceCalculationResult)
            : base(solarTransmittanceCalculationResult)
        {
            if(solarTransmittanceCalculationResult != null)
            {
                totalSolarEnergyTransmittance = solarTransmittanceCalculationResult.totalSolarEnergyTransmittance;
                lightTransmittance = solarTransmittanceCalculationResult.lightTransmittance;
                thermalTransmittance = solarTransmittanceCalculationResult.thermalTransmittance;
            }
        }

        public GlazingCalculationResult(System.Guid constructionGuid, string source, double totalSolarEnergyTransmittance, double lightTransmittance, double thermalTransmittance)
            : base(typeof(GlazingCalculationResult).ToString(), source, constructionGuid.ToString())
        {
            this.totalSolarEnergyTransmittance = totalSolarEnergyTransmittance;
            this.thermalTransmittance = thermalTransmittance;
            this.lightTransmittance = lightTransmittance;
        }

        public double TotalSolarEnergyTransmittance
        {
            get
            {
                return totalSolarEnergyTransmittance;
            }
        }

        public double LightTransmittance
        {
            get
            {
                return lightTransmittance;
            }
        }

        public double ThermalTransmittance
        {
            get
            {
                return thermalTransmittance;
            }
        }

        public double Factor
        {
            get
            {
                double? value = Query.Factor(TotalSolarEnergyTransmittance, LightTransmittance);
                if(value == null || !value.HasValue)
                {
                    return double.NaN;
                }

                return value.Value;
            }
        }

        public override bool FromJsonObject(JsonObject jObject)
        {
            if (jObject == null)
            {
                return false;
            }

            bool result = base.FromJsonObject(jObject);
            if(!result)
            {
                return result;
            }

            if(jObject.ContainsKey("TotalSolarEnergyTransmittance"))
            {
                totalSolarEnergyTransmittance = jObject["TotalSolarEnergyTransmittance"]?.GetValue<double>() ?? default(double);
            }

            if (jObject.ContainsKey("LightTransmittance"))
            {
                lightTransmittance = jObject["LightTransmittance"]?.GetValue<double>() ?? default(double);
            }

            if (jObject.ContainsKey("ThermalTransmittance"))
            {
                thermalTransmittance = jObject["ThermalTransmittance"]?.GetValue<double>() ?? default(double);
            }

            return result;
        }

        public JsonObject ToJsonObject()
        {
            JsonObject jObject = base.ToJsonObject();
            if(jObject == null)
            {
                return null;
            }

            if (!double.IsNaN(TotalSolarEnergyTransmittance))
            {
                jObject.Add("TotalSolarEnergyTransmittance", totalSolarEnergyTransmittance);
            }

            if (!double.IsNaN(lightTransmittance))
            {
                jObject.Add("LightTransmittance", lightTransmittance);
            }

            if (!double.IsNaN(ThermalTransmittance))
            {
                jObject.Add("ThermalTransmittance", ThermalTransmittance);
            }

            return jObject;
        }
    }
}
