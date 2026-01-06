// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;


namespace SAM.Analytical.Tas
{
    public static partial class Convert
    {
        public static FeatureShade ToSAM(this TBD.IFeatureShade featureShade)
        {
            if (featureShade == null)
            {
                return null;
            }

            return new FeatureShade(
                featureShade.name, 
                featureShade.description,
                featureShade.surfaceHeight,
                featureShade.surfaceWidth,
                featureShade.leftFinDepth,
                featureShade.leftFinOffset,
                featureShade.leftFinTransmittance,
                featureShade.rightFinDepth,
                featureShade.rightFinOffset,
                featureShade.rightFinTransmittance,
                featureShade.overhangDepth,
                featureShade.overhangOffset,
                featureShade.overhangTransmittance);
        }
    }
}
