// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas
{
    public static partial class Convert
    {
        public static InternalCondition ToSAM(this TBD.InternalCondition internalCondition, double area = double.NaN)
        {
            if (internalCondition == null)
            {
                return null;
            }

            InternalCondition result = new InternalCondition(internalCondition.name);

            TBD.Emitter emitter = null;

            emitter = internalCondition.GetHeatingEmitter();
            if (emitter != null)
            {
                result.SetValue(InternalConditionParameter.HeatingEmitterRadiantProportion, emitter.radiantProportion);
                result.SetValue(InternalConditionParameter.HeatingEmitterCoefficient, emitter.viewCoefficient);
            }

            emitter = internalCondition.GetCoolingEmitter();
            if (emitter != null)
            {
                result.SetValue(InternalConditionParameter.CoolingEmitterRadiantProportion, emitter.radiantProportion);
                result.SetValue(InternalConditionParameter.CoolingEmitterCoefficient, emitter.viewCoefficient);
            }

            TBD.InternalGain internalGain = internalCondition.GetInternalGain();
            if (internalGain != null)
            {
                result.SetValue(InternalConditionParameter.LightingRadiantProportion, internalGain.lightingRadProp);
                result.SetValue(InternalConditionParameter.OccupancyRadiantProportion, internalGain.occupantRadProp);
                result.SetValue(InternalConditionParameter.EquipmentRadiantProportion, internalGain.equipmentRadProp);

                result.SetValue(InternalConditionParameter.LightingViewCoefficient, internalGain.lightingViewCoefficient);
                result.SetValue(InternalConditionParameter.OccupancyViewCoefficient, internalGain.occupantViewCoefficient);
                result.SetValue(InternalConditionParameter.EquipmentViewCoefficient, internalGain.equipmentViewCoefficient);

                TBD.profile profile_TBD = null;
                profile_TBD = internalGain.GetProfile((int)TBD.Profiles.ticI);
                if (profile_TBD != null)
                {
                    result.SetValue(InternalConditionParameter.InfiltrationProfileName, string.Format("{0} [{1}]", internalCondition.name, profile_TBD.name));
                    result.SetValue(InternalConditionParameter.InfiltrationAirChangesPerHour, profile_TBD.GetExtremeValue(true));
                }

                profile_TBD = internalGain.GetProfile((int)TBD.Profiles.ticLG);
                if (profile_TBD != null)
                {
                    result.SetValue(InternalConditionParameter.LightingProfileName, string.Format("{0} [{1}]", internalCondition.name, profile_TBD.name));
                    result.SetValue(InternalConditionParameter.LightingGainPerArea, profile_TBD.GetExtremeValue(true));
                    result.SetValue(InternalConditionParameter.LightingLevel, internalGain.targetIlluminance);
                }

                double personGain = internalGain.personGain;

                // TBD occupancy sensible/latent gains are per floor-area (W/m2); personGain is the
                // per-person metabolic rate (W/p). Read the per-area gains, derive the occupancy from
                // them + personGain, then store the gains PER PERSON (perArea * areaPerPerson) so a
                // round-trip reproduces the original per-area gains AND the metabolic rate (the
                // per-person sensible+latent sums back to personGain).
                double sensiblePerArea = double.NaN;
                double latentPerArea = double.NaN;

                profile_TBD = internalGain.GetProfile((int)TBD.Profiles.ticOSG);
                if (profile_TBD != null)
                {
                    sensiblePerArea = profile_TBD.GetExtremeValue(true);
                }

                profile_TBD = internalGain.GetProfile((int)TBD.Profiles.ticOLG);
                if (profile_TBD != null)
                {
                    latentPerArea = profile_TBD.GetExtremeValue(true);
                    result.SetValue(InternalConditionParameter.OccupancyProfileName, string.Format("{0} [{1}]", internalCondition.name, profile_TBD.name));
                }

                double gainPerArea = (double.IsNaN(sensiblePerArea) ? 0 : sensiblePerArea) + (double.IsNaN(latentPerArea) ? 0 : latentPerArea);
                if (!double.IsNaN(area) && !double.IsNaN(personGain) && personGain > 0 && gainPerArea > 0)
                {
                    double occupancy = (gainPerArea * area) / personGain;
                    double areaPerPerson = occupancy > 0 ? area / occupancy : double.NaN;   // == personGain / gainPerArea
                    if (!double.IsNaN(areaPerPerson))
                    {
                        result.SetValue(InternalConditionParameter.AreaPerPerson, areaPerPerson);

                        if (!double.IsNaN(sensiblePerArea))
                        {
                            result.SetValue(InternalConditionParameter.OccupancySensibleGainPerPerson, sensiblePerArea * areaPerPerson);
                        }

                        if (!double.IsNaN(latentPerArea))
                        {
                            result.SetValue(InternalConditionParameter.OccupancyLatentGainPerPerson, latentPerArea * areaPerPerson);
                        }
                    }
                }
                else
                {
                    // No usable personGain/area — keep the raw per-area values rather than nothing.
                    if (!double.IsNaN(sensiblePerArea))
                    {
                        result.SetValue(InternalConditionParameter.OccupancySensibleGainPerPerson, sensiblePerArea);
                    }
                    if (!double.IsNaN(latentPerArea))
                    {
                        result.SetValue(InternalConditionParameter.OccupancyLatentGainPerPerson, latentPerArea);
                    }
                }

                // Outside air per person. TBD freshAirRate is l/s/p; SAM stores SupplyAirFlowPerPerson
                // in m3/s/p (export multiplies back by 1000). Without this the import kept SAM's default
                // (8 l/s/p) and dropped the source value (e.g. 40).
                if (!float.IsNaN(internalGain.freshAirRate))
                {
                    result.SetValue(InternalConditionParameter.SupplyAirFlowPerPerson, internalGain.freshAirRate / 1000.0);
                }

                profile_TBD = internalGain.GetProfile((int)TBD.Profiles.ticESG);
                if (profile_TBD != null)
                {
                    result.SetValue(InternalConditionParameter.EquipmentSensibleProfileName, string.Format("{0} [{1}]", internalCondition.name, profile_TBD.name));
                    result.SetValue(InternalConditionParameter.EquipmentSensibleGainPerArea, profile_TBD.GetExtremeValue(true));
                }

                profile_TBD = internalGain.GetProfile((int)TBD.Profiles.ticELG);
                if (profile_TBD != null)
                {
                    result.SetValue(InternalConditionParameter.EquipmentLatentProfileName, string.Format("{0} [{1}]", internalCondition.name, profile_TBD.name));
                    result.SetValue(InternalConditionParameter.EquipmentLatentGainPerArea, profile_TBD.GetExtremeValue(true));
                }

                profile_TBD = internalGain.GetProfile((int)TBD.Profiles.ticCOG);
                if (profile_TBD != null)
                {
                    result.SetValue(InternalConditionParameter.PollutantProfileName, string.Format("{0} [{1}]", internalCondition.name, profile_TBD.name));
                    result.SetValue(InternalConditionParameter.PollutantGenerationPerArea, profile_TBD.GetExtremeValue(true));
                }

                profile_TBD = internalGain.GetProfile((int)TBD.Profiles.ticV);
                if (profile_TBD != null)
                {
                    result.SetValue(InternalConditionParameter.VentilationProfileName, string.Format("{0} [{1}]", internalCondition.name, profile_TBD.name));
                    result.SetValue(InternalConditionParameter.SupplyAirFlow, profile_TBD.GetExtremeValue(true));
                }
            }

            TBD.Thermostat thermostat = internalCondition.GetThermostat();
            if (internalGain != null)
            {
                TBD.profile profile_TBD = null;

                profile_TBD = thermostat.GetProfile((int)TBD.Profiles.ticUL);
                if (profile_TBD != null)
                {
                    result.SetValue(InternalConditionParameter.CoolingProfileName, string.Format("{0} [{1}]", internalCondition.name, profile_TBD.name));
                }

                profile_TBD = thermostat.GetProfile((int)TBD.Profiles.ticLL);
                if (profile_TBD != null)
                {
                    result.SetValue(InternalConditionParameter.HeatingProfileName, string.Format("{0} [{1}]", internalCondition.name, profile_TBD.name));
                }

                profile_TBD = thermostat.GetProfile((int)TBD.Profiles.ticHLL);
                if (profile_TBD != null)
                {
                    result.SetValue(InternalConditionParameter.HumidificationProfileName, string.Format("{0} [{1}]", internalCondition.name, profile_TBD.name));
                }

                profile_TBD = thermostat.GetProfile((int)TBD.Profiles.ticHUL);
                if (profile_TBD != null)
                {
                    result.SetValue(InternalConditionParameter.DehumidificationProfileName, string.Format("{0} [{1}]", internalCondition.name, profile_TBD.name));
                }
            }

            return result;
        }

        public static InternalCondition ToSAM(this TIC.InternalCondition internalCondition, double area = double.NaN)
        {
            if(internalCondition == null)
            {
                return null;
            }

            InternalCondition result = new InternalCondition(internalCondition.name);

            TIC.InternalGain internalGain = internalCondition.GetInternalGain();
            if (internalGain != null)
            {
                result.SetValue(InternalConditionParameter.LightingRadiantProportion, internalGain.lightingRadProp);
                result.SetValue(InternalConditionParameter.OccupancyRadiantProportion, internalGain.occupantRadProp);
                result.SetValue(InternalConditionParameter.EquipmentRadiantProportion, internalGain.equipmentRadProp);

                result.SetValue(InternalConditionParameter.LightingViewCoefficient, internalGain.lightingViewCoefficient);
                result.SetValue(InternalConditionParameter.OccupancyViewCoefficient, internalGain.occupantViewCoefficient);
                result.SetValue(InternalConditionParameter.EquipmentViewCoefficient, internalGain.equipmentViewCoefficient);

                TIC.profile profile_TIC = null;
                profile_TIC = internalGain.GetProfile((int)TBD.Profiles.ticI);
                if (profile_TIC != null)
                {
                    result.SetValue(InternalConditionParameter.InfiltrationProfileName, string.Format("{0} [{1}]", internalCondition.name, profile_TIC.name));
                    result.SetValue(InternalConditionParameter.InfiltrationAirChangesPerHour, profile_TIC.GetExtremeValue(true));
                }

                profile_TIC = internalGain.GetProfile((int)TBD.Profiles.ticLG);
                if (profile_TIC != null)
                {
                    result.SetValue(InternalConditionParameter.LightingProfileName, string.Format("{0} [{1}]", internalCondition.name, profile_TIC.name));
                    result.SetValue(InternalConditionParameter.LightingGainPerArea, profile_TIC.GetExtremeValue(true));
                    result.SetValue(InternalConditionParameter.LightingLevel, internalGain.targetIlluminance);
                }

                double personGain = internalGain.personGain;

                // TBD occupancy sensible/latent gains are per floor-area (W/m2); personGain is the
                // per-person metabolic rate (W/p). Read the per-area gains, derive the occupancy from
                // them + personGain, then store the gains PER PERSON (perArea * areaPerPerson) so a
                // round-trip reproduces the original per-area gains AND the metabolic rate.
                double sensiblePerArea = double.NaN;
                double latentPerArea = double.NaN;

                profile_TIC = internalGain.GetProfile((int)TBD.Profiles.ticOSG);
                if (profile_TIC != null)
                {
                    sensiblePerArea = profile_TIC.GetExtremeValue(true);
                }

                profile_TIC = internalGain.GetProfile((int)TBD.Profiles.ticOLG);
                if (profile_TIC != null)
                {
                    latentPerArea = profile_TIC.GetExtremeValue(true);
                    result.SetValue(InternalConditionParameter.OccupancyProfileName, string.Format("{0} [{1}]", internalCondition.name, profile_TIC.name));
                }

                double gainPerArea = (double.IsNaN(sensiblePerArea) ? 0 : sensiblePerArea) + (double.IsNaN(latentPerArea) ? 0 : latentPerArea);
                if (!double.IsNaN(area) && !double.IsNaN(personGain) && personGain > 0 && gainPerArea > 0)
                {
                    double occupancy = (gainPerArea * area) / personGain;
                    double areaPerPerson = occupancy > 0 ? area / occupancy : double.NaN;   // == personGain / gainPerArea
                    if (!double.IsNaN(areaPerPerson))
                    {
                        result.SetValue(InternalConditionParameter.AreaPerPerson, areaPerPerson);

                        if (!double.IsNaN(sensiblePerArea))
                        {
                            result.SetValue(InternalConditionParameter.OccupancySensibleGainPerPerson, sensiblePerArea * areaPerPerson);
                        }

                        if (!double.IsNaN(latentPerArea))
                        {
                            result.SetValue(InternalConditionParameter.OccupancyLatentGainPerPerson, latentPerArea * areaPerPerson);
                        }
                    }
                }
                else
                {
                    // No usable personGain/area — keep the raw per-area values rather than nothing.
                    if (!double.IsNaN(sensiblePerArea))
                    {
                        result.SetValue(InternalConditionParameter.OccupancySensibleGainPerPerson, sensiblePerArea);
                    }
                    if (!double.IsNaN(latentPerArea))
                    {
                        result.SetValue(InternalConditionParameter.OccupancyLatentGainPerPerson, latentPerArea);
                    }
                }

                // Outside air per person. TBD freshAirRate is l/s/p; SAM stores SupplyAirFlowPerPerson
                // in m3/s/p (export multiplies back by 1000).
                if (!float.IsNaN(internalGain.freshAirRate))
                {
                    result.SetValue(InternalConditionParameter.SupplyAirFlowPerPerson, internalGain.freshAirRate / 1000.0);
                }

                profile_TIC = internalGain.GetProfile((int)TBD.Profiles.ticESG);
                if (profile_TIC != null)
                {
                    result.SetValue(InternalConditionParameter.EquipmentSensibleProfileName, string.Format("{0} [{1}]", internalCondition.name, profile_TIC.name));
                    result.SetValue(InternalConditionParameter.EquipmentSensibleGainPerArea, profile_TIC.GetExtremeValue(true));
                }

                profile_TIC = internalGain.GetProfile((int)TBD.Profiles.ticELG);
                if (profile_TIC != null)
                {
                    result.SetValue(InternalConditionParameter.EquipmentLatentProfileName, string.Format("{0} [{1}]", internalCondition.name, profile_TIC.name));
                    result.SetValue(InternalConditionParameter.EquipmentLatentGainPerArea, profile_TIC.GetExtremeValue(true));
                }

                profile_TIC = internalGain.GetProfile((int)TBD.Profiles.ticCOG);
                if (profile_TIC != null)
                {
                    result.SetValue(InternalConditionParameter.PollutantProfileName, string.Format("{0} [{1}]", internalCondition.name, profile_TIC.name));
                    result.SetValue(InternalConditionParameter.PollutantGenerationPerArea, profile_TIC.GetExtremeValue(true));
                }

                profile_TIC = internalGain.GetProfile((int)TBD.Profiles.ticV);
                if (profile_TIC != null)
                {
                    result.SetValue(InternalConditionParameter.VentilationProfileName, string.Format("{0} [{1}]", internalCondition.name, profile_TIC.name));
                    result.SetValue(InternalConditionParameter.SupplyAirFlow, profile_TIC.GetExtremeValue(true));
                }

            }

            TIC.Thermostat thermostat = internalCondition.GetThermostat();
            if (internalGain != null)
            {
                TIC.profile profile_TIC = null;

                profile_TIC = thermostat.GetProfile((int)TBD.Profiles.ticUL);
                if (profile_TIC != null)
                {
                    result.SetValue(InternalConditionParameter.CoolingProfileName, string.Format("{0} [{1}]", internalCondition.name, profile_TIC.name));
                }

                profile_TIC = thermostat.GetProfile((int)TBD.Profiles.ticLL);
                if (profile_TIC != null)
                {
                    result.SetValue(InternalConditionParameter.HeatingProfileName, string.Format("{0} [{1}]", internalCondition.name, profile_TIC.name));
                }

                profile_TIC = thermostat.GetProfile((int)TBD.Profiles.ticHLL);
                if (profile_TIC != null)
                {
                    result.SetValue(InternalConditionParameter.HumidificationProfileName, string.Format("{0} [{1}]", internalCondition.name, profile_TIC.name));
                }

                profile_TIC = thermostat.GetProfile((int)TBD.Profiles.ticHUL);
                if (profile_TIC != null)
                {
                    result.SetValue(InternalConditionParameter.DehumidificationProfileName, string.Format("{0} [{1}]", internalCondition.name, profile_TIC.name));
                }
            }

            return result;
        }
    }
}
