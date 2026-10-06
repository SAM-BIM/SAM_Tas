// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using System.Globalization;

namespace SAM.Analytical.Tas
{
    /// <summary>
    /// What a direct SAM -> T3D conversion did: how much it created, and - just as important - everything it
    /// declined to create and why. A skipped panel is a hole in the thermal model, so it is never dropped
    /// silently: it is counted here and named in <see cref="Skipped"/>.
    /// </summary>
    public class T3DImportReport
    {
        /// <summary>TAS zones created (one per SAM space that bounds at least one imported panel).</summary>
        public int Zones { get; set; }

        /// <summary>TAS elements created (building element definitions).</summary>
        public int Elements { get; set; }

        /// <summary>TAS window types created (aperture definitions - not one per aperture).</summary>
        public int Windows { get; set; }

        /// <summary>Every surface imported, of any kind.</summary>
        public int Surfaces { get; set; }

        /// <summary>Surfaces imported as plain exposed surfaces of one zone.</summary>
        public int ExternalSurfaces { get; set; }

        /// <summary>Surfaces imported as <c>AddInternalSurface</c>, linked between two zones.</summary>
        public int InternalSurfaces { get; set; }

        /// <summary>Surfaces imported with <c>isAdiabatic = true</c> (TBD <c>tbdNullLink</c>).</summary>
        public int AdiabaticSurfaces { get; set; }

        /// <summary>Surfaces imported on a ground element (TBD <c>tbdGround</c>).</summary>
        public int GroundSurfaces { get; set; }

        /// <summary>Openings imported - one per SAM aperture.</summary>
        public int Openings { get; set; }

        /// <summary>
        /// Shade panels. <c>Shades</c> counts the panels the model has; <see cref="ShadesImported"/> how many were
        /// handed to <c>AddShadeSurface</c>. They differ when <see cref="ToT3DOptions.ImportShades"/> is false.
        /// </summary>
        public int Shades { get; set; }

        /// <summary>Shade panels handed to <c>WrImportIDF.AddShadeSurface</c>.</summary>
        public int ShadesImported { get; set; }

        /// <summary>
        /// SAM space GUID -> the zone name it was given, for every zone created. The zone description carries the
        /// GUID itself (TAS's own zone GUID is read-only), so this is the same mapping the later TBD steps read.
        /// </summary>
        public Dictionary<System.Guid, string> ZoneNames { get; } = new Dictionary<System.Guid, string>();

        /// <summary>
        /// Everything not imported, one sentence each, with the reason. Empty means the whole model went in.
        /// </summary>
        public List<string> Skipped { get; } = new List<string>();

        /// <summary>
        /// Things imported with a caveat that does not lose geometry - an orientation that could not be
        /// verified against a closed shell, an aperture snapped onto its host plane, an element split, a hole
        /// ignored.
        /// </summary>
        public List<string> Notes { get; } = new List<string>();

        /// <summary>False when the conversion could not run at all (no model, no TAS document, TAS refused).</summary>
        public bool Success { get; set; }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "zones={0} elements={1} windows={2} surfaces={3} (external={4} internal={5} adiabatic={6} ground={7}) openings={8} shades={9}/{10} skipped={11} notes={12} success={13}",
                Zones, Elements, Windows, Surfaces, ExternalSurfaces, InternalSurfaces, AdiabaticSurfaces, GroundSurfaces, Openings, ShadesImported, Shades, Skipped.Count, Notes.Count, Success);
        }
    }
}
