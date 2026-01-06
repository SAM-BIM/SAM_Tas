// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Xml.Linq;

namespace SAM.Core.Tas.UKBR
{
    public class ZoneGUID : UKBRElement
    {
        public override string UKBRName => "ZoneGUID";

        public ZoneGUID(XElement xElement)
            : base(xElement)
        {

        }

        public Guid GUID
        {
            get
            {
                if(string.IsNullOrWhiteSpace(xElement?.Value))
                {
                    return Guid.Empty;
                }
                
                return new Guid(xElement.Value);
            }
        }
    }
}
