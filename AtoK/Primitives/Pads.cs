using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;

namespace ConvertToKicad
{
    public partial class ConvertPCBDoc
    {
        // class for pad objects
        class Pad : PCBObject //Object
        {
            public string Number { get; set; }
            public string Type { get; set; }
            string Shape { get; set; }
            double X { get; set; }
            double Y { get; set; }
            public double Rotation { get; set; }
            double SizeX { get; set; }
            double SizeY { get; set; }
            double Drill { get; set; }
            string Layer { get; set; }
            double Width { get; set; }
            double RRatio { get; set; }
            public double PasteMaskExpansion { get; set; }
            public double SolderMaskExpansion { get; set; }
            int Net { get; set; }
            string Net_name { get; set; }
            private readonly int Zone_connect;

            private Pad()
            {
                Number = "0";
                Type = "thru_hole";
                Shape = "circle";
                X = 0;
                Y = 0;
                Rotation = 0;
                SizeX = 0;
                SizeY = 0;
                Drill = 0;
                Layer = "";
                Width = 0;
                Net = 0;
                Net_name = "";
                RRatio = 0;
                PasteMaskExpansion = 0;
                SolderMaskExpansion = 0;
            }

            public Pad(string number, string type, string shape, double x, double y, double rotation, double sizex, double sizey, double drill, string layer, int net, double rratio)
            {
                if (number == "")
                    number = "0";
                if(number.Contains(" "))
                    Number = $"\"{number}\"";
                else
                    Number = number;

                Type = type;
                Shape = shape;
                RRatio = rratio;
                X = Math.Round(x, Precision);
                Y = Math.Round(y, Precision);
                Rotation = rotation;
                if (Rotation > 0 && Rotation < 1)
                    Rotation = 0; // TODO fix this frig
                SizeX = Math.Round(sizex, Precision);
                SizeY = Math.Round(sizey, Precision);
                Drill = drill;
                Layer = layer;
                Net = net;
                if (Net == -1)
                    Net = 0;
                Net_name = $"\"{NetsL[Net].Name}\"";
                // TODO should get this from rules
                Zone_connect = 1; // default to thermal connect
                Component = 0;
            }

            public Pad(double XSize, double YSize)
            {
                Number = "0";
                Type = "thru_hole";
                Shape = "circle";
                X = 0;
                Y = 0;
                Rotation = 0;
                SizeX = XSize;
                SizeY = YSize;
                Drill = 0;
                Layer = "";
                Width = 0;
                Net = 0;
                Net_name = "";
            }

            public override string ToString()
            {
                if (Shape == "roundrect")
                {
                    return $"    (pad {Number} {Type} {Shape} (at {X} {-Y} {Rotation}) (size {SizeX} {SizeY}) (drill {Drill}) (layers {Layer})\n" +
                            $"      (net {Net} {Net_name}) (roundrect_rratio {RRatio}) (zone_connect {Zone_connect}))\n";
                }
                else
                if (Shape == "octagonal")
                {
                    // make octagonal pad out of polygon
                    return DoOctagonalPad(Number, Type, X, Y, Rotation, SizeX, SizeY, Layer);
                }
                else
                {
                    string NET = (Net != 0) ? $"(net { Net} { Net_name})" : "";
                    string PME = (PasteMaskExpansion != 0) ? $"(solder_paste_margin {PasteMaskExpansion}) " : "";
                    string SME = (SolderMaskExpansion != 0) ? $"(solder_mask_margin {SolderMaskExpansion}) " : "";
                    string PAD = (PME != "") || (SME != "") || (NET != "")? "      " : "";
                    return $"    (pad {Number} {Type} {Shape} (at {X} {-Y} {Rotation}) (size {SizeX} {SizeY}) (drill {Drill}) (layers {Layer})\n"
                           + PAD + PME + SME + NET +
                            $" (zone_connect {Zone_connect}))\n";
                }
            }

