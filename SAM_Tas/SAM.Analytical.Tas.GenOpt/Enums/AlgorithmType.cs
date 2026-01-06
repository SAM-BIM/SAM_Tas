// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas.GenOpt
{
    public enum AlgorithmType
    {
        GPSHookeJeeves,
        GPSCoordinateSearch,
        DiscreteArmijoGradient,
        PSOIW,
        PSOCC,
        PSOCCMesh,
        GPSPSOCCHJ,
        NelderMeadONeill,
        GoldenSection,
        Fibonacci,
        Parametric,
        Mesh
    }
}
