﻿using SAM.Analytical.Systems;
using System.Collections.Generic;
using System.Linq;
using TPD;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Convert
    {
        public static MultiChiller ToTPD(this DisplaySystemMultiChiller displaySystemMultiChiller, PlantRoom plantRoom)
        {
            if (displaySystemMultiChiller == null || plantRoom == null)
            {
                return null;
            }

            MultiChiller result = plantRoom.AddMultiChiller();

            dynamic @dynamic = result;
            @dynamic.Name = displaySystemMultiChiller.Name;
            @dynamic.Description = displaySystemMultiChiller.Description;

            result.Setpoint?.Update(displaySystemMultiChiller.Setpoint);
            result.Duty?.Update(displaySystemMultiChiller.Duty, plantRoom);
            result.DesignDeltaT = displaySystemMultiChiller.DesignTemperatureDifference;
            result.Capacity = displaySystemMultiChiller.Capacity;
            result.DesignPressureDrop = displaySystemMultiChiller.DesignPressureDrop;
            result.Sequence = displaySystemMultiChiller.Sequence.ToTPD();
            result.Multiplicity = displaySystemMultiChiller.Multiplicity;
            result.LossesInSizing = displaySystemMultiChiller.LossesInSizing.ToTPD();

            IEnumerable<SystemMultiChillerItem> systemMultiBoilerItems = displaySystemMultiChiller.Items;
            if (systemMultiBoilerItems != null)
            {
                for (int i = 0; i < systemMultiBoilerItems.Count(); i++)
                {
                    SystemMultiChillerItem systemMultiChillerItem = systemMultiBoilerItems.ElementAt(i);

                    int index = i + 1;

                    result.SetChillerPercentage(index, systemMultiChillerItem.Percentage);
                    result.SetChillerThreshold(index, systemMultiChillerItem.Threshold);
                    ProfileData profileData;

                    profileData = result.GetChillerEfficiency(index);
                    profileData.Update(systemMultiChillerItem.Efficiency);

                    profileData = result.GetChillerCondenserFanLoad(index);
                    profileData.Update(systemMultiChillerItem.CondenserFanLoad);

                    FuelSource fuelSource;

                    fuelSource = plantRoom.FuelSource(systemMultiChillerItem.GetValue<string>(Core.Systems.SystemObjectParameter.EnergySourceName));
                    if (fuelSource != null)
                    {
                        ((@dynamic)result).SetFuelSource((i - 1) * 2, fuelSource);
                    }

                    fuelSource = plantRoom.FuelSource(systemMultiChillerItem.GetValue<string>(Core.Systems.SystemObjectParameter.FanEnergySourceName));
                    if (fuelSource != null)
                    {
                        ((@dynamic)result).SetFuelSource(((i - 1) * 2) + 1, fuelSource);
                    }
                }
            }

            displaySystemMultiChiller.SetLocation(result as PlantComponent);

            return result;
        }
    }
}
