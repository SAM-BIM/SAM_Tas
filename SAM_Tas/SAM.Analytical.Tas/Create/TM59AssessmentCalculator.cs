// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;

namespace SAM.Analytical.Tas
{
    public static partial class Create
    {
        /// <summary>
        /// A <see cref="Analytical.TM59AssessmentCalculator"/> set up to read a model that TAS wrote.
        /// <para>
        /// <b>Three values, and all three are TAS's.</b> The assessment recipe itself is engine-free and lives
        /// in <c>SAM.Analytical</c>; what is not engine-free is the vocabulary the TSD conversion writes -
        /// notably "Occupant Sensible Gain", where the analytical vocabulary says "Occupancy Sensible Gain" -
        /// and the provenance a result carries when the model has no name. Reading the wrong series key is
        /// silent: the space simply produces no assessment.
        /// </para>
        /// <para>
        /// <b>Why a factory rather than three assignments at the call site.</b> These are exactly the values
        /// <see cref="OverheatingCalculator"/> already supplies, and a caller that restated them would be a
        /// second place to keep them right - which is the drift the recipe was extracted to stop. A caller
        /// asks for a calculator; TAS's vocabulary stays in TAS's assembly.
        /// </para>
        /// </summary>
        /// <param name="analyticalModel">
        /// The model read back from a TAS simulation - fresh spaces carrying hourly series, not the design
        /// model.
        /// </param>
        public static Analytical.TM59AssessmentCalculator TM59AssessmentCalculator(this AnalyticalModel analyticalModel)
        {
            return new Analytical.TM59AssessmentCalculator(analyticalModel)
            {
                //The keys TAS wrote, which are not what the analytical vocabulary would have called them.
                ResultantTemperatureSeriesKey = SpaceDataType.ResultantTemperature.Text(),
                OccupancySensibleGainSeriesKey = SpaceDataType.OccupantSensibleGain.Text(),

                //Provenance only, and the same fallback OverheatingCalculator has always stamped.
                SourceFallback = Query.Source(),
            };
        }
    }
}
