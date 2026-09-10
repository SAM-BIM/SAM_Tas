// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Systems;
using SAM.Core.Systems;
using System.Collections.Generic;
using TPD;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Convert
    {
        public static SystemZone ToTPD(this DisplaySystemSpace displaySystemSpace, SystemPlantRoom systemPlantRoom, global::TPD.System system, SystemZone systemZone = null, bool addSystemSpaceComponents = true)
        {
            return ToTPD(displaySystemSpace, systemPlantRoom, system, null, systemZone, addSystemSpaceComponents);
        }

        /// <summary>
        /// Converts one materialised room to a TAS <c>SystemZone</c>.
        /// <para>
        /// <b>This is the pairing point.</b> When a
        /// <see cref="SystemVentilationConversionContext"/> is supplied, three things happen here that
        /// cannot correctly happen anywhere else, because this is the only moment at which the
        /// analytical room, the native zone and the TSD are all in hand:
        /// </para>
        /// <list type="number">
        /// <item><description>the intended <c>ZoneLoad</c> is bound by <b>identity</b> -
        /// <c>TSDData.GetZoneLoadForGuid(SpaceParameter.ZoneGuid)</c> then
        /// <c>SystemComponent.AddZoneLoad</c>. Measured on licensed TAS, a TSD zone load's
        /// <c>GUID</c> <i>is</i> the TBD zone guid SAM stamps onto the analytical space, so no name is
        /// involved at any step. The code this replaces compared
        /// <c>SystemSpaceParameter.SpaceName</c> with <c>ZoneLoad.Name</c>, which on the shipped
        /// template compared "System Zone 1" with "Cell 1" and bound nothing at all - and which, when
        /// it did match, would alias two rooms sharing a display
        /// name;</description></item>
        /// <item><description>the room's <b>supply</b> duty is written absolutely -
        /// <c>FlowRate</c> and <c>FreshAir</c>, <c>Value</c> in l/s with
        /// <c>Type = tpdSizedVariableValue</c> - from the duty PR1's supply connection states, not from
        /// the template prototype's sizing rule. Leaving <c>Type</c> alone would let TAS size the zone
        /// by air changes per hour and discard the litres per second
        /// entirely;</description></item>
        /// <item><description>the native identities are recorded against the room, read back off the
        /// objects rather than assumed, so the reconciliation has something independent to compare the
        /// intent with.</description></item>
        /// </list>
        /// <para>
        /// With no context supplied the method behaves exactly as it always has, so no existing caller
        /// changes.
        /// </para>
        /// </summary>
        public static SystemZone ToTPD(this DisplaySystemSpace displaySystemSpace, SystemPlantRoom systemPlantRoom, global::TPD.System system, SystemVentilationConversionContext systemVentilationConversionContext, SystemZone systemZone = null, bool addSystemSpaceComponents = true)
        {
            if(displaySystemSpace == null || system == null)
            {
                return null;
            }

            SystemZone result = systemZone;
            if(systemZone == null)
            {
                result = system.AddSystemZone();
            }

            result.Flags &= ~(int)tpdSystemZoneFlags.tpdSystemZoneFlagDisplacementVent;
            result.Flags &= ~(int)tpdSystemZoneFlags.tpdSystemZoneFlagModelInterzoneFlow;
            result.Flags &= ~(int)tpdSystemZoneFlags.tpdSystemZoneFlagModelVentFlow;

            EnergyCentre energyCentre = system.GetPlantRoom()?.GetEnergyCentre();

            dynamic @dynamic = result;

            dynamic.name = displaySystemSpace.Name;
            dynamic.Description = displaySystemSpace.Description;

            result.TemperatureSetpoint.Update(displaySystemSpace.TemperatureSetpoint, energyCentre);
            result.RHSetpoint.Update(displaySystemSpace.RelativeHumiditySetpoint, energyCentre);
            result.PollutantSetpoint.Update(displaySystemSpace.PollutantSetpoint, energyCentre);
            result.FlowRate.Update(displaySystemSpace.FlowRate, energyCentre);
            result.FreshAir.Update(displaySystemSpace.FreshAir, energyCentre);

            result.MinimumFlowFraction = displaySystemSpace.MinimumDesignFlowFraction;

            if (displaySystemSpace.DisplacementVentilation)
            {
                result.DisplacementVent = displaySystemSpace.DisplacementVentilation.ToTPD();
                result.Flags = result.Flags | (int)tpdSystemZoneFlags.tpdSystemZoneFlagDisplacementVent;
            }

            if (displaySystemSpace.ModelInterzoneFlow)
            {
                result.Flags = result.Flags | (int)tpdSystemZoneFlags.tpdSystemZoneFlagModelInterzoneFlow;
            }

            if (displaySystemSpace.ModelVentilationFlow)
            {
                result.Flags = result.Flags | (int)tpdSystemZoneFlags.tpdSystemZoneFlagModelVentFlow;
            }

            CollectionLink collectionLink;

            collectionLink = displaySystemSpace.GetValue<CollectionLink>(SystemSpaceParameter.EquipmentElectricalCollection);
            if (collectionLink != null)
            {
                ElectricalGroup electricalGroup = system.GetPlantRoom()?.ElectricalGroups()?.Find(x => ((dynamic)x).Name == collectionLink.Name);
                if (electricalGroup != null)
                {
                    @dynamic.SetElectricalGroup1(electricalGroup);
                }
            }

            collectionLink = displaySystemSpace.GetValue<CollectionLink>(SystemSpaceParameter.LightingElectricalCollection);
            if (collectionLink != null)
            {
                ElectricalGroup electricalGroup = system.GetPlantRoom()?.ElectricalGroups()?.Find(x => ((dynamic)x).Name == collectionLink.Name);
                if (electricalGroup != null)
                {
                    @dynamic.SetElectricalGroup2(electricalGroup);
                }
            }

            collectionLink = displaySystemSpace.GetValue<CollectionLink>(SystemSpaceParameter.DomesticHotWaterCollection);
            if (collectionLink != null)
            {
                DHWGroup dHWGroup = system.GetPlantRoom()?.DHWGroups()?.Find(x => ((dynamic)x).Name == collectionLink.Name);
                if (dHWGroup != null)
                {
                    @dynamic.SetDHWGroup(dHWGroup);
                }
            }

            SystemVentilationRoomIntent systemVentilationRoomIntent = systemVentilationConversionContext?.RoomIntent(displaySystemSpace.Guid);

            if (systemVentilationRoomIntent == null)
            {
                //----------------------------------------------------------------------------------------
                //No explicit intent for this room. This is every caller that predates the Part O
                //ventilation route, and it keeps the behaviour it had - including the name match, which
                //is why the route above never relies on it.
                //----------------------------------------------------------------------------------------
                if (energyCentre != null)
                {
                    List<ZoneLoad> zoneLoads = Query.ZoneLoads(energyCentre.GetTSDData(1), new DisplaySystemSpace[] { displaySystemSpace });
                    if (zoneLoads != null)
                    {
                        foreach (ZoneLoad zoneLoad in zoneLoads)
                        {
                            @dynamic.AddZoneLoad(zoneLoad);
                        }
                    }
                }
            }
            else
            {
                Modify.BindZoneLoad(result, energyCentre, systemVentilationRoomIntent, systemVentilationConversionContext);
                Modify.SetSupplyDesignFlowRate(result, systemVentilationRoomIntent, systemVentilationConversionContext, out double? designFlowRate_Supply_Lps);

                systemVentilationConversionContext.RecordPairing(displaySystemSpace.Guid, Query.NativeReference(result));

                systemVentilationConversionContext.RecordRoomPairing(
                    displaySystemSpace.Guid,
                    Query.NativeReference(result),
                    Query.NativeReference_ZoneLoad(result),
                    Query.NativeReference(system),
                    designFlowRate_Supply_Lps);

                //An identity that does not round-trip through the system it was captured from is not an
                //identity, and PR3 must not be handed one.
                string reference_SystemZone = Query.NativeReference(result);
                if (!Query.RoundTrips(system, reference_SystemZone))
                {
                    systemVentilationConversionContext.Refuse(string.Format(
                        "Room {0} was materialised as native zone {1}, which does not resolve back through its own "
                        + "TAS system.",
                        systemVentilationRoomIntent.Guid_Space,
                        reference_SystemZone ?? "<no identifier>"));
                }
            }

            if (addSystemSpaceComponents)
            {
                List<IZoneComponent> zoneComponents = Modify.AddSystemZoneComponents(result, displaySystemSpace, systemPlantRoom);
            }

            if (systemZone == null)
            {
                displaySystemSpace.SetLocation(result as global::TPD.SystemComponent);
            }

            return result;
        }
    }
}