            private string DoOctagonalPad(string Number, string Type, double X, double Y, double Rotation, double SizeX, double SizeY, string Layer)
            {
                // boy this turned out to be more complicated than I would have liked
                double Cx, Cy;

                Point2D[] Points = new Point2D[8];

                Cx = Math.Min(SizeX, SizeY) / 4;
                Cy = SizeY / 2;

                Points[0] = new Point2D(-SizeX/2, -(Cy-Cx));
                Points[1] = new Point2D(-SizeX/2+Cx, -Cy);
                Points[2] = new Point2D(SizeX / 2 - Cx, -Cy);
                Points[3] = new Point2D(SizeX / 2, -(Cy - Cx));
                Points[4] = new Point2D(SizeX / 2, Cy-Cx);
                Points[5] = new Point2D(SizeX / 2 - Cx, Cy);
                Points[6] = new Point2D(-SizeX / 2 + Cx, Cy);
                Points[7] = new Point2D(-SizeX / 2, (Cy - Cx));

                double PadSizeX = Points[3].X - Points[0].X;
                double PadSizeY = Points[4].Y - Points[3].Y;

                string hole = $"(drill {Drill})";

                if (Type == "smd")
                    hole = "";

                // make octagonal pad out of polygon
                StringBuilder ret = new StringBuilder("");
                ret.Append($"    (pad {Number} {Type} custom (at {X} {Y}  {Rotation}) (size {PadSizeX} {PadSizeY}) {hole} (layers {Layer})\n");
                ret.Append($"      (net {Net} {Net_name}) (zone_connect {Zone_connect})");
                ret.Append($"      (zone_connect {Zone_connect})\n"); // 0=none 1=thermal 2=solid get from rules
                ret.Append($"      (options (clearance outline) (anchor rect))\n");
                ret.Append($"      (primitives\n");
                ret.Append($"         (gr_poly (pts\n");
                ret.Append($"         (xy {Points[0].X} {-Points[0].Y})\n");
                ret.Append($"         (xy {Points[1].X} {-Points[1].Y})\n");
                ret.Append($"         (xy {Points[2].X} {-Points[2].Y})\n");
                ret.Append($"         (xy {Points[3].X} {-Points[3].Y})\n");
                ret.Append($"         (xy {Points[4].X} {-Points[4].Y})\n");
                ret.Append($"         (xy {Points[5].X} {-Points[5].Y})\n");
                ret.Append($"         (xy {Points[6].X} {-Points[6].Y})\n");
                ret.Append($"         (xy {Points[7].X} {-Points[7].Y})\n      )))\n    )\n");
                return ret.ToString();
            }

            public string ToString(double x, double y)
            {
                Point2D p = new Point2D(X - x, Y - y);

                CheckMinMax(p.X, p.Y);

                string NET = (Net != 0) ? $"(net { Net} { Net_name})" : "";
                string PME = (PasteMaskExpansion != 0) ? $"(solder_paste_margin {PasteMaskExpansion}) " : "";
                string SME = (SolderMaskExpansion != 0) ? $"(solder_mask_margin {SolderMaskExpansion}) " : "";
                string PAD = (PME != "") || (SME != "") || (NET != "") ? "      " : "";
                if (Shape == "roundrect")
                {
                    return $"    (pad {Number} {Type} {Shape} (at {Math.Round(X, Precision)} {-Math.Round(Y, Precision)} {Rotation}) (size {SizeX} {SizeY}) (drill {Drill}) (layers {Layer})\n"
                        + PAD + PME + SME + NET +
                        $" (roundrect_rratio {RRatio}) (zone_connect {Zone_connect}))\n";
                }
                else
                if (Shape == "octagonal")
                {
                    // make octagonal pad out of polygon
                    return DoOctagonalPad(Number, Type, p.X, -p.Y, Rotation, SizeX, SizeY, Layer);
                }
                else
                {
                    return $"    (pad {Number} {Type} {Shape} (at {p.X} {-p.Y} {Rotation}) (size {SizeX} {SizeY}) (drill {Drill}) (layers {Layer})\n"
                        + PME + SME +
                        $"      (net {Net} {Net_name}) (zone_connect {Zone_connect}))\n";
                }
            }

