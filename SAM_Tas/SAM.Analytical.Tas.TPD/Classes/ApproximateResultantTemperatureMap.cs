// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Systems;
using SAM.Core;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace SAM.Analytical.Tas.TPD
{
    /// <summary>
    /// The <b>legacy, approximate</b> TPD preparation: one pass, no second simulation, and a
    /// <c>ResultantTemperature</c> series that is <b>synthesised</b> as the mean of the companion TSD's mean
    /// radiant temperature and the TPD's zone temperature.
    /// <para>
    /// <b>This is not the TPD-full route and must stay visibly distinct from it.</b> The authoritative TPD-full
    /// route is <see cref="ResultantTemperaturePreparation"/>: it simulates the actual system, carries the first
    /// pass's result into a copy of the TBD, simulates that copy, and reads a real <c>ResultantTemperature</c>
    /// out of the second TSD. This class does none of that. It exists because it shipped, and callers depend on
    /// it; it is kept as a compatibility route, not as an alternative answer to the same question. A failed
    /// TPD-full run must <b>never</b> fall back to it - reporting a synthesised series in place of a simulated
    /// one would present an approximation as the real thing.
    /// </para>
    /// <para>
    /// <b>Why the arithmetic lives here and not in <c>SAM.Analytical</c>.</b> Averaging mean radiant temperature
    /// with a systems-model zone temperature is a TAS-specific accommodation for a series TPD does not produce.
    /// <c>TM59AssessmentCalculator</c> assesses whatever hourly <c>ResultantTemperature</c> it is handed and must
    /// not learn where the series came from, or the engine-neutral half of the boundary is lost. Preparation
    /// differs; assessment does not.
    /// </para>
    /// <para>
    /// <b>Association is identity-first.</b> The route it replaced took the first TPD result whose <i>name</i>
    /// matched the space, which in a block of flats silently pairs one dwelling's system results with another
    /// dwelling's room - every flat has a "Bedroom 2". Where an identity cannot be resolved unambiguously the
    /// space is refused and reported, never paired with a same-named one.
    /// </para>
    /// <para>
    /// <b>Free of TAS COM types</b>, so the preparation can be tested without an installed TAS. The engine
    /// vocabulary it needs - the identity a space carries across the round trip and the two series keys - is
    /// supplied by the caller, the same seam <c>TM59AssessmentCalculator</c> uses for
    /// <c>ResultantTemperatureSeriesKey</c>.
    /// </para>
    /// </summary>
    public class ApproximateResultantTemperatureMap
    {
        private readonly List<string> refusals = new List<string>();

        private readonly List<Space> spaces_Prepared = new List<Space>();

        /// <summary>
        /// Synthesises the approximate <c>ResultantTemperature</c> onto the simulation model's spaces.
        /// </summary>
        /// <param name="analyticalModel_Simulation">
        /// The model read back from the TSD beside the TPD, carrying the mean radiant temperature series. Not
        /// modified: a new model is returned by <see cref="AnalyticalModel"/>.
        /// </param>
        /// <param name="systemSpaceResults">The TPD's per-zone results, carrying the zone temperature series.</param>
        /// <param name="func_StableKey">
        /// The engine identity a space carries across the round trip, matched against each result's
        /// <c>Reference</c>. For TAS the caller passes <c>Analytical.Tas.Query.SimulationSpaceKey</c>. Null falls
        /// the whole map back to unique names, which refuses duplicates rather than guessing between them.
        /// </param>
        /// <param name="seriesKey_MeanRadiantTemperature">The key the TSD conversion wrote the radiant series under.</param>
        /// <param name="seriesKey_ResultantTemperature">The key the assessment will read the synthesised series from.</param>
        public ApproximateResultantTemperatureMap(AnalyticalModel analyticalModel_Simulation, IEnumerable<SystemSpaceResult> systemSpaceResults, Func<Space, string> func_StableKey, string seriesKey_MeanRadiantTemperature, string seriesKey_ResultantTemperature)
        {
            if (analyticalModel_Simulation == null)
            {
                refusals.Add("No simulation model was supplied, so there is nothing to prepare for assessment.");
                return;
            }

            if (string.IsNullOrWhiteSpace(seriesKey_MeanRadiantTemperature) || string.IsNullOrWhiteSpace(seriesKey_ResultantTemperature))
            {
                refusals.Add("The mean radiant temperature and resultant temperature series keys are both required: without them the preparation would silently write a series the assessment does not read.");
                return;
            }

            List<Space> spaces = analyticalModel_Simulation.GetSpaces();
            if (spaces == null || spaces.Count == 0)
            {
                refusals.Add("The simulation model carries no spaces, so there is nothing to prepare for assessment.");
                AnalyticalModel = analyticalModel_Simulation;
                return;
            }

            Dictionary<string, List<SystemSpaceResult>> dictionary_Reference = Group(systemSpaceResults, x => x.Reference);
            Dictionary<string, List<SystemSpaceResult>> dictionary_Name = Group(systemSpaceResults, x => x.Name);

            //Whether identity is in use is decided ONCE, for the whole model, and not per space.
            //
            //A space's engine identity and a TPD result's Reference are stamped by different halves of TAS, so a
            //model where they do not correspond at all is a legitimate shape - and the only thing left there is
            //the name, which is what this route has always used. But deciding that space by space would be
            //actively dangerous: a stamped space whose identity simply has no result would fall through to the
            //name rule and collect a SAME-NAMED sibling's system results. Three flats, one "Bedroom 2", and the
            //flat with no result silently reports its neighbour's. So either every match is by identity or every
            //match is by name, and in name mode duplicates are refused rather than guessed between.
            bool identity = false;
            foreach (Space space in spaces)
            {
                string key = func_StableKey == null ? null : func_StableKey.Invoke(space);
                if (!string.IsNullOrWhiteSpace(key) && dictionary_Reference.ContainsKey(key))
                {
                    identity = true;
                    break;
                }
            }

            AdjacencyCluster adjacencyCluster = analyticalModel_Simulation.AdjacencyCluster;

            foreach (Space space in spaces)
            {
                SystemSpaceResult systemSpaceResult = Resolve(space, func_StableKey, dictionary_Reference, dictionary_Name, identity);
                if (systemSpaceResult == null)
                {
                    continue;
                }

                Space space_Prepared = Synthesise(space, systemSpaceResult, seriesKey_MeanRadiantTemperature, seriesKey_ResultantTemperature);
                if (space_Prepared == null)
                {
                    continue;
                }

                adjacencyCluster.AddObject(space_Prepared);
                spaces_Prepared.Add(space_Prepared);
            }

            AnalyticalModel = new AnalyticalModel(analyticalModel_Simulation, adjacencyCluster);
        }

        /// <summary>
        /// The prepared model - the simulation model with a synthesised <c>ResultantTemperature</c> on every space
        /// that could be resolved and read. <b>This is the model the common assessment runs on</b>, and the point
        /// at which this route stops being distinguishable from any other: from here on it is an
        /// <c>AnalyticalModel</c> carrying the required hourly series, nothing more.
        /// </summary>
        public AnalyticalModel AnalyticalModel { get; }

        /// <summary>
        /// Spaces that received a synthesised series. A space absent from here produced no assessment, and the
        /// reason is in <see cref="Refusals"/>.
        /// </summary>
        public List<Space> Prepared => new List<Space>(spaces_Prepared);

        /// <summary>
        /// Every space this preparation refused, and why. <b>Reported, not swallowed</b> - a room missing from the
        /// assessment because its identity could not be resolved is a gap the user has to be able to see.
        /// </summary>
        public List<string> Refusals => new List<string>(refusals);

        /// <summary>
        /// Finds the TPD result belonging to this space: the engine identity first, then a unique name, then
        /// refusal. Never the first same-named candidate.
        /// </summary>
        private SystemSpaceResult Resolve(Space space, Func<Space, string> func_StableKey, Dictionary<string, List<SystemSpaceResult>> dictionary_Reference, Dictionary<string, List<SystemSpaceResult>> dictionary_Name, bool identity)
        {
            List<SystemSpaceResult> systemSpaceResults;

            if (identity)
            {
                string key = func_StableKey == null ? null : func_StableKey.Invoke(space);

                if (string.IsNullOrWhiteSpace(key))
                {
                    refusals.Add(string.Format("Space '{0}' carries no identity while the rest of the model is matched by identity, so no TPD result can be attributed to it.", space.Name));
                    return null;
                }

                if (!dictionary_Reference.TryGetValue(key, out systemSpaceResults))
                {
                    refusals.Add(string.Format("Space '{0}' has no TPD result for its identity '{1}', so it produces no approximate resultant temperature.", space.Name, key));
                    return null;
                }

                if (systemSpaceResults.Count > 1)
                {
                    //One identity on two results is a broken model, not a naming coincidence.
                    refusals.Add(string.Format("Space '{0}' matches {1} TPD results by identity '{2}', so no result can be attributed to it.", space.Name, systemSpaceResults.Count, key));
                    return null;
                }

                return systemSpaceResults[0];
            }

            if (dictionary_Name.TryGetValue(space.Name ?? string.Empty, out systemSpaceResults))
            {
                if (systemSpaceResults.Count == 1)
                {
                    return systemSpaceResults[0];
                }

                //Two results share this name - typically the same room in two flats. Refusing here is the whole
                //reason this route stopped matching by name.
                refusals.Add(string.Format("Space '{0}' matches {1} TPD results by name and carries no identity that tells them apart, so no result can be attributed to it.", space.Name, systemSpaceResults.Count));
                return null;
            }

            refusals.Add(string.Format("Space '{0}' has no TPD result, so it produces no approximate resultant temperature.", space.Name));
            return null;
        }

        /// <summary>
        /// Writes the synthesised series onto a copy of the space, or refuses where either input series is
        /// missing or the two disagree on length.
        /// </summary>
        private Space Synthesise(Space space, SystemSpaceResult systemSpaceResult, string seriesKey_MeanRadiantTemperature, string seriesKey_ResultantTemperature)
        {
            ParameterSet parameterSet = space.GetParameterSets() == null ? null : space.GetParameterSets().Find(x => x.Contains(seriesKey_MeanRadiantTemperature));
            if (parameterSet == null)
            {
                refusals.Add(string.Format("Space '{0}' carries no '{1}' series, so no approximate resultant temperature can be synthesised for it.", space.Name, seriesKey_MeanRadiantTemperature));
                return null;
            }

            JsonArray jsonArray_MeanRadiantTemperature = parameterSet.ToObject(seriesKey_MeanRadiantTemperature) as JsonArray;
            if (jsonArray_MeanRadiantTemperature == null || jsonArray_MeanRadiantTemperature.Count == 0)
            {
                refusals.Add(string.Format("Space '{0}' carries an empty '{1}' series, so no approximate resultant temperature can be synthesised for it.", space.Name, seriesKey_MeanRadiantTemperature));
                return null;
            }

            IndexedDoubles indexedDoubles = systemSpaceResult[SpaceDataType.ZoneTemperature.ToString()];
            if (indexedDoubles == null)
            {
                refusals.Add(string.Format("The TPD result for space '{0}' carries no zone temperature, so no approximate resultant temperature can be synthesised for it.", space.Name));
                return null;
            }

            //A bounded read always returns as many values as were asked for - it WRAPS the available indices to
            //fill the range rather than running out. So the length of what comes back proves nothing, and the
            //check has to be against how much the TPD result actually holds. A ten-hour result silently
            //stretched over a year would otherwise assess a fabricated series.
            int? index_Min = indexedDoubles.GetMinIndex();
            int? index_Max = indexedDoubles.GetMaxIndex();

            int count_Available = index_Min.HasValue && index_Max.HasValue ? index_Max.Value - index_Min.Value + 1 : 0;
            if (count_Available < jsonArray_MeanRadiantTemperature.Count)
            {
                refusals.Add(string.Format("Space '{0}' has {1} radiant temperature values but its TPD result covers only {2} hours, so the two cannot be combined.", space.Name, jsonArray_MeanRadiantTemperature.Count, count_Available));
                return null;
            }

            List<double> values_ZoneTemperature = indexedDoubles.GetValues(new Range<int>(0, jsonArray_MeanRadiantTemperature.Count - 1), true);
            if (values_ZoneTemperature == null || values_ZoneTemperature.Count < jsonArray_MeanRadiantTemperature.Count)
            {
                refusals.Add(string.Format("Space '{0}' produced no readable TPD zone temperature series.", space.Name));
                return null;
            }

            JsonArray jsonArray_ResultantTemperature = new JsonArray();
            for (int i = 0; i < jsonArray_MeanRadiantTemperature.Count; i++)
            {
                jsonArray_ResultantTemperature.Add(((double)jsonArray_MeanRadiantTemperature[i] + values_ZoneTemperature[i]) / 2);
            }

            Space result = new Space(space);

            ParameterSet parameterSet_Result = new ParameterSet(parameterSet);
            parameterSet_Result.Add(seriesKey_ResultantTemperature, jsonArray_ResultantTemperature);
            result.Add(parameterSet_Result);

            return result;
        }

        private static Dictionary<string, List<SystemSpaceResult>> Group(IEnumerable<SystemSpaceResult> systemSpaceResults, Func<SystemSpaceResult, string> func)
        {
            Dictionary<string, List<SystemSpaceResult>> result = new Dictionary<string, List<SystemSpaceResult>>();

            foreach (SystemSpaceResult systemSpaceResult in systemSpaceResults ?? new List<SystemSpaceResult>())
            {
                if (systemSpaceResult == null)
                {
                    continue;
                }

                string key = func.Invoke(systemSpaceResult);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                List<SystemSpaceResult> systemSpaceResults_Key;
                if (!result.TryGetValue(key, out systemSpaceResults_Key))
                {
                    systemSpaceResults_Key = new List<SystemSpaceResult>();
                    result[key] = systemSpaceResults_Key;
                }

                systemSpaceResults_Key.Add(systemSpaceResult);
            }

            return result;
        }
    }
}
