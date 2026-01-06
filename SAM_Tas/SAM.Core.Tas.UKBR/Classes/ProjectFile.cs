// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Xml.Linq;

namespace SAM.Core.Tas.UKBR
{
    public class ProjectFile : UKBRElement
    {
        public override string UKBRName => "ProjectFile";

        public ProjectFile(XElement xElement)
            : base(xElement)
        {

        }

        public int Modified
        {
            get
            {
                return Query.Value<int>(xElement?.Attribute("Modified"));
            }
        }

        public string FilePath
        {
            get
            {
                return Query.Value<string>(xElement?.Attribute("FilePath"));
            }
        }
    }
}
