// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Linq;
using System.Xml.Linq;

namespace SAM.Core.Tas.UKBR
{
    public class Zones : UKBRElements<Zone>
    {
        public override string UKBRName => "Zones";

        public Zones(XElement xElement)
            : base(xElement)
        {

        }

        public Zone Zone(string name)
        {
            return this.ToList().Find(x => x.Name == name);
        }

        public Zone Zone(Guid guid)
        {
            return this.ToList().Find(x => x.GUID == guid);
        }
    }
}
