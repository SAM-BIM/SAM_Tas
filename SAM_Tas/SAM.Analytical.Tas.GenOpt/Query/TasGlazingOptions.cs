// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Tas.GenOpt
{
    public static partial class Query
    {
        /// <summary>
        /// The options of a glazing choice for one TBD glazing construction (PR7a-2, answer 4, owner-approved defaults):
        /// <list type="number">
        /// <item>Option 1 is the glazing the model uses now (its TBD construction name), so the result table compares
        /// every system with it.</item>
        /// <item>The pool systems of the filter's aperture type that are transparent, have finite g, Ug and light, g in
        /// the requested range, Ug ≤ current U + allowance and light ≥ current light − allowance (both compared to
        /// 0.001).</item>
        /// <item>Systems with the same g, Ug and light to 0.001 are counted once (the first in pool order), and so is a
        /// system equal to the current glazing.</item>
        /// <item>They follow option 1 in g order (then Ug, name, Guid). When more pass than fit, the kept ones are spread
        /// evenly over that order, the lowest and highest g always kept.</item>
        /// <item>An option's text is the system's name, with its 6-character short id appended when two pool systems of
        /// the choice's aperture type share the name (or the text would repeat another option's).</item>
        /// </list>
        /// Nothing is calculated here: the values come with the pool (<see cref="TasGlazingSystems"/>).
        /// </summary>
        /// <param name="current">The glazing construction the choice replaces.</param>
        /// <param name="pool">The pool, in source order (model, default library, My glazing systems, loaded files).</param>
        /// <param name="filter">The filter; null for the defaults.</param>
        public static List<TasGlazingOption> TasGlazingOptions(TasGlazingConstructionInfo current, IEnumerable<TasGlazingSystem> pool, TasGlazingFilter filter = null)
        {
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            filter = filter ?? new TasGlazingFilter();
            List<TasGlazingSystem> systems = (pool ?? Enumerable.Empty<TasGlazingSystem>()).Where(x => x != null).ToList();

            List<TasGlazingOption> result = new List<TasGlazingOption>
            {
                new TasGlazingOption(1, current.Name, current.Name, current.G, current.U, current.Light, "Model (current)", null),
            };

            double maximumUg = Round(current.U + filter.UgAllowance);
            double minimumLight = Round(current.Light - filter.LightAllowance);
            HashSet<Tuple<double, double, double>> seen = new HashSet<Tuple<double, double, double>> { Key(current.G, current.U, current.Light) };
            List<TasGlazingSystem> candidates = new List<TasGlazingSystem>();
            foreach (TasGlazingSystem system in systems)
            {
                if (!system.Transparent || !string.Equals(system.ApertureType, filter.ApertureType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!Finite(system.G) || !Finite(system.Ug) || !Finite(system.Light))
                {
                    continue;
                }

                if ((filter.MinimumG.HasValue && system.G < filter.MinimumG.Value) || (filter.MaximumG.HasValue && system.G > filter.MaximumG.Value))
                {
                    continue;
                }

                if (Round(system.Ug) > maximumUg || Round(system.Light) < minimumLight)
                {
                    continue;
                }

                if (!seen.Add(Key(system.G, system.Ug, system.Light)))
                {
                    continue;
                }

                candidates.Add(system);
            }

            candidates = candidates
                .OrderBy(x => x.G)
                .ThenBy(x => x.Ug)
                .ThenBy(x => x.Name ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(x => x.Guid)
                .ToList();

            int places = System.Math.Max(0, filter.MaximumOptions - 1);
            List<TasGlazingSystem> chosen = Spread(candidates, places);

            // Names are compared within the systems a choice of this aperture type could offer (a door of the same
            // name is never an option, so it does not make a window's name ambiguous).
            Dictionary<string, int> nameCounts = systems
                .Where(x => x.Transparent && string.Equals(x.ApertureType, filter.ApertureType, StringComparison.OrdinalIgnoreCase))
                .GroupBy(x => x.Name ?? string.Empty, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);
            HashSet<string> texts = new HashSet<string>(StringComparer.Ordinal) { current.Name };
            foreach (TasGlazingSystem system in chosen)
            {
                string name = system.Name ?? string.Empty;
                string text = (nameCounts.TryGetValue(name, out int count) && count > 1) || texts.Contains(name) || string.IsNullOrWhiteSpace(name) ? (name + " " + system.ShortId).Trim() : name;
                if (!texts.Add(text))
                {
                    continue;
                }

                result.Add(new TasGlazingOption(result.Count + 1, text, TasGlazingPaneConstruction(system), system.G, system.Ug, system.Light, system.Source, system));
            }

            return result;
        }

        /// <summary>
        /// The TBD pane construction name a pool system is written under before the run (<see cref="TasModelRunner"/>):
        /// SAM_Tas' own naming (<c>Modify.UpdateConstructions</c>) of the system renamed "&lt;name&gt; &lt;short id&gt;",
        /// so it never reuses (and overwrites) a construction the model has. Null when the system is not attached.
        /// </summary>
        public static string TasGlazingPaneConstruction(TasGlazingSystem tasGlazingSystem)
        {
            if (tasGlazingSystem?.ApertureConstruction == null)
            {
                return null;
            }

            return PaneConstructionName(UniqueApertureConstruction(tasGlazingSystem));
        }

        /// <summary>The system renamed "&lt;name&gt; &lt;short id&gt;", keeping its Guid (PR7a-2's unique-name rule).</summary>
        internal static ApertureConstruction UniqueApertureConstruction(TasGlazingSystem tasGlazingSystem)
        {
            ApertureConstruction apertureConstruction = tasGlazingSystem.ApertureConstruction;
            return new ApertureConstruction(apertureConstruction.Guid, apertureConstruction, apertureConstruction.Name + " " + tasGlazingSystem.ShortId);
        }

        private static string PaneConstructionName(ApertureConstruction apertureConstruction)
        {
            string name = global::SAM.Analytical.Tas.Query.Name(apertureConstruction.UniqueName(), true, true, false, false);
            return global::SAM.Analytical.Query.PaneApertureConstructionUniqueName(name);
        }

        /// <summary>
        /// Keeps <paramref name="count"/> items spread evenly over the ordered list (the first and last always kept), or
        /// all of them when they fit.
        /// </summary>
        private static List<T> Spread<T>(List<T> items, int count)
        {
            if (items.Count <= count)
            {
                return items;
            }

            if (count <= 0)
            {
                return new List<T>();
            }

            if (count == 1)
            {
                return new List<T> { items[0] };
            }

            List<T> result = new List<T>();
            for (int i = 0; i < count; i++)
            {
                result.Add(items[(int)System.Math.Round((double)i * (items.Count - 1) / (count - 1), MidpointRounding.AwayFromZero)]);
            }

            return result;
        }

        private static Tuple<double, double, double> Key(double g, double u, double light)
        {
            return Tuple.Create(Round(g), Round(u), Round(light));
        }

        private static double Round(double value)
        {
            return System.Math.Round(value, 3, MidpointRounding.AwayFromZero);
        }

        private static bool Finite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
