// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.ComponentModel;

namespace SAM.Analytical.Tas
{
    /// <summary>
    /// How <see cref="WorkflowCalculator"/> gets a SAM model into a TAS3D <c>.t3d</c>, ahead of the T3D -> TBD
    /// export.
    /// <para>
    /// <b><see cref="GbXML"/> is the default and the established route.</b> A <see cref="WorkflowSettings"/> that
    /// does not state a route - a new instance, or a serialized one written before this setting existed - means
    /// <see cref="GbXML"/>, so no existing caller changes behaviour. <see cref="Direct"/> is only ever selected by
    /// an explicit, serialized choice.
    /// </para>
    /// </summary>
    public enum T3DRoute
    {
        /// <summary>
        /// <c>SAM -> gbXML -> ImportGBXML -> UpdateT3D -> TBD</c>. Driven by <see cref="WorkflowSettings.Path_gbXML"/>.
        /// </summary>
        [Description("gbXML")] GbXML = 0,

        /// <summary>
        /// <c>SAM -> direct TAS geometry importer (T3DDocument.CreateIDFImport / WrImportIDF) -> T3D -> TBD</c>.
        /// No gbXML is written or read; <see cref="WorkflowSettings.Path_gbXML"/> is ignored.
        /// </summary>
        [Description("Direct")] Direct = 1
    }
}
