using System.Text.Json.Nodes;
using SAM.Core;

namespace SAM.Analytical.Tas
{
    public class LayerThicknessCalculationResult : Result, IConstructionCalculationResult
    {
        private string constructionName;
        private int layerIndex;
        private double thermalTransmittance;
        private double calculatedThermalTransmittance;
        private double initialThermalTransmittance;
        private double thickness;

        public LayerThicknessCalculationResult(JsonObject jObject)
            :base(jObject)
        {

        }

        public LayerThicknessCalculationResult(LayerThicknessCalculationResult layerThicknessCalculationResult)
            :base(layerThicknessCalculationResult)
        {
            if(layerThicknessCalculationResult != null)
            {
                constructionName = layerThicknessCalculationResult.constructionName;
                layerIndex = layerThicknessCalculationResult.layerIndex;
                thermalTransmittance = layerThicknessCalculationResult.thermalTransmittance;
                calculatedThermalTransmittance = layerThicknessCalculationResult.calculatedThermalTransmittance;
                initialThermalTransmittance = layerThicknessCalculationResult.initialThermalTransmittance;
                thickness = layerThicknessCalculationResult.thickness;
            }
        }

        public LayerThicknessCalculationResult(string source, string constructionName, int layerIndex, double thickness, double initialThermalTransmittance, double thermalTransmittance, double calculatedThermalTransmittance)
            : base(constructionName, source, constructionName)
        {
            this.constructionName = constructionName;
            this.layerIndex = layerIndex;
            this.thickness = thickness;
            this.initialThermalTransmittance = initialThermalTransmittance;
            this.thermalTransmittance = thermalTransmittance;
            this.calculatedThermalTransmittance = calculatedThermalTransmittance;
        }

        public double Thickness
        {
            get
            {
                return thickness;
            }
        }

        public string ConstructionName
        {
            get
            {
                return constructionName;
            }
        }

        public int LayerIndex
        {
            get
            {
                return layerIndex;
            }
        }

        public double InitialThermalTransmittance
        {
            get
            {
                return initialThermalTransmittance;
            }
        }

        public double ThermalTransmittance
        {
            get
            {
                return thermalTransmittance;
            }
        }

        public double CalculatedThermalTransmittance
        {
            get
            {
                return calculatedThermalTransmittance;
            }
        }

        public override bool FromJsonObject(JsonObject jObject)
        {
            if(!base.FromJsonObject(jObject))
            {
                return false;
            }

            if(jObject.ContainsKey("ConstructionName"))
            {
                constructionName = jObject["ConstructionName"]?.GetValue<string>() ?? null;
            }

            if (jObject.ContainsKey("LayerIndex"))
            {
                layerIndex = jObject["LayerIndex"]?.GetValue<int>() ?? default(int);
            }

            if (jObject.ContainsKey("InitialThermalTransmittance"))
            {
                initialThermalTransmittance = jObject["InitialThermalTransmittance"]?.GetValue<double>() ?? default(double);
            }

            if (jObject.ContainsKey("ThermalTransmittance"))
            {
                thermalTransmittance = jObject["ThermalTransmittance"]?.GetValue<double>() ?? default(double);
            }

            if (jObject.ContainsKey("CalculatedThermalTransmittance"))
            {
                calculatedThermalTransmittance = jObject["CalculatedThermalTransmittance"]?.GetValue<double>() ?? default(double);
            }

            if (jObject.ContainsKey("Thickness"))
            {
                thickness = jObject["Thickness"]?.GetValue<double>() ?? default(double);
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

            if(constructionName != null)
            {
                result.Add("ConstructionName", constructionName);
            }

            result.Add("LayerIndex", layerIndex);

            if (!double.IsNaN(initialThermalTransmittance))
            {
                result.Add("InitialThermalTransmittance", initialThermalTransmittance);
            }

            if (!double.IsNaN(thermalTransmittance))
            {
                result.Add("ThermalTransmittance", thermalTransmittance);
            }

            if (!double.IsNaN(calculatedThermalTransmittance))
            {
                result.Add("CalculatedThermalTransmittance", calculatedThermalTransmittance);
            }

            if (!double.IsNaN(thickness))
            {
                result.Add("Thickness", thickness);
            }

            return result;

        }

    }
}
