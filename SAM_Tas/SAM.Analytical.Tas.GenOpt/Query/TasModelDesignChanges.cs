// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Optimisation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SAM.Analytical.Tas.GenOpt
{
    public static partial class Query
    {
        /// <summary>
        /// "Apply best design" (PR9): the changes a best point of a "tas-model" run makes, one per design variable in the
        /// definition's order. Nothing is read or written; the point is checked against the definition so that only a
        /// point the run could have evaluated is ever applied:
        /// <list type="bullet">
        /// <item>one finite value per variable, inside the variable's minimum–maximum range (inclusive, exact);</item>
        /// <item>a glazing choice: a whole option number 1..n (as the generated script requires), resolved to the run's
        /// option of that number (<see cref="TasModelRunner.GlazingOptions"/>, the definition's order);</item>
        /// <item>a supported target kind with every reference key the script needs.</item>
        /// </list>
        /// Values are kept exactly as given (no rounding); a setpoint is narrowed to the TBD's float only where it is
        /// written, as the script does.
        /// </summary>
        /// <param name="optimisationDefinition">The run's definition (engine "tas-model").</param>
        /// <param name="point">The best point, in the definition's variable order (<c>NativeGenOptOutcome.BestEntry.Coordinates</c>).</param>
        /// <param name="glazingOptions">The run's glazing options by variable name; needed only for a glazing choice.</param>
        /// <exception cref="ArgumentException">The point does not fit the definition; the message says which variable and why.</exception>
        public static List<TasModelDesignChange> TasModelDesignChanges(OptimisationDefinition optimisationDefinition, IReadOnlyList<double> point, IReadOnlyDictionary<string, IReadOnlyList<TasGlazingOption>> glazingOptions = null)
        {
            if (optimisationDefinition == null)
            {
                throw new ArgumentNullException(nameof(optimisationDefinition));
            }

            if (point == null)
            {
                throw new ArgumentNullException(nameof(point));
            }

            List<DesignVariable> variables = optimisationDefinition.Variables ?? new List<DesignVariable>();
            if (variables.Count == 0)
            {
                throw new ArgumentException("The definition has no design variable, so there is nothing to apply.", nameof(optimisationDefinition));
            }

            if (point.Count != variables.Count)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "The best point has {0} values but the definition has {1} design variables.", point.Count, variables.Count), nameof(point));
            }

            List<TasModelDesignChange> result = new List<TasModelDesignChange>();
            for (int i = 0; i < variables.Count; i++)
            {
                DesignVariable designVariable = variables[i];
                double value = point[i];
                string name = designVariable?.Name;
                string kind = designVariable?.Target?.Kind;

                if (!TasModelKind.IsBuildingTarget(kind) && kind != TasModelKind.ControllerSetpoint)
                {
                    throw new ArgumentException("Design variable “" + name + "” has no tas-model target (" + (kind ?? "none") + "), so it cannot be applied.", nameof(optimisationDefinition));
                }

                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    throw new ArgumentException("The best value of “" + name + "” is not a number.", nameof(point));
                }

                if (value < designVariable.Minimum || value > designVariable.Maximum)
                {
                    throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "The best value of “{0}” ({1}) is outside its range {2} to {3}.", name, value.ToString("R", CultureInfo.InvariantCulture), designVariable.Minimum.ToString("R", CultureInfo.InvariantCulture), designVariable.Maximum.ToString("R", CultureInfo.InvariantCulture)), nameof(point));
                }

                foreach (string key in DesignChangeReferenceKeys(kind))
                {
                    if (designVariable.Target.Reference == null || !designVariable.Target.Reference.TryGetValue(key, out string reference) || string.IsNullOrEmpty(reference))
                    {
                        throw new ArgumentException("The target of “" + name + "” does not say which " + key + ".", nameof(optimisationDefinition));
                    }
                }

                TasGlazingOption option = null;
                if (kind == TasModelKind.GlazingChoice)
                {
                    int number = (int)System.Math.Round(value);
                    if (System.Math.Abs(value - number) > 1e-9)
                    {
                        throw new ArgumentException("The best value of “" + name + "” (" + value.ToString("R", CultureInfo.InvariantCulture) + ") is not an option number.", nameof(point));
                    }

                    IReadOnlyList<TasGlazingOption> options = null;
                    if (glazingOptions == null || !glazingOptions.TryGetValue(name, out options) || options == null || options.Count == 0)
                    {
                        throw new ArgumentException("The glazing options of “" + name + "” are not known, so its best option cannot be applied.", nameof(glazingOptions));
                    }

                    List<string> listed = designVariable.Target.Options ?? new List<string>();
                    if (options.Count != listed.Count || !options.Select(x => x?.Text).SequenceEqual(listed, StringComparer.Ordinal))
                    {
                        throw new ArgumentException("The glazing options of “" + name + "” are not the ones its definition lists.", nameof(glazingOptions));
                    }

                    if (number < 1 || number > options.Count)
                    {
                        throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "The best value of “{0}” is option {1}, but there are options 1 to {2}.", name, number, options.Count), nameof(point));
                    }

                    option = options[number - 1];
                    if (option.Number != number)
                    {
                        throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Option {0} of “{1}” is numbered {2}.", number, name, option.Number), nameof(glazingOptions));
                    }

                    if (!option.IsCurrent && (option.System?.ApertureConstruction == null || string.IsNullOrEmpty(option.PaneConstruction)))
                    {
                        throw new ArgumentException("Glazing option “" + option.Text + "” of “" + name + "” has no glazing system to write.", nameof(glazingOptions));
                    }
                }

                result.Add(new TasModelDesignChange(designVariable, value, option));
            }

            return result;
        }

        private static IEnumerable<string> DesignChangeReferenceKeys(string kind)
        {
            switch (kind)
            {
                case TasModelKind.HeatingSetpoint:
                case TasModelKind.CoolingSetpoint:
                    return new[] { TasModelKind.InternalConditionKey };
                case TasModelKind.GlazingChoice:
                    return new[] { TasModelKind.GlazingConstructionKey };
                case TasModelKind.ControllerSetpoint:
                    return new[] { TasModelKind.PlantRoomKey, TasModelKind.ControllerKey };
                default:
                    return new string[0];
            }
        }
    }
}
