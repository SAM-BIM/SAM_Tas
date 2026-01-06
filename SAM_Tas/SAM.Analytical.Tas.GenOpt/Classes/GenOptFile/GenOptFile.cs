// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas.GenOpt
{
    public abstract class GenOptFile : GenOptObject, IGenOptFile
    {
        public bool Save(string path)
        {
            if(string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            if(!System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(path)))
            {
                return false;
            }

            string text = Text;
            if(text == null)
            {
                text = string.Empty;
            }

            System.IO.File.WriteAllText(path, text);
            return true;
        }
    }
}
