﻿using SAM.Analytical.Systems;
using TPD;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Convert
    {
        public static Exchanger ToTPD(this DisplaySystemExchanger displaySystemExchanger, global::TPD.System system)
        {
            if(displaySystemExchanger == null || system == null)
            {
                return null;
            }

            Exchanger result = system.AddExchanger();

            PlantRoom plantRoom = system.GetPlantRoom();

            EnergyCentre energyCentre = plantRoom.GetEnergyCentre();

            dynamic @dynamic = result;
            result.ExchLatType = displaySystemExchanger.ExchangerLatentType.ToTPD();
            result.ExchangerType = displaySystemExchanger.ExchangerType.ToTPD();
            result.SensibleEfficiency?.Update(displaySystemExchanger.SensibleEfficiency, energyCentre);
            result.HeatTransSurfArea = displaySystemExchanger.HeatTransferSurfaceArea;
            result.HeatTransCoeff = displaySystemExchanger.HeatTransferCoefficient;
            result.ExchLatType = displaySystemExchanger.ExchangerLatentType.ToTPD();
            result.LatentEfficiency?.Update(displaySystemExchanger.LatentEfficiency, energyCentre);
            result.SetpointMethod = displaySystemExchanger.SetpointMode.ToTPD();
            result.Setpoint?.Update(displaySystemExchanger.Setpoint, energyCentre);
            result.ElectricalLoad?.Update(displaySystemExchanger.ElectricalLoad, energyCentre);
            result.Duty?.Update(displaySystemExchanger.Duty, system);
            result.BypassFactor?.Update(displaySystemExchanger.BypassFactor, energyCentre);

            CollectionLink collectionLink = displaySystemExchanger.GetValue<CollectionLink>(AirSystemComponentParameter.ElectricalCollection);
            if (collectionLink != null)
            {
                ElectricalGroup electricalGroup = system.GetPlantRoom()?.ElectricalGroups()?.Find(x => ((dynamic)x).Name == collectionLink.Name);
                if (electricalGroup != null)
                {
                    @dynamic.SetElectricalGroup1(electricalGroup);
                }
            }

            Modify.SetSchedule((SystemComponent)result, displaySystemExchanger.ScheduleName);

            //result.LatentEfficiency.Value = displaySystemExchanger.LatentEfficiency;
            //result.SensibleEfficiency.Value = displaySystemExchanger.SensibleEfficiency;
            //result.Setpoint.Value = 14;
            //result.Flags = tpdExchangerFlags.tpdExchangerFlagAdjustForOptimiser;

            displaySystemExchanger.SetLocation(result as SystemComponent);

            return result;
        }
    }
}
