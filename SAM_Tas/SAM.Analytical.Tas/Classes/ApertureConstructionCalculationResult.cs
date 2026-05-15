// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Text.Json.Nodes;
using SAM.Core;

namespace SAM.Analytical.Tas
{
    public class ApertureConstructionCalculationResult : Result, IApertureConstructionCalculationResult
    {
        private ApertureType apertureType = ApertureType.Undefined;
        
        private string initialApertureConstructionName = null;
        private double initialPaneThermalTransmittance = double.NaN;
        private double initialFrameThermalTransmittance = double.NaN;

        private string apertureConstructionName;
        private double paneThermalTransmittance;
        private double frameThermalTransmittance;

        private double calculatedPaneThermalTransmittance;
        private double calculatedFrameThermalTransmittance;

        public ApertureConstructionCalculationResult(JsonObject jObject)
            :base(jObject)
        {

        }

        public ApertureConstructionCalculationResult(ApertureConstructionCalculationResult apertureConstructionCalculationResult)
            :base(apertureConstructionCalculationResult)
        {
            if(apertureConstructionCalculationResult != null)
            {
                apertureType = apertureConstructionCalculationResult.apertureType;

                initialApertureConstructionName = apertureConstructionCalculationResult.initialApertureConstructionName;
                initialPaneThermalTransmittance = apertureConstructionCalculationResult.initialPaneThermalTransmittance;
                initialFrameThermalTransmittance = apertureConstructionCalculationResult.initialFrameThermalTransmittance;

                apertureConstructionName = apertureConstructionCalculationResult.apertureConstructionName;
                paneThermalTransmittance = apertureConstructionCalculationResult.paneThermalTransmittance;
                frameThermalTransmittance = apertureConstructionCalculationResult.frameThermalTransmittance;

                calculatedPaneThermalTransmittance = apertureConstructionCalculationResult.calculatedPaneThermalTransmittance;
                calculatedFrameThermalTransmittance = apertureConstructionCalculationResult.calculatedFrameThermalTransmittance;
            }
        }

        public ApertureConstructionCalculationResult(
            string source, 
            ApertureType apertureType,
            string initialApertureConstructionName, 
            double initialPaneThermalTransmittance, 
            double initialFrameThermalTransmittance, 
            string apertureConstructionName, 
            double paneThermalTransmittance,
            double frameThermalTransmittance,
            double calculatedPaneThermalTransmittance,
            double calculatedFrameThermalTransmittance)
            : base(apertureConstructionName, source, apertureConstructionName)
        {
            this.apertureType = apertureType;

            this.initialApertureConstructionName = initialApertureConstructionName;
            this.initialPaneThermalTransmittance = initialPaneThermalTransmittance;
            this.initialFrameThermalTransmittance = initialFrameThermalTransmittance;

            this.apertureConstructionName = apertureConstructionName;
            this.paneThermalTransmittance = paneThermalTransmittance;
            this.frameThermalTransmittance = frameThermalTransmittance;

            this.calculatedPaneThermalTransmittance = calculatedPaneThermalTransmittance;
            this.calculatedFrameThermalTransmittance = calculatedFrameThermalTransmittance;
        }

        public ApertureType ApertureType
        {
            get
            {
                return apertureType;
            }
        }

        public string InitialApertureConstructionName
        {
            get
            {
                return initialApertureConstructionName;
            }
        }

        public double InitialPaneThermalTransmittance
        {
            get
            {
                return initialPaneThermalTransmittance;
            }
        }

        public double InitialFrameThermalTransmittance
        {
            get
            {
                return initialFrameThermalTransmittance;
            }
        }

        public string ApertureConstructionName
        {
            get
            {
                return apertureConstructionName;
            }
        }

        public double PaneThermalTransmittance
        {
            get
            {
                return paneThermalTransmittance;
            }
        }

        public double FrameThermalTransmittance
        {
            get
            {
                return frameThermalTransmittance;
            }
        }

