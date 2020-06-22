using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ConvertToKicad
{
    public partial class ConvertPCBDoc
    {
        // class for net objects
        class Net : PCBObject
        {
            private readonly int Number;
            public string Name { get; set; }

            Net()
            {
                Number = 0;
                Name = "";
            }

            public Net(int number, string name)
            {
                Number = number;
                Name = ConvertIfNegated(name);
            }

            public override string ToString()
            {
                if (Number == 0)
                    return "  (net 0 \"\")\n";
                return $"  (net {Number} \"{Name}\")\n";
            }
        }

        // class for the nets document entry in the pcbdoc file
        class Nets : PcbDocEntry
        {
            static int net_no;
            public static string GetNetName(int Net)
            {
                return $"\"{NetsL[Net].Name}\"";
            }

            public Nets(string filename, string cmfilename, string record, Type type, int offset) : base(filename, cmfilename, record, type, offset)
            {
                net_no = 0;
            }

            public override bool ProcessLine(string line)
            {
                base.ProcessLine();
                string Net_name = GetString(line, "|NAME=");
                byte[] bytes = Encoding.ASCII.GetBytes(Net_name);
                Net net = new Net(net_no + 1, Net_name);

                NetsL.Add(net);
                net_no++;

                return true;
            }

            public override void FinishOff()
            {
                base.FinishOff();
            }
        }
    }
}
