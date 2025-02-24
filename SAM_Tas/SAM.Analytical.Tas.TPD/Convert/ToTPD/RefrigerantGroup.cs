﻿using SAM.Analytical.Systems;
using TPD;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Convert
    {
        public static RefrigerantGroup ToTPD(this DisplayRefrigerantSystemCollection displayRefrigerantSystemCollection, PlantRoom plantRoom)
        {
            if (displayRefrigerantSystemCollection == null || plantRoom == null)
            {
                return null;
            }

            RefrigerantGroup result = plantRoom.AddRefrigerantGroup();

            EnergyCentre energyCentre = plantRoom.GetEnergyCentre();

            dynamic @dynamic = result;
            @dynamic.Name = displayRefrigerantSystemCollection.Name;
            @dynamic.Description = displayRefrigerantSystemCollection.Description;

            result.PipeLength?.Update(displayRefrigerantSystemCollection.PipeLength, energyCentre);

            displayRefrigerantSystemCollection.SetLocation(result as PlantComponent);

            return result;
        }
    }
}
