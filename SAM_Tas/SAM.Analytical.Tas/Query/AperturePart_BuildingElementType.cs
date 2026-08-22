// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        /// <summary>
        /// Which half of an aperture a TBD building element is, from its <c>BEType</c>.
        /// <para>
        /// <b>Why not <see cref="AperturePart(int)"/>.</b> That overload answers <c>Frame</c> for
        /// <c>BEType</c> 14, which is TAS's DOOR type - a mapping the aperture-type write relies on and which
        /// is fine where it is used, but wrong as a statement about which half of an opening a physical surface
        /// is. A door leaf is the glazed half of its opening, not its frame. Reading it the other way on the
        /// import would classify a door's own surface as the frame and leave the opening with no pane, which
        /// then takes the pane's opening controls and result mapping with it.
        /// </para>
        /// <para>
        /// So this reads the <c>TBD.BuildingElementType</c> enumeration directly and answers only where TAS is
        /// unambiguous: glazing, a rooflight and a door leaf are PANES; a frame element is a FRAME. Anything
        /// else - including a curtain wall or a vehicle door, which are openings TAS models differently -
        /// answers <c>Undefined</c>, and the caller falls back to the naming convention rather than being
        /// handed a guess.
        /// </para>
        /// </summary>
        public static AperturePart AperturePart_BuildingElementType(TBD.buildingElement buildingElement)
        {
            if (buildingElement == null)
            {
                return Analytical.AperturePart.Undefined;
            }

            switch ((TBD.BuildingElementType)buildingElement.BEType)
            {
                case TBD.BuildingElementType.GLAZING:
                case TBD.BuildingElementType.ROOFLIGHT:
                case TBD.BuildingElementType.DOORELEMENT:
                    return Analytical.AperturePart.Pane;

                case TBD.BuildingElementType.FRAMEELEMENT:
                    return Analytical.AperturePart.Frame;
            }

            return Analytical.AperturePart.Undefined;
        }
    }
}
