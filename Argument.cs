using System;
using System.Diagnostics;

namespace CIMImport
{
    public class Argument
    {
        public static void Parse(string[] args, ref string cimfilepath, ref string coordinatefileath) //, ref string topology, ref string model)
        {
            if (args.Length == 0)
            {
                Console.WriteLine(GetUsageHelp());
                System.Environment.Exit(1);
            }

            // process args
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "-txt_path":
                        try { cimfilepath = args[++i]; }
                        catch (Exception e) { }
                        break;

                    case "-coor_path":
                        try { coordinatefileath = args[++i]; }
                        catch (Exception e) { }
                        break;
                   
                    default:
                        GetUsageHelp();
                        break;
                }
            }
        }

        public static string GetUsageHelp()
        {
            return "Usage: " + Process.GetCurrentProcess().ProcessName + " -txt_path <(string) cim file path>";
        }
    }
}