            public override string ToString(double x, double y, double ModuleRotation)
            {
                // point relative to module's centre x,y
                Point2D p = new Point2D(X - x, Y - y);

                p.Rotate(-ModuleRotation);

                //CheckMinMax(p.X, p.Y);
                string NET = (Net != 0) ? $"(net { Net} { Net_name})" : "";
                string PME = (PasteMaskExpansion != 0) ? $"(solder_paste_margin {PasteMaskExpansion}) " : "";
                string SME = (SolderMaskExpansion != 0) ? $"(solder_mask_margin {SolderMaskExpansion}) " : "";
                string PAD = (PME != "") || (SME != "") || (NET != "") ? "      " : "";
                if (Shape == "roundrect")
                {
                    return $"    (pad {Number} {Type} {Shape} (at {Math.Round(p.X, Precision)} {-Math.Round(p.Y, Precision)} {Rotation}) (size {SizeX} {SizeY}) (drill {Drill}) (layers {Layer})\n"
                         + PAD + PME + SME + NET +
                           $" (roundrect_rratio {RRatio}) (zone_connect {Zone_connect}))\n";
                }
                if (Shape == "octagonal")
                {
                    // make octagonal pad out of polygon
                    return DoOctagonalPad(Number, Type, p.X, -p.Y, Rotation, SizeX, SizeY, Layer);
                }
                else
                {
                    return $"    (pad {Number} {Type} {Shape} (at {p.X} {-p.Y} {Rotation}) (size {SizeX} {SizeY}) (drill {Drill}) (layers {Layer})\n"
                         + PAD + PME + SME + NET +
                           $" (zone_connect {Zone_connect}))\n";
                }
            }

            public string ToModuleString(double x, double y, double ModuleRotation)
            {
                // point relative to modules centre
                Point2D p = new Point2D(X - x, Y - y);

                p.Rotate(-ModuleRotation);

                string NET = (Net != 0) ? $"(net { Net} { Net_name})" : "";
                string PME = (PasteMaskExpansion != 0) ? $"(solder_paste_margin {PasteMaskExpansion}) " : "";
                string SME = (SolderMaskExpansion != 0) ? $"(solder_mask_margin {SolderMaskExpansion}) " : "";
                string PAD = (PME != "") || (SME != "") || (NET != "") ? "      " : "";

                if (Shape != "octagonal")
                {
                    return $"    (pad {Number} {Type} {Shape} (at {p.X} {-p.Y} {Rotation + ModuleRotation}) (size {SizeX} {SizeY}) (drill {Drill}) (layers {Layer})\n"
                         + PAD + PME + SME + NET +
                           $" (zone_connect {Zone_connect}))\n";
                }
                else
                {
                    // make octagonal pad out of polygon
                    return DoOctagonalPad(Number, Type, p.X, -p.Y, Rotation + ModuleRotation, SizeX, SizeY, Layer);
                }
            }
        }

        // class for the pads document entry in the pcbdoc file
        class Pads : PcbDocEntry
        {
            // this struct for Altium versions up and including Summer 09
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            unsafe struct PadStruct
            {
                public fixed byte U0[19];       //  0  19 ???
                public int Offset;              //  19 4 offset to pad layer info add 559 get start of layer0 shape
                public byte Layer;              //  23 1 layer
                public short U1;                //  24 2 Flags
                                                //       bit 4 0=locked
                                                //       bit 5 force tenting on top
                                                //       bit 6 force tenting on bottom
                                                //       bit 7 Test point top
                                                //       bit 8 Test point bottom
                public short Net;               //  26 2 net
                public short U2;                //  28 2 ??
                public short Component;         //  30 2 component
                public short U3;                //  32 2 ??
                public short U4;                //  34 2 ??
                public int X;                   //  36 4 X
                public int Y;                   //  40 4 Y
                public int XSize;               //  44 4 XSize
                public int YSize;               //  48 4 YSize
                public int MidXSize;            //  52 4 mid X size
                public int MidYSize;            //  56 4 mid Y size
                public int BotXSize;            //  60 4 bottom X size
                public int BotYSize;            //  64 4 bottom Y size
                public int HoleSize;            //  68 4 holesize
                public byte TopShape;           //  72 1 top shape
                public byte MidShape;           //  73 1 middle shape
                public byte BotShape;           //  74 1 bottom shape
                public double Rotation;         //  75 8 rotation
                public byte Plated;             //  83 1 plated
                public byte U7;                 //  84 1 ???
                public byte PadMode;            //  85 1 PadMode
                public fixed byte U8[5];        //  86 5 ???
                public int CCW;                 //  91 4 CCW
                public byte CEN;                //  95 1 CEN
                public byte U9;                 //  96 1 ???
                public int CAG;                 //  97 4 CAG
                public int CPR;                 // 101 4 CPR
                public int CPC;                 // 105 4 CPC
                public int PasteMaskExpansion;  // 109 4 paste mask expansion  (CPE)
                public int SolderMaskExpansion; // 113 4 solder mask expansion (CSE)
                public byte CPL;                // 117 1 CPL
                public fixed byte U10[6];       // 118 6 ???
                public byte UsePasteMaskRules;  // 124 1 use paste expansion rules (CPEV)
                public byte UseSolderMaskRules; // 125 1 use solder mask expansion rules (CSEV)
                public fixed byte U11[3];       // 126 7 ??? 
                public int HoleRotation;        // 129 4 hole rotation
                public short JumperID;          // 133 2 jumper ID
                public fixed byte U12[6];       // 135 6 ???

                public fixed int MidLayerXSize[29]; // 141 29*4 Midlayers 2-30
                public fixed int MidLayerYSixe[29]; // 257 29*4 MidLayers 2-30
                public fixed byte U13[29];          // 373 29*1 Midlayers 2-30 unknown
                public fixed byte U14[673 - 402];   // 402 271
                public fixed byte PadShapes[32];    // 673 Padshapes on 32 layers top bottom and 30 inner layers
                public fixed byte RRatios[32];      // 705 RRatios for top, middle 30, and bottom layers
                public fixed byte U15[32];          // 743 ???
                public fixed byte U16[1];           // 737 ???
                public fixed byte U17[1];           // 738 ???
            }

