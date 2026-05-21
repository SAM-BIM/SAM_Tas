using SAM.Geometry.SolarCalculator;

namespace SAM.Analytical.Tas
{
    public static partial class Create
    {
        public static SolarModel SolarModel(this TBD.Building building)
        {
            if(building is null)
            {
                return null;
            }

            Core.Location location = new (building.name, building.longitude, building.latitude, 0);

            SolarModel result = new (location);

            object @object = building.GetShadeDays();



            return result;
        }
    }
}