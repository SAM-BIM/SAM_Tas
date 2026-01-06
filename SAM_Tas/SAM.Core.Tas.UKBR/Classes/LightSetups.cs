// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Linq;
using System.Xml.Linq;

namespace SAM.Core.Tas.UKBR
{
    public class LightSetups : UKBRElements<LightSetup>
    {
        public override string UKBRName => "LightSetups";

        public LightSetups(XElement xElement)
            : base(xElement)
        {

        }

        public LightSetup LightSetup(string name)
        {
            return this.ToList().Find(x => x.Name == name);
        }
    }
}
