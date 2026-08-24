// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        /// <summary>
        /// Read every TBD internal-condition profile slot in <paramref name="building"/> once, and resolve the
        /// shared SAM profile definitions and names the import will use.
        /// <para>
        /// This is the ONLY place the values are read over COM to BUILD the index. The resulting
        /// <see cref="Analytical.Tas.ProfileReuseIndex"/> then answers both the <c>ProfileLibrary</c> build and
        /// every <c>InternalCondition</c> conversion from that one read, and no path can disagree with the
        /// library about what a profile reference names.
        /// </para>
        /// <para>
        /// One exception pays a second COM read: an ambiguous slot cannot answer from
        /// <see cref="Analytical.Tas.ProfileReuseIndex.GetProfileName(string, int)"/>, so the conversion falls
        /// back to <see cref="Analytical.Tas.ProfileReuseIndex.GetProfileName(string, System.Collections.Generic.IEnumerable{double})"/>,
        /// which re-marshals the same <c>TBD.profile</c>'s values the conversion already holds a handle to. That
        /// path is correct - it is keyed on the definition itself and always answers - just not free of a
        /// second read.
        /// </para>
        /// <para>
        /// <b>The slot set is exactly the one the legacy <c>Convert.ToSAM_Profiles</c> emits</b> - the eight
        /// internal-gain slots and the four thermostat slots, <c>ticV</c> (ventilation) included. Collecting
        /// ticV is what makes the imported <c>InternalConditionParameter.VentilationProfileName</c> resolve:
        /// the import has always written that reference but never emitted the profile behind it, so it dangled
        /// until the slot joined the reusable set.
        /// </para>
        /// </summary>
        /// <returns>A resolved index, or null when there is nothing to read.</returns>
        public static ProfileReuseIndex ProfileReuseIndex(this TBD.Building building)
        {
            List<TBD.InternalCondition> internalConditions_TBD = InternalConditions(building);
            if (internalConditions_TBD == null)
            {
                return null;
            }

            ProfileReuseIndex result = new ProfileReuseIndex();

            foreach (TBD.InternalCondition internalCondition_TBD in internalConditions_TBD)
            {
                Register(result, internalCondition_TBD);
            }

            result.Resolve();

            return result;
        }

        /// <summary>
        /// The <c>TBD.Profiles</c> slots an internal gain contributes to the reusable profile set, and the SAM
        /// <see cref="ProfileType"/> each maps to. Mirrors <c>Convert.ToSAM_Profiles</c> exactly.
        /// </summary>
        internal static readonly KeyValuePair<TBD.Profiles, ProfileType>[] ProfileSlots_InternalGain =
        {
            new KeyValuePair<TBD.Profiles, ProfileType>(TBD.Profiles.ticI, ProfileType.Infiltration),
            new KeyValuePair<TBD.Profiles, ProfileType>(TBD.Profiles.ticV, ProfileType.Ventilation),
            new KeyValuePair<TBD.Profiles, ProfileType>(TBD.Profiles.ticLG, ProfileType.Lighting),
            new KeyValuePair<TBD.Profiles, ProfileType>(TBD.Profiles.ticOLG, ProfileType.Occupancy),
            new KeyValuePair<TBD.Profiles, ProfileType>(TBD.Profiles.ticOSG, ProfileType.Occupancy),
            new KeyValuePair<TBD.Profiles, ProfileType>(TBD.Profiles.ticESG, ProfileType.EquipmentSensible),
            new KeyValuePair<TBD.Profiles, ProfileType>(TBD.Profiles.ticELG, ProfileType.EquipmentLatent),
            new KeyValuePair<TBD.Profiles, ProfileType>(TBD.Profiles.ticCOG, ProfileType.Pollutant),
        };

        /// <summary>
        /// The <c>TBD.Profiles</c> slots a thermostat contributes, and the SAM <see cref="ProfileType"/> each maps
        /// to. Mirrors <c>Convert.ToSAM_Profiles</c> exactly.
        /// </summary>
        internal static readonly KeyValuePair<TBD.Profiles, ProfileType>[] ProfileSlots_Thermostat =
        {
            new KeyValuePair<TBD.Profiles, ProfileType>(TBD.Profiles.ticUL, ProfileType.Cooling),
            new KeyValuePair<TBD.Profiles, ProfileType>(TBD.Profiles.ticLL, ProfileType.Heating),
            new KeyValuePair<TBD.Profiles, ProfileType>(TBD.Profiles.ticHLL, ProfileType.Humidification),
            new KeyValuePair<TBD.Profiles, ProfileType>(TBD.Profiles.ticHUL, ProfileType.Dehumidification),
        };

        /// <summary>
        /// The name an imported profile carries when it is NOT a shared definition - today's
        /// <c>"{internal condition} [{profile}]"</c>. Still the name of every zero-length (TAS function) profile,
        /// of the un-emitted ventilation reference, and of every profile produced by a conversion given no index.
        /// </summary>
        internal static string ProfileName_Legacy(string internalConditionName, string profileName)
        {
            return string.Format("{0} [{1}]", internalConditionName, profileName);
        }

        private static void Register(ProfileReuseIndex profileReuseIndex, TBD.InternalCondition internalCondition_TBD)
        {
            if (profileReuseIndex == null || internalCondition_TBD == null)
            {
                return;
            }

            string name = internalCondition_TBD.name;

            TBD.InternalGain internalGain = internalCondition_TBD.GetInternalGain();
            if (internalGain != null)
            {
                foreach (KeyValuePair<TBD.Profiles, ProfileType> keyValuePair in ProfileSlots_InternalGain)
                {
                    Register(profileReuseIndex, name, internalGain.GetProfile((int)keyValuePair.Key), keyValuePair.Key, keyValuePair.Value);
                }
            }

            TBD.Thermostat thermostat = internalCondition_TBD.GetThermostat();
            if (thermostat != null)
            {
                foreach (KeyValuePair<TBD.Profiles, ProfileType> keyValuePair in ProfileSlots_Thermostat)
                {
                    Register(profileReuseIndex, name, thermostat.GetProfile((int)keyValuePair.Key), keyValuePair.Key, keyValuePair.Value);
                }
            }
        }

        private static void Register(ProfileReuseIndex profileReuseIndex, string internalConditionName, TBD.profile profile_TBD, TBD.Profiles slot, ProfileType profileType)
        {
            if (profile_TBD == null)
            {
                return;
            }

            profileReuseIndex.Register(
                internalConditionName,
                (int)slot,
                Analytical.Query.Text(profileType),
                Core.Tas.Query.Values(profile_TBD),
                profile_TBD.name,
                ProfileName_Legacy(internalConditionName, profile_TBD.name));
        }
    }
}
