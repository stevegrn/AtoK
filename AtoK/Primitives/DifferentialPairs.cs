using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ConvertToKicad
{
    public partial class ConvertPCBDoc
    {
        // class for the differential pairs document entry in the pcbdoc file
        class DifferentialPairs : PcbDocEntry
        {
            public DifferentialPairs(string filename, string cmfilename, string record, Type type, int offset) : base(filename, cmfilename, record, type, offset)
            {
                StartTimer();
            }

            public override bool ProcessLine(string line)
            {
                base.ProcessLine();
                string NetPlus = GetString(line, "POSITIVENETNAME=");
                string NetMinus = GetString(line, "NEGATIVENETNAME=");
                foreach (var net in NetsL)
                {
                    if (net.Name == NetPlus)
                    {
                        // rename net to PCBNew format
                        net.Name = net.Name.Replace("_P", "+");
                        break;
                    }
                }
                foreach (var net in NetsL)
                {
                    if (net.Name == NetMinus)
                    {
                        net.Name = net.Name.Replace("_N", "-");
                        break;
                    }
                }
                return true;
            }
        }

    }
}
