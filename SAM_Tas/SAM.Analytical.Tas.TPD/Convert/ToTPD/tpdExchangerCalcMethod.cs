// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Systems;
using TPD;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Convert
    {
        public static tpdExchangerCalcMethod ToTPD(this ExchangerCalculationMethod exchangerCalculationMethod)
        {
            switch (exchangerCalculationMethod)
            {
                case ExchangerCalculationMethod.Duty:
                    return tpdExchangerCalcMethod.tpdExchangerCalcDuty;

                case ExchangerCalculationMethod.Simple:
                    return tpdExchangerCalcMethod.tpdExchangerCalcSimple;

                case ExchangerCalculationMethod.NTU:
                    return tpdExchangerCalcMethod.tpdExchangerCalcNTU;
            }

            throw new System.NotImplementedException();
        }
    }
}
