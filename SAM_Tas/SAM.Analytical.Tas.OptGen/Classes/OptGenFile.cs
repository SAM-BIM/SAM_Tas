﻿namespace SAM.Analytical.Tas.OptGen
{
    public abstract class OptGenFile : OptGenObject, IOptGenFile
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