        public double CalculatedPaneThermalTransmittance
        {
            get
            {
                return calculatedPaneThermalTransmittance;
            }
        }

        public double CalculatedFrameThermalTransmittance
        {
            get
            {
                return calculatedFrameThermalTransmittance;
            }
        }

        public override bool FromJsonObject(JsonObject jObject)
        {
            if(!base.FromJsonObject(jObject))
            {
                return false;
            }

            if (jObject.ContainsKey("ApertureType"))
            {
                apertureType = Core.Query.Enum<ApertureType>(jObject["ApertureType"]?.GetValue<string>() ?? null);
            }

            if (jObject.ContainsKey("InitialApertureConstructionName"))
            {
                initialApertureConstructionName = jObject["InitialApertureConstructionName"]?.GetValue<string>() ?? null;
            }

            if (jObject.ContainsKey("InitialPaneThermalTransmittance"))
            {
                initialPaneThermalTransmittance = jObject["InitialPaneThermalTransmittance"]?.GetValue<double>() ?? default(double);
            }

            if (jObject.ContainsKey("InitialFrameThermalTransmittance"))
            {
                initialFrameThermalTransmittance = jObject["InitialFrameThermalTransmittance"]?.GetValue<double>() ?? default(double);
            }

            if (jObject.ContainsKey("ApertureConstructionName"))
            {
                apertureConstructionName = jObject["ApertureConstructionName"]?.GetValue<string>() ?? null;
            }

            if (jObject.ContainsKey("PaneThermalTransmittance"))
            {
                paneThermalTransmittance = jObject["PaneThermalTransmittance"]?.GetValue<double>() ?? default(double);
            }

            if (jObject.ContainsKey("FrameThermalTransmittance"))
            {
                frameThermalTransmittance = jObject["FrameThermalTransmittance"]?.GetValue<double>() ?? default(double);
            }

            if (jObject.ContainsKey("CalculatedPaneThermalTransmittance"))
            {
                calculatedPaneThermalTransmittance = jObject["CalculatedPaneThermalTransmittance"]?.GetValue<double>() ?? default(double);
            }

            if (jObject.ContainsKey("CalculatedFrameThermalTransmittance"))
            {
                calculatedFrameThermalTransmittance = jObject["CalculatedFrameThermalTransmittance"]?.GetValue<double>() ?? default(double);
            }

            return true;
        }

        public override JsonObject ToJsonObject()
        {
            JsonObject result = base.ToJsonObject();
            if(result == null )
            {
                return result;
            }

            if(apertureType != ApertureType.Undefined)
            {
                result.Add("ApertureType", apertureType.ToString());
            }

            if (initialApertureConstructionName != null)
            {
                result.Add("InitialApertureConstructionName", initialApertureConstructionName);
            }

            if (!double.IsNaN(initialPaneThermalTransmittance))
            {
                result.Add("InitialPaneThermalTransmittance", initialPaneThermalTransmittance);
            }

            if (!double.IsNaN(initialFrameThermalTransmittance))
            {
                result.Add("InitialFrameThermalTransmittance", initialFrameThermalTransmittance);
            }

            if (apertureConstructionName != null)
            {
                result.Add("ApertureConstructionName", apertureConstructionName);
            }

            if (!double.IsNaN(paneThermalTransmittance))
            {
                result.Add("PaneThermalTransmittance", paneThermalTransmittance);
            }

            if (!double.IsNaN(frameThermalTransmittance))
            {
                result.Add("FrameThermalTransmittance", frameThermalTransmittance);
            }

            if (!double.IsNaN(calculatedPaneThermalTransmittance))
            {
                result.Add("CalculatedPaneThermalTransmittance", calculatedPaneThermalTransmittance);
            }

            if (!double.IsNaN(calculatedFrameThermalTransmittance))
            {
                result.Add("CalculatedFrameThermalTransmittance", calculatedFrameThermalTransmittance);
            }

            return result;

        }

    }
}
