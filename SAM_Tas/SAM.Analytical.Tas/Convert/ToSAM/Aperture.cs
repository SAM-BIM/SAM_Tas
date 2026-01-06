// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;

namespace SAM.Analytical.Tas
{
    public static partial class Convert
    {
        public static Aperture ToSAM(this TAS3D.window window)
        {
            if (window == null)
                return null;

            ParameterSet parameterSet = Create.ParameterSet(ActiveSetting.Setting, window);

            Aperture aperture = new Aperture(new ApertureConstruction(window.name, ApertureType.Window), null);
            aperture.Add(parameterSet);

            return aperture;
        }
    }
}
