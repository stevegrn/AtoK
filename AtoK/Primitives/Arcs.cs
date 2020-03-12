using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ConvertToKicad
{
    public partial class ConvertPCBDoc
    {
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
                base.ProcessLine();
                ArcStruct a = ByteArrayToStructure<ArcStruct>(line);
                Layer = (Layers)a.Layer;
                net = a.net;
                net++;
                Component = a.Component;
                X1 = Math.Round((double)a.X1 * 25.4 / 10000000 - originX, Precision);
                Y1 = Math.Round((double)a.Y1 * 25.4 / 10000000 - originY, Precision);
                Radius = Math.Round((double)a.Radius * 25.4 / 10000000, Precision);
                StartAngle = a.StartAngle;
                EndAngle = a.EndAngle % 360;
                Width = Math.Round((double)(a.Width * 25.4 / 10000000), Precision);

                bool InComponent = Component != -1;
                double Angle;

                if (EndAngle < StartAngle)
                    EndAngle += 360;

                Angle = Math.Round(-(EndAngle - StartAngle), Precision);
                double X = Math.Round(X1 + Radius * Math.Cos(StartAngle * Math.PI / 180), Precision);
                double Y = Math.Round(Y1 + Radius * Math.Sin(StartAngle * Math.PI / 180), Precision);
                string layer = Brd.GetLayer(Layer);

                if (!InComponent)
                {
                    if (net > 0 && Brd.IsCopperLayer(Layer))
                    {
                        // we have an arc/track on a copper layer and it has a net
                        // these aren't supported by KiCad yet so generate a curve out of track segments
                        // first normalise it so that the centre is at 0,0
                        // save the centre point
                        double XC = X1;
                        double YC = Y1;

                        X = Math.Round(X - XC, Precision);
                        Y = Math.Round(Y - YC, Precision);

                        double radius = Math.Sqrt(X * X + Y * Y);
                        // start angle in radians
                        double start_angle = StartAngle * Math.PI / 180;
                        double end_angle = EndAngle * Math.PI / 180;
                        double X2 = Math.Round(Radius * Math.Cos(end_angle), Precision);
                        double Y2 = Math.Round(Radius * Math.Sin(end_angle), Precision);
                        X = Math.Round(Radius * Math.Cos(start_angle), Precision);
                        Y = Math.Round(Radius * Math.Sin(start_angle), Precision);

                        for (double angle = start_angle; angle < end_angle; angle += 2 * Math.PI / 72)
                        {
                            X1 = Math.Round(Radius * Math.Cos(angle), Precision);
                            Y1 = Math.Round(Radius * Math.Sin(angle), Precision);
                            tracks += ($"  (segment (start {XC + X} {-(YC + Y)}) (end {XC + X1} {-(YC + Y1)}) (width {Width}) (layer {layer}) (net {net}))\n");
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
                        if ((layer != "Edge.Cuts") || !Brd.CheckExistingArc(X1, Y1, X, Y, Angle))
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

            public override bool ProcessFile(byte[] data)
            {
                return base.ProcessFile(data);
            }
        }
    }
}
