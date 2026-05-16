// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Tas;
using System.IO;
using TPD;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Modify
    {
        public static bool Simulate(string path_TPD, int startHour, int endHour)
        {
            if(string.IsNullOrWhiteSpace(path_TPD) || !File.Exists(path_TPD))
            {
                return false;
            }

            using (SAMTPDDocument sAMTPDDocument = new SAMTPDDocument(path_TPD))
            {
                TPDDoc tPDDoc = sAMTPDDocument.TPDDocument;

                if (tPDDoc?.EnergyCentre != null)
                {
                    tPDDoc.Simulate(startHour + 1, endHour + 1, 0);
                    tPDDoc.Save();
                }
            }


            return true;
        }

    }
}