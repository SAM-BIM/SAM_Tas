// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas
{
    /// <summary>
    /// What the direct SAM -> T3D converter (<c>Convert.ToT3D(AnalyticalModel, T3DDocument, ToT3DOptions, ...)</c>)
    /// is asked to do. One small options object rather than a boolean per decision, so a new decision does not
    /// change a signature.
    /// </summary>
    public class ToT3DOptions
    {
        /// <summary>
        /// Whether TAS applies the building elements' widths when it builds the model (<c>SetUseBEWidths</c>).
        /// <para>
        /// <b>Defaults to false - the same default as <see cref="WorkflowSettings.UseWidths"/></b> - so the
        /// SAM panel polygons are the geometry, which is what the gbXML route delivers out of the box. TAS's own
        /// importer default is ON, and with it each input polygon is treated as a centre line and offset by half
        /// the element width (a 5 x 4 x 3 m box with 0.30/0.35/0.40 m elements: 17.39 m2 and 45.649 m3 instead of
        /// 20 m2 and 60 m3). Turning it on is therefore a modelling statement - "my panels are centre lines" -
        /// that SAM does not make for its own geometry.
        /// </para>
        /// </summary>
        public bool UseWidths { get; set; } = false;

        /// <summary>
        /// One TAS element per panel instead of one per SAM construction, so each surface can carry its own
        /// width. <b>Defaults to false</b>: the element is the construction's definition, and a per-panel element
        /// would give a 600-space model tens of thousands of definitions that differ in nothing but name.
        /// </summary>
        public bool ElementPerPanel { get; set; } = false;

        /// <summary>
        /// Whether <c>PanelType.Shade</c> panels are imported through <c>WrImportIDF.AddShadeSurface</c>.
        /// <para>
        /// The call is accepted by TAS, but whether the shade it creates takes part in the T3D -> TBD shading
        /// calculation is the thing the direct-route validation measures; see the notes on
        /// <see cref="T3DImportReport.Shades"/>. When false, shade panels are neither imported nor lost
        /// silently: they are counted and listed in the report so the caller can take the established route.
        /// </para>
        /// </summary>
        public bool ImportShades { get; set; } = true;

        /// <summary>The name of the TAS zone set that holds every SAM zone.</summary>
        public string ZoneSetName { get; set; } = "SAM";

        /// <summary>
        /// The distance [m] below which two vertices of one polygon are the same vertex, and within which a
        /// vertex lies on the line through its neighbours. See <c>Query.TasPolygon</c>.
        /// </summary>
        public double Tolerance { get; set; } = Core.Tolerance.MacroDistance;

        /// <summary>
        /// The largest distance [m] an aperture vertex may lie off its host panel's plane and still be snapped
        /// onto it. A larger deviation is reported, and the opening is still emitted on the snapped polygon:
        /// TAS builds an opening on its host plane, so the alternative is no opening at all.
        /// </summary>
        public double SnapTolerance { get; set; } = 0.05;
    }
}
