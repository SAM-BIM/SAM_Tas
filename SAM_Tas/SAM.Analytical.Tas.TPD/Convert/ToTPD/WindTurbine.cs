﻿using SAM.Analytical.Systems;
using TPD;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Convert
    {
        public static WindTurbine ToTPD(this DisplaySystemWindTurbine displaySystemWindTurbine, PlantRoom plantRoom)
        {
            if (displaySystemWindTurbine == null || plantRoom == null)
            {
                return null;
            }

            WindTurbine result = plantRoom.AddWindTurbine();

            dynamic @dynamic = result;
            @dynamic.Name = displaySystemWindTurbine.Name;
            @dynamic.Description = displaySystemWindTurbine.Description;

            result.HubHeight = displaySystemWindTurbine.HubHeight;
            result.Area = displaySystemWindTurbine.Area;
            result.MinSpeed = displaySystemWindTurbine.MinSpeed;
            result.CutOffSpeed = displaySystemWindTurbine.CutOffSpeed;
            result.Multiplicity = displaySystemWindTurbine.Multiplicity;
            result.Efficiency?.Update(displaySystemWindTurbine.Efficiency);

            FuelSource fuelSource = plantRoom.FuelSource(displaySystemWindTurbine.GetValue<string>(Core.Systems.SystemObjectParameter.ElectricalEnergySourceName));
            if (fuelSource != null)
            {
                ((@dynamic)result).SetFuelSource(1, fuelSource);
            }

            displaySystemWindTurbine.SetLocation(result as PlantComponent);

            return result;
        }
    }
}
