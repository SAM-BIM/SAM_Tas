// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Convert
    {
        /// <summary>
        /// Reads the TBD aperture types assigned to a window/door building element back into a SAM
        /// <see cref="IOpeningProperties"/> (the inverse of <c>Modify.SetApertureType</c>), so that
        /// operable-opening data survives a TBD -> SAM -> TBD round-trip. Returns null when the
        /// building element carries no aperture types.
        /// </summary>
        public static IOpeningProperties ToSAM_OpeningProperties(this TBD.buildingElement buildingElement)
        {
            if (buildingElement == null)
            {
                return null;
            }

            List<TBD.ApertureType> apertureTypes = buildingElement.ApertureTypes();
            if (apertureTypes == null || apertureTypes.Count == 0)
            {
                return null;
            }

            List<ISingleOpeningProperties> singleOpeningProperties = new List<ISingleOpeningProperties>();
            foreach (TBD.ApertureType apertureType in apertureTypes)
            {
                ISingleOpeningProperties single = ToSAM_SingleOpeningProperties(apertureType);
                if (single != null)
                {
                    singleOpeningProperties.Add(single);
                }
            }

            if (singleOpeningProperties.Count == 0)
            {
                return null;
            }

            if (singleOpeningProperties.Count == 1)
            {
                return singleOpeningProperties[0];
            }

            return new MultipleOpeningProperties(singleOpeningProperties);
        }

        private static ISingleOpeningProperties ToSAM_SingleOpeningProperties(TBD.ApertureType apertureType)
        {
            if (apertureType == null)
            {
                return null;
            }

            // dischargeCoefficient and GetProfile() are accessed via dynamic to match how
            // Modify.SetApertureType writes them (they sit on the concrete COM type, not the
            // marshalled interface).
            dynamic apertureType_Dynamic = apertureType;

            double dischargeCoefficient = 0;
            try { dischargeCoefficient = System.Convert.ToDouble(apertureType_Dynamic.dischargeCoefficient); }
            catch { }

            double factor = 1;
            string function = null;
            Profile profile_SAM = null;

            try
            {
                TBD.profile profile = apertureType_Dynamic.GetProfile();
                if (profile != null)
                {
                    factor = System.Convert.ToDouble(profile.factor);

                    if (profile.type == TBD.ProfileTypes.ticFunctionProfile && !string.IsNullOrWhiteSpace(profile.function))
                    {
                        function = profile.function;
                    }

                    TBD.schedule schedule = profile.schedule;
                    if (schedule != null)
                    {
                        List<double> values = new List<double>(24);
                        for (int i = 0; i < 24; i++)
                        {
                            values.Add(System.Convert.ToDouble(schedule.get_values(i)));
                        }

                        string profileName = string.IsNullOrWhiteSpace(schedule.name) ? apertureType.name : schedule.name;
                        profile_SAM = new Profile(profileName, values);
                    }
                }
            }
            catch { }

            ProfileOpeningProperties openingProperties = new ProfileOpeningProperties(dischargeCoefficient, profile_SAM)
            {
                Factor = factor
            };

            if (!string.IsNullOrWhiteSpace(apertureType.description))
            {
                openingProperties.SetValue(OpeningPropertiesParameter.Description, apertureType.description);
            }

            if (!string.IsNullOrWhiteSpace(function))
            {
                openingProperties.SetValue(OpeningPropertiesParameter.Function, function);
            }

            return openingProperties;
        }
    }
}
