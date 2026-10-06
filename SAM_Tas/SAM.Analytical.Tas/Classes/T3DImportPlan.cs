// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    /// <summary>
    /// What a direct SAM -> T3D conversion is going to ask TAS to build, decided entirely from the SAM model and
    /// <b>with no TAS COM object involved</b>: every zone, element, window type, surface, opening and shade, with
    /// its final polygon.
    /// <para>
    /// The conversion is deliberately two halves. <see cref="Query.T3DImportPlan"/> reads the model and makes every
    /// decision (what is a shade, what is adiabatic, which side the normal points, which construction a panel
    /// shares an element with, which window type an aperture uses); <c>Convert.ToT3D</c> then replays the plan
    /// into a <c>T3DDocument</c>, in the one order <c>WrImportIDF</c> allows. The split is what lets all of the
    /// decisions be tested without a TAS licence, and keeps the COM half short enough to read at a glance.
    /// </para>
    /// </summary>
    public class T3DImportPlan
    {
        /// <summary>The building name (the model's name).</summary>
        public string Name { get; set; }

        /// <summary>The building description.</summary>
        public string Description { get; set; }

        /// <summary>The north angle [degrees], or NaN when the model states none (TAS keeps its own).</summary>
        public double NorthAngle { get; set; } = double.NaN;

        /// <summary>The element definitions, by <see cref="T3DElementSpec.Key"/>.</summary>
        public List<T3DElementSpec> Elements { get; } = new List<T3DElementSpec>();

        /// <summary>The window types, by <see cref="T3DWindowSpec.Key"/>.</summary>
        public List<T3DWindowSpec> Windows { get; } = new List<T3DWindowSpec>();

        /// <summary>The zones, in creation order. A surface refers to one by index.</summary>
        public List<T3DZoneSpec> Zones { get; } = new List<T3DZoneSpec>();

        /// <summary>The surfaces in the order they must be handed to TAS - each directly followed by its openings.</summary>
        public List<T3DSurfaceSpec> Surfaces { get; } = new List<T3DSurfaceSpec>();

        /// <summary>The shade surfaces.</summary>
        public List<T3DShadeSpec> Shades { get; } = new List<T3DShadeSpec>();

        /// <summary>What the plan could not turn into geometry, and what it noticed along the way.</summary>
        public T3DImportReport Report { get; } = new T3DImportReport();

        // Whether every panel carrying an aperture construction is external, per aperture construction. One answer for the whole construction,
        // so it is worked out once instead of once per aperture (which would rescan every panel for each aperture).
        internal Dictionary<System.Guid, bool> AllHostsExternal { get; } = new Dictionary<System.Guid, bool>();
    }

    /// <summary>One TAS <c>Element</c> (a building element definition).</summary>
    public class T3DElementSpec
    {
        /// <summary>Identity within the plan: the construction GUID plus the flags that make two uses of one construction different elements.</summary>
        public string Key { get; set; }
        public string Name { get; set; }
        public uint Colour { get; set; }

        /// <summary>The width [m] TAS offsets by when widths are on; 0 switches this element's width off.</summary>
        public double Width { get; set; }
        public bool Transparent { get; set; }
        public bool InternalShadows { get; set; }

        /// <summary>The TAS <c>BEType</c>, or -1 when SAM states none (the TAS default is left).</summary>
        public int BEType { get; set; } = -1;
        public bool Ground { get; set; }
        public bool Ghost { get; set; }
        public bool ZoneFloorArea { get; set; }

        /// <summary>The SAM construction GUID, kept on the element so the TAS side can be traced back to SAM.</summary>
        public string Description { get; set; }
    }

    /// <summary>One TAS <c>window</c> (an aperture definition).</summary>
    public class T3DWindowSpec
    {
        public string Key { get; set; }

        /// <summary>
        /// The window name. TAS appends <c>-pane</c> / <c>-frame</c> to it directly to name the two building
        /// elements it creates, so it is written with a trailing space and a <c>Windows: </c> / <c>Doors: </c>
        /// prefix: the elements are then named <c>Windows: SIM_EXT_GLZ -pane</c>, exactly the shared definition
        /// names the rest of SAM_Tas (<c>Query.BuildingElementName</c>) already produces and reads.
        /// </summary>
        public string Name { get; set; }

        /// <summary>The <c>openingType</c> argument of <c>AddWindow</c>: 0 window, 1 rooflight, 2 door.</summary>
        public int OpeningType { get; set; }

        /// <summary>The window's <c>positionType</c> (0 window, 1 rooflight, 2 door, 4 floor), or -1 to leave what <c>AddWindow</c> set.</summary>
        public int PositionType { get; set; } = -1;
        public uint Colour { get; set; }
        public double Height { get; set; }
        public double Width { get; set; }
        public double Level { get; set; }
        public bool Transparent { get; set; }
        public bool InternalShadows { get; set; }

        /// <summary>The frame width [m], or NaN to leave the TAS default.</summary>
        public double FrameWidth { get; set; } = double.NaN;

        /// <summary>The frame as a percentage of the opening [%], or NaN to leave the TAS default.</summary>
        public double FramePercent { get; set; } = double.NaN;

        /// <summary>The SAM aperture construction GUID.</summary>
        public string Description { get; set; }
    }

    /// <summary>One TAS <c>Zone</c>.</summary>
    public class T3DZoneSpec
    {
        public string Name { get; set; }

        /// <summary>The SAM space this zone is; written into the zone description (see <see cref="Query.ZoneDescription"/>).</summary>
        public System.Guid SpaceGuid { get; set; }
        public string Description { get; set; }

        /// <summary>The zone colour, or null to leave TAS's.</summary>
        public uint? Colour { get; set; }

        /// <summary>True for a SAM <c>ExternalSpace</c> - what is outside the building, not a conditioned zone.</summary>
        public bool External { get; set; }
    }

    /// <summary>Which <c>WrImportIDF</c> call a surface is made with.</summary>
    public enum T3DSurfaceKind
    {
        /// <summary><c>AddSurface(zone, element, reverse, false, ...)</c>: one zone, exposed.</summary>
        External,

        /// <summary><c>AddSurface</c> on a ground element: TAS makes it <c>tbdGround</c>.</summary>
        Ground,

        /// <summary><c>AddSurface(..., isAdiabatic: true, ...)</c>: one zone, <c>tbdNullLink</c>.</summary>
        Adiabatic,

        /// <summary><c>AddInternalSurface(zone, zone2, ...)</c>: one surface linked between two zones.</summary>
        Internal,

        /// <summary>
        /// One side of an adiabatic panel that separates two zones: <c>AddSurface(..., isAdiabatic: true, ...)</c>
        /// on this zone only. The panel contributes two of these, one per zone, which is how the gbXML route ends up
        /// too - both sides null-linked, no heat flow between the spaces.
        /// </summary>
        InternalAdiabaticSide
    }

    /// <summary>One surface the plan hands to TAS, with the openings that must follow it.</summary>
    public class T3DSurfaceSpec
    {
        public System.Guid PanelGuid { get; set; }
        public string PanelName { get; set; }
        public T3DSurfaceKind Kind { get; set; }

        /// <summary>Index into <see cref="T3DImportPlan.Zones"/>.</summary>
        public int Zone { get; set; }

        /// <summary>Index of the second zone for <see cref="T3DSurfaceKind.Internal"/>, otherwise -1.</summary>
        public int Zone2 { get; set; } = -1;
        public string ElementKey { get; set; }

        /// <summary><c>reverseElement</c>: the construction's layers run the other way round. Not derived from SAM today - always false.</summary>
        public bool ReverseElement { get; set; }

        /// <summary>The polygon, <c>double[3, n]</c>, normal out of <see cref="Zone"/> (into <see cref="Zone2"/> for an internal surface).</summary>
        public double[,] Coordinates { get; set; }

        /// <summary>Emitted straight after the surface, in order - <c>AddOpening</c> attaches to the surface added last.</summary>
        public List<T3DOpeningSpec> Openings { get; } = new List<T3DOpeningSpec>();
    }

    /// <summary>One SAM aperture as an opening on the surface it follows.</summary>
    public class T3DOpeningSpec
    {
        public System.Guid ApertureGuid { get; set; }
        public string WindowKey { get; set; }

        /// <summary>The polygon, <c>double[3, n]</c>, on the host surface's plane and wound the same way.</summary>
        public double[,] Coordinates { get; set; }
    }

    /// <summary>One shade panel handed to <c>AddShadeSurface</c>.</summary>
    public class T3DShadeSpec
    {
        public System.Guid PanelGuid { get; set; }
        public string PanelName { get; set; }
        public string ElementKey { get; set; }
        public bool ReverseElement { get; set; }
        public double[,] Coordinates { get; set; }
    }
}
