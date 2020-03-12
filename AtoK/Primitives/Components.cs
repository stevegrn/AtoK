using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ConvertToKicad
{
    public partial class ConvertPCBDoc
    {
        // class for the components document entry in the pcbdoc file
        class Components : PcbDocEntry
        {
            public Components(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }

            public override bool ProcessLine(string line)
            {
                base.ProcessLine();
                Module Mod = new Module(line);
                ModulesL.Add(Mod);
                return true;
            }

            public override void FinishOff()
            {
                base.FinishOff();
            }
        }
    }
}
