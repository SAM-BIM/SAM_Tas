// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Xml.Linq;

namespace SAM.Core.Tas.UKBR
{
    public class CurrentLights : UKBRElement
    {
        public override string UKBRName => "CurrentLights";

        public CurrentLights(XElement xElement)
            : base(xElement)
        {

        }

        public bool bBackSpaceSensor
        {
            get
            {
                return Query.Value<bool>(xElement?.Attribute("bBackSpaceSensor"));
            }
        }

        public bool bEfficacyLightingFunc
        {
            get
            {
                return Query.Value<bool>(xElement?.Attribute("bEfficacyLightingFunc"));
            }
        }

        public double LampEfficiencyPer100Lux
        {
            get
            {
                return Query.Value<double>(xElement?.Attribute("LampEfficiencyPer100Lux"));
            }
        }

        public double LampGeneralPowerDensity
        {
            get
            {
                return Query.Value<double>(xElement?.Attribute("LampGeneralPowerDensity"));
            }
        }

        public double LampEfficacy
        {
            get
            {
                return Query.Value<double>(xElement?.Attribute("LampEfficacy"));
            }
        }

        public double LampType
        {
            get
            {
                return Query.Value<double>(xElement?.Attribute("LampType"));
            }
        }
    }
}
