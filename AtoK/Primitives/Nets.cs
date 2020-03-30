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
        class Net : Object
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

                // now go through the nets looking for differential pairs and convert to KiCad format
                // i.e. ending in + and - rather than _N and _P
                List<Net> pos = new List<Net>();
                List<Net> neg = new List<Net>();
                foreach (var net in NetsL)
                {
                    if (net.Name.Length > 2)
                    {
                        string trailing = net.Name.Substring(net.Name.Length - 2, 2);
                        if (trailing == "_P")
                        {
                            // potential pair candidate
                            pos.Add(net);
                        }
                        else
                        if (trailing == "_N")
                        {
                            // potential pair candidate
                            neg.Add(net);
                        }
                    }
                }
                // find pairs and rename
                foreach (var pnet in pos)
                {
                    foreach (var nnet in neg)
                    {
                        if (pnet.Name.Substring(0, pnet.Name.Length - 2) == nnet.Name.Substring(0, nnet.Name.Length - 2))
                        {
                            // we've got a differential pair
                            for (var i = 0; i < NetsL.Count; i++)
                            {
                                if (NetsL[i].Name == pnet.Name)
                                    NetsL[i].Name = NetsL[i].Name.Substring(0, nnet.Name.Length - 2) + "+";
                                if (NetsL[i].Name == nnet.Name)
                                    NetsL[i].Name = NetsL[i].Name.Substring(0, nnet.Name.Length - 2) + "-";
                            }
                        }
                    }
                }
            }
        }

    }
}
