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

                    case "probe-windows":
                        return Probes.Windows(args);

                    case "synthetic":
                        return SyntheticValidation.Run(args[1]);

                    case "compare":
                        return TbdCompare.Run(args);

                    case "inspect":
                        return Inspect.Run(args);

                    case "workflow":
                        return WorkflowRun.Run(args);

                    default:
                        Console.WriteLine("modes: dump <tbd> | probe <outDir> | synthetic <outDir> | inspect <model.sam> [out.txt] | compare <gbxml.tbd> <direct.tbd> <outPrefix> | workflow <model.sam> <outDir> <gbxml|direct> [simulate] [widths] [name=<stem>]");
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
