/*
 * This program source code file is part of AtoK
 * Copyright (C) 2020 Stephen Green
 *
 * This program is free software: you can redistribute it and/or modify it
 * under the terms of the GNU General Public License as published by the
 * Free Software Foundation, either version 3 of the License, or (at your
 * option) any later version.
 *
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License along
 * with this program.  If not, see <http://www.gnu.or/licenses/>.
 */

using Ionic.Zlib;
using OpenMcdf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace unpack
{
    class Program
    {
        const int Precision=3; // after the decimal point precision i.e. 3 digits
        static string net_classes = "";
        static string tracks = "";
        static string texts = "";
        static string arcs = "";
        static string fills = "";
        static string keepouts = "";
        static string board_outline = "";

        static string CurrentLayer = "";
        static int CurrentModule;
        static string filename = "";
        static string output_filename = "";
        static char[] charsToTrim = { 'm', 'i', 'l' };

        static ObjectList<Net>       NetsL;
        static ObjectList<Module>    ModulesL;
        static ObjectList<Polygon>   PolygonsL;
        static ObjectList<Line>      LinesL;
        static ObjectList<Pad>       PadsL;
        static ObjectList<String>    Strings;
        static ObjectList<Via>       ViasL;
        static ObjectList<Fill>      FillsL;
        static ObjectList<Dimension> DimensionsL;
        static ObjectList<Rule>      RulesL;
        static ObjectList<Region>    RegionsL;

        static double originX = 0;
        static double originY = 0;
        static bool ExtractFiles = false; // default to not extracting data to files
        static bool CreateLib = false;    // default to not creating library

        // used to decompress the 3D models in the PcbDoc file
        static string ZlibCodecDecompress(byte[] compressed)
        {
            int outputSize = 2048;
            byte[] output = new Byte[outputSize];

            // If you have a ZLIB stream, set this to true.  If you have
            // a bare DEFLATE stream, set this to false.
            bool expectRfc1950Header = true;

            using (MemoryStream ms = new MemoryStream())
            {
                ZlibCodec compressor = new ZlibCodec();
                compressor.InitializeInflate(expectRfc1950Header);

                compressor.InputBuffer = compressed;
                compressor.AvailableBytesIn = compressed.Length;
                compressor.NextIn = 0;
                compressor.OutputBuffer = output;

                foreach (var f in new FlushType[] { FlushType.None, FlushType.Finish })
                {
                    int bytesToWrite = 0;
                    do
                    {
                        compressor.AvailableBytesOut = outputSize;
                        compressor.NextOut = 0;
                        compressor.Inflate(f);

                        bytesToWrite = outputSize - compressor.AvailableBytesOut;
                        if (bytesToWrite > 0)
                            ms.Write(output, 0, bytesToWrite);
                    }
                    while ((f == FlushType.None && (compressor.AvailableBytesIn != 0 || compressor.AvailableBytesOut == 0)) ||
                           (f == FlushType.Finish && bytesToWrite != 0));
                }

                compressor.EndInflate();

                return UTF8Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        // convert string such as "29470.3502mil" to millimeters
        // relative to the board origin X
        static double GetCoordinateX(string coord)
        {
            if (coord == "")
                return 0;
            string c = coord.Trim(charsToTrim);
            double C = Convert.ToDouble(c) * 25.4 / 1000.0;
            return Math.Round(C - originX, Precision);
        }

        // convert string such as "29470.3502mil" to millimeters
        // relative to the board origin Y
        static double GetCoordinateY(string coord)
        {
            if (coord == "")
                return 0;
            string c = coord.Trim(charsToTrim);
            double C = Convert.ToDouble(c) * 25.4 / 1000.0;
            return Math.Round(C - originY, Precision);
        }

        static string ToLiteral(string input)
        {
            StringBuilder literal = new StringBuilder(input.Length + 2);
            foreach (var c in input)
            {
                switch (c)
                {
                    case '\'': literal.Append(@"\'"); break;
                    case '\"': literal.Append("\\\""); break;
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

        // find the string 's' within the string 'line' and return the remaining string up to the next '|'
        static public string GetString(string line, string s)
        {
            int index;
            int start, end;
            int length;
            index = line.IndexOf(s);
            if (index == -1)
                return "";
            start = index + s.Length;
            end = line.IndexOf('|', start);
            if (end == -1)
                length = line.Length - start;
            else
                length = end - start;

            string param = line.Substring(start, length);
            return ToLiteral(param.TrimEnd('\0'));
        }

        // get unsigned 32 bit number
        static public UInt32 GetUInt32(string number)
        {
            if (number == "")
                return 0;
            return Convert.ToUInt32(number);
        }

        // get double number
        static public double GetDouble(string number)
        {
            if (number == "")
                return 0;
            return Math.Round(Convert.ToDouble(number.Trim(charsToTrim)), Precision);
        }

        static public string GetNetName(int net_no)
        {
            if (net_no == -1)
                return "\"\"";
            return $"\"{NetsL[net_no].Name}\"";
        }

        // Altium layer numbers
        public enum Layers
        {
            top_layer       = 1,
            mid_1           = 2,
            mid_2           = 3,
            mid_3           = 4,
            mid_4           = 5,
            mid_5           = 6,
            mid_6           = 7,
            mid_7           = 8,
            mid_8           = 9,
            mid_9           = 10,
            mid_10          = 11,
            mid_11          = 12,
            mid_12          = 13,
            mid_13          = 14,
            mid_14          = 15,
            mid_15          = 16,
            mid_16          = 17,
            mid_17          = 18,
            mid_18          = 19,
            mid_19          = 20,
            mid_20          = 21,
            mid_21          = 22,
            mid_22          = 23,
            mid_23          = 24,
            mid_24          = 25,
            mid_25          = 26,
            mid_26          = 27,
            mid_27          = 28,
            mid_28          = 29,
            mid_29          = 30,
            mid_30          = 31,
            bottom_layer    = 32,

            Top_Overlay     = 33,
            Bottom_Overlay  = 34,
            Top_Paste       = 35,
            Bottom_Paste    = 36,
            Top_Solder      = 37,
            Bottom_Solder   = 38,

            plane_1         = 39,
            plane_2         = 40,
            plane_3         = 41,
            plane_4         = 42,
            plane_5         = 43,
            plane_6         = 44,
            plane_7         = 45,
            plane_8         = 46,
            plane_9         = 47,
            plane_10        = 48,
            plane_11        = 49,
            plane_12        = 50,
            plane_13        = 51,
            plane_14        = 52,
            plane_15        = 53,
            plane_16        = 54,

            Drill_Guide     = 55,
            Keepout_Layer   = 56,

            Mech_1          = 57,
            Mech_2          = 58,
            Mech_3          = 59,
            Mech_4          = 60,
            Mech_5          = 61,
            Mech_6          = 62,
            Mech_7          = 63,
            Mech_8          = 64,
            Mech_9          = 65,
            Mech_10         = 66,
            Mech_11         = 67,
            Mech_12         = 68,
            Mech_13         = 69,
            Mech_14         = 70,
            Mech_15         = 71,
            Mech_16         = 72,

            Drill_Drawing   = 73,
            Multi_Layer     = 74
        }

        // Altium layer names
        static readonly string[] LayerNames =
        {
            "UNKNOWN",
            "TOP",
            "MID1",
            "MID2",
            "MID3",
            "MID4",
            "MID5",
            "MID6",
            "MID7",
            "MID8",
            "MID9",
            "MID10",
            "MID11",
            "MID12",
            "MID13",
            "MID14",
            "MID15",
            "MID16",
            "MID17",
            "MID18",
            "MID19",
            "MID20",
            "MID21",
            "MID22",
            "MID23",
            "MID24",
            "MID25",
            "MID26",
            "MID27",
            "MID28",
            "MID29",
            "MID30",
            "BOTTOM",
            "TOPOVERLAY",
            "BOTTOMOVERLAY",
            "TOPPASTE",
            "BOTTOMPASTE",
            "TOPSOLDER",
            "BOTTOMSOLDER",
            "PLANE1",
            "PLANE2",
            "PLANE3",
            "PLANE4",
            "PLANE5",
            "PLANE6",
            "PLANE7",
            "PLANE8",
            "PLANE9",
            "PLANE10",
            "PLANE11",
            "PLANE12",
            "PLANE13",
            "PLANE14",
            "PLANE15",
            "PLANE16",
            "DRILLGUIDE",
            "KEEPOUT",
            "MECHANICAL1",
            "MECHANICAL2",
            "MECHANICAL3",
            "MECHANICAL4",
            "MECHANICAL5",
            "MECHANICAL6",
            "MECHANICAL7",
            "MECHANICAL8",
            "MECHANICAL9",
            "MECHANICAL10",
            "MECHANICAL11",
            "MECHANICAL12",
            "MECHANICAL13",
            "MECHANICAL14",
            "MECHANICAL15",
            "MECHANICAL16",
            "DRILLDRAWING",
            "MULTILAYER",
            "UNKNOWN",
            "UNKNOWN",
            "UNKNOWN",
            "UNKNOWN",
            "UNKNOWN",
            "UNKNOWN",
            "UNKNOWN",
            "UNKNOWN",
            "UNKNOWN",
            "UNKNOWN",
            "UNKNOWN",
        };

        // convert Altium negation to Kicad negation
        // e.g. convert "M\_\W\E\" "~M_WE"
        static public string ConvertIfNegated(string Name)
        {
            if (Name.IndexOf('\\') == -1)
                return Name;
            // name is negated fully or in part
            // prepend ~ and another if negated chars end before end of string
            string ret = "";
            bool negating = false;
            for (var i = 0; i < Name.Length; i++)
            {
                if ((i!=Name.Length-1) && Name[i + 1] == '\\')
                {
                    if (!negating)
                    {
                        negating = true;
                        ret += '~';
                    }
                    ret += Name[i];
                    i++;
                }
                else
                {
                    if (negating)
                        negating = false;
                    ret += '~';
                    ret += Name[i];
                }
            }
            return ret;
        }

        class Point2D
        {
            public double X { get; set; }
            public double Y { get; set; }

            public Point2D()
            {
                X = 0;
                Y = 0;
            }

            public Point2D(double x, double y)
            {
                X = Math.Round(x, Precision);
                Y = Math.Round(y, Precision);
            }

            public void Rotate(double rotation)
            {
                rotation %= 360;
                if (rotation != 0)
                {
                    rotation = rotation * Math.PI / 180;
                    double xp = X;
                    double yp = Y;
                    // do the rotation transform
                    X = Math.Round(xp * Math.Cos(rotation) - yp * Math.Sin(rotation), Precision);
                    Y = Math.Round(xp * Math.Sin(rotation) + yp * Math.Cos(rotation), Precision);
                }
            }

            public void Translate(double x, double y)
            {
                X += x; X = Math.Round(X, Precision);
                Y += y; Y = Math.Round(Y, Precision);
            }

            public Point2D Rotate(Point2D center, double angle)
            {
                angle = (angle) * (Math.PI / 180); // Convert to radians
                var RotatedX = Math.Cos(angle) * (X - center.X) - Math.Sin(angle) * (Y - center.Y) + center.X;
                var RotatedY = Math.Sin(angle) * (X - center.X) + Math.Cos(angle) * (Y - center.Y) + center.Y;
                return new Point2D(RotatedX, RotatedY);
            }
        }

        // class for track/arc objects which are part of the board outline
        class BoundaryObject : Object
        {
            public double X1    { get; set; }
            public double Y1    { get; set; }
            public double X2    { get; set; }
            public double Y2    { get; set; }
            public double Angle { get; set; } // for arcs

            private BoundaryObject()
            {
                X1 = 0;
                Y1 = 0;
                X2 = 0;
                Y2 = 0;
            }

            public BoundaryObject(double x1, double y1, double x2, double y2)
            {
                X1 = x1;
                Y1 = y1;
                X2 = x2;
                Y2 = y2;
                Angle = 0;
                Line line = new Line(x1, y1, x2, y2, Layers.Mech_1, 0.1);
            }

            public BoundaryObject(double x1, double y1, double x2, double y2, double angle)
            {
                X1 = x1;
                Y1 = y1;
                X2 = x2;
                Y2 = y2;
                Angle = angle;
                Arc arc = new Arc(x1, y1, x2, y2, angle, "Edge.Cuts", 0.1);
            }

        }


        // class for track objects
        class Line : Object
        {
            double X1 { get; set; }
            double Y1 { get; set; }
            double X2 { get; set; }
            double Y2 { get; set; }
            Layers Layer { get; set; }
            double Width { get; set; }

            private Line()
            {
                X1 = 0;
                Y1 = 0;
                X2 = 0;
                Y2 = 0;
                Layer = 0;
                Width = 0;
            }

            public Line(double x1, double y1, double x2, double y2, Layers layer, double width)
            {
                X1 = x1;
                Y1 = y1;
                X2 = x2;
                Y2 = y2;
                Layer = layer;
                Width = width;
            }

            public string ToString(double x, double y)
            {
                // convert to relative to component origin
                Point2D p1 = new Point2D(X1 - x, Y1 - y);
                Point2D p2 = new Point2D(X2 - x, Y2 - y);

                // create line relative to component origin
                return $"    (fp_line (start {p1.X} {-p1.Y}) (end {p2.X} {-p2.Y}) (layer {Brd.GetLayer(Layer)}) (width {Width}))\n";
            }

            public override string ToString(double x, double y, double ModuleRotation)
            {
                // convert to relative to component origin
                Point2D p1 = new Point2D(X1 - x, Y1 - y);
                Point2D p2 = new Point2D(X2 - x, Y2 - y);

                p1.Rotate(ModuleRotation);
                p2.Rotate(ModuleRotation);

                // create line relative to component origin
                return $"    (fp_line (start {p1.X} {-p1.Y}) (end {p2.X} {-p2.Y}) (layer {Brd.GetLayer(Layer)}) (width {Width}))\n";
            }
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
                X = x; // absolute poisition of string
                Y = y;
                Layer = layer;
                SizeX = sizeX;
                SizeY = sizeY;
                Thickness = thickness;
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

                double Angle = (90 - (Rotation-ModuleRotation)) * Math.PI / 180;
                double X1 = SizeY / 2 * Math.Cos(Angle);
                double Y1 = SizeY / 2 * Math.Sin(Angle);

                if (Reference == "reference" || Reference == "value" || Value == "%V" || Value == "%R")
                    return $"    (fp_text {Reference} \"{Value}\" (at {Math.Round(p.X-X1, Precision)} {-Math.Round(p.Y+Y1, Precision)} {Rotation} unlocked) (layer {Layer}) {Hide}\n      (effects (font (size {SizeX} {SizeY}) (thickness {Thickness})) (justify left {Mirror}))\n    )\n";


                return $"    (fp_text {Reference} \"{Value}\" (at {Math.Round(p.X-X1, Precision)} {-Math.Round(p.Y+Y1, Precision)} {Rotation} unlocked) (layer {Layer}) {Hide}\n      (effects (font (size {SizeX} {SizeY}) (thickness {Thickness})) (justify left {Mirror}))\n    )\n";
            }

            public string ToRefString(double x, double y, double ModuleRotation)
            {
                Point2D p = new Point2D(X - x, Y - y);

                p.Rotate(-ModuleRotation);

                return $"    (fp_text reference REF** (at {p.X} {-p.Y} unlocked) (layer {Layer})\n      (effects (font (size {SizeX} {SizeY}) (thickness {Thickness})) (justify left {Mirror}))\n    )\n";
            }

        }

        // class for Arc objects
        class Arc : Object
        {
            double X1 { get; set; }
            double Y1 { get; set; }
            double X2 { get; set; }
            double Y2 { get; set; }
            double Angle { get; set; }
            private readonly string Layer;
            private readonly double Width;

            private Arc()
            {
                X1 = 0;
                Y1 = 0;
                X2 = 0;
                Y2 = 0;
                Angle = 0;
                Layer = "";
                Width = 0;
            }

            public Arc(double x1, double y1, double x2, double y2, double angle, string layer, double width)
            {
                X1 = x1;
                Y1 = y1;
                X2 = x2;
                Y2 = y2;
                Angle = angle;
                Layer = layer;
                Width = width;
            }

            public string ToString(double x, double y)
            {
                if (Math.Abs(Angle) == 360)
                    // it's a circle
                    return $"    (fp_circle (center {Math.Round(X1 - x, Precision)} {Math.Round(-(Y1 - y), Precision)}) (end {Math.Round(X2 - x, Precision)} {Math.Round(-(Y2 - y), Precision)}) (layer {Layer}) (width {Width}))\n";
                else
                    return $"    (fp_arc (start {Math.Round(X1 - x, Precision)} {Math.Round(-(Y1 - y), Precision)}) (end {Math.Round(X2 - x, Precision)} {Math.Round(-(Y2 - y), Precision)}) (angle {Angle}) (layer {Layer}) (width {Width}))\n";
            }

            public override string ToString(double x, double y, double ModuleRotation)
            {
                Point2D p1 = new Point2D(X1 - x, Y1 - y);
                Point2D p2 = new Point2D(X2 - x, Y2 - y);
                p1.Rotate(ModuleRotation);
                p2.Rotate(ModuleRotation);

                if (Math.Abs(Angle) == 360)
                {
                    // it's a circle
                    return $"    (fp_circle (center {p1.X} {-p1.Y}) (end {p2.X} {-p2.Y}) (layer {Layer}) (width {Width}))\n";
                }
                else
                {
                    return $"    (fp_arc (start {p1.X} {-p1.Y}) (end {p2.X} {-p2.Y}) (angle {Angle}) (layer {Layer}) (width {Width}))\n";
                }
            }
        }

        // class for via objects
        class Via : Object
        {
            double X { get; set; }
            double Y { get; set; }
            double Size { get; set; }
            double Drill { get; set; }
            int Net { get; set; }

            private Via()
            {
                X = 0;
                Y = 0;
                Size = 0;
                Drill = 0;
                Net = 0;
            }

            public Via(double x, double y, double size, double drill, int net)
            {
                X = x;
                Y = y;
                Size = size;
                Drill = drill;
                Net = net;
            }

            override public string ToString() //double X, double Y)
            {
                return $"  (via (at {Math.Round(X, Precision)} {Math.Round(-Y, Precision)}) (size {Size}) (drill {Drill}) (layers F.Cu B.Cu) (net {Net}))";
            }
        }

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
                return $"(net {Number} \"{ Name}\")\n";
            }
        }

        // class for fill objects
        class Fill : Object
        {
            private readonly double X1, Y1, X2, Y2, CX, CY;
            private readonly string layer;
            private readonly string net;

            private Fill()
            {
                X1 = 0;
                Y1 = 0;
                X2 = 0;
                Y2 = 0;
                CX = 0;
                CY = 0;
            }

            public Fill(double x1, double y1, double x2, double y2, string Layer, string Net)
            {
                X1 = x1;
                Y1 = y1;
                X2 = x2;
                Y2 = y2;
                CX = Math.Round(X2 - (X2 - X1) / 2, Precision); // centre X
                CY = Math.Round(Y2 - (Y2 - Y1) / 2, Precision); // centre Y
                layer = Layer;
                net = Net;
            }

            public override string ToString()
            {
                return $"  (gr_poly (pts (xy {X1} {-Y1}) (xy {X1} {-Y2}) (xy {X2} {-Y2}) (xy {X2} {-Y1})) (layer {layer}) (width 0))";
            }


            public override string ToString(double x, double y, double ModuleRotation)
            {
                // rotate the fill around the fill's centre point
                double angle = (ModuleRotation) * (Math.PI / 180); // Convert to radians
                // this is essentially doing a translate to 0,0 a rotate and a translate back
                double rotatedX1 = Math.Cos(angle) * (X1 - CX) - Math.Sin(angle) * (Y1 - CY) + CX;
                double rotatedY1 = Math.Sin(angle) * (X1 - CX) + Math.Cos(angle) * (Y1 - CY) + CY;
                double rotatedX2 = Math.Cos(angle) * (X2 - CX) - Math.Sin(angle) * (Y2 - CY) + CX;
                double rotatedY2 = Math.Sin(angle) * (X2 - CX) + Math.Cos(angle) * (Y2 - CY) + CY;

                // now make the points relative to the component origin
                Point2D p1 = new Point2D(rotatedX1 - x, rotatedY1 - y);
                Point2D p2 = new Point2D(rotatedX2 - x, rotatedY2 - y);

                // now rotate these points about the component centre
                p1.Rotate(ModuleRotation);
                p2.Rotate(ModuleRotation);

                // NB (width 0) = filled
                return $"    (fp_poly (pts (xy {p1.X} {-p1.Y}) (xy {p1.X} {-p2.Y}) (xy {p2.X} {-p2.Y}) (xy {p2.X} {-p1.Y})) (layer {layer}) (width 0))\n";
            }
        }

        // class for point objects
        class Point : Object
        {
            private readonly double X, Y;

            private Point()
            { }

            public Point(double x, double y)
            {
                X = x;
                Y = y;
            }

            public override string ToString()
            {
                return $"(xy {X} {-Y}) ";
            }

            public override string ToString(double x, double y, double ModuleRotation)
            {
                double angle = (ModuleRotation) * (Math.PI / 180); // Convert to radians
                double RotatedX = Math.Round(Math.Cos(angle) * (X - x) - Math.Sin(angle) * (Y - y), Precision);
                double RotatedY = Math.Round( Math.Sin(angle) * (X - x) + Math.Cos(angle) * (Y - y), Precision);
                return $"(xy {RotatedX} {-RotatedY}) ";
            }


        }

        // class for polygon objects
        class Polygon : Object
        {
            List<Point> Points;
            int NetNo { get; set; }
            string NetName { get; set; }
            string Layer { get; set; }
            public bool InComponent { get; set; }
            public int Component { get; set; }

            public void AddPoint(double X, double Y)
            {
                Point Point = new Point(X, Y);
                Points.Add(Point);
            }

            private Polygon()
            {

            }

            public Polygon(string line) // string Layer, int net)
            {
                string param;
                Int32 net=0;
                
                if ((param = GetString(line, "|NET=")) != "")
                {
                    net = Convert.ToInt32(param) + 1;
                }
                if ((param = GetString(line, "|COMPONENT=")) != "")
                {
                    Component = Convert.ToInt32(param);
                    InComponent = true;
                }
                if ((param = GetString(line, "|LAYER=")) != "")
                {
                    Layer = Brd.GetLayer(param);
                }

                NetNo = net;
                NetName = GetNetName(NetNo);
                Points = new List<Point>();
                string[] coords;
                coords = line.Split('|');
                // now add all the vertices
                for (var j = 0; j < coords.Length; j++)
                {
                    if (coords[j].StartsWith("VX"))
                    {
                        var start = coords[j].IndexOf('=') + 1;
                        string coord = coords[j].Substring(start);
                        double x = Math.Round(GetCoordinateX(coord.Trim(charsToTrim)), Precision);
                        j++;
                        coord = coords[j].Substring(start);
                        double y = Math.Round(GetCoordinateY(coord.Trim(charsToTrim)), Precision);
                        AddPoint(x, y);
                    }
                }

            }

            public override string ToString()
            {
                string ret = "";
                string connectstyle = "";

                double clearance = GetRuleValue("Clearance", "PolygonClearance");
                if (Layer.Substring(0, 2) == "In")
                {
                    // this is an inner layer so use plane clearance
                    clearance = GetRuleValue("PlaneClearance", "PlaneClearance");
                }

                ret = $"  (zone (net {NetNo}) (net_name {NetName}) (layer {Layer}) (tstamp 0) (hatch edge 0.508)";
                ret += $"    (priority 100)\n";
                ret += $"    (connect_pads {connectstyle} (clearance {clearance}))\n"; // TODO sort out these numbers properly
                ret += $"    (min_thickness 0.2)\n";
                ret += "    (fill yes (arc_segments 16) (thermal_gap 0.2) (thermal_bridge_width 0.3))\n";
                var i = 0;
                ret += "    (polygon (pts\n        ";
                foreach (var Point in Points)
                {
                    i++;
                    if ((i % 5) == 0)
                        ret += "\n        ";
                    ret += Point.ToString();
                }
                ret += "\n      )\n    )\n  )\n";

                return ret;
            }

            public override string ToString(double x, double y, double rotation)
            {
                string ret = "";
/*
                double clearance = GetRuleValue("Clearance", "PolygonClearance");
                var i = 0;
                ret += $"(zone(net 81)(net_name "AGND2")(layer B.Cu)(tstamp 0)(hatch edge 0.508)(priority 100)
                        (connect_pads(clearance 0.254))
                        (min_thickness 0.2)
                        (fill yes(arc_segments 16)(thermal_gap 0.2)(thermal_bridge_width 0.3))";

                ret += "    (gr_poly (pts\n        "; // TODO mod for in component as well
                foreach (var Point in Points)
                {
                    i++;
                    if ((i % 5) == 0)
                        ret += "\n        ";
                    ret += Point.ToString(x, y, rotation);
                }
                ret += "\n      )\n    )\n";
*/
                return ret;
            }
        }

        // class for pad objects
        class Pad : Object
        {
            public string Number { get; set; }
            string Type { get; set; }
            string Shape { get; set; }
            double X { get; set; }
            double Y { get; set; }
            public double Rotation { get; set; }
            double SizeX { get; set; }
            double SizeY { get; set; }
            double Drill { get; set; }
            string Layer { get; set; }
            double Width { get; set; }
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
            }

            public Pad(string number, string type, string shape, double x, double y, double rotation, double sizex, double sizey, double drill, string layer, int net)
            {
                Number = number;
                Type = type;
                Shape = shape;
                X = x;
                Y = y;
                Rotation = rotation;
                SizeX = sizex;
                SizeY = sizey;
                Drill = drill;
                Layer = layer;
                Net = net;
                if (Net == -1)
                    Net = 0;
                Net_name = $"\"{NetsL[Net].Name}\"";
                // TODO should get this from rules
                Zone_connect = 1; // default to thermal connect 
            }

            override public string ToString()
            {
                if (Shape != "octagonal")
                {
                    return $"    (pad {Number} {Type} {Shape} (at {Math.Round(X, Precision)} {-Math.Round(Y, Precision)} {Rotation}) (size {SizeX} {SizeY}) (drill {Drill}) (layers {Layer})\n" +
                            $"      (net {Net} {Net_name}) (zone_connect {Zone_connect}))\n";
                }
                else
                {
                    // make octagonal pad out of polygon
                    return DoOctagonalPad(Number, Type, X, Y, Rotation, SizeX, SizeY, Layer);
                }
            }

            private string DoOctagonalPad(string Number, string Type, double X, double Y, double Rotation, double SizeX, double SizeY, string Layer)
            {
                double Cx, Cy;

                Cx = -SizeX / 2;
                Cy = -SizeY / 2;
                double Size = (SizeX < SizeY) ? SizeX : SizeY;
                double dl = Size / 4;
                string hole = $"(drill {Drill})";

                if (Type == "smd")
                    hole = "";

                // make octagonal pad out of polygon
                string
                ret = $"    (pad {Number} {Type} custom (at {X} {Y}  {Rotation}) (size {Size} {Size}) {hole} (layers {Layer})\n";
                ret += $"      (net {Net} {Net_name}) (zone_connect {Zone_connect})";
                ret += $"      (zone_connect {Zone_connect})\n"; // 0=none 1=thermal 2=solid get from rules
                ret += $"      (options (clearance outline) (anchor rect))\n";
                ret += $"      (primitives\n";
                ret += $"         (gr_poly (pts\n";
                ret += $"         (xy {Cx} {-(Cy + dl)})\n";
                ret += $"         (xy {Cx + dl} {-Cy})\n";
                ret += $"         (xy {Cx + SizeX - dl} {-Cy})\n";
                ret += $"         (xy {Cx + SizeX} {-(Cy + dl)})\n";
                ret += $"         (xy {Cx + SizeX} {-(Cy + SizeY - dl)})\n";
                ret += $"         (xy {Cx + SizeX - dl} {-(Cy + SizeY)})\n";
                ret += $"         (xy {Cx + dl} {-(Cy + SizeY)})\n";
                ret += $"         (xy {Cx} {-(Cy + SizeY - dl)})\n      )))\n    )\n";
                return ret;
            }

            public string ToString(double x, double y)
            {
                Point2D p = new Point2D(X - x, Y - y);

                if (Shape != "octagonal")
                {
                    return $"    (pad {Number} {Type} {Shape} (at {p.X} {-p.Y} {Rotation}) (size {SizeX} {SizeY}) (drill {Drill}) (layers {Layer})\n" +
                            $"      (net {Net} {Net_name}) (zone_connect {Zone_connect}))\n";
                }
                else
                {
                    // make octagonal pad out of polygon
                    return DoOctagonalPad(Number, Type, p.X, -p.Y, Rotation, SizeX, SizeY, Layer);
                }
            }

            public override string ToString(double x, double y, double ModuleRotation)
            {
                // point relative to modules centre
                Point2D p = new Point2D(X - x, Y - y);

                p.Rotate(-ModuleRotation);

                if (Shape != "octagonal")
                {
                    return $"    (pad {Number} {Type} {Shape} (at {p.X} {-p.Y} {Rotation}) (size {SizeX} {SizeY}) (drill {Drill}) (layers {Layer})\n" +
                        $"      (net {Net} {Net_name})  (zone_connect {Zone_connect}))\n";
                }
                else
                {
                    // make octagonal pad out of polygon
                    return DoOctagonalPad(Number, Type, p.X, -p.Y, Rotation, SizeX, SizeY, Layer);
                }
            }

            public string ToModuleString(double x, double y, double ModuleRotation)
            {
                // point relative to modules centre
                Point2D p = new Point2D(X - x, Y - y);

                p.Rotate(-ModuleRotation);

                if (Shape != "octagonal")
                {
                    return $"    (pad {Number} {Type} {Shape} (at {p.X} {-p.Y} {Rotation + ModuleRotation}) (size {SizeX} {SizeY}) (drill {Drill}) (layers {Layer})\n" +
                        $"      (net {Net} {Net_name})  (zone_connect {Zone_connect}))\n";
                }
                else
                {
                    // make octagonal pad out of polygon
                    return DoOctagonalPad(Number, Type, p.X, -p.Y, Rotation + ModuleRotation, SizeX, SizeY, Layer);
                }
            }
        }

        // class for component body objects
        class ComponentBody : Object
        {
            public string ID;
            public string Identifier;
            public UInt32 Checksum;
            public double StandoffHeight;
            public double OverallHeight;
            public double Model2dRot;
            public double X, Y;
            public double Model3dRotX, Model3dRotY, Model3dRotZ;
            public double TextureRotation;
            public string BodyLayer;

            ComponentBody()
            {

            }

            public ComponentBody(string line)
            {
                string Ident = GetString(line,"IDENTIFIER");
                Ident = Ident.Substring(1, Ident.Length - 1);
                if (Ident != "")
                {
                    string[] chars = Ident.Split(',');
                    foreach (string c in chars)
                    {
                        Int32 x = Convert.ToInt32(c);
                        Identifier += Convert.ToChar(x);
                    }
                }
                else
                    Identifier = "";
                ID              = GetString(line, "MODELID=");
                Checksum        = GetUInt32(GetString(line, "MODEL.CHECKSUM="));
                StandoffHeight  = GetNumberInMM(GetString(line, "MODEL.3D.DZ="));
                OverallHeight   = GetNumberInMM(GetString(line, "OVERALLHEIGHT="));
                X               = Math.Round(GetNumberInMM(GetString(line, "MODEL.2D.X=")) - originX, Precision);
                Y               = Math.Round(GetNumberInMM(GetString(line, "MODEL.2D.Y=")) - originY, Precision);
                Model2dRot      = GetDouble(GetString(line, "MODEL.2D.ROTATION="));   // rotation of footprint
                Model3dRotX     = GetDouble(GetString(line, "MODEL.3D.ROTX="));       // rotation about x for 3d model
                Model3dRotY     = GetDouble(GetString(line, "MODEL.3D.ROTY="));       // rotation about y for 3d model
                Model3dRotZ     = GetDouble(GetString(line, "MODEL.3D.ROTZ="));       // rotation about z for 3d model
                TextureRotation = GetDouble(GetString(line, "TEXTUREROTATION="));     // yet another rotation
                BodyLayer       = GetString(line, "BODYPROJECTION=");
            }

            public override string ToString(double x, double y, double modulerotation)
            {
                string FileName = Mods.GetFilename(ID).ToLower();
                string ret = "";

                Point2D p = new Point2D(X - x, Y - y);
                Model2dRot = Model2dRot % 360;

                if (FileName != "")
                {
                    double ModRotZ;

                    if (CurrentLayer == "B.Cu")
                    {
                        p.Rotate(-modulerotation);
                        p.X = p.X;
                        p.Y = -p.Y;
                        ModRotZ = (Model3dRotZ + (360-(Model2dRot - modulerotation))) % 360;
                    }
                    else
                    {
                        p.Rotate(-modulerotation);
                        ModRotZ = (Model3dRotZ + Model2dRot - modulerotation + 360 ) % 360;
                    }
                    if ((BodyLayer == "1" && ModulesL[CurrentModule].Layer == "F.Cu") || // 3d model layer
                        (BodyLayer == "0" && ModulesL[CurrentModule].Layer == "B.Cu"))
                    {
                        Model3dRotY = Model3dRotY - 180;
                    }

                    ret += $"    (model \"$(KIPRJMOD)/Models/{FileName}\"\n";
                    ret += $"        (offset (xyz {p.X} {p.Y} {StandoffHeight}))\n";
                    ret += $"        (scale (xyz {1} {1} {1}))\n";
                    ret += $"        (rotate (xyz {-Model3dRotX} {-Model3dRotY} {-ModRotZ}))\n    )\n";
                    return ret;
                }

                return $"# ID={ID} Checksum = {Checksum} StandoffHeight={StandoffHeight} OverallHeight={OverallHeight} x={X - x} y={-(Y - y)} rotx={Model3dRotX} roty={Model3dRotY} rotz={Model3dRotZ}\n";
            }
        }

        // class for module objects
        class Module : Object
        {
            static int    ID = 0;
            public string Name         { get; set; }
            public string Layer        { get; set; }
            string        Tedit        { get; set; }
            string        Tstamp       { get; set; }
            public string Designator   { get; set; }
            public bool   DesignatorOn { get; set; }
            public string Comment      { get; set; }
            public bool   CommentOn    { get; set; }
            public double X            { get; set; }
            public double Y            { get; set; }
            string        Path         { get; set; }
            string        Attr         { get; set; }
            public double Rotation     { get; set; }
            // primitives
            public ObjectList<Line>            Lines            { get; set; }
            public ObjectList<Pad>             Pads             { get; set; }
            public ObjectList<String>          Strings          { get; set; }
            public ObjectList<Via>             Vias             { get; set; }
            public ObjectList<Arc>             Arcs             { get; set; }
            public ObjectList<Fill>            Fills            { get; set; }
            public ObjectList<Polygon>         Polygons         { get; set; }
            public ObjectList<ComponentBody>   ComponentBodies  { get; set; }
            public ObjectList<ShapeBasedModel> ShapeBasedModels { get; set; }
            public ObjectList<Region>          Regions          { get; set; }

            public Module()
            {
            }

            public Module(string line)
            {
                string param;
                Name = GetString(line, "|PATTERN=");
                if(Name.Contains("\\"))
                    Name = Name.Replace("\\", "_"); // TODO check this out
                if ((param = GetString(line, "|X=").Trim(charsToTrim)) != "")
                {
                    X = GetCoordinateX(param);
                }
                if ((param = GetString(line, "|Y=").Trim(charsToTrim)) != "")
                {
                    Y = GetCoordinateY(param);
                }
                Layer = "";
                if ((param = GetString(line, "|LAYER=")) != "")
                {
                    Layer = Brd.GetLayer(param);
                }
                DesignatorOn = true;
                if ((param = GetString(line, "|NAMEON=")) != "")
                {
                    DesignatorOn = param == "TRUE";
                }
                CommentOn = true;
                if ((param = GetString(line, "|COMMENTON=")) != "")
                {
                    CommentOn = param == "TRUE";
                }
                Rotation = 0;
                if ((param = GetString(line, "|ROTATION=").Trim(charsToTrim)) != "")
                {
                    Rotation = Convert.ToDouble(param);
                    if (Rotation == 360)
                        Rotation = 0;
                }
                if (Layer == "F.Cu" || Layer == "B.Cu")
                    Attr = "smd";
                else
                    Attr = "thru_hole";

                Tedit  = "(tedit 0)";
                Tstamp = "(tstamp 0)";
                // create the object lists for this component
                Lines            = new ObjectList<Line>();
                Pads             = new ObjectList<Pad>();
                Strings          = new ObjectList<String>();
                ViasL            = new ObjectList<Via>();
                Arcs             = new ObjectList<Arc>();
                Fills            = new ObjectList<Fill>();
                Polygons         = new ObjectList<Polygon>();
                Regions          = new ObjectList<Region>();
                ComponentBodies  = new ObjectList<ComponentBody>();
                ShapeBasedModels = new ObjectList<ShapeBasedModel>();
                ID++; // update for next Module
            }

            void AddLine(Line line)
            {
                Lines.Add(line);
            }

            void AddPad(Pad pad)
            {
                Pads.Add(pad);
            }

            void AddText(String str)
            {
                Strings.Add(str);
            }

            public class PadComparer : IComparer<Pad>
            {
                public int Compare(Pad x, Pad y)
                {
                    if (x == null)
                    {
                        if (y == null)
                        {
                            // If x is null and y is null, they're
                            // equal. 
                            return 0;
                        }
                        else
                        {
                            // If x is null and y is not null, y
                            // is greater. 
                            return -1;
                        }
                    }
                    else
                    {
                        // If x is not null...
                        //
                        if (y == null)
                        // ...and y is null, x is greater.
                        {
                            return 1;
                        }
                        else
                        {
                            // sort them with ordinary string comparison.
                            //
                            return x.Number.CompareTo(y.Number);
                        }
                    }
                }
            }


            public override string ToString()
            {
                string ret = "";
                ret = $"  (module \"{Name}\" (layer {Layer}) {Tedit} {Tstamp}\n";
                ret += $"    (at {X} {-Y} {Rotation})\n";
                ret += $"    (attr {Attr})\n";

                // this bit is for a particular test board where the idiot had put the Comment as .designator
                foreach (var str in Strings)
                {
                    if (str.Value.ToLower() == ".comment")
                        str.Value = Comment;
                    if (str.Value.ToLower() == ".designator")
                        str.Value = Designator;
                }

                ret += Strings.ToString(X, Y, Rotation);

                PadComparer pc = new PadComparer();
                // put pads in numerical order (not really necessary)
                Pads.Sort(pc);

                ret += Pads.ToString(X, Y, Rotation);
                // ret += Vias.ToString(X, Y, Rotation); // vias not allowed in modules...yet
                ret += Lines.ToString(X, Y, -Rotation);
                ret += Arcs.ToString(X, Y, -Rotation);
                ret += Fills.ToString(X, Y, -Rotation);
                ret += Polygons.ToString();
                ret += Regions.ToString(X, Y, -Rotation);
                CurrentLayer = Layer;
                ret += ComponentBodies.ToString(X, Y, Rotation); // (Layer=="F.Cu")?-Rotation:-(Rotation-180));
                ret += ShapeBasedModels.ToString(X, Y, -Rotation);
                ret += "  )\n";
                return ret;
            }

            // output the module as a Library component
            public string ToModule()
            {
                string ret = "";
                ret = $"  (module \"{Name}\" (layer {Layer}) {Tedit} {Tstamp}\n";
                ret += $"  (descr \"\")";
                ret += $"  (tags \"\")";
                ret += $"  (attr {Attr})\n";

                foreach (var String in Strings)
                {
                    string str;

                    str = String.ToString(X, Y, Rotation);

                    if (str.Contains("fp_text reference"))
                    {
                        str = String.ToRefString(X, Y, Rotation);
                    }

                    ret += str;
                }
                foreach (var Pad in Pads)
                {
                    ret += Pad.ToModuleString(X, Y, Rotation);
                }
                foreach (var Line in Lines)
                {
                    ret += Line.ToString(X, Y, Rotation);
                }
                // Vias in components are done as pads
                //                foreach (var Via in Vias)
                //                {
                //                    ret += Via.ToString(X, Y);
                //                }
                foreach (var Arc in Arcs)
                {
                    ret += Arc.ToString(X, Y, Rotation);
                }
                foreach (var Polygon in Polygons)
                {
                    ret += Polygon.ToString();
                }
                foreach (var Fill in Fills)
                    ret += Fill.ToString(X, Y, -Rotation);
                foreach (var region in Regions)
                    ret += region.ToString(X, Y, -Rotation);
                foreach (var ComponentBody in ComponentBodies)
                {
                    ret += ComponentBody.ToString(X, Y, -Rotation);
                }
                ret += "  )\n";
                return ret;

            }
        }

        // class for dimension objects (not all dimension types catered for)
        class Dimension : Object
        {
            private readonly string layer = "";
            private string text = "";
            private readonly double X1 = 0, Y1 = 0, X2 = 0, Y2 = 0, LX = 0, LY = 0, HX = 0, HY = 0, REFERENCE0POINTX = 0, REFERENCE0POINTY = 0,
                REFERENCE1POINTX = 0, REFERENCE1POINTY = 0, ARROWSIZE = 0, ARROWLINEWIDTH = 0, ARROWLENGTH = 0, TEXTX = 0, TEXTY = 0, TEXT1X = 0, TEXT1Y = 0,
                LINEWIDTH = 0, TEXTHEIGHT = 0, TEXTWIDTH = 0, ANGLE = 0;
            private readonly int TEXTPRECISION;
            private readonly Int16 DIMENSIONKIND;
            private readonly char[] charsToTrim = { 'm', 'i', 'l' };
            double length;

            private string GetString(string line, string s)
            {
                int index;
                int start, end;
                int length;
                index = line.IndexOf(s);
                if (index == -1)
                    return "";
                start = index + s.Length;
                end = line.IndexOf('|', start);
                if (end == -1)
                    length = line.Length - start;
                else
                    length = end - start;

                string param = line.Substring(start, length);

                return param;
            }

            private Dimension()
            {

            }

            public Dimension(string line)
            {
                string param;
                if ((param = GetString(line, "|X1=").Trim(charsToTrim)) != "")
                {
                    X1 = GetCoordinateX(param);
                }
                if ((param = GetString(line, "|Y1=").Trim(charsToTrim)) != "")
                {
                    Y1 = GetCoordinateY(param);
                }
                if ((param = GetString(line, "|X2=").Trim(charsToTrim)) != "")
                {
                    X2 = GetCoordinateX(param);
                }
                if ((param = GetString(line, "|Y2=").Trim(charsToTrim)) != "")
                {
                    Y2 = GetCoordinateY(param);
                }

                if ((param = GetString(line, "|DIMENSIONKIND=")) != "")
                {
                    DIMENSIONKIND = Convert.ToInt16(param);
                }
                if ((param = GetString(line, "|DIMENSIONLAYER=")) != "")
                {
                    layer = Brd.GetLayer(param);
                }
                if ((param = GetString(line, "|LX=").Trim(charsToTrim)) != "")
                {
                    LX = GetCoordinateX(param);
                }
                if ((param = GetString(line, "|LY=").Trim(charsToTrim)) != "")
                {
                    LY = GetCoordinateY(param);
                }
                if ((param = GetString(line, "|HX=").Trim(charsToTrim)) != "")
                {
                    HX = GetCoordinateX(param);
                }
                if ((param = GetString(line, "|HY=").Trim(charsToTrim)) != "")
                {
                    HY = GetCoordinateY(param);
                }
                if ((param = GetString(line, "|REFERENCE0POINTX=").Trim(charsToTrim)) != "")
                {
                    REFERENCE0POINTX = GetCoordinateX(param);
                }
                if ((param = GetString(line, "|REFERENCE0POINTY=").Trim(charsToTrim)) != "")
                {
                    REFERENCE0POINTY = GetCoordinateY(param);
                }
                if ((param = GetString(line, "|REFERENCE1POINTX=").Trim(charsToTrim)) != "")
                {
                    REFERENCE1POINTX = GetCoordinateX(param);
                }
                if ((param = GetString(line, "|REFERENCE1POINTY=").Trim(charsToTrim)) != "")
                {
                    REFERENCE1POINTY = GetCoordinateY(param);
                }
                if ((param = GetString(line, "|ANGLE=").Trim(charsToTrim)) != "")
                {
                    ANGLE = Math.Round(Convert.ToDouble(param), Precision);
                    if (ANGLE == 180 || ANGLE == 360)
                        ANGLE = 0;
                    if (ANGLE == 270)
                        ANGLE = 90;
                }
                if ((param = GetString(line, "|ARROWSIZE=").Trim(charsToTrim)) != "")
                {
                    ARROWSIZE = GetNumberInMM(param);
                }
                if ((param = GetString(line, "|ARROWLINEWIDTH=").Trim(charsToTrim)) != "")
                {
                    ARROWLINEWIDTH = GetNumberInMM(param);
                }
                if ((param = GetString(line, "|ARROWLENGTH=").Trim(charsToTrim)) != "")
                {
                    ARROWLENGTH = GetNumberInMM(param);
                }
                if ((param = GetString(line, "|LINEWIDTH=").Trim(charsToTrim)) != "")
                {
                    LINEWIDTH = GetNumberInMM(param);
                }
                if ((param = GetString(line, "|TEXTHEIGHT=").Trim(charsToTrim)) != "")
                {
                    TEXTHEIGHT = GetNumberInMM(param);
                }
                if ((param = GetString(line, "|TEXTWIDTH=").Trim(charsToTrim)) != "")
                {
                    TEXTWIDTH = GetNumberInMM(param);
                }
                if ((param = GetString(line, "|TEXTHEIGHT=").Trim(charsToTrim)) != "")
                {
                    TEXTHEIGHT = GetNumberInMM(param);
                }
                if ((param = GetString(line, "|TEXTX=").Trim(charsToTrim)) != "")
                {
                    TEXTX = GetCoordinateX(param);
                }
                if ((param = GetString(line, "|TEXTY=").Trim(charsToTrim)) != "")
                {
                    TEXTY = GetCoordinateY(param);
                }
                if ((param = GetString(line, "|TEXT1X=").Trim(charsToTrim)) != "")
                {
                    TEXT1X = GetCoordinateX(param);
                }
                if ((param = GetString(line, "|TEXT1Y=").Trim(charsToTrim)) != "")
                {
                    TEXT1Y = GetCoordinateY(param);
                }
                if ((param = GetString(line, "|TEXTPRECISION=").Trim(charsToTrim)) != "")
                {
                    TEXTPRECISION = Convert.ToInt32(param);
                }

            }

            override public string ToString()
            {
                if (DIMENSIONKIND != 1) // TODO fix this - filter out radial dimensions
                {
                    Console.Error.WriteLine($"Unsupported Dimension kind ({DIMENSIONKIND}) at {X1},{Y1}");
                    return "";
                }
                Point2D R0 = new Point2D(REFERENCE0POINTX, REFERENCE0POINTY);
                Point2D R1 = new Point2D(REFERENCE1POINTX, REFERENCE1POINTY);
                Point2D centre = new Point2D(X1, Y1);

                // rotate the two reference points to make horizontal dimension
                R0 = R0.Rotate(centre, -ANGLE);
                R1 = R1.Rotate(centre, -ANGLE);
                Point2D end = new Point2D(R1.X, Y1 );
                // calculate the length of the crossbar
                length = Math.Round(R1.X - X1, 1);
                // calculate the end points of the arrow features
                Point2D a1a = new Point2D(X1 + ARROWSIZE, Y1 + ARROWSIZE / 3);
                Point2D a1b = new Point2D(X1 + ARROWSIZE, Y1 - ARROWSIZE / 3);
                Point2D a2a = new Point2D(end.X - ARROWSIZE, end.Y + ARROWSIZE / 3);
                Point2D a2b = new Point2D(end.X - ARROWSIZE, end.Y - ARROWSIZE / 3);
                if (length < 0)
                {
                    // there must be a better way to do this but hey ho
                    length = -length;
                    // calculate the end points of the arrow features
                    a1a = new Point2D(X1 - ARROWSIZE, Y1 + ARROWSIZE / 3);
                    a1b = new Point2D(X1 - ARROWSIZE, Y1 - ARROWSIZE / 3);
                    a2a = new Point2D(end.X + ARROWSIZE, end.Y + ARROWSIZE / 3);
                    a2b = new Point2D(end.X + ARROWSIZE, end.Y - ARROWSIZE / 3);
                }

                // rotate all the points back
                a1a = a1a.Rotate(centre, ANGLE);
                a1b = a1b.Rotate(centre, ANGLE);
                a2a = a2a.Rotate(centre, ANGLE);
                a2b = a2b.Rotate(centre, ANGLE);

                R0 = R0.Rotate(centre, ANGLE);
                R1 = R1.Rotate(centre, ANGLE);
                end = end.Rotate(centre, ANGLE);

                text = $"\"{length}mm\"";

                string string1 = $@"
    (dimension 176 (width {LINEWIDTH}) (layer {layer})
      (gr_text {text} (at {TEXT1X} {-TEXT1Y} {ANGLE}) (layer {layer})
          (effects (font (size {TEXTHEIGHT} {TEXTHEIGHT}) (thickness {LINEWIDTH})) (justify left ))
      )
      (feature1 (pts (xy {end.X} {-end.Y}) (xy {R1.X} {-R1.Y})  ))
      (feature2 (pts  (xy {X1} {-Y1}) (xy {R0.X} {-R0.Y})))
      (crossbar (pts (xy {X1} {-Y1}) (xy {end.X} {-end.Y})))
      (arrow1a  (pts (xy {X1} {-Y1}) (xy {a1a.X} {-a1a.Y})))
      (arrow1b  (pts (xy {X1} {-Y1}) (xy {a1b.X} {-a1b.Y})))
      (arrow2a  (pts (xy {end.X} {-end.Y}) (xy {a2a.X} {-a2a.Y})))
      (arrow2b  (pts (xy {end.X} {-end.Y}) (xy {a2b.X} {-a2b.Y})))
    )";
                // debug string to show with pads the X1,Y1 point and the REF0 and REF1 points
                string string2 = $@"
        (net 0 """")
        (net 1 ""X1"")
        (net 2 ""X2"")
        (net 3 ""REF0"")
        (net 4 ""REF1"")
        (net 5 ""LX"")
        (net 6 ""HX"")
        (net 7 ""TEXTX"")
        (net 8 ""TEXT1X"")

            (module ""PAD"" (layer F.Cu) (tedit 0) (tstamp 0)
              (at {X1} {-Y1} 0)
              (pad 0 smd circle (at 0 0 0) (size 0.9 0.8) (drill 0) (layers F.Cu)(net 1 ""X1"")(zone_connect 1))
            )

            (module ""PAD"" (layer F.Cu) (tedit 0) (tstamp 0)
              (at {REFERENCE0POINTX} {-REFERENCE0POINTY} 0)
              (pad 0 smd circle (at 0 0 0) (size 0.9 0.8) (drill 0) (layers F.Cu)(net 3 ""REF0"")(zone_connect 1))
            )
            (module ""PAD"" (layer F.Cu) (tedit 0) (tstamp 0)
              (at {REFERENCE1POINTX} {-REFERENCE1POINTY} 0)
              (pad 0 smd circle (at 0 0 0) (size 0.9 0.8) (drill 0) (layers F.Cu)(net 4 ""REF1"")(zone_connect 1))
            )
        ";
                return string1; //+string2;
            }
        }

        // convert Altium mil value string to millimeters
        static public double GetNumberInMM(string number)
        {
            if (number == "")
                return 0;
            return Math.Round(Convert.ToDouble(number.Trim(charsToTrim)) * 25.4 / 1000.0, Precision);
        }

        // convert Altium int mil value to mm
        static public double ToMM(int val)
        {
            return Math.Round((double)val * 25.4 / 10000000, Precision);
        }

        // convert Altium double mil value to mm 
        static public double ToMM(double val)
        {
            return Math.Round(val * 25.4 / 10000000, Precision);
        }

        // convert 32 bit fixed point value in mil to mm 
        static public double ToMM(byte[] arr, int pos)
        {
            Int32 val = arr[pos] + (arr[pos + 1] << 8) + (arr[pos + 2] << 16) + (arr[pos + 3] << 24);
            return Math.Round((double)val * 25.4 / 10000000, Precision);
        }

        // convert 32 bit fixed point value in mil to mm 
        static public double Bytes2mm(byte[] arr)
        {
            // altium store numbers as fixed point /10000 to get value in mils
            Int32 val = arr[0] + (arr[1] << 8) + (arr[2] << 16) + (arr[3] << 24);
            return Math.Round((double)val * 25.4 / 10000000, Precision);
        }

        // convert byte array to unsigned 16 bit integer
        static public UInt16 B2UInt16(byte[] arr, int pos)
        {
            return (UInt16)(arr[pos]+(arr[pos+1]<<8));
        }

        // convert byte array to 16 bit integer
        static public Int16 B2Int16(byte[] arr, int pos)
        {
            return (Int16)(arr[pos] + (arr[pos + 1] << 8));
        }

        // see if module is in module list
        static bool InList(Module mod, List<Module> Modules)
        {
            foreach (var Mod in Modules)
            {
                if (Mod.Name == mod.Name)
                    return true;
            }
            return false;
        }

        // convert byte array to structure
        static unsafe T ByteArrayToStructure<T>(byte[] bytes) where T : struct
        {
            fixed (byte* ptr = &bytes[0])
            {
                return (T)Marshal.PtrToStructure((IntPtr)ptr, typeof(T));
            }
        }

        enum Type { text, binary, mixed, special };

        // base class for the different document entries in the pcbdoc file
        class PcbDocEntry
        {
            public string FileName { get; set; }
            public string Record   { get; set; }
            public Type   Type     { get; set; }
            private readonly int offset;
            public uint Binary_size { get; set; }

            List<byte[]> binary;

            public PcbDocEntry()
            {
            }

            public PcbDocEntry(string filename, string record, Type type, int off)
            {
                binary = new List<byte[]>();
                FileName = filename;
                Record = record;
                Type = type;
                offset = off;
                Binary_size = 0;
            }

            public virtual bool ProcessBinaryFile(byte[] data)
            {
                if (Binary_size == 0)
                    return false;

                MemoryStream ms = new MemoryStream(data);
                long size = ms.Length;

                UInt32 pos = 0;
                BinaryReader br = new BinaryReader(ms, System.Text.Encoding.UTF8);
                while (pos < size)
                {
                    ms.Seek(pos, SeekOrigin.Begin);
                    byte[] line = br.ReadBytes((int)Binary_size);
                    binary.Add(line);
                    ProcessLine(line);
                    pos += Binary_size;
                }
                return true;

            }

            public virtual bool ProcessFile(byte[] data)
            {
                if((Binary_size != 0) && (Type == Type.binary))
                {
                    return ProcessBinaryFile(data);
                }

                if (Type != Type.text)
                    return false;

                using (MemoryStream ms = new MemoryStream(data))
                {
                    UInt32 pos = 0;
                    long size = ms.Length;

                    if (size == 0)
                        return false;

                    BinaryReader br = new BinaryReader(ms, System.Text.Encoding.UTF8);
                    ms.Seek(0, SeekOrigin.Begin);
                    while (pos+4 < size)
                    {
                        ms.Seek(pos+offset-4, SeekOrigin.Begin);
                        uint next = br.ReadUInt32();
                        if(pos+next>size)
                        {
                            // obviously erroneous value for next so exit
                            break;
                        }
                        char[] line = br.ReadChars((int)next);

                        string str = new string(line);
                        if(str.Length > 10) // fudge
                            ProcessLine(str.TrimEnd('\0'));
                        pos += (next + (UInt32)offset);
                    }
                }

                return true;
            }

            public virtual bool ProcessLine(string line)
            {
                return true;
            }

            public string ConvertToString(byte[] bytes)
            {
                return new string(bytes.Select(Convert.ToChar).ToArray());
            }


            public virtual bool ProcessLine(byte[] line)
            {
                return true;
            }

            // do any processing required at end of file processing
            public virtual void FinishOff()
            {
            }
        }

        class FileVersionInfo : PcbDocEntry
        {
            public FileVersionInfo(string filename, string record, Type type, int off) : base(filename,record,type,off)
            {

            }
        }

        // class for the board document in the pcbdoc file
        class Board : PcbDocEntry
        {
            public List<BoundaryObject> BoundaryObjects;

            public class Layer
            {
                public string Name                  { get; set; }
                public int    Prev                  { get; set; }
                public int    Next                  { get; set; }
                public bool   MechEnabled           { get; set; }
                public double CopperThickness       { get; set; }
                public int    DielectricType        { get; set; }
                public double DielectricConstant    { get; set; }
                public double DielectricHeight      { get; set; }
                public string DielectricMaterial    { get; set; }
                public int    Number                { get; set; }
                public string PcbNewLayer           { get; set; }
                public string AltiumName            { get; set; }
                public Layer(string line, int number)
                {
                    Name               = GetString(line, $"LAYER{number}NAME=");
                    Prev               = Convert.ToInt16(GetString(line, $"LAYER{number}PREV="));
                    Next               = Convert.ToInt16(GetString(line, $"LAYER{number}NEXT="));
                    MechEnabled        = GetString(line, $"LAYER{number}MECHENABLED=")=="TRUE";
                    CopperThickness    = GetNumberInMM(GetString(line, $"LAYER{number}COPTHICK="));
                    DielectricType     = Convert.ToInt16(GetString(line, $"LAYER{number}DIELTYPE="));
                    DielectricConstant = Convert.ToDouble(GetString(line, $"LAYER{number}DIELCONST="));
                    DielectricHeight   = GetNumberInMM(GetString(line, $"LAYER{number}DIELHEIGHT="));
                    DielectricMaterial = GetString(line, $"LAYER{number}DIELMATERIAL=");
                    Number             = number;
                    AltiumName         = LayerNames[Number];
                }

                public void AssignPcbNewLayer(int Number, int total)
                {
                    if (Number == 0) PcbNewLayer = "F.Cu";
                    else if (Number == total - 1) PcbNewLayer = "B.Cu";
                    else PcbNewLayer = $"In{Number}.Cu";
                }

            }

            public double SheetWidth { get; set; }
            public double SheetHeight { get; set; }
            public double OriginX { get; set; }
            public double OriginY { get; set; }
            public bool DesignatorDisplayMode { get; set; }
            public int InnerLayerCount { get; set; }
            public List<Layer> LayersL;
            public List<Layer> OrderedLayers;

            public Board()
            {
            }

            public Board(string filename, string record, Type type, int off) : base(filename, record, type, off)
            {
                LayersL = new List<Layer>();
                OrderedLayers = new List<Layer>();
                BoundaryObjects = new List<BoundaryObject>();
            }

            public void BoardAddLine(double x1, double y1, double x2, double y2)
            {
                BoundaryObject Line = new BoundaryObject(x1, y1, x2, y2);
                BoundaryObjects.Add(Line);
            }

            public bool CheckExistingLine(double x1, double y1, double x2, double y2)
            {
                foreach (var Line in BoundaryObjects)
                {
                    if (Line.X1 == x1 || Line.Y1 == y1 || Line.X2 == x2 || Line.Y2 == y2)
                        return true;
                }
                return false;
            }

            public void BoardAddArc(double x1, double y1, double x2, double y2, double angle)
            {
                BoundaryObject Arc = new BoundaryObject(x1, y1, x2, y2, angle);
                BoundaryObjects.Add(Arc);
            }

            public bool CheckExistingArc(double x1, double y1, double x2, double y2, double angle)
            {
                foreach (var Arc in BoundaryObjects)
                {
                    if (Arc.X1 == x1 && Arc.Y1 == y1 && Arc.X2 == x2 && Arc.Y2 == y2 && Arc.Angle==angle)
                        return true;
                }
                return false;
            }

            public override bool ProcessLine(string line)
            {
                try
                {
                    InnerLayerCount = 0;
                    string ORIGINX = GetString(line, "ORIGINX=");
                    string ORIGINY = GetString(line, "ORIGINX=");
                    if(ORIGINX != "")
                        OriginX = originX = GetCoordinateX(ORIGINX);
                    if(ORIGINY != "")
                        OriginY = originY = GetCoordinateY(GetString(line, "ORIGINY="));
                    if(ORIGINX!="" && ORIGINY !="")
                    {
                        List<string> strings= new List<string>();
                        // this is the first line in the file and contains the board outline details
                        // TODO define Edge.Cuts from this data
                        int count = 0;
                        string search;
                        int position = 0;
                        bool done = false;
                        do
                        {
                            search = $"KIND{count}=";
                            int start = line.IndexOf(search, position);
                            search = $"KIND{count+1}=";
                            int end = line.IndexOf(search, position);
                            if (end == -1)
                            {
                                end = line.IndexOf("SHELVED", position);
                                done = true;
                            }
                            position = end;
                            string found = line.Substring(start, end-start);
                            strings.Add(found);
                            count++;
                        } while (!done);

                        count = 0;
                        double x, y, cx, cy, sa, ea, r, x0=0, y0=0, nx, ny;
                        foreach (var found in strings)
                        {
                            search = $"KIND{count}=";
                            string Kind = GetString(found, search);
                            x = GetCoordinateX(GetString(found, $"VX{count}="));
                            y = GetCoordinateY(GetString(found, $"VY{count}="));
                            if(count==0)
                            {
                                // record first coordinate
                                x0 = x;
                                y0 = y;
                            }
                            if(count<strings.Count-1)
                            {
                                nx = GetCoordinateX(GetString(strings[count+1], $"VX{count + 1}="));
                                ny = GetCoordinateY(GetString(strings[count + 1], $"VY{count + 1}="));
                            }
                            else
                            {
                                nx = x0;
                                ny = y0;
                            }
                            cx = GetCoordinateX(GetString(found, $"CX{count}="));
                            cy = GetCoordinateY(GetString(found, $"CY{count}="));
                            sa = Convert.ToDouble(GetString(found, $"SA{count}="));
                            ea = Convert.ToDouble(GetString(found, $"EA{count}="));
                            r = GetNumberInMM(GetString(found, $"R{count}="));
                            count++;
                            if(Kind=="0")
                            {
                                board_outline += $"  (gr_line (start {x} {-y}) (end {nx} {-ny}) (layer Edge.Cuts) (width 0.05))\n";
                                BoardAddLine(x, y, nx, ny);
                            }
                            else
                            {
                                double X1 = cx;
                                double Y1 = cy;
     

                                if (ea < sa)
                                    ea += 360;

                                double Angle = Math.Round(-(ea - sa), Precision);
                                double X = Math.Round(cx + r * Math.Cos(sa * Math.PI / 180), Precision);
                                double Y = Math.Round(cy + r * Math.Sin(sa * Math.PI / 180), Precision);
                                board_outline += $"  (gr_arc (start {X1} {-Y1}) (end {X} {-Y}) (angle {Angle}) (layer Edge.Cuts) (width {0.05}))\n";
                                BoardAddArc(X1, Y1, X, Y, Angle);
                            }
                        }

                    }

                    if (LayersL.Count == 0)
                    {
                        try
                        {
                            for (var i = 1; i < 83; i++)
                            {
                                Layer Layer = new Layer(line, i);
                                //Debug.WriteLine($"Found layer - {Layer.AltiumName} name={Layer.Name} number={Layer.Number}");
                                if (Layer.Prev != 0 || Layer.Next != 0)
                                    // only add layers that are in the layer stack
                                    LayersL.Add(Layer);
                            }
                        }
                        catch(Exception Ex)
                        {
                            Debug.WriteLine(Ex.ToString());
                        }

                        // now sort the list in terms of the prev,next parameters
                        // first in list is the layer with Prev=0

                        int Next = 0;

                        foreach (var Layer in LayersL)
                        {
                            if (Layer.Prev == 0)
                            {
                                OrderedLayers.Add(Layer);
                                Next = Layer.Next;
                                break;
                            }
                        }


                        bool End = false;
                        do
                        {
                            foreach (var Layer in LayersL)
                            {
                                if (Layer.Number == Next)
                                {
                                    OrderedLayers.Add(Layer);
                                    Next = Layer.Next;
                                    if (Layer.Next == 0)
                                        End = true;
                                    break;
                                }
                            }
                        } while (!End);

                        for(var i=0; i<OrderedLayers.Count; i++)
                        {
                            OrderedLayers[i].AssignPcbNewLayer(i, OrderedLayers.Count);
                        }

                        InnerLayerCount = OrderedLayers.Count;

                        SheetWidth = GetNumberInMM(GetString(line, "SHEETWIDTH="));
                        SheetHeight = GetNumberInMM(GetString(line, "SHEETHEIGHT="));
                        DesignatorDisplayMode = GetString(line, "DESIGNATORDISPLAYMODE=") == "1";
                    }
                }
                catch(Exception Ex)
                {
                    Debug.WriteLine(Ex.ToString());
                }

                return true;
            }

            // output the layers as a string
            public override string ToString()
            {
                string Layers = "";
                int i = 0;
                foreach(var Layer in OrderedLayers)
                {
                    string Type = (Layer.Name.Substring(0, Precision) == "Int") ? "power" : "signal";
                    Layers += $"    ({i++} {Layer.PcbNewLayer} {Type})\n";
                }
                return Layers;
            }

            // binary based get layer
            public string GetLayer(Layers AltiumLayer)
            {
                // perhaps should get layer mapping from a file...not everybody uses the mech layers the same
                // TODO it's not as simple as this as the layer stackup determines which Inx.Cu is mapped to which Altium layer
                // this bit takes care of any copper layers
                foreach(var Layer in OrderedLayers)
                {
                    if((Layers)Layer.Number == AltiumLayer)
                    {
                        return Layer.PcbNewLayer;
                    }
                }

                switch (AltiumLayer)
                {
/*
                    case Layers.top_layer: return "F.Cu";*/
                    case Layers.Multi_Layer: return "*.Cu"; // *.Mask";
/*                    case Layers.bottom_layer: return "B.Cu";
                    case Layers.plane_1: return "In1.Cu";
                    case Layers.plane_2: return "In2.Cu";
                    case Layers.plane_3: return "In3.Cu";
                    case Layers.plane_4: return "In4.Cu";
                    case Layers.plane_5: return "In5.Cu";
                    case Layers.plane_6: return "In6.Cu";
                    case Layers.plane_7: return "In7.Cu";
                    case Layers.plane_8: return "In8.Cu";
                    case Layers.plane_9: return "In9.Cu";
                    case Layers.plane_10: return "In10.Cu";
                    case Layers.plane_11: return "In11.Cu";
                    case Layers.plane_12: return "In12.Cu";
                    case Layers.plane_13: return "In13.Cu";
                    case Layers.plane_14: return "In14.Cu";
                    case Layers.plane_15: return "In15.Cu";
                    case Layers.plane_16: return "In16.Cu";
*/
                    case Layers.Top_Overlay:    return "F.SilkS";
                    case Layers.Bottom_Overlay: return "B.SilkS";
                    case Layers.Keepout_Layer:  return "Margin";
                    case Layers.Mech_1:         return "Edge.Cuts";
                    case Layers.Mech_13:        return "Dwgs.User";
                    case Layers.Mech_15:        return "F.CrtYd";
                    case Layers.Mech_16:        return "B.CrtYd";
                    case Layers.Mech_11:        return "Eco1.User";
                    case Layers.Top_Solder:     return "F.Mask";
                    case Layers.Bottom_Solder:  return "B.Mask";
                    case Layers.Mech_9:         return "Dwgs.User";
                    case Layers.Mech_10:        return "Dwgs.User";
                    case Layers.Bottom_Paste:   return "B.Paste";
                    case Layers.Top_Paste:      return "F.Paste";
                    case Layers.Drill_Drawing:  return "Dwgs.User";
                    case Layers.Drill_Guide:    return "Dwgs.User";
                    default:                    return "Dwgs.User";
                }
            }

            // string based get layer i.e. for ASCII data.dat files
            public string GetLayer(string AltiumLayer)
            {
                string layer = "";

                foreach (var Layer in OrderedLayers)
                {
                    if (Layer.AltiumName == AltiumLayer)
                    {
                        return Layer.PcbNewLayer;
                    }
                }

                switch (AltiumLayer)
                {

/*                  case "TOP": layer += "F.Cu"; break;
                    case "BOTTOM": layer += "B.Cu"; break;
                    case "PLANE1": layer += "In1.Cu"; break;
                    case "PLANE2": layer += "In2.Cu"; break;
*/
                    case "MULTILAYER":    layer += "F.Cu F.Mask B.Cu B.Mask"; break;
                    case "TOPOVERLAY":    layer += "F.SilkS"; break;
                    case "BOTTOMOVERLAY": layer += "B.SilkS"; break;
                    case "KEEPOUT":       layer += "Margin"; break;
                    case "MECHANICAL1":   layer += "Edge.Cuts"; break;
                    case "MECHANICAL3":   layer += "Dwgs.User"; break;
                    case "MECHANICAL13":  return "Dwgs.User";
                    case "MECHANICAL15":  layer += "F.CrtYd"; break;
                    case "MECHANICAL16":  layer += "B.CrtYd"; break;
                    case "MECHANICAL11":  layer += "Eco1.User"; break;
                    case "TOPSOLDER":     layer += "F.Mask"; break;
                    case "BOTTOMSOLDER":  layer += "B.Mask"; break;
                    case "MECHANICAL9":   layer += "Dwgs.User"; break;
                    case "MECHANICAL10":  layer += "Dwgs.User"; break;
                    case "BOTTOMPASTE":   layer += "B.Paste"; break;
                    case "TOPPASTE":      layer += "F.Paste"; break;
                    case "DRILLDRAWING":  layer += "Dwgs.User"; break;
                    case "DRILLGUIDE":    layer += "Dwgs.User"; break;
                    default: return AltiumLayer;
                }
                return layer;
            }

            public bool IsCopperLayer(Layers AltiumLayer)
            {
                foreach (var Layer in OrderedLayers)
                {
                    if ((Layers)Layer.Number == AltiumLayer)
                    {
                        return true;
                    }
                }

                return false;
            }
/*
            // get the number of a copper layer
            public int GetAltiumCopperLayerNumber(string Layer)
            {
                switch (Layer)
                {
                    case "Top Layer": return 1;
                    case "MidLayer1": return 2;
                    case "MidLayer2": return 3;
                    case "MidLayer3": return 4;
                    case "MidLayer4": return 5;
                    case "MidLayer5": return 6;
                    case "MidLayer6": return 7;
                    case "MidLayer7": return 8;
                    case "MidLayer8": return 9;
                    case "MidLayer9": return 10;
                    case "MidLayer10": return 11;
                    case "MidLayer11": return 12;
                    case "MidLayer12": return 13;
                    case "MidLayer13": return 14;
                    case "MidLayer14": return 15;
                    case "MidLayer15": return 16;
                    case "MidLayer16": return 17;
                    case "MidLayer17": return 18;
                    case "MidLayer18": return 19;
                    case "MidLayer19": return 20;
                    case "MidLayer20": return 21;
                    case "MidLayer21": return 22;
                    case "MidLayer22": return 23;
                    case "MidLayer23": return 24;
                    case "MidLayer24": return 25;
                    case "MidLayer25": return 26;
                    case "MidLayer26": return 27;
                    case "MidLayer27": return 28;
                    case "MidLayer28": return 29;
                    case "MidLayer29": return 30;
                    case "MidLayer30": return 31;
                    case "Bottom Layer": return 32;
                    case "InternalPlane1": return 39;
                    case "InternalPlane2": return 40;
                    case "InternalPlane3": return 41;
                    case "InternalPlane4": return 42;
                    case "InternalPlane5": return 43;
                    case "InternalPlane6": return 44;
                    case "InternalPlane7": return 45;
                    case "InternalPlane8": return 46;
                    case "InternalPlane9": return 47;
                    case "InternalPlane10": return 48;
                    case "InternalPlane11": return 49;
                    case "InternalPlane12": return 50;
                    case "InternalPlane13": return 51;
                    case "InternalPlane14": return 52;
                    case "InternalPlane15": return 53;
                    case "InternalPlane16": return 54;
                }
                return 0;
            }
*/
            // used for non-net tracks on inner copper layers
            // to eliminate them as PCBNEW doesn't do negative planes
            public bool OnInnerLayer(Layers Layer)
            {
                for(var i=1; i<OrderedLayers.Count; i++)
                {
                    if ((Layers)OrderedLayers[i].Number == Layer)
                        return true;
                }
                return false;
            }

        }

        abstract class Object
        {
            public Object()
            { }

            public virtual string ToString(double x, double y, double rotation)
            {
                return "";
            }
        }

        // class for creating lists of objects
        class ObjectList<T> : List<T> where T : Object
        {
            
            public ObjectList() : base()
            {
            }

            public new void Add(T newobj)
            {
                base.Add(newobj);
            }

            public override string ToString()
            {
                string ret = "";
                for (var i = 0; i < base.Count; i++)
                {
                    string type = $"{base[i].GetType()}";
                    if(type.IndexOf("Module")!=-1)
                    { 
                        CurrentModule = i;
                    }
                    ret += base[i].ToString();
                }
                return ret;
            }

            public string ToString(double x, double y, double rotation)
            {
                string ret = "";
                for(var i=0; i< base.Count; i++)
                {
                    ret += base[i].ToString(x, y, rotation);
                }
                return ret;
            }
        }

        // advanced placer options document class - not implemented
        class AdvancedPlacerOptions : PcbDocEntry
        {
            public AdvancedPlacerOptions(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }
        }

        // design rule checker options document class - not implemented
        class DesignRuleCheckerOptions : PcbDocEntry
        {
            public DesignRuleCheckerOptions(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }
        }

        // pin swap options document class - not implemented
        class PinSwapOptions : PcbDocEntry
        {
            public PinSwapOptions(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }
        }

        // class for the net classes document entry in the pcbdoc file
        class Classes : PcbDocEntry
        {
            public Classes(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }

            public override bool ProcessLine(string line)
            {
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
                        Kind = 0;
                        Debug.WriteLine(Ex.Message);
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

                    net_classes += ($" (net_class \"{name}\"  \"{name}\"\n    (clearance 0.127)\n    (trace_width 0.254)\n    (via_dia 0.889)\n    (via_drill 0.635)\n    (uvia_dia 0.508)\n    (uvia_drill 0.127)\n");
                    for (int c = 10; c < words.Length-1; c++)
                    {
                        split = words[c].Split('=');
                        net_classes += ($"    (add_net \"{ConvertIfNegated(ToLiteral(split[1]))}\")\n");
                    }
                    net_classes += (" )\n");
                }

                return true;
            }
        }

        // class for the nets document entry in the pcbdoc file
        class Nets : PcbDocEntry
        {
            static int net_no;
            public Nets(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
                net_no = 0;
            }

            public override bool ProcessLine(string line)
            {
                string Net_name = GetString(line, "|NAME=");
                byte[] bytes = Encoding.ASCII.GetBytes(Net_name);
                Net net = new Net(net_no + 1, Net_name);

                NetsL.Add(net);

                net_no++;

                return true;
            }

            public override void FinishOff()
            {
                // now go throught the nets looking for differential pairs and convert to KiCad format
                // i.e. ending in + and - rather than _N and _P
                List<Net> pos = new List<Net>();
                List<Net> neg = new List<Net>();
                foreach (var net in NetsL)
                {
                    if (net.Name.Length > 2)
                    {
                        string trailing = net.Name.Substring(net.Name.Length-2, 2);
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
                foreach(var pnet in pos)
                {
                    foreach(var nnet in neg)
                    {
                        if(pnet.Name.Substring(0, pnet.Name.Length - 2) == nnet.Name.Substring(0, nnet.Name.Length - 2))
                        {
                            // we've got a differential pair
                            for(var i = 0; i<NetsL.Count; i++)
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

        // class for the components document entry in the pcbdoc file
        class Components : PcbDocEntry
        {
            public Components(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }

            public override bool ProcessLine(string line)
            {
                Module Mod = new Module(line);
                ModulesL.Add(Mod);
                return true;
            }
        }

        // class for the polygons document entry in the pcbdoc file
        class Polygons : PcbDocEntry
        {
            public Polygons(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {

            }

            public override bool ProcessLine(string line)
            {
                Polygon Poly = new Polygon(line);
                if (Poly.InComponent)
                    ModulesL[Poly.Component].Polygons.Add(Poly);
                else
                    PolygonsL.Add(Poly);
                return true;
            }
        }

        // class for scope objects used in design rules
        class Scope : Object
        {
            public string Expression { get; set; }
            public string Kind { get; set; }
            public string Value { get; set; }
            public double Gap { get; set; }

            Scope()
            {
            }

            public Scope(int n, string rule)
            {
                Kind = GetString(rule, $"SCOPE{n}_0_KIND=");
                Expression = GetString(rule, $"SCOPE{n}EXPRESSION=");
                Value = GetString(rule, $"SCOPE{n}_0_VALUE=");
                Gap = GetNumberInMM(GetString(rule, "|GAP="));
            }

            public override string ToString()
            {
                return $"Scope={Kind} Expression={Expression} Value={Value} Gap={Gap}";
            }
        }

        // class for design rule objects
        class Rule : Object
        {
            private readonly string Line;
            public string Name { get; set; }
            public string RuleKind { get; set; }
            public string NetScope { get; set; }
            public string LayerKind { get; set; }
            public bool Enabled { get; set; }
            public int Priority { get; set; }
            public string Comment { get; set; }
            public Scope Scope1 { get; set; }
            public Scope Scope2 { get; set; }
            public double Value { get; set; }
            public double ValueMin { get; set; }
            public double ValueMax { get; set; }
            public bool Valid;

            private Rule()
            {
            }

            public Rule(string line)
            {
                string param;

                Line = line;
                if ((param = GetString(line, "|ENABLED=")) != "")
                {
                    Enabled = param == "TRUE";
                }
                Name      = GetString(line, "|NAME=");
                RuleKind  = GetString(line, "|RULEKIND=");
                NetScope  = GetString(line, "|NETSCOPE=");
                Comment   = GetString(line, "|COMMENT=");
                LayerKind = GetString(line, "|LAYERKIND=");
                Priority  = Convert.ToInt16(GetString(line, "PRIORITY="));
                Scope1 = new Scope(1, line);
//                Debug.WriteLine(Name + " " + RuleKind + " " + Scope1.ToString());
                Scope2 = new Scope(2, line);
//                Debug.WriteLine(Name+" "+RuleKind + " " + Scope2.ToString());
                Value = 0;
                Valid = false;

                switch (RuleKind)
                {
                    case "Clearance":
                    case "ComponentClearance":
                    case "PolygonClearance":
                        {
                            Value = GetNumberInMM(GetString(line, "GAP="));
                            Valid = true;
                        }
                        break;
                    case "PasteMaskExpansion":
                    case "SolderMaskExpansion":
                        {
                            Value = GetNumberInMM(GetString(line, "EXPANSION="));
                            Valid = true;
                        }
                        break;
                    case "PlaneClearance":
                        {
                            Value = GetNumberInMM(GetString(line, "CLEARANCE="));
                            Valid = true;
                        }
                        break;
                    case "MinimumSolderMaskSliver":
                        {
                            Value = GetNumberInMM(GetString(line, "MINSOLDERMASKWIDTH="));
                            Valid = true;
                        }
                        break;
                    case "HoleSize":
                        {
                            ValueMin = GetNumberInMM(GetString(line, "MINLIMIT="));
                            Valid = true;
                            ValueMax = GetNumberInMM(GetString(line, "MAXLIMIT="));
                            Valid = true;
                        }
                        break;
                    case "PolygonConnect":
                        {
                            Value = GetNumberInMM(GetString(line, "RELIEFCONDUCTORWIDTH="));
                            Valid = true;
                        }
                        break;
                    case "Width":
                        {
                        }
                        break;
                    case "MinimumAnnularRing":
                        {
                        }
                        break;
                    case "SMDToCorner":
                        {
                        }
                        break;
                    case "RoutingVias":
                        {
                        }
                        break;

                    case "ShortCircuit": break;
                    case "DiffPairsRouting": break;
                    case "NetAntennae": break;
                    case "SilkscreenOverComponentPads": break;
                    case "HoleToHoleClearance": break;
                    case "FanoutControl": break;
                    case "LayerPairs": break;
                    case "Height": break;
                    case "Testpoint": break;
                    case "TestPointUsage": break;
                    case "RoutingTopology": break;
                    case "RoutingCorners": break;
                    case "RoutingLayers": break;
                    case "RoutingPriority": break;
                    case "PlaneConnect": break;
                    case "UnRoutedNet": break;
                    default: break;
                }
            }

            public override string ToString()
            {
                return "";
            }
        }

        // get a design rule value e.g. polygon clearance
        static double GetRuleValue(string kind, string name)
        {
            foreach (var Rule in RulesL)
            {
                if ((Rule.RuleKind == kind) && (Rule.Name == name))
                {
                    return Rule.Value;
                }
            }
            return 0;
        }


        // class for the rules document entry in the pcbdoc file
        class Rules : PcbDocEntry
        {
            public Rules(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {

            }

            public override bool ProcessLine(string line)
            {
                try
                {
                    Rule Rule = new Rule(line);
                    if (Rule.Enabled && Rule.Valid)
                        RulesL.Add(Rule);
                }
                catch (Exception Ex)
                {
                    Debug.WriteLine(Ex.Message);
                }
                return true;
            }
        }

        // class for the dimensions document entry in the pcbdoc file
        class Dimensions : PcbDocEntry
        {
            public Dimensions(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }

            public override bool ProcessLine(string line)
            {
                if (line.Substring(0, 1) != "|")
                    return false;
                Dimension Dimension = new Dimension(line); //, l);
                DimensionsL.Add(Dimension);
                return true;
            }
        }

        // class for the arcs document entry in the pcbdoc file
        class Arcs : PcbDocEntry
        {
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            struct ArcStruct
            {
                public byte type;         //
                public UInt32 next;       // 
                public byte Layer;        // 0
                public Int16 u0;          // 1
                public Int16 net;         // 3
                public Int16 u1;          // 5
                public Int16 Component;   // 7
                public Int32 u2;          // 9
                public Int32 X1;          // 13
                public Int32 Y1;          // 17
                public Int32 Radius;      // 21
                public double StartAngle; // 25
                public double EndAngle;   // 33
                public Int32 Width;       // 41	
            };

            // record length 57
            public Arcs(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
                Binary_size = 57;
            }

            public override bool ProcessLine(byte[] line)
            {
                double StartAngle, EndAngle, Radius, Width;
                Layers Layer;
                double X1, Y1;
                Int16 Component;
                Int16 net;

                ArcStruct a = ByteArrayToStructure<ArcStruct>(line);
                Layer      = (Layers)a.Layer;
                net        = a.net;
                net++;
                Component  = a.Component;
                X1         = Math.Round((double)a.X1 * 25.4 / 10000000 - originX, Precision);
                Y1         = Math.Round((double)a.Y1 * 25.4 / 10000000 - originY, Precision);
                Radius     = Math.Round((double)a.Radius * 25.4 / 10000000, Precision);
                StartAngle = a.StartAngle;
                EndAngle   = a.EndAngle;
                Width      = (double)(a.Width * 25.4 / 10000000);

                bool InComponent = Component != -1;
                double Angle;

                if (EndAngle < StartAngle)
                    EndAngle += 360;

                Angle        = Math.Round(-(EndAngle - StartAngle),Precision);
                double X     = Math.Round(X1 + Radius * Math.Cos(StartAngle * Math.PI / 180), Precision);
                double Y     = Math.Round(Y1 + Radius * Math.Sin(StartAngle * Math.PI / 180), Precision);
                string layer = Brd.GetLayer(Layer);

                if (!InComponent)
                {
                    if (net > 0 && Brd.IsCopperLayer(Layer))
                    {
                        // we have an arc track on a copper layer and it has a net
                        // these aren't supported by KiCad yet so generate a curve out of track segments
                        // first normalise it so that the centre is at 0,0
                        // save the centre point
                        double XC = X1;
                        double YC = Y1;

                        X = Math.Round(X - XC,Precision);
                        Y = Math.Round(Y - YC, Precision);

                        double radius = Math.Sqrt(X * X + Y * Y);
                        // start angle in radians
                        double start_angle = StartAngle * Math.PI / 180;
                        double end_angle = EndAngle * Math.PI / 180;
                        double X2 = Math.Round(Radius * Math.Cos(end_angle),Precision);
                        double Y2 = Math.Round(Radius * Math.Sin(end_angle),Precision);
                        X = Math.Round(Radius * Math.Cos(start_angle), Precision);
                        Y = Math.Round(Radius * Math.Sin(start_angle), Precision);

                        for (double angle = start_angle; angle<end_angle; angle += 2*Math.PI/72)
                        {
                            X1 = Math.Round(Radius * Math.Cos(angle),Precision);
                            Y1 = Math.Round(Radius * Math.Sin(angle),Precision);
                            tracks += ($"  (segment (start {XC+X} {-(YC+Y)}) (end {XC+X1} {-(YC+Y1)}) (width {Width}) (layer {layer}) (net {net}))\n");
                            X = X1;
                            Y = Y1;
                        }
                        // do last segment
                        if (X != X2 || Y != Y2)
                        {
                            tracks += ($"  (segment (start {X + XC} {-(Y + YC)}) (end {X2 + XC} {-(Y2 + YC)}) (width {Width}) (layer {layer}) (net {net}))\n");
                        }
                    }
                    else
                    {
                        // only add if not part of board outline
                        if((layer!="Edge.Cuts") || !Brd.CheckExistingArc(X1,Y1,X,Y,Angle))
                            arcs += $"  (gr_arc (start {X1} {-Y1}) (end {X} {-Y}) (angle {Angle}) (layer {layer}) (width {Width}))\n";
                    }
                }
                else
                {
                    Arc Arc = new Arc(X1, Y1, X, Y, Angle, Brd.GetLayer(Layer), Width);
                    ModulesL[Component].Arcs.Add(Arc);
                }
                return true;
            }
        }

        // class for the pads document entry in the pcbdoc file
        class Pads : PcbDocEntry
        {
            // Layout of memory after the pad name string
            // normally 141 bytes long but if it is a surface mount pad and it has a hole shape which is not round
            // or it has the plated attribute set then the record size becomes 737 for some bizarre reason
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            unsafe struct PadStruct
            {
                public fixed byte U0[23];       //  0  23 ???
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
                public fixed byte  U11[3];      // 126 7 ??? 
                public int HoleRotation;        // 129 4 hole rotation
                public short JumperID;          // 133 2 jumper ID
                public fixed byte U12[6];       // 135 6 ???
                public fixed int MidLayerXSize[29]; // 141 29*4 Midlayers 2-30
                public fixed int MidLayerYSixe[29]; // 257 29*4 MidLayers 2-30

            }

            // just to be awkward records not same size varies with name of pad
            // entry header looks like 02 xx xx xx xx yy where yy = (byte)(xxxxxxxx) e.g. 02 03 00 00 00 02
            public Pads(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
                Binary_size = 1;
            }

            public override bool ProcessBinaryFile(byte[] data)
            {
                if (Binary_size == 0)
                    return false;

                ;

                try
                {
                    using (MemoryStream ms = new MemoryStream(data))
                    {
                        UInt32 pos = 0;
                        long size = ms.Length;

                        BinaryReader br = new BinaryReader(ms, System.Text.Encoding.UTF8);
                        ms.Seek(pos, SeekOrigin.Begin);
                        while (pos < size)
                        {
                            long p = pos;
                            ms.Seek(pos, SeekOrigin.Begin);
                            byte recordtype = br.ReadByte(); // should be 2
                            UInt32 next = br.ReadUInt32();
                            uint l = br.ReadByte(); // string length
                            uint headersize = 6 + l;
                            string name = new string(br.ReadChars((int)l));
                            // we are now pointing to the pad record
                            //
                            Layers Layer;
                            byte shape;
                            bool plated;
                            byte UsePasteMaskRules;
                            byte UseSolderMaskRules;
                            double X, Y, XSize, YSize, HoleSize; //, MIDXSize, MIDYSize, BottomXSize, BottomYSize;
                            double Rotation = 0;
                            double PasteMaskExpansion;
                            double SolderMaskExpansion;
                            Int16 Component;
                            Int16 Net;
                            Int16 JumperID;
                            byte[] bytes = br.ReadBytes(141);  // read the number of bytes found in a normal record
                            pos = (uint)ms.Position;

                            // let's use some "unsafe" stufF
                            PadStruct pad = ByteArrayToStructure<PadStruct>(bytes);

                            Layer               = (Layers)pad.Layer;
                            Net                 = pad.Net;
                            Component           = pad.Component;
                            X                   = ToMM(pad.X) - originX;
                            Y                   = ToMM(pad.Y) - originY;
                            XSize               = ToMM(pad.XSize);
                            YSize               = ToMM(pad.YSize);
                            HoleSize            = ToMM(pad.HoleSize);
                            shape               = pad.TopShape;
                            Rotation            = pad.Rotation;
                            plated              = pad.Plated != 0;
                            PasteMaskExpansion  = ToMM(pad.PasteMaskExpansion);
                            SolderMaskExpansion = ToMM(pad.SolderMaskExpansion);
                            UsePasteMaskRules   = pad.UsePasteMaskRules;
                            UseSolderMaskRules  = pad.UseSolderMaskRules;
                            JumperID            = pad.JumperID;

                            bool InComponent = Component != -1;

                            string[] shapes = { "unknown", "circle", "rect", "octagonal", "rounded" };
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
                                    name = "\"\"";
                                }
                            }
                            else
                                type = "smd";
                            string layer = Brd.GetLayer(Layer);
                            if (type == "smd")
                                layer += layer == "F.Cu" ? " F.Mask F.Paste" : (layer == "B.Cu") ? " B.Mask B.Paste" : "";
                            else
                                layer += " *.Mask";

                            if(XSize < HoleSize | YSize < HoleSize)
                            {
                                XSize = YSize = HoleSize;
                            }

                            if (!InComponent)
                            {
                                Pad Pad = new Pad(name, type, shapes[shape % 4], X, Y, Rotation, XSize, YSize, HoleSize, layer, Net + 1);
                                PadsL.Add(Pad);
                            }
                            else
                            {
                                Pad Pad = new Pad(name, type, shapes[shape % 4], X, Y, Rotation, XSize, YSize, HoleSize, layer, Net + 1);
                                ModulesL[Component].Pads.Add(Pad);
                            }
                            
                            // now check for abnormal record
                            // e.g. smd pad with plated set or holeshape not round
                            if((pos < size) && (br.ReadByte() != 2))
                            {
                                pos += (737 - 141);
                            }
                        }
                    }
                }
                catch(Exception Ex)
                {
                    Debug.WriteLine(Ex.Message);
                }

                return true;
            }
        }

        // class for the vias document entry in the pcbdoc file
        class Vias : PcbDocEntry
        {
            // record size 208
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            unsafe struct ViaStruct
            {
                public byte  Type;
                public UInt32 Offset;
                public byte  StartLayer;             //  0 1 start layer
                public byte  EndLayer;               //  1 1 end layer
                public byte  U0;                     //  2 1 ???
                public Int16 net;                    //  3 2 net
                public Int16 U1;                     //  5 2 ???
                public Int16 Component;              //  7 2 component
                public Int16 U2;                     //  9 2 ???
                public Int16 U3;                     //  11 2 ???
                public Int32 X;                      //  13 4 X
                public Int32 Y;                      //  17 4 Y
                public Int32 Width;                  //  21 4 Width
                public Int32 Hole;                   //  25 4 Hole
                public fixed byte U4[75-29];         //  28 46 ???
                public fixed Int32 LayerSizes[32];   // pad size on layers top, mid1,...mid30, bottom
            }

            public Vias(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
                Binary_size = 208;
            }

            public override bool ProcessLine(byte[] line)
            {
                Layers StartLayer, EndLayer;
                double X, Y, Width, HoleSize;
                Int16 Component;
                Int16 Net;
                ViaStruct via = ByteArrayToStructure<ViaStruct>(line);

                StartLayer = (Layers)via.StartLayer;
                EndLayer = (Layers)via.EndLayer;
                Net = via.net;
                Net++;
                Component = via.Component;
                X         = Math.Round(ToMM(via.X) - originX, Precision);
                Y         = Math.Round(ToMM(via.Y) - originY, Precision);
                Width     = ToMM(via.Width);
                HoleSize  = ToMM(via.Hole);
                bool InComponent = Component != -1;

                if (!InComponent)
                {
                    Via Via = new Via(X, Y, Width, HoleSize, Net);
                    ViasL.Add(Via);
                }
                else
                {
                    // can't have vias in components in Kicad (yet) so add as a pad
                    Pad Pad = new Pad("0", "thru_hole", "circle", X, Y, 0, Width, Width, HoleSize, "*.Cu", Net);
                    ModulesL[Component].Pads.Add(Pad);
                }
                return true;
            }
        }

        // class for the tracks document entry in the pcbdoc file
        class Tracks : PcbDocEntry
        {
            // record length 46
            public Tracks(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
                Binary_size = 46;
            }

            public override bool ProcessLine(byte[] line)
            {
                double X1, Y1, X2, Y2, width;
                int net;
                Layers layer;
                bool InComponent = false;
                Int16 component = 0;
                net = B2UInt16(line, 3 + 5);
                if (net == 0x0000FFFF)
                    net = 0;
                else
                    net++;
                component = B2Int16(line, 7 + 5);
                InComponent = (component != -1);
                X1 = Math.Round(ToMM(line, 13 + 5)-originX, Precision);
                Y1 = Math.Round(ToMM(line, 17 + 5)-originY, Precision);
                X2 = Math.Round(ToMM(line, 21 + 5)-originX, Precision);
                Y2 = Math.Round(ToMM(line, 25 + 5)-originY, Precision);
                width = Math.Round(ToMM(line, 29 + 5), Precision);

                layer = (Layers)line[5];
                string Layer = Brd.GetLayer(layer);
                int ComponentNumber = 0;
                if (InComponent)
                {
                    // belongs to a component definition
                    ComponentNumber = component;
                }
                // check for and reject very short tracks
                if((Math.Abs(X1-X2)<0.001) && (Math.Abs(Y1-Y2)<0.001))
                {
                    Console.Error.WriteLine($"Zero length track rejected at X1={X1} Y1={Y1} X2={X2} y2={Y2} ");
                    return true;
                }
                if (!InComponent)
                {
                    if (net == 0)
                    {
                        if (!Brd.OnInnerLayer(layer))
                        {
                            if ((Layer != "Edge.Cuts") || !Brd.CheckExistingLine(X1, -Y1, X2, -Y2))
                                tracks += ($"  (gr_line (start {X1} {-Y1}) (end {X2} {-Y2}) (width {width}) (layer {Layer}))\n");
                        }
                    }
                    else
                    {
                        tracks += ($"  (segment (start {X1} {-Y1}) (end {X2} {-Y2}) (width {width}) (layer {Layer}) (net {net}))\n");
                    }
                    //                    }
                }
                else
                {
                    Line Line = new Line(X1, Y1, X2, Y2, layer, width);
                    ModulesL[ComponentNumber].Lines.Add(Line);
                }
                return true;
            }
        }

        // class for the texts document entry in the pcbdoc file
        class Texts : PcbDocEntry
        {
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            unsafe struct Text
            {
                public byte Type;                        //   0 1 type should be 5
                public UInt32 FontLen;                   //   1 4 FontLen
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
                if (name[0] == '.')
                {
                    // this is a special string so reinterpret it
                    switch (name.ToLower())
                    {
                        case ".layer_name": name = layer; break;
                        case ".designator": if((Component != -1)&&(ModulesL[Component].Designator!=null)) name = ModulesL[Component].Designator; break;
                        case ".comment": if ((Component != -1)&& (ModulesL[Component].Designator != null)) name = ModulesL[Component].Comment; break;
                        default: break;
                    }
                }
                return name;
            }


            public Texts(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
                Binary_size = 1; // variable record length
            }

            public override bool ProcessBinaryFile(byte[] data)
            {

                if (Binary_size == 0)
                    return false;

                List<UInt32> Headers = new List<UInt32>();

                try
                {
                    using (MemoryStream ms = new MemoryStream(data))
                    {
                        long p = 0;
                        long size = ms.Length;

                        BinaryReader br = new BinaryReader(ms, System.Text.Encoding.UTF8);
                        while (p < size)
                        {
                            byte[] bytes = br.ReadBytes(0xEC);  // read the number of bytes up to actual text
                            p = (uint)ms.Position;
                            // map text structure to the bytes
                            Text text = ByteArrayToStructure<Text>(bytes);

                            Layers Layer        = (Layers)text.Layer;
                            Int16  Component    = text.Component;
                            bool   InComponent  = Component != -1;
                            double X            = Math.Round(ToMM(text.X) - originX, Precision);
                            double Y            = Math.Round(ToMM(text.Y) - originY, Precision);
                            double Height       = ToMM(text.Height);
                            double Rotation     = text.Rotation % 360; // altium seem to store 0 as 360 quite a lot!
                            bool   Mirror       = text.Mirror != 0;
                            double Thickness    = ToMM(text.Thickness);
                            bool   IsComment    = text.IsComment != 0;
                            bool   IsDesignator = text.IsDesignator != 0;
                            bool   TrueType     = text.TrueType != 0;
                            UInt32 TextLen      = text.StrLen;

                            byte[] textbytes = br.ReadBytes((int)TextLen);
                            p = ms.Position;

                            string str = Encoding.UTF8.GetString(textbytes, 0, (int)TextLen);
                            string layer = Brd.GetLayer(Layer);

                            str = ConvertSpecialStrings(str, Component, layer);
                            if (TrueType)
                            {
                                Thickness = Height / 10;    // fudge to get width the same as stroke font
                                Height = Height / 1.86;     // fudge to get height the same as stroke font
                            }

                            if (!InComponent)
                            {
                                double Angle = (90 - Rotation) * Math.PI / 180;
                                double X1 = Height / 2 * Math.Cos(Angle);
                                double Y1 = Height / 2 * Math.Sin(Angle);

                                texts += $"  (gr_text \"{str}\" (at {Math.Round(X-X1, Precision)} {-Math.Round(Y+Y1, Precision)} {Rotation})  (layer {layer}) (effects (font (size {Height} {Height}) (thickness {Thickness})) (justify left {(Mirror ? "mirror" : "")})))\n";
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
                                    if (str.Contains('_'))
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

                                    if (str.Contains('_'))
                                    {
                                        // should only do this if Board-DESIGNATORDISPLAYMODE=1
                                        // get the seperator from the project file (.PrjPCB)
                                        // under ChannelRoomLevelSeperator
                                        // Virtual designator TODO get this from board info
                                        str = str.Substring(0, str.IndexOf('_'));
                                    }
                                    Mod.Comment = str;
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
                    Debug.WriteLine(Ex.Message);
                }

                return true;
            }



            public override bool ProcessLine(string line)
            {
                return base.ProcessLine(line);
            }
        }

        // class for the fills document entry in the pcbdoc file
        class Fills : PcbDocEntry
        {
            Int16 Net;
            string NetName;
            double X1, Y1, X2, Y2;
            double Rotation;
            Int16 Component;
            bool InComponent;
            Layers Layer;
            int Locked;
            int Keepout;

            public Fills(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
                //record length 47
                Binary_size = 47;
            }

            public override bool ProcessLine(byte[] record)
            {
                using (MemoryStream ms = new MemoryStream(record))
                {
                    // Use the memory stream in a binary reader.
                    using (BinaryReader br = new BinaryReader(ms))
                    {
                        ms.Seek(0+5, SeekOrigin.Begin);
                        Layer = (Layers)br.ReadByte(); // line offset 0
                        Locked = br.ReadByte();        // line offset 1
                        Keepout = (int)br.ReadByte();  // line offset 2

                        ms.Seek(3 + 5, SeekOrigin.Begin);
                        Net = br.ReadInt16();
                        Net += 1;
                        NetName = $"\"{NetsL[Net].Name}\"";
                        ms.Seek(7 + 5, SeekOrigin.Begin);
                        Component = br.ReadInt16();
                        if (Component != -1)
                            InComponent = true;
                        ms.Seek(13 + 5, SeekOrigin.Begin);
                        X1 = Math.Round(Bytes2mm(br.ReadBytes(4)) - originX, Precision);
                        ms.Seek(17 + 5, SeekOrigin.Begin);
                        Y1 = Math.Round(Bytes2mm(br.ReadBytes(4)) - originY, Precision);
                        ms.Seek(21 + 5, SeekOrigin.Begin);
                        X2 = Math.Round(Bytes2mm(br.ReadBytes(4)) - originX, Precision);
                        ms.Seek(25 + 5, SeekOrigin.Begin);
                        Y2 = Math.Round(Bytes2mm(br.ReadBytes(4)) - originY, Precision);
                        ms.Seek(29 + 5, SeekOrigin.Begin);
                        Rotation = br.ReadDouble();
                    }
                    if(Keepout == 2)
                    {
                        Point2D p1 = new Point2D(X1, Y1);
                        Point2D p2 = new Point2D(X2, Y2);
                        Point2D c = new Point2D(X1+(X2-X1)/2, Y1+(Y2-Y1)/2);
                        if(InComponent)
                        {
                            // need to factor in component's rotation
                            double rot = ModulesL[Component].Rotation;
                            p1 = p1.Rotate(c, rot);
                            p2 = p2.Rotate(c, rot);
                        }
                        string layer = "";
                        if (Layer == Layers.Keepout_Layer)
                            layer = "*.Cu";
                        else
                            layer = Brd.GetLayer(Layer);

                        // generate a keepout
                        keepouts += 
$@"
    (zone(net 0)(net_name """")(layers {layer})(tstamp 0)(hatch edge 0.508)
      (connect_pads(clearance 0.508))
      (min_thickness 0.254)
      (keepout(tracks not_allowed)(vias not_allowed)(copperpour not_allowed))
      (fill(arc_segments 32)(thermal_gap 0.508)(thermal_bridge_width 0.508))
      (polygon
        (pts
          (xy {p1.X} {-p1.Y})(xy {p2.X} {-p1.Y})(xy {p2.X} {-p2.Y})(xy {p1.X} {-p2.Y})
         )
      )
    )
";

                    }
                    else if (!InComponent) // keepouts not allowed in components (yet)
                    {
                        fills += $"(gr_poly (pts (xy {X1} {-(Y1)}) (xy {X1} {-(Y2)}) (xy {X2} {-(Y2)}) (xy {X2} {-(Y1)})) (layer {Brd.GetLayer(Layer)}) (width 0))\n";
                    }
                    else
                    {
                        Fill Fill = new Fill(X1, Y1, X2, Y2, Brd.GetLayer(Layer), GetNetName(Net));
                        if (Component < ModulesL.Count)
                            ModulesL[Component].Fills.Add(Fill);
                    }
                    return true;
                }
            }
        }

        // class for the differential pairs document entry in the pcbdoc file
        class DifferentialPairs : PcbDocEntry
        {
            public DifferentialPairs(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }

            public override bool ProcessLine(string line)
            {
                return base.ProcessLine(line);
            }
        }

        // class for region objects
        class Region : Object
        {
            List<Point> Points;
            int Net_no { get; set; }
            string Net_name { get; set; }
            string Layer { get; set; }
            bool InComponent { get; set; }
            public bool Keepout { get; set; }

            public void AddPoint(double X, double Y)
            {
                Point Point = new Point(X, Y);
                Points.Add(Point);
            }

            private Region()
            {

            }

            public Region(string layer, int net, bool keepout)
            {
                Layer = layer;
                Net_no = net;
                Net_name = GetNetName(Net_no);
                Points = new List<Point>();
                Keepout = keepout;
            }

            public override string ToString()
            {
                string ret = "";
                string connectstyle = "";

                double clearance = GetRuleValue("Clearance", "PolygonClearance");
                if (Layer.Substring(0, 2) == "In")
                {
                    // this is an inner layer so use plane clearance
                    clearance = GetRuleValue("PlaneClearance", "PlaneClearance");
                }

                if (!Keepout)
                    Layer = "Dwgs.User";
                ret = $"  (zone (net {Net_no}) (net_name {Net_name}) (layer {Layer}) (tstamp 0) (hatch edge 0.508)";
                ret += $"    (priority 100)\n";
                ret += $"    (connect_pads {connectstyle} (clearance {clearance}))\n"; // TODO sort out these numbers properly
                ret += $"    (min_thickness 0.2)\n";
                if (Keepout)
                    ret += "(keepout(copperpour not_allowed))\n";
                ret += "    (fill yes (arc_segments 16) (thermal_gap 0.2) (thermal_bridge_width 0.3))\n";
                var i = 0;
                ret += "    (polygon (pts\n        ";
                foreach (var Point in Points)
                {
                    i++;
                    if ((i % 5) == 0)
                        ret += "\n        ";
                    ret += Point.ToString();
                }
                ret += "\n      )\n    )\n  )\n";

                return ret;
            }

            // inside component region
            // presently this is not allowed (V5.1.2)
            public override string ToString(double x, double y, double ModuleRotation)
            {
                string ret = "";

                double clearance = GetRuleValue("Clearance", "PolygonClearance");
                if (Layer.Substring(0, 2) == "In")
                {
                    // this is an inner layer so use plane clearance
                    clearance = GetRuleValue("PlaneClearance", "PlaneClearance");
                }

                /*
                    string connectstyle = ""; // TODO get connect style from rules
                    ret = $"  (zone (net {net_no}) (net_name {net_name}) (layer {Layer}) (tstamp 0) (hatch edge 0.508)";
                    ret += $"    (priority 100)\n";
                    ret += $"    (connect_pads {connectstyle} (clearance {clearance}))\n"; // TODO sort out these numbers properly
                    ret += $"    (min_thickness 0.2)\n";
                    ret += "    (fill yes (arc_segments 16) (thermal_gap 0.2) (thermal_bridge_width 0.3))\n";
                 */
                var i = 0;
                ret += "    (fp_poly (pts\n        ";
                foreach (var Point in Points)
                {
                    i++;
                    if ((i % 5) == 0)
                        ret += "\n        ";
                    ret += Point.ToString(x, y, ModuleRotation);
                }
                //                ret += "\n      )\n    )\n  )\n";
                ret += $" ) ( layer {Brd.GetLayer(Layer)}) (width 0)\n    )\n";

                return ret;
            }
        }

        // class for the regions document entry in the pcbdoc file
        class Regions : PcbDocEntry
        {
            class Point
            {
                private readonly double X;
                private readonly double Y;

                Point()
                {
                }

                public Point(double x, double y)
                {
                    X = Math.Round(ToMM(x) - originX, Precision);
                    Y = Math.Round(ToMM(y) - originY, Precision);
                }
            }

            private readonly List<Point> Points;

            // variable length records
            public Regions(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
                Binary_size = 1;
                Points = new List<Point>();
            }

            public override bool ProcessLine(byte[] record)
            {
                Layers Layer;
                Int16  Component;
                bool   InComponent;

                using (MemoryStream ms = new MemoryStream(record))
                {
                    // Use the memory stream in a binary reader.
                    using (BinaryReader br = new BinaryReader(ms))
                    {
                        try
                        {
                            ms.Seek(0, SeekOrigin.Begin);
                            Layer = (Layers)br.ReadByte();
                            ms.Seek(2, SeekOrigin.Begin);
                            bool Keepout = br.ReadByte() != 0;
                            ms.Seek(3, SeekOrigin.Begin);
                            int net = (int)br.ReadInt16();
                            ms.Seek(7, SeekOrigin.Begin);
                            Component = br.ReadInt16();
                            InComponent = Component != -1;
                            ms.Seek(0x12, SeekOrigin.Begin);
                            int strlen = br.ReadInt32();
                            ms.Seek(0x16, SeekOrigin.Begin);
                            byte[] bytes = br.ReadBytes(strlen);
                            string str = ConvertToString(bytes);
                            ms.Seek(0x16 + strlen, SeekOrigin.Begin);
                            Int32 DataLen = br.ReadInt32();
                            string l = Brd.GetLayer((Layers)Layer);
                            if (GetString(str, "ISBOARDCUTOUT=") == "TRUE")
                                l = "Edge.Cuts";
                            Region r = new Region(l, net, Keepout);
                            while (DataLen-- > 0)
                            {
                                double X = br.ReadDouble();
                                double Y = br.ReadDouble();
                                r.AddPoint(ToMM(X) - originX, ToMM(Y) - originY);
                            }
                            r.Keepout = Keepout;


                            if (!InComponent)
                            {
                                RegionsL.Add(r);
                            }
                            else
                            {
                                if(!Keepout)
                                    ModulesL[Component].Regions.Add(r);
                                else
                                    // until keepouts are allowed in components
                                    // just add as a board region
                                    RegionsL.Add(r);
                            }
                        }
                        catch (Exception Ex)
                        {
                            Debug.WriteLine(Ex.ToString());
                        }
                    }
                }

                return true;
            }

            public override bool ProcessBinaryFile(byte[] data)
            {

                if (Binary_size == 0)
                    return false;

                List<UInt32> Headers = new List<UInt32>();

                try
                {
                    using (MemoryStream ms = new MemoryStream(data))
                    {
                        long p = 0;
                        long size = ms.Length;
                        BinaryReader br = new BinaryReader(ms, System.Text.Encoding.UTF8);
                        // find the headers
                        while (p < size)
                        {
                            ms.Seek(p + 1, SeekOrigin.Begin);
                            UInt32 RecordLength = br.ReadUInt32();
                            // we are now pointing to the region record
                            //
                            byte[] record = br.ReadBytes((int)RecordLength);
                            p = ms.Position;
                            ProcessLine(record);
                        }
                    }
                }
                catch (Exception Ex)
                {
                    Debug.WriteLine(Ex.Message);
                }
                return true;
            }
        }

        // class for the 3D models document entry in the pcbdoc file
        class Models6 : PcbDocEntry
        { 
            public Models6(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            { 
            }

            public override bool ProcessFile(byte[] data)
            {
                FileInfo file = new System.IO.FileInfo(filename);
                using (MemoryStream ms = new MemoryStream(data))
                {
                    UInt32 pos = 0;
                    long size = ms.Length;

                    UInt32 Component = 0;
                    BinaryReader br = new BinaryReader(ms, System.Text.Encoding.UTF8);
                    while (pos < size)
                    {
                        ms.Seek(pos, SeekOrigin.Begin);
                        uint next = br.ReadUInt32();
                        char[] line = br.ReadChars((int)next);
                        string str = new string(line);
                        Model Model = new Model(str, Component);
                        if (Model != null)
                            Mods.Add(Model);
                        pos += next + 4;
                        Component++;
                    }
                }
                return true;
            }
        }

        class ComponentBodies : PcbDocEntry
        {
            int ComponentNumber;
            int missed;
            public string Layer { get; set; }

/*
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            struct CompBody
            {
                public byte type;         //
                public UInt32 next;       // 
                public byte Layer;        // 0
                public Int16 u0;          // 1
                public Int16 u1;          // 3
                public Int16 u2;          // 5
                public Int16 Component;   // 7
                public fixed byte u3[13]
            };
*/

            // variable entry size
            public ComponentBodies(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
                Binary_size = 1;
            }

            public override bool ProcessBinaryFile(byte[] data)
            {
                if (Binary_size == 0)
                    return false;

                using (MemoryStream ms = new MemoryStream(data))
                {
                    UInt32 pos = 0;
                    long size = ms.Length;
                    BinaryReader br = new BinaryReader(ms, System.Text.Encoding.UTF8);
                    try
                    {
                        ComponentNumber = 0;
                        missed = 0;
                        while (pos < size)
                        {
                            byte layer;
                            ms.Seek(pos, SeekOrigin.Begin);
                            byte type = br.ReadByte();
                            UInt32 next = br.ReadUInt32();
                            layer = br.ReadByte();
                            byte[] dat1 = br.ReadBytes(6);
                            ComponentNumber = br.ReadUInt16();
                            byte[] dat2 = br.ReadBytes(13);
                            ms.Seek(pos+0x17, SeekOrigin.Begin);
                            UInt32 length = br.ReadUInt32();
                            char[] line = br.ReadChars((int)length);
                            string str = new string(line);
                            ProcessLine(str);
                            pos += 5 + next;
                        }
                    }
                    catch(Exception Ex)
                    {
                        Debug.WriteLine(Ex.Message);
                    }
                }
                return true;
            }

            public override bool ProcessLine( string line)
            {
                ComponentBody ComponentBody = new ComponentBody(line);
                if (ComponentNumber >= ModulesL.Count)
                {
                    missed++;
                    return false;
                }
                if (ComponentBody != null)
                    ModulesL[ComponentNumber].ComponentBodies.Add(ComponentBody);
                return true;
            }
        }

        class ShapeBasedComponentBodies : PcbDocEntry
        {
            // variable entry size
            public ShapeBasedComponentBodies(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
                Binary_size = 1;
            }

            public override bool ProcessBinaryFile(byte[] data)
            {
                if (Binary_size == 0)
                    return false;

                using (MemoryStream ms = new MemoryStream(data))
                {
                    UInt32 pos = 0;
                    long size = ms.Length;
                    BinaryReader br = new BinaryReader(ms, System.Text.Encoding.UTF8);
                    while (pos < size)
                    {
                        ms.Seek(pos, SeekOrigin.Begin);
                        byte type = br.ReadByte();
                        UInt32 offset = br.ReadUInt32();
                        ms.Seek(pos+5, SeekOrigin.Begin);
                        byte[] line = br.ReadBytes((int)offset);
                        try
                        {
                            ProcessLine(line);
                        }
                        catch (Exception Ex)
                        {
                            Debug.WriteLine(Ex.Message);
                        };
                        pos += 5+offset;
                    }
                }

                return true;

            }

            public override bool ProcessLine(byte[] line)
            {
                Int16 ComponentNumber = 0;
                using (MemoryStream ms = new MemoryStream(line))
                {
                    // Use the memory stream in a binary reader.
                    using (BinaryReader br = new BinaryReader(ms))
                    {
                        ms.Seek(7, SeekOrigin.Begin);
                        ComponentNumber = br.ReadInt16();
                        ms.Seek(0x12, SeekOrigin.Begin);
                        UInt32 strlen = br.ReadUInt32();
                        char[] chrs = br.ReadChars((int)strlen);
                        string str = new string(chrs);
                        UInt32 DataLen = br.ReadUInt32();
                        byte[] data = br.ReadBytes((int)DataLen);
                        string Data = Encoding.UTF8.GetString(data, 0, data.Length);
                        ShapeBasedModel Model = new ShapeBasedModel(str);
                        if ((Model != null) && (ComponentNumber != -1))
                            ModulesL[ComponentNumber].ShapeBasedModels.Add(Model);
                        //ShapeBasedMods.Add(Model);
                    }
                }
                return true;
            }
        }

        // class for the embedded fonts document entry in the pcbdoc file (not implemented)
        class EmbeddedFonts : PcbDocEntry
        {
            public EmbeddedFonts(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }
        }

        // class for the shape based regions document entry in the pcbdoc file (not implemented)
        class ShapeBasedRegions : PcbDocEntry
        {
            public ShapeBasedRegions(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }
        }

        // class for the connections document entry in the pcbdoc file (not implemented)
        class Connections : PcbDocEntry
        {
            public Connections(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }
        }

        // class for the coordinates document entry in the pcbdoc file (not implemented)
        class Coordinates : PcbDocEntry
        {
            public Coordinates(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }
        }

        // class for the embeddeds document entry in the pcbdoc file (not implemented)
        class Embeddeds : PcbDocEntry
        {
            public Embeddeds(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }
        }

        // class for the embedded boards document entry in the pcbdoc file (not implemented)
        class EmbeddedBoards : PcbDocEntry
        {
            public EmbeddedBoards(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }
        }

        // class for the fromtos document entry in the pcbdoc file
        class FromTos : PcbDocEntry
        {
            public FromTos(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {

            }

            public override bool ProcessLine(string line)
            {
                return base.ProcessLine(line);
            }
        }

        // class for the modelsnoembeds document entry in the pcbdoc file (not implemented)
        class ModelsNoEmbeds : PcbDocEntry
        {
            public ModelsNoEmbeds(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }
        }

        // class for the textures document entry in the pcbdoc file (not implemented)
        class Textures : PcbDocEntry
        {
            public Textures(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }
        }

        static List<PcbDocEntry> PcbObjects;

        // class for the 3D models
        class Model
        {
            public string FileName { get; set; }
            public string ID       { get; set; }
            private readonly UInt32 Component;
            private readonly string Checksum;
            private readonly double ROTX, ROTY, ROTZ, DZ;

            Model()
            {
            }

            public Model(string line, UInt32 Number)
            {
                FileName  = GetString(line, "NAME=");
                ID        = GetString(line, "ID=");
                Component = Number;
                Checksum  = GetString(line, "CHECKSUM=");
                // rename file to include the checksum
                FileName  = System.IO.Path.GetFileNameWithoutExtension(FileName);
                // append the checksum to cater for same name models with different contents
                FileName += $"{{{Checksum}}}.step";
                ROTX      = GetDouble(GetString(line, "ROTX="));
                ROTY      = GetDouble(GetString(line, "ROTY="));
                ROTZ      = GetDouble(GetString(line, "ROTZ="));
                DZ        = GetDouble(GetString(line, "DZ="));
            }
        }

        // class for a shape based 3d model
        // creation of a .step file equivalent or .wrl not implemented
        // any volunteers?
        class ShapeBasedModel : Object
        {
            public string ID             { get; set; }
            public string Checksum       { get; set; }
            public double X              { get; set; }
            public double Y              { get; set; }
            public double ROTX           { get; set; }
            public double ROTY           { get; set; }
            public double ROTZ           { get; set; }
            public double DZ             { get; set; }
            public double MinZ           { get; set; }
            public double MaxZ           { get; set; }
            public string Type           { get; set; } // 0 = extruded 1 = Cone 2 = Cylinder 3 = sphere
            public double Rotation       { get; set; }
            public double CylinderRadius { get; set; }
            public double CylinderHeight { get; set; }
            public double SphereRadius   { get; set; }
            private readonly string Line;
            public string Identifier;
            public UInt32 Colour;
            public double Opacity;

            ShapeBasedModel()
            {
            }

            public ShapeBasedModel(string line)
            {
                Line = line;
                try
                {
                    CylinderRadius = 0;
                    CylinderHeight = 0;
                    SphereRadius = 0;
                    MinZ = 0;
                    MaxZ = 0;

                    ID = GetString(line, "ID=");
                    Checksum = GetString(line, "CHECKSUM=");
                    string Id = GetString(line, "IDENTIFIER=");
                    string[] chars = Id.Split(',');
                    Colour = GetUInt32(GetString(line,"BODYCOLOR3D="));
                    Opacity = GetDouble(GetString(line,"BODYOPACITY3D="));

                    ROTX = GetDouble(GetString(line, "MODEL.3D.ROTX="));
                    ROTY = GetDouble(GetString(line, "MODEL.3D.ROTY="));
                    ROTZ = GetDouble(GetString(line, "MODEL.3D.ROTZ="));
                    DZ = GetDouble(GetString(line, "MODEL.3D.DZ="));
                    Rotation = GetDouble(GetString(line, "MODEL.2D.ROTATION="));
                    X = GetNumberInMM(GetString(line, "MODEL.2D.X="));
                    Y = GetNumberInMM(GetString(line, "MODEL.2D.Y="));
                    Type = GetString(line, "MODEL.MODELTYPE=");
                    switch (Type)
                    {
                        case "0": // Extruded
                            {
                                MinZ = GetNumberInMM(GetString(line, "MODEL.EXTRUDED.MINZ="));
                                MaxZ = GetNumberInMM(GetString(line, "MODEL.EXTRUDED.MAXZ="));
                            }
                            break;
                        case "1": // TODO ????
                            {

                            }
                            break;
                        case "2": // Cylinder
                            {
                                CylinderRadius = GetNumberInMM(GetString(line, "MODEL.CYLINDER.RADIUS="));
                                CylinderHeight = GetNumberInMM(GetString(line, "MODEL.CYLINDER.HEIGHT="));
                            }
                            break;
                        case "3": // Sphere
                            {
                                SphereRadius = GetNumberInMM(GetString(line, "MODEL.SPHERE.RADIUS="));
                            }
                            break;
                        default: break;
                    }
                    Identifier = "";
                    if (Id != "")
                    {
                        for (var i = 0; i < chars.Length; i++)
                        {
                            Int32 res = Convert.ToInt32(chars[i]);
                            Identifier += Convert.ToChar(res);
                        }
                    }
                }
                catch (Exception Ex)
                {
                    Debug.WriteLine(Ex.Message);
                }
            }

            public override string ToString(double x, double y, double ModuleRotation)
            {
                return $"# ID {ID} X={X + x} Y={Y + y} Rotation={Rotation + ModuleRotation} ROTX={ROTX} ROTY={ROTY} ROTZ={ROTZ} Type={Type}\n#{Line}\n";
            }
        }

        class ShapeBasedModels
        {
            public List<ShapeBasedModel> ShapeBasedMods;

            public ShapeBasedModels()
            {
                ShapeBasedMods = new List<ShapeBasedModel>();
            }

            public void Add(ShapeBasedModel Model)
            {
                if (Model != null)
                    ShapeBasedMods.Add(Model);
            }

        }

        static ShapeBasedModels ShapeBasedMods;

        class Models
        {
            public List<Model> Mods;

            public Models()
            {
                Mods = new List<Model>();
            }

            public void Add(Model Model)
            {
                if (Model != null)
                    Mods.Add(Model);
            }

            // get the model filename
            public string GetFilename(string ID)
            {
                foreach (var Mod in Mods)
                {
                    if (Mod.ID == ID)
                        return Mod.FileName;
                }
                return "";
            }
        }

        static Models Mods;

        static bool ProcessModelsFile(string filename)
        {
            FileInfo file = new System.IO.FileInfo(filename);
            long size = new System.IO.FileInfo(filename).Length;

            using (FileStream fs = file.OpenRead())
            {
                UInt32 pos = 0;
                UInt32 Component = 0;
                BinaryReader br = new BinaryReader(fs, System.Text.Encoding.UTF8);
                while (pos < size)
                {
                    fs.Seek(pos, SeekOrigin.Begin);
                    uint next = br.ReadUInt32();
                    char[] line = br.ReadChars((int)next);
                    string str = new string(line);
                    Model Model = new Model(str, Component);
                    try
                    {
                        string f = Model.FileName;
                        if (!File.Exists(f))
                            // rename the file
                            File.Move($"{Component}.step", f);
                        else
                            File.Delete($"{Component}.step");
                    }
                    catch(Exception Ex)
                    {
                        Debug.WriteLine(Ex.Message);
                    }
                    pos += next+4;
                    Component++;
                }
            }
            return true;
        }

        static bool MakeDir(string name)
        {
            if (!Directory.Exists(name))
            {
                // create the library directory
                DirectoryInfo info = Directory.CreateDirectory(name);
                if (!info.Exists)
                {
                    Console.WriteLine($@"failed to create directory ""{name}""");
                    return false;
                }
            }
            return true;
        }

        static void ProcessPcbObject(CompoundFile cf, PcbDocEntry Object)
        {
            CFStorage storage = cf.RootStorage.GetStorage(Object.FileName);
            if (MakeDir(storage.Name))
            {
                Directory.SetCurrentDirectory(@".\" + storage.Name);

                if (storage.Name == "Models")
                {
                    // extract the model files to 0.dat,1.dat...
                    // this is called for each of the directory entries
                    void vs(CFItem it)
                    {
                        // write all entries (0,1,2..n,Data) to files (0.dat,1.dat...)
                        if (it.Name != "Header")
                        {
                            using (System.IO.BinaryWriter file = new System.IO.BinaryWriter(File.OpenWrite(it.Name + ".dat")))
                            {
                                // get file contents and write to file
                                CFStream stream = it as CFStream;

                                byte[] temp = stream.GetData();
                                try
                                {
                                    // Writer raw data                
                                    file.Write(temp);
                                    file.Flush();
                                    file.Close();
                                }
                                catch
                                {
                                }
                                string filename = it.Name + ".dat";
                                if (filename != "Data.dat")
                                {
                                    // uncompress the x.dat file to a .step file
                                    // step file is renamed to it's actual name later in the process
                                    byte[] compressed = File.ReadAllBytes(filename);
                                    string Inflated = ZlibCodecDecompress(compressed);

                                    File.WriteAllText(it.Name + ".step", Inflated);
                                    File.Delete(filename); // no longer need .dat file
                                }
                            }
                        }
                    }

                    // calls the Action delegate for each of the directory entries
                    storage.VisitEntries(vs, true);
                }

                try
                {
                    if (ExtractFiles)
                    {
                        CFStream datastream = storage.GetStream("Data");
                        // get file contents and write to file
                        byte[] temp = datastream.GetData();
                        FileStream F = new FileStream("Data.dat", FileMode.OpenOrCreate, FileAccess.ReadWrite);
                        F.SetLength(0); // prevent old data being reinterpreted i.e. when file is smaller than last time
                        F.Close();
                        using (System.IO.BinaryWriter file = new System.IO.BinaryWriter(File.OpenWrite("Data.dat")))
                        {
                            try
                            {
                                // Writer raw data                
                                file.Write(temp);
                                file.Flush();
                                file.Close();
                            }
                            catch (Exception Ex)
                            {
                                Debug.WriteLine(Ex.Message);
                            }
                        }
                    }
                    CFStream stream = storage.GetStream("Data");
                    // get file contents and process
                    byte[] data = stream.GetData();
                    if (Object.Binary_size != 0)
                        Object.ProcessBinaryFile(data);
                    else
                        Object.ProcessFile(data);
                }
                catch (Exception Ex)
                {
                    Debug.WriteLine(Ex.Message);
                }

                Object.FinishOff();
                Directory.SetCurrentDirectory(@"..\");
                string Dir = Directory.GetCurrentDirectory();
            }
        }

        static Board Brd;

        static void ClearFolder(string FolderName)
        {
            try
            {
                DirectoryInfo dir = new DirectoryInfo(FolderName);

                foreach (FileInfo fi in dir.GetFiles())
                {
                    fi.Delete();
                }

                foreach (DirectoryInfo di in dir.GetDirectories())
                {
                    // recurse
                    ClearFolder(di.FullName);
                    di.Delete();
                }
            }
            catch (Exception Ex)
            {
                Debug.WriteLine(Ex.ToString());
            }
        }

        unsafe static void Main(string[] args)
        {
            Brd            = new Board();
            ModulesL       = new ObjectList<Module>();
            NetsL          = new ObjectList<Net>();
            PolygonsL      = new ObjectList<Polygon>();
            LinesL         = new ObjectList<Line>();
            PadsL          = new ObjectList<Pad>();
            Strings        = new ObjectList<String>();
            ViasL          = new ObjectList<Via>();
            FillsL         = new ObjectList<Fill>();
            DimensionsL    = new ObjectList<Dimension>();
            RulesL         = new ObjectList<Rule>();
            Mods           = new Models();
            ShapeBasedMods = new ShapeBasedModels();
            RegionsL       = new ObjectList<Region>();

            // start off the nets
            Net NoNet = new Net(0, "");
            NetsL.Add(NoNet);

            for (var i = 0; i < args.Length; i++)
            {
                if (args[i].Substring(0, 1) == "-")
                {
                    // this is a command line option
                    if (args[i] == "-e")
                        ExtractFiles = true;
                    if (args[i] == "-l")
                        CreateLib = true;
                }
                else
                    filename = args[i];
            }

            if(!File.Exists(filename))
            {
                Console.Error.WriteLine($"File {filename} doesn't exist");
                System.Environment.Exit(0);
            }

            if((filename.Length - filename.IndexOf(".pcbdoc" , StringComparison.OrdinalIgnoreCase)) != 7)
            {
                Console.Error.WriteLine($"File {filename} should end in '.pcbdoc'");
                System.Environment.Exit(0);
            }

            int index = filename.IndexOf('.');
            output_filename = filename.Substring(0,index) + ".kicad_pcb";
            if (index == -1)
            {
                Console.Error.WriteLine($"File {filename} is not valid pcb file");
                System.Environment.Exit(0);
            }

            if (filename.Substring(index, filename.Length - index).ToLower() != ".pcbdoc")
            {
                Console.Error.WriteLine($"File {filename} is not valid pcb file");
                System.Environment.Exit(0);
            }

            // Initialise the PcbObjects list
            PcbObjects = new List<PcbDocEntry>
            {
                new FileVersionInfo          ("FileVersionInfo",              "",                         Type.text,   4), // not used
                new Board                    ("Board6",                       "Board",                    Type.text,   4),
                new Rules                    ("Rules6",                       "Rule",                     Type.text,   6),
                new AdvancedPlacerOptions    ("Advanced Placer Options6",     "AdvancedPlacerOptions",    Type.text,   4), // not used
                new DesignRuleCheckerOptions ("Design Rule Checker Options6", "DesignRuleCheckerOptions", Type.text,   4), // not used
                new PinSwapOptions           ("Pin Swap Options6",            "PinSwapOptions",           Type.text,   4), // not used
                new Classes                  ("Classes6",                     "Class",                    Type.text,   4),
                new Nets                     ("Nets6",                        "Net",                      Type.text,   4),
                new Components               ("Components6",                  "Component",                Type.text,   4),
                new Polygons                 ("Polygons6",                    "Polygon",                  Type.text,   4),
                new Dimensions               ("Dimensions6",                  "Embedded",                 Type.text,   6),
                new Arcs                     ("Arcs6",                        "Arc",                      Type.binary, 4),
                new Pads                     ("Pads6",                        "Pad",                      Type.binary, 4),
                new Vias                     ("Vias6",                        "Via",                      Type.binary, 4),
                new Tracks                   ("Tracks6",                      "Track",                    Type.binary, 4),
                new Texts                    ("Texts6",                       "Text",                     Type.binary, 4),
                new Fills                    ("Fills6",                       "Fill",                     Type.binary, 4),
                new DifferentialPairs        ("DifferentialPairs6",           "DifferentialPair",         Type.text,   4),
                new Regions                  ("Regions6",                     "Region",                   Type.binary, 4),
                new Models6                  ("Models",                       "ComponentBody",            Type.text,   4),
                new ComponentBodies          ("ComponentBodies6",             "ComponentBody",            Type.binary, 4),
                new ShapeBasedComponentBodies("ShapeBasedComponentBodies6",   "ComponentBody",            Type.binary, 4)
            /* Not interested in the rest of these
                , 
                new EmbeddedFonts            ("EmbeddedFonts6",               "",                         Type.binary, 4),
                new ShapeBasedRegions        ("ShapeBasedRegions6",           "",                         Type.mixed,  4),
                new Connections              ("Connections6",                 "",                         Type.binary, 4),
                new Coordinates              ("Coordinates6",                 "",                         Type.binary, 4),
                new Embeddeds                ("Embeddeds6",                   "",                         Type.binary, 4),
                new EmbeddedBoards           ("EmbeddedBoards6",              "",                         Type.binary, 4),
                new FromTos                  ("FromTos6",                     "",                         Type.binary, 4),
                new ModelsNoEmbeds           ("ModelsNoEmbed",                "",                         Type.binary, 4),
                new Textures                 ("Textures",                     "",                         Type.binary, 4)
            */
            };

            Brd = (Board)PcbObjects[1]; // has layer stack set up in it now

            CompoundFile cf = new CompoundFile(filename);

            string UnpackDirectory = filename.Substring(0,filename.IndexOf('.')) + "-Kicad";

            UnpackDirectory = UnpackDirectory.Replace('.', '-');
            if (!Directory.Exists(UnpackDirectory))
            {
                // create the output directory
                Directory.CreateDirectory(UnpackDirectory);
            }

            // clear out the directory
            ClearFolder(UnpackDirectory);

            // change to the directory
            Directory.SetCurrentDirectory(".\\" + UnpackDirectory);

            if (MakeDir(cf.RootStorage.Name))
            {
                Directory.SetCurrentDirectory(".\\" + cf.RootStorage.Name);

                foreach( var Object in PcbObjects)
                {
                    ProcessPcbObject(cf, Object);
                }
            }

            // sort out the 3D models
            Directory.SetCurrentDirectory("Models");
            ProcessModelsFile("Data.dat");
            if(!ExtractFiles)
                File.Delete("Data.dat");
            Directory.SetCurrentDirectory(@"..\..");
            try
            {
                if(Directory.Exists("Models"))
                    Directory.Delete("Models", true);
            }
            catch (IOException exp)
            {
                Debug.WriteLine(exp.Message);
            }
            Directory.Move(@"Root Entry\Models", "Models");
            if(!ExtractFiles)
                Directory.Delete("Root Entry", true);
            System.IO.StreamWriter OutFile = new System.IO.StreamWriter(output_filename);

            // TODO find extremes of pcb to select correct page size
            {
                OutFile.WriteLine("(kicad_pcb (version 4) (host pcbnew \"(2014 - 07 - 21 BZR 5016) - product\")");
                OutFile.WriteLine("");
                OutFile.WriteLine("  (general");
                OutFile.WriteLine("    (links 0)");
                OutFile.WriteLine("    (no_connects 0)");
                OutFile.WriteLine("    (area 0 0 0 0)");
                OutFile.WriteLine("    (thickness 1.6)");
                OutFile.WriteLine("    (drawings 0)");
                OutFile.WriteLine($"    (tracks {LinesL.Count})");
                OutFile.WriteLine($"    (zones {PolygonsL.Count})");
                OutFile.WriteLine($"    (modules {ModulesL.Count})");
                OutFile.WriteLine($"    (nets {NetsL.Count})");
                OutFile.WriteLine("  )");
                OutFile.WriteLine("");
                OutFile.WriteLine("  (page A4)");
                OutFile.WriteLine("  (layers");
                // output the layer stack
                OutFile.WriteLine(Brd.ToString());
                OutFile.WriteLine("    (32 B.Adhes user)");
                OutFile.WriteLine("    (33 F.Adhes user)");
                OutFile.WriteLine("    (34 B.Paste user)");
                OutFile.WriteLine("    (35 F.Paste user)");
                OutFile.WriteLine("    (36 B.SilkS user)");
                OutFile.WriteLine("    (37 F.SilkS user)");
                OutFile.WriteLine("    (38 B.Mask user)");
                OutFile.WriteLine("    (39 F.Mask user)");
                OutFile.WriteLine("    (40 Dwgs.User user)");
                OutFile.WriteLine("    (41 Cmts.User user)");
                OutFile.WriteLine("    (42 Eco1.User user)");
                OutFile.WriteLine("    (43 Eco2.User user)");
                OutFile.WriteLine("    (44 Edge.Cuts user)");
                OutFile.WriteLine("    (45 Margin user)");
                OutFile.WriteLine("    (46 B.CrtYd user)");
                OutFile.WriteLine("    (47 F.CrtYd user)");
                OutFile.WriteLine("    (48 B.Fab user)");
                OutFile.WriteLine("    (49 F.Fab user)");
                OutFile.WriteLine("");
                OutFile.WriteLine("  )");
                OutFile.WriteLine("");
                OutFile.WriteLine("  (setup");
                OutFile.WriteLine("    (last_trace_width 0.254)");
                OutFile.WriteLine("    (trace_clearance 0.127)");
                OutFile.WriteLine("    (zone_clearance 0.0144)");
                OutFile.WriteLine("    (zone_45_only no)");
                OutFile.WriteLine("    (trace_min 0.254)");
                OutFile.WriteLine("    (segment_width 0.2)");
                OutFile.WriteLine("    (edge_width 0.1)");
                OutFile.WriteLine("    (via_size 0.889)");
                OutFile.WriteLine("    (via_drill 0.635)");
                OutFile.WriteLine("    (via_min_size 0.889)");
                OutFile.WriteLine("    (via_min_drill 0.508)");
                OutFile.WriteLine("    (uvia_size 0.508)");
                OutFile.WriteLine("    (uvia_drill 0.127)");
                OutFile.WriteLine("    (uvias_allowed no)");
                OutFile.WriteLine("    (uvia_min_size 0.508)");
                OutFile.WriteLine("    (uvia_min_drill 0.127)");
                OutFile.WriteLine("    (pcb_text_width 0.3)");
                OutFile.WriteLine("    (pcb_text_size 1.5 1.5)");
                OutFile.WriteLine("    (mod_edge_width 0.15)");
                OutFile.WriteLine("    (mod_text_size 1 1)");
                OutFile.WriteLine("    (mod_text_width 0.15)");
                OutFile.WriteLine("    (pad_size 1.5 1.5)");
                OutFile.WriteLine("    (pad_drill 0.6)");
                OutFile.WriteLine("    (pad_to_mask_clearance 0.1)"); // TODO should get value from design rules
                OutFile.WriteLine("    (aux_axis_origin 0 0)");
                OutFile.WriteLine("    (visible_elements 7FFFF77F)");
                OutFile.WriteLine("    (pcbplotparams");
                OutFile.WriteLine("      (layerselection 262143)");
                OutFile.WriteLine("      (usegerberextensions false)");
                OutFile.WriteLine("      (excludeedgelayer true)");
                OutFile.WriteLine("      (linewidth 0.100000)");
                OutFile.WriteLine("      (plotframeref false)");
                OutFile.WriteLine("      (viasonmask false)");
                OutFile.WriteLine("      (mode 1)");
                OutFile.WriteLine("      (useauxorigin false)");
                OutFile.WriteLine("      (hpglpennumber 1)");
                OutFile.WriteLine("      (hpglpenspeed 20)");
                OutFile.WriteLine("      (hpglpendiameter 15)");
                OutFile.WriteLine("      (hpglpenoverlay 2)");
                OutFile.WriteLine("      (psnegative false)");
                OutFile.WriteLine("      (psa4output false)");
                OutFile.WriteLine("      (plotreference true)");
                OutFile.WriteLine("      (plotvalue true)");
                OutFile.WriteLine("      (plotinvisibletext false)");
                OutFile.WriteLine("      (padsonsilk false)");
                OutFile.WriteLine("      (subtractmaskfromsilk false)");
                OutFile.WriteLine("      (outputformat 1)");
                OutFile.WriteLine("      (mirror false)");
                OutFile.WriteLine("      (drillshape 0)");
                OutFile.WriteLine("      (scaleselection 1)");
                OutFile.WriteLine("      (outputdirectory \"GerberOutput/\"))");
                OutFile.WriteLine("  )");
                OutFile.WriteLine("");
            }

            OutFile.WriteLine(NetsL.ToString());
            OutFile.WriteLine(net_classes);
            OutFile.WriteLine(board_outline);
            OutFile.WriteLine(ModulesL.ToString());
            //            PadsL.ToString(); N.B. KiCad doesn't allow free standing pads yet ...TODO create modules to do this
            OutFile.WriteLine(PolygonsL.ToString());
            OutFile.WriteLine(ViasL.ToString());
            OutFile.WriteLine(tracks);
            OutFile.WriteLine(arcs);
            OutFile.WriteLine(texts);
            OutFile.WriteLine(fills);
            OutFile.WriteLine(keepouts);
            OutFile.WriteLine(DimensionsL.ToString());
            OutFile.WriteLine(RegionsL.ToString());
            OutFile.WriteLine(")");

            OutFile.Close();

            if (CreateLib)
            {
                // now create library directory and fill with modules
                // now make directory based on the PCB filename
                string dir = filename.Substring(0, filename.ToLower().IndexOf(".PcbDoc".ToLower())) + ".pretty";
                if (!Directory.Exists(dir))
                {
                    // create the library directory
                    Directory.CreateDirectory(dir);
                }
                // change to the directory
                Directory.SetCurrentDirectory(".\\" + dir);

                List<Module> UniqueModules = new List<Module>();

                // build list of unique modules
                foreach (var Mod in ModulesL)
                {
                    if (!InList(Mod, UniqueModules))
                    {
                        UniqueModules.Add(Mod);
                    }
                }

                // write out each of the unique modules into the library
                foreach (var Mod in UniqueModules)
                {
                    string fileName = "";
                    try
                    {
                        fileName = Mod.Name + ".kicad_mod";
                        // Check if file already exists. If yes, delete it.     
                        if (File.Exists(fileName))
                        {
                            File.Delete(fileName);
                        }

                        // Create a new file     
                        using (System.IO.StreamWriter file = new System.IO.StreamWriter(fileName))
                        {
                            file.Write(Mod.ToModule());
                            file.Close();
                        }
                    }
                    catch (Exception Ex)
                    {
                        Debug.WriteLine(Ex.ToString());
                    }
                }
            }
            Directory.SetCurrentDirectory("..\\");
        }
    }
}

