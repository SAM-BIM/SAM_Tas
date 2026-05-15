using System.Text.Json.Nodes;
namespace SAM.Core.Tas
{
    public class SurfaceOutputSpec : SAMObject
    {
        public string Description { get; set; }
        public bool ApertureData { get; set; }
        public bool Condensation { get; set; }
        public bool Convection { get; set; }
        public bool SolarGain { get; set; }
        public bool Conduction { get; set; }
        public bool LongWave { get; set; }
        public bool Temperature { get; set; }

        public SurfaceOutputSpec(string name)
            : base(name)
        {

        }

        public SurfaceOutputSpec(JsonObject jObject)
            : base(jObject)
        {

        }

        public SurfaceOutputSpec(SurfaceOutputSpec surfaceOutputSpec)
            : base(surfaceOutputSpec)
        {
            if(surfaceOutputSpec != null)
            {
                Description = surfaceOutputSpec.Description;
                ApertureData = surfaceOutputSpec.ApertureData;
                Condensation = surfaceOutputSpec.Condensation;
                Convection = surfaceOutputSpec.Convection;
                SolarGain = surfaceOutputSpec.SolarGain;
                Conduction = surfaceOutputSpec.Conduction;
                LongWave = surfaceOutputSpec.LongWave;
                Temperature = surfaceOutputSpec.Temperature;
            }
        }

        public override bool FromJsonObject(JsonObject jObject)
        {
            if(! base.FromJsonObject(jObject))
            {
                return false;
            }

            if(jObject.ContainsKey("Description"))
            {
                Description = jObject["Description"]?.GetValue<string>() ?? null;
            }

            if (jObject.ContainsKey("ApertureData"))
            {
                ApertureData = jObject["ApertureData"]?.GetValue<bool>() ?? default(bool);
            }

            if (jObject.ContainsKey("Condensation"))
            {
                Condensation = jObject["Condensation"]?.GetValue<bool>() ?? default(bool);
            }

            if (jObject.ContainsKey("Conduction"))
            {
                Conduction = jObject["Conduction"]?.GetValue<bool>() ?? default(bool);
            }

            if (jObject.ContainsKey("SolarGain"))
            {
                SolarGain = jObject["SolarGain"]?.GetValue<bool>() ?? default(bool);
            }

            if (jObject.ContainsKey("Conduction"))
            {
                Conduction = jObject["Conduction"]?.GetValue<bool>() ?? default(bool);
            }

            if (jObject.ContainsKey("LongWave"))
            {
                LongWave = jObject["LongWave"]?.GetValue<bool>() ?? default(bool);
            }

            if (jObject.ContainsKey("Temperature"))
            {
                Temperature = jObject["Temperature"]?.GetValue<bool>() ?? default(bool);
            }

            return true;
        }

        public override JsonObject ToJsonObject()
        {
            JsonObject jObject =  base.ToJsonObject();
            if(jObject == null)
            {
                return jObject;
            }

            if(Description != null)
            {
                jObject.Add("Description", Description);
            }

            jObject.Add("ApertureData", ApertureData);
            jObject.Add("Condensation", Condensation);
            jObject.Add("Convection", Convection);
            jObject.Add("SolarGain", SolarGain);
            jObject.Add("Conduction", Conduction);
            jObject.Add("LongWave", LongWave);
            jObject.Add("Temperature", Temperature);

            return jObject;
        }
    }
}
