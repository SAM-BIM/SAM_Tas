// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using TPD;
using SAM.Analytical.Systems;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Convert
    {
        public static ExchangerCalculationMethod ToSAM(this tpdExchangerCalcMethod tpdExchangerCalcMethod)
        {
            switch(tpdExchangerCalcMethod)
            {
                case tpdExchangerCalcMethod.tpdExchangerCalcSimple:
                    return ExchangerCalculationMethod.Simple;

                case tpdExchangerCalcMethod.tpdExchangerCalcDuty:
                    return ExchangerCalculationMethod.Duty;

                case tpdExchangerCalcMethod.tpdExchangerCalcNTU:
                    return ExchangerCalculationMethod.NTU;
            }

            return ExchangerCalculationMethod.Simple;
        }
    }
}
