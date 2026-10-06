using SAM.Core.Tas;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SAM.Analytical.Tas.DirectT3D.Validation
{
    /// <summary>The outcome of one direct SAM -> T3D -> TBD run over a model.</summary>
    public sealed class DirectRunResult
    {
        public T3DImportReport Report;
        public TbdSnapshot Tbd;
        public string Path_T3D;
        public string Path_TBD;
        public bool Converted;
        public bool Exported;
        public double Milliseconds_Convert;
        public double Milliseconds_Export;
    }

    public static class DirectRun
    {
        /// <summary>
        /// Converts the model through the direct route into a fresh T3D, saves it, exports it to a TBD through
        /// TAS's own T3D -> TBD export (the same call the workflow makes) and reads the TBD back with the
        /// independent snapshot reader.
        /// </summary>
        public static DirectRunResult Run(AnalyticalModel analyticalModel, ToT3DOptions options, string directory, string name)
        {
            Directory.CreateDirectory(directory);

            DirectRunResult result = new DirectRunResult
            {
                Path_T3D = Path.Combine(directory, name + ".t3d"),
                Path_TBD = Path.Combine(directory, name + ".tbd")
            };

            if (File.Exists(result.Path_T3D)) File.Delete(result.Path_T3D);
            if (File.Exists(result.Path_TBD)) File.Delete(result.Path_TBD);

            using (SAMT3DDocument sAMT3DDocument = new SAMT3DDocument())
            {
                TAS3D.T3DDocument t3DDocument = sAMT3DDocument.T3DDocument;

                System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
                result.Converted = analyticalModel.ToT3D(t3DDocument, options, out result.Report);
                result.Milliseconds_Convert = stopwatch.Elapsed.TotalMilliseconds;

                if (result.Converted)
                {
                    t3DDocument.Save(result.Path_T3D);

                    stopwatch.Restart();
                    result.Exported = Convert.ToTBD(t3DDocument, result.Path_TBD, 1, 365, 15, true, false);
                    result.Milliseconds_Export = stopwatch.Elapsed.TotalMilliseconds;
                }
            }

            if (result.Exported)
            {
                result.Tbd = TbdSnapshot.Read(result.Path_TBD);
            }

            return result;
        }
    }
}
