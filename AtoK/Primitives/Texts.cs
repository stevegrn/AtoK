using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ConvertToKicad
{
    public partial class ConvertPCBDoc
    {
        // add \ to any illegal characters (e.g. ") in the string
        static string ToLiteral(string input)
        {
            StringBuilder literal = new StringBuilder(input.Length + 2);
            foreach (var c in input)
            {
                switch (c)
                {
                    case '\'': literal.Append(@"\'"); break;
                    case '\"': literal.Append(@"\"""); break;
                    //    case '\\': literal.Append(@"\\"); break;
                    case '\0': literal.Append(@"\0"); break;
                    case '\a': literal.Append(@"\a"); break;
                    case '\b': literal.Append(@"\b"); break;
                    case '\f': literal.Append(@"\f"); break;
                    case '\n': literal.Append(@"\n"); break;
                    case '\r': literal.Append(@"\r"); break;
                    case '\t': literal.Append(@"\t"); break;
                    case '\v': literal.Append(@"\v"); break;
                    default:
                        // ASCII printable character
                        if (c >= 0x20 && c <= 0x7e)
                        {
                            literal.Append(c);
                            // As UTF16 escaped character
                        }
                        else
                        {
                            literal.Append(@"\u");
                            literal.Append(((int)c).ToString("x4"));
                        }
                        break;
                }
            }
            return literal.ToString();
        }


        // class for text objects
        class String : Object
        {
            string Reference { get; set; }
            public string Value { get; set; }
            double X { get; set; }
            double Y { get; set; }
            string Layer { get; set; }
            double SizeX { get; set; }
            double SizeY { get; set; }
            double Thickness { get; set; }
            double Rotation { get; set; }
            string Hide { get; set; }
            string Mirror { get; set; }

            private String()
            {
                Reference = "";
                Value = "";
                X = 0;
                Y = 0;
                Layer = "";
                SizeX = 0;
                SizeY = 0;
                Thickness = 0;
                Hide = "";
            }

            public String(string reference, string value, double x, double y, double rotation, string layer, double sizeX, double sizeY, double thickness, string hide, bool mirror)
            {
                Reference = reference;
                Value = ToLiteral(value);
                X = x; // absolute position of string
                Y = y;
                Layer = layer;
                SizeX = Math.Round(sizeX, Precision);
                SizeY = Math.Round(sizeY, Precision);
                Thickness = Math.Round(thickness, Precision);
                Rotation = rotation;
                Rotation %= 360;
                Hide = hide;
                Mirror = (mirror) ? "mirror" : "";
            }

            public string ToString(double x, double y)
            {
                Point2D p = new Point2D(X - x, Y - y);

                return $"    (fp_text {Reference} \"{Value}\" (at {p.X} {-p.Y} {Rotation} unlocked) (layer {Layer}) {Hide}\n      (effects (font (size {SizeX} {SizeY}) (thickness {Thickness})) (justify left {Mirror}))\n    )\n";
            }

            public override string ToString(double x, double y, double ModuleRotation)
            {
                // make strings position relative to the module
                Point2D p = new Point2D(X - x, Y - y);
                p.Rotate(-ModuleRotation);

                double Angle = (90 - (Rotation - ModuleRotation)) * Math.PI / 180;
                double X1 = SizeY / 2 * Math.Cos(Angle);
                double Y1 = SizeY / 2 * Math.Sin(Angle);

                if (Reference == "reference" || Reference == "value" || Value == "%V" || Value == "%R")
                    return $"    (fp_text {Reference} \"{Value}\" (at {Math.Round(p.X - X1, Precision)} {-Math.Round(p.Y + Y1, Precision)} {Rotation} unlocked) (layer {Layer}) {Hide}\n      (effects (font (size {SizeX} {SizeY}) (thickness {Thickness})) (justify left {Mirror}))\n    )\n";


                return $"    (fp_text {Reference} \"{Value}\" (at {Math.Round(p.X - X1, Precision)} {-Math.Round(p.Y + Y1, Precision)} {Rotation} unlocked) (layer {Layer}) {Hide}\n      (effects (font (size {SizeX} {SizeY}) (thickness {Thickness})) (justify left {Mirror}))\n    )\n";
            }

            public string ToRefString(double x, double y, double ModuleRotation)
            {
                Point2D p = new Point2D(X - x, Y - y);

                p.Rotate(-ModuleRotation);

                return $"    (fp_text reference REF** (at {p.X} {-p.Y} unlocked) (layer {Layer})\n      (effects (font (size {SizeX} {SizeY}) (thickness {Thickness})) (justify left {Mirror}))\n    )\n";
            }

        }

        // class for the texts document entry in the pcbdoc file
        class Texts : PcbDocEntry
        {
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            unsafe struct Text
            {
                //      public byte Type;                        //   0 1 type should be 5
                //      public UInt32 FontLen;                   //   1 4 FontLen
                public byte Layer;                       //   5 1 Layer
                public byte flags;                       //   6 1 flags
                public fixed byte U0[5];                 //   7 5 ???
                public Int16 Component;                  //  12 2 component number
                public Int32 U1;                         //  14 4 ???
                public Int32 X;                          //  18 4 X
                public Int32 Y;                          //  22 4 Y
                public Int32 Height;                     //  26 4 Height
                public Int16 U2;                         //  30 2 ???
                public double Rotation;                  //  32 8 rotation
                public byte Mirror;                      //  40 1 mirror
                public Int32 Thickness;                  //  41 4 thickness
                public byte IsComment;                   //  45 1 designator flag
                public byte IsDesignator;                //  46 1 comment flag
                public fixed byte U3[4];                 //  47 120 ???
                public fixed UInt16 FontName[32];        //  51 64 FontName in unicode (C# char=16 bits) 
                public fixed byte U4[50];                // 115 50 ???
                public byte TrueType;                    // 165 1 Truetype flag
                public fixed UInt16 BarcodeFontName[32]; // 166 64 Barcode FontName in unicode (C# char=16 bits) 
                public byte U5;                          // 230 1x1 ???
                public Int32 Length;                     // 231 2 length of the string length + string
                public byte StrLen;                      // 233 1 length of following string
                public fixed byte String[256];           // 234 strlen the string next record at EC + StrLen
            }

            string ConvertSpecialStrings(string name, Int16 Component, string layer)
            {
                if (name != "" && name[0] == '.')
                {
                    // this is a special string so reinterpret it
                    switch (name.ToLower())
                    {
                        case ".layer_name": name = layer; break;
                        case ".designator": if ((Component != -1) && (ModulesL[Component].Designator != null)) name = ModulesL[Component].Designator; break;
                        case ".comment": if ((Component != -1) && (ModulesL[Component].Comment != null)) name = ModulesL[Component].Comment; break;
                        default: break;
                    }
                }
                return name;
            }


            public Texts(string filename, string cmfilename, string record, Type type, int offset) : base(filename, cmfilename, record, type, offset)
            {
                Binary_size = 1; // variable record length
            }

            public override bool ProcessBinaryFile(byte[] data)
            {
                StartTimer();
                if (Binary_size == 0)
                    return false;

                List<UInt32> Headers = new List<UInt32>();

                long p = 0;
                Int32 len;
                string str = "";
                try
                {
                    using (MemoryStream ms = new MemoryStream(data))
                    {
                        long size = ms.Length;
                        if (size == 0)
                            return true;
                        BinaryReader br = new BinaryReader(ms, System.Text.Encoding.UTF8);
                        while (p < size)
                        {
                            base.ProcessLine(); // keep count
                            byte record_type = br.ReadByte();
                            if (record_type != 5)
                                break;
                            len = br.ReadInt32();
                            byte[] bytes = br.ReadBytes(len); // 0xEC);  // read the number of bytes up to actual text
                            // map text structure to the bytes
                            Text text = ByteArrayToStructure<Text>(bytes);

                            Layers Layer = (Layers)text.Layer;
                            Int16 Component = text.Component;
                            bool InComponent = Component != -1;
                            double X = Math.Round(ToMM(text.X) - originX, Precision);
                            double Y = Math.Round(ToMM(text.Y) - originY, Precision);
                            double Height = Math.Round(ToMM(text.Height), Precision);
                            double Rotation = text.Rotation % 360; // altium seem to store 0 as 360 quite a lot!
                            bool Mirror = text.Mirror != 0;
                            double Thickness = ToMM(text.Thickness);
                            bool IsComment = text.IsComment != 0;
                            bool IsDesignator = text.IsDesignator != 0;
                            bool TrueType = text.TrueType != 0;
                            UInt32 TextLen = br.ReadUInt32();

                            byte strlen = br.ReadByte();
                            byte[] textbytes = br.ReadBytes(strlen);
                            p = ms.Position; // now at end of record

                            str = Encoding.UTF8.GetString(textbytes, 0, strlen);
                            string layer = Brd.GetLayer(Layer);

                            str = ConvertSpecialStrings(str, Component, layer);
                            if (TrueType)
                            {
                                Thickness = Height / 10;    // fudge to get width the same as stroke font
                                Height = Math.Round(Height / 2.2, Precision);     // fudge to get height the same as stroke font
                            }

                            str = str.Replace("\r", "");
                            str = str.Replace("\n", "\\n");
                            if (!InComponent)
                            {
                                double Angle = (90 - Rotation) * Math.PI / 180;
                                double X1 = Height / 2 * Math.Cos(Angle);
                                double Y1 = Height / 2 * Math.Sin(Angle);

                                List<string> Layers = Brd.GetLayers(layer);
                                foreach (var L in Layers)
                                {
                                    texts.Append($"  (gr_text \"{ToLiteral(str)}\" (at {Math.Round(X - X1, Precision)} {-Math.Round(Y + Y1, Precision)} {Math.Round(Rotation, Precision)})  (layer {L}) (effects (font (size {Height} {Height}) (thickness {Thickness})) (justify left {(Mirror ? "mirror" : "")})))\n");
                                }
                            }
                            else
                            {
                                Module Mod = ModulesL[Component];
                                string Hide = "";
                                string type = "";
                                if (IsDesignator)
                                {
                                    type = "reference";
                                    Hide = Mod.DesignatorOn ? "" : "hide";
                                    if (str.Contains("_"))
                                    {
                                        // Virtual designator TODO get this from board info
                                        // in project file
                                        str = str.Substring(0, str.IndexOf('_'));
                                    }
                                    Mod.Designator = str;
                                }
                                else if (IsComment)
                                {
                                    type = "value";
                                    Hide = Mod.CommentOn ? "" : "hide";
                                    if (layer == "F.Cu")
                                        layer = "F.Fab";
                                    else
                                        if (layer == "B.Cu")
                                        layer = "B.Fab";

                                    if (str.Contains("_"))
                                    {
                                        // should only do this if Board-DESIGNATORDISPLAYMODE=1
                                        // get the seperator from the project file (.PrjPCB)
                                        // under ChannelRoomLevelSeperator
                                        // Virtual designator TODO get this from board info
                                        str = str.Substring(0, str.IndexOf('_'));
                                    }
                                    Mod.Comment = ToLiteral(str);
                                }
                                else
                                {
                                    type = "user";
                                    if (str == Mod.Comment)
                                        str = "%V";
                                    if (str == Mod.Designator)
                                        str = "%R";
                                }

                                String String = new String(type, str, X, Y, Rotation, layer, Height, Height, Thickness, Hide, Mirror);
                                ModulesL[Component].Strings.Add(String);
                            }

                        }
                    }
                }
                catch (Exception Ex)
                {
                    CheckThreadAbort(Ex);
                }

                return true;
            }

            public override bool ProcessLine(string line)
            {
                base.ProcessLine();
                return base.ProcessLine(line);
            }
        }
    }
}