            // pad layer info position in pad record = Offset in previous structure
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            unsafe struct PadLayers
            {
                public fixed byte U0[27];            // not known yet
                public fixed int  MidLayerXSize[29]; //  27 29*4 Midlayers 2-30
                public fixed int  MidLayerYSixe[29]; // 143 29*4 MidLayers 2-30
                public fixed byte U13[29];           // 259 29*1 Midlayers 2-30 unknown
                public fixed byte U14[1];            // 288 ???
                public fixed byte HoleShape[1];      // 289 Hole shape 0=Round 1 = Square 2 = slot
                public fixed byte U15[559 - 290];    // 290 269
                public fixed byte PadShapes[32];     // 559 Padshapes on 32 layers top bottom and 30 inner layers
                public fixed byte RRatios[32];       // 591 RRatios for top, middle 30, and bottom layers
                public fixed byte U16[32];           // 650 ???
                public fixed byte U17[1];            // 665 ???
                public fixed byte U18[1];            // 666 ???
            }

            // just to be awkward records not same size varies with name of pad
            // entry header looks like 02 xx xx xx xx yy where yy = (byte)(xxxxxxxx) e.g. 02 03 00 00 00 02
            public Pads(string filename, string cmfilename, string record, Type type, int offset) : base(filename, cmfilename, record, type, offset)
            {
                Binary_size = 1;
            }

            private static void AddText(FileStream fs, string value)
            {
                byte[] info = new UTF8Encoding(true).GetBytes(value);
                fs.Write(info, 0, info.Length);
            }

