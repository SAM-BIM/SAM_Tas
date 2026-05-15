using System.Text.Json.Nodes;
namespace SAM.Core.Tas
{
    public class ZoneSurfaceReference : IJSAMObject
    {
        public int SurfaceNumber { get; private set; } = -1;

        public string ZoneGuid { get; private set; } = null;

        public ZoneSurfaceReference(int surfaceNumber, string zoneGuid)
        {
            SurfaceNumber = surfaceNumber;
            ZoneGuid = zoneGuid;
        }

        public ZoneSurfaceReference(JsonObject jObject)
        {
            FromJsonObject(jObject);
        }

        public ZoneSurfaceReference(ZoneSurfaceReference zoneSurfaceReference)
        {
            if(zoneSurfaceReference != null)
            {
                SurfaceNumber = zoneSurfaceReference.SurfaceNumber;
                ZoneGuid = zoneSurfaceReference.ZoneGuid;
            }
        }

        public bool FromJsonObject(JsonObject jObject)
        {
            if(jObject.ContainsKey("SurfaceNumber"))
            {
                SurfaceNumber = jObject["SurfaceNumber"]?.GetValue<int>() ?? default(int);
            }

            if (jObject.ContainsKey("ZoneGuid"))
            {
                ZoneGuid = jObject["ZoneGuid"]?.GetValue<string>() ?? null;
            }

            return true;
        }

        public JsonObject ToJsonObject()
        {
            JsonObject result =  new JsonObject();
            result.Add("_type", Core.Query.FullTypeName(this));

            if(SurfaceNumber != -1)
            {
                result.Add("SurfaceNumber", SurfaceNumber);
            }

            if (ZoneGuid != null)
            {
                result.Add("ZoneGuid", ZoneGuid);
            }

            return result;
        }
    }
}
