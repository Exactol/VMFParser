using System;
using System.Collections.Generic;

namespace VMFParser
{
    class Program
    {
        static void Main(string[] args)
        {
            //TODO change to debug.writeline for performance gains
            
            VMF vmf = new VMF(@"C:\Program Files (x86)\Steam\steamapps\common\SourceSDK_Content\tf\mapsrc\Koth\koth_tropic_a1.vmf", true);
            //Console.WriteLine(vmf);

            Console.WriteLine(vmf["world"]);

            Console.Read();
        }
    }
}
