using System;
using System.Collections.Generic;

namespace VMFParser
{
    class Program
    {
        static void Main(string[] args)
        {
            //TODO change to debug.writeline for performance gains
            
            VMF vmf = new VMF(@"C:\Program Files (x86)\Steam\steamapps\common\SourceSDK_Content\tf\mapsrc\Koth\koth_tropic_a1.vmf");
            Console.WriteLine(vmf);
            string test = vmf.ToString();
            //List<Solid> solids = vmf.GetSolids();

            //foreach (var solid in solids)
            //{
            //    Console.WriteLine(solid);
            //}

            Console.Read();
        }
    }
}
