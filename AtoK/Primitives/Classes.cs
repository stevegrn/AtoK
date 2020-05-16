using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ConvertToKicad
{
    public partial class ConvertPCBDoc
    {
        // class for the net classes document entry in the pcbdoc file
        class Classes : PcbDocEntry
        {
            public Classes(string filename, string cmfilename, string record, Type type, int offset) : base(filename, cmfilename, record, type, offset)
            {
            }

            public override bool ProcessLine(string line)
            {
                base.ProcessLine();
                string[] split;
                Int32 Kind = -1;
                string param;
                bool SuperClass = false;

                if ((param = GetString(line, "|KIND=")) != "")
                {
                    try
                    {
                        Kind = Convert.ToInt32(param);
                    }
                    catch (Exception Ex)
                    {
                        CheckThreadAbort(Ex);
                        Kind = 0;
                    };
                }
                if ((param = GetString(line, "|SUPERCLASS=")) != "")
                {
                    SuperClass = param == "TRUE";
                }


                if (Kind == 0 && SuperClass == false)
                {
                    string[] words = line.Split('|');
                    string name = GetString(line, "|NAME=");

                    net_classes.Append($"  (net_class \"{name}\"  \"{name}\"\n    (clearance 0.127)\n    (trace_width 0.254)\n    (via_dia 0.889)\n    (via_drill 0.635)\n    (uvia_dia 0.508)\n    (uvia_drill 0.127)\n");
                    for (int c = 10; c < words.Length - 1; c++)
                    {
                        split = words[c].Split('=');
                        net_classes.Append($"    (add_net \"{ConvertIfNegated(ToLiteral(split[1]))}\")\n");
                    }
                    net_classes.Append(" )\n");
                }
                return true;
            }
        }
    }
}
