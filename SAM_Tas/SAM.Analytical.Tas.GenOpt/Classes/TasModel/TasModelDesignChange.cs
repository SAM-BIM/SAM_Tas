// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Optimisation;

namespace SAM.Analytical.Tas.GenOpt
{
    /// <summary>
    /// One change of "Apply best design" (PR9): a design variable of a "tas-model" definition at its best value, with the
    /// model item it changes. <see cref="Query.TasModelDesignChanges"/> builds the list from a best point;
    /// <see cref="TasModelDesignApplier"/> writes it into the project's Tas files with the same rules as the generated
    /// script (<see cref="Create.TasScript"/>), so the files hold exactly the design that was evaluated.
    /// </summary>
    public sealed class TasModelDesignChange
    {
        internal TasModelDesignChange(DesignVariable designVariable, double value, TasGlazingOption glazingOption)
        {
            VariableName = designVariable.Name;
            Target = new OptimisationTarget(designVariable.Target);
            Value = value;
            GlazingOption = glazingOption;
        }

        /// <summary>The design variable's name in the definition.</summary>
        public string VariableName { get; }

        /// <summary>The variable's binding (kind and reference).</summary>
        public OptimisationTarget Target { get; }

        public string Kind => Target.Kind;

        /// <summary>
        /// The best value, exactly as the optimisation reported it (no rounding). A setpoint is written to the TBD as the
        /// float the script wrote (<c>(float)Value</c>); a controller setpoint as this double; a glazing choice is the
        /// option number.
        /// </summary>
        public double Value { get; }

        /// <summary>For a glazing choice, the chosen option (<see cref="TasGlazingOption.IsCurrent"/> for option 1); otherwise null.</summary>
        public TasGlazingOption GlazingOption { get; }

        /// <summary>True for a heating or cooling setpoint of an internal condition (TBD).</summary>
        public bool IsSetpoint => Kind == TasModelKind.HeatingSetpoint || Kind == TasModelKind.CoolingSetpoint;

        /// <summary>True for a heating setpoint (thermostat lower limit).</summary>
        public bool IsHeating => Kind == TasModelKind.HeatingSetpoint;

        /// <summary>True for a glazing choice (TBD).</summary>
        public bool IsGlazing => Kind == TasModelKind.GlazingChoice;

        /// <summary>True for a plant controller setpoint (TPD only: the SAM model does not hold plant controllers).</summary>
        public bool IsController => Kind == TasModelKind.ControllerSetpoint;

        /// <summary>True when the change is written to the TBD; otherwise it is written to the TPD.</summary>
        public bool IsBuilding => TasModelKind.IsBuildingTarget(Kind);

        /// <summary>The internal condition of a setpoint (exact TBD name); otherwise null.</summary>
        public string InternalCondition => Reference(TasModelKind.InternalConditionKey);

        /// <summary>The TBD glazing construction a glazing choice replaces (exact name); otherwise null.</summary>
        public string GlazingConstruction => Reference(TasModelKind.GlazingConstructionKey);

        /// <summary>The plant room of a controller (exact TPD name); otherwise null.</summary>
        public string PlantRoom => Reference(TasModelKind.PlantRoomKey);

        /// <summary>The controller (exact TPD name); otherwise null.</summary>
        public string Controller => Reference(TasModelKind.ControllerKey);

        /// <summary>The option number of a glazing choice; 0 otherwise.</summary>
        public int OptionNumber => GlazingOption?.Number ?? 0;

        private string Reference(string key)
        {
            return Target.Reference != null && Target.Reference.TryGetValue(key, out string value) ? value : null;
        }
    }
}
