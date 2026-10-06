using System;

namespace SAM.Analytical.Tas.DirectT3D.Validation
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            string mode = args.Length > 0 ? args[0] : string.Empty;
            try
            {
                switch (mode)
                {
                    case "dump":
                        Console.Write(TbdSnapshot.Read(args[1]).ToText());
                        return 0;

                    case "probe":
                        return Probes.Run(args);

                    case "synthetic":
                        return SyntheticValidation.Run(args[1]);

                    default:
                        Console.WriteLine("modes: dump <tbd> | probe <outDir> | synthetic <outDir>");
                        return 2;
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("ERROR: " + exception);
                return 1;
            }
        }
    }
}
