using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VMFParser
{
    class OBJ
    {
        private StreamWriter sw;

        OBJ()
        {
            
        }

        private void WriteVertex(Vertex vert)
        {
            sw.Write("v " + vert);
        }
    }
}
