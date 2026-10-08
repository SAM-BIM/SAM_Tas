// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Math;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SAM.Analytical.Tas.GenOpt
{
    public static partial class Convert
    {
        /// <summary>
        /// Maps GenOpt parameters and objectives to a SAM.Math problem. Each parameter value is the double GenOpt reads
        /// from the text the GenOpt-format writer produces (<see cref="GenOptNumber"/>, owner decision D1). The first objective is
        /// the one minimised; the others are recorded.
        /// </summary>
        public static OptimisationProblem ToSAM_OptimisationProblem(this IEnumerable<IParameter> parameters, ObjectiveFunctionLocation objectiveFunctionLocation)
        {
            List<NumberParameter> numberParameters = NumberParameters(parameters);
            List<Objective> objectives = Objectives(objectiveFunctionLocation);

            return new OptimisationProblem(numberParameters.ConvertAll(ToSAM_OptimisationParameter), objectives.Count);
        }

        /// <summary>One NumberParameter, with GenOpt's reading of the writer's text for Ini, Min, Max and Step.</summary>
        public static OptimisationParameter ToSAM_OptimisationParameter(this NumberParameter numberParameter)
        {
            if (numberParameter == null)
            {
                throw new ArgumentNullException(nameof(numberParameter));
            }

            string name = numberParameter.Name;
            return new OptimisationParameter(
                name,
                GenOptNumber.ReadParameterValue(numberParameter.Initial, name + " Ini"),
                GenOptNumber.ReadParameterValue(numberParameter.Min, name + " Min"),
                GenOptNumber.ReadParameterValue(numberParameter.Max, name + " Max"),
                GenOptNumber.ReadParameterValue(numberParameter.Step, name + " Step"));
        }

        /// <summary>
        /// The parameters as NumberParameters, validated: at least one; NumberParameter only; names present, unique and
        /// free of ',' and line breaks (they become Variables.txt CSV fields).
        /// </summary>
        public static List<NumberParameter> NumberParameters(IEnumerable<IParameter> parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            List<NumberParameter> result = new List<NumberParameter>();
            foreach (IParameter parameter in parameters)
            {
                NumberParameter numberParameter = parameter as NumberParameter;
                if (numberParameter == null)
                {
                    throw new NotSupportedException("Only NumberParameter is supported by the native optimiser; got " + (parameter == null ? "null" : parameter.GetType().Name) + ".");
                }

                string name = numberParameter.Name;
                if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(new[] { ',', '\r', '\n' }) >= 0)
                {
                    throw new GenOptCompatibilityException("Parameter name '" + name + "' is not usable: it must be non-empty and contain no ',' or line break.");
                }

                if (result.Any(x => x.Name == name))
                {
                    throw new GenOptCompatibilityException("Parameter name '" + name + "' is used twice.");
                }

                result.Add(numberParameter);
            }

            if (result.Count == 0)
            {
                throw new GenOptCompatibilityException("At least one optimisation parameter is required.");
            }

            return result;
        }

        /// <summary>The objectives in order (the first is minimised), each with a name and a non-empty delimiter.</summary>
        public static List<Objective> Objectives(ObjectiveFunctionLocation objectiveFunctionLocation)
        {
            List<Objective> objectives = objectiveFunctionLocation?.Objectives;
            if (objectives == null || objectives.Count == 0)
            {
                throw new GenOptCompatibilityException("At least one objective (the first is minimised, 'Result' in Tas) is required.");
            }

            foreach (Objective objective in objectives)
            {
                if (string.IsNullOrWhiteSpace(objective?.Name) || string.IsNullOrEmpty(objective.Delimiter))
                {
                    throw new GenOptCompatibilityException(string.Format(CultureInfo.InvariantCulture, "Objective '{0}' needs a name and a delimiter.", objective?.Name));
                }
            }

            return new List<Objective>(objectives);
        }
    }
}