            public override bool ProcessBinaryFile(byte[] data)
            {
                bool GenerateTxtFile = true; // set to true to generate text version of the file
                FileStream TextFile = null;
                StartTimer();
                if (Binary_size == 0)
                    return false;

                if (GenerateTxtFile)
                {
                    if (ExtractFiles)
                        TextFile = File.Open("Data.txt", FileMode.OpenOrCreate);
                }
                try
                {
                    using (MemoryStream ms = new MemoryStream(data))
                    {
                        UInt32 pos = 0;
                        uint size = (uint)ms.Length;

                        BinaryReader br = new BinaryReader(ms, System.Text.Encoding.UTF8);
                        ms.Seek(pos, SeekOrigin.Begin);
                        List<UInt32> Starts = new List<UInt32>();
                        // look for record starts
                        {
                            Stopwatch timer = new Stopwatch();
                            timer.Start();
                            // signature is
                            // byte 02
                            // int32 length
                            // byte strlen = length - 1
                            // string strlen ascci chars
                            // bytes 01 00 00 00
                            while (pos < size)
                            {
                                ms.Seek(pos, SeekOrigin.Begin);
                                byte recordtype = br.ReadByte(); // should be 2
                                if (recordtype == 2)
                                {
                                    // possible start
                                    UInt32 nextnamelength = br.ReadUInt32();
                                    if (nextnamelength < 256)
                                    {
                                        uint l = br.ReadByte(); // string length
                                        if (l == nextnamelength - 1)
                                        {
                                            // everything ok so far
                                            // now check bytes in string are ASCII
                                            bool found = true;
                                            for (int i = 0; i < l; i++)
                                            {
                                                byte c = br.ReadByte();
                                                if (c < 0x20 || c > 0x7F)
                                                    found = false;
                                            }
                                            if (br.ReadByte() != 1)
                                                found = false;
                                            if (br.ReadByte() != 0)
                                                found = false;
                                            if (br.ReadByte() != 0)
                                                found = false;
                                            if (br.ReadByte() != 0)
                                                found = false;
                                            if (br.ReadByte() != 0)
                                                found = false;
                                            if (found)
                                            {
                                               // OutputString($"Found header at {pos:x08}");
                                                Starts.Add(pos);
                                            }
                                        }
                                    }
                                }
                                pos = pos + 1;
                            }
                        }
                        pos = 0;
                        int index = -1;
                        Int16 Component;
                        byte[] r = br.ReadBytes(2);

                        try
                        {
                            UInt32 Longest = 0;
                            UInt32 len;
                            // find the length of the logest pad record
                            foreach (var p in Starts)
                            {
                                index++;
                                if (index < Starts.Count - 1)
                                {
                                    len = Starts[index + 1] - Starts[index];
                                }
                                else
                                    len = size - Starts[index];
                                if (len > Longest)
                                    Longest = len;
                            }
                            index = -1;
                            if (GenerateTxtFile && ExtractFiles)
                            {
                                AddText(TextFile, $"Altium Version {VersionString}\n");
                                // generate headers for .txt file
                                string Header1 = "Pos                 ";
                                for (int i = 0; i < Longest; i++)
                                    Header1 += $"{(i / 100),-3:D2}";
                                string Header2 = "                    ";
                                for (int i = 0; i < Longest; i++)
                                    Header2 += $"{(i % 100),-3:D2}";
                                string Header3 = "--------------------";
                                for (int i = 0; i < Longest; i++)
                                    Header3 += $"---";
                                if (ExtractFiles)
                                {
                                    AddText(TextFile, Header1 + "\n");
                                    AddText(TextFile, Header2 + "\n");
                                    AddText(TextFile, Header3 + "\n");
                                }
                            }
                            foreach (var p in Starts)
                            {
                                pos = p;
                                //OutputString($"Processing pad record at {pos:x08}");
                                index++;
                                base.ProcessLine();
                                ms.Seek(p, SeekOrigin.Begin);
                                byte recordtype = br.ReadByte(); // should be 2
                                UInt32 next = br.ReadUInt32();
                                uint l = br.ReadByte(); // string length
                                                        //pos =  (uint)ms.Position;
                                string name;
                                if (l != 0)
                                    name = new string(br.ReadChars((int)l));
                                else
                                    name = "";
                                pos = (uint)ms.Position;
                                // find out how many bytes to read
                                if (index < Starts.Count - 1)
                                {
                                    len = Starts[index + 1] - Starts[index];
                                }
                                else
                                    len = size - Starts[index];

                                // we are now pointing to the pad record
                                //
                                Layers Layer;
                                byte shape;
                                bool plated;
                                double X, Y, XSize, YSize, HoleSize; //, MIDXSize, MIDYSize, BottomXSize, BottomYSize;
                                double Rotation = 0;
                                double RRatio = 0;
                                Int16 Net;
                                Int16 JumperID;
                                byte[] bytes = br.ReadBytes((int)len - 6); // get record after header stuff

                                if (GenerateTxtFile)
                                {
                                    if (p == 0)
                                        r = bytes; // get reference bytes
                                    StringBuilder text = new StringBuilder("");
                                    int count = 0;
                                    int CompareLimit = Math.Min(r.Length, bytes.Length);
                                    foreach (var c in bytes)
                                    {
                                        if (count < CompareLimit && r[count] != bytes[count])
                                            text.Append($"|{c:X2}");
                                        else
                                            text.Append($" {c:X2}");
                                        count++;
                                    }
                                    if (ExtractFiles)
                                        AddText(TextFile, $"{pos:X8} " + $"{len - name.Length,4} {name,4} {text.ToString()}\n");
                                }
                                PadStruct pad = ByteArrayToStructure<PadStruct>(bytes);
                                ms.Seek(pos + pad.Offset, SeekOrigin.Begin);
                                int LayersSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(PadLayers));

                                byte[] layerbytes = br.ReadBytes(LayersSize); // get layers data
                                PadLayers PadLayers = ByteArrayToStructure<PadLayers>(layerbytes);
                                Layer = (Layers)pad.Layer;
                                Net = pad.Net;
                                Net++;
                                string n = NetsL[Net].Name;
                                Component = pad.Component;
                                if (Component != -1 && Component != 0)
                                    X = 0;
                                X = ToMM(pad.X) - originX;
                                Y = ToMM(pad.Y) - originY;
                                XSize = ToMM(pad.XSize);
                                YSize = ToMM(pad.YSize);
                                HoleSize = ToMM(pad.HoleSize);
                                unsafe
                                {
                                    shape = pad.TopShape;
                                    if (len > 160 && shape == 1) // TODO this can't be right
                                    {
                                        shape = PadLayers.PadShapes[0];
                                        RRatio = (double)PadLayers.RRatios[0] / 200;
                                    }
                                    else
                                    {
                                        RRatio = 0;
                                    }
                                }
                                if (shape > 5)
                                    shape = 5;
                                string[] shapes = { "circle", "circle", "rect", "octagonal", "oval", "roundrect" };
                                if (shapes[shape] == "circle" && XSize != YSize)
                                    shape = 4; // oval pad
                                Rotation = pad.Rotation;
                                plated = pad.Plated != 0;
                                JumperID = pad.JumperID;
                                bool InComponent = Component != -1;

                                string type;
                                if (Layer == Layers.Multi_Layer)
                                {
                                    if (plated)
                                    {
                                        type = "thru_hole";
                                    }
                                    else
                                    {
                                        type = "np_thru_hole";
                                    //    name = "\"\"";
                                    }
                                    if (HoleSize < 0.01)
                                        OutputError($"Zero size hole through hole pad at {X} {Y}");
                                }
                                else
                                    type = "smd";
                                string layer = Brd.GetLayer(Layer);
                                if (!Brd.IsCopperLayer(Layer))
                                {
                                    OutputError($"Pad on non copper layer = {Layer} at {X} {-Y}");
                                    // TODO generate circular polygon instead
                                    continue;
                                }
                                if (layer == "Margin") // TODO sort this keepout layer malarky
                                    layer = "Dwgs.User";
                                if (type == "smd")
                                    layer += layer == "F.Cu" ? " F.Mask F.Paste" : (layer == "B.Cu") ? " B.Mask B.Paste" : "";
                                else
                                    layer += " *.Mask";

                                if (XSize < HoleSize | YSize < HoleSize)
                                {
                                    XSize = YSize = HoleSize;
                                }

                                Pad Pad = new Pad(name, type, shapes[shape], X, Y, Rotation, XSize, YSize, HoleSize, layer, Net, RRatio)
                                {
                                    Component = Component
                                };
                                if (pad.UsePasteMaskRules == 1)
                                    Pad.PasteMaskExpansion = GetRuleValue(Pad, "PasteMaskExpansion", "PasteMaskExpansion");
                                else
                                    Pad.PasteMaskExpansion = ToMM(pad.PasteMaskExpansion);
                                if (pad.UseSolderMaskRules == 1)
                                    Pad.SolderMaskExpansion = GetRuleValue(Pad, "SolderMaskExpansion", "SolderMaskExpansion");
                                else
                                    Pad.SolderMaskExpansion = ToMM(pad.SolderMaskExpansion);


                                if (!InComponent)
                                {
                                    PadsL.Add(Pad);
                                    // free pads not allowed (at present) in PcbNew so generate a single pad module
                                    Module M = new Module($"FreePad{ModulesL.Count}", X, Y, XSize, YSize, Pad);
                                    ModulesL.Add(M);

                                }
                                else
                                {
                                    try
                                    {
                                        ModulesL[Component].Pads.Add(Pad);
                                    }
                                    catch (Exception Ex)
                                    {
                                        CheckThreadAbort(Ex, $"At position {pos} in Pads file");
                                    }
                                }
                            }
                        }
                        catch (Exception Ex)
                        {
                            CheckThreadAbort(Ex);
                        }
                        if(GenerateTxtFile && TextFile != null)
                            TextFile.Close();
                    }
                }
                catch (Exception Ex)
                {
                    CheckThreadAbort(Ex);
                }

                return true;
            }
        }
    }
}
