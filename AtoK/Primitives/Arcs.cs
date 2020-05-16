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
                {
                    double Xs = Math.Round(X1 - x, Precision);
                    double Ys = Math.Round(Y1 - y, Precision);
                    double Xe = Math.Round(X2 - x, Precision);
                    double Ye = Math.Round(Y2 - y, Precision);
                    CheckMinMax(Xs, Ys);
                    CheckMinMax(Xe, Xe);
                    var arc = new StringBuilder("");
                    List<string> Layers = Brd.GetLayers(Layer);
                    foreach (var L in Layers)
                    {
                        arc.Append($"    (fp_arc (start {Xs} {-Ys}) (end {Xe} {Ye}) (angle {Math.Round(Angle, Precision)}) (layer {L}) (width {Width}))\n");
                    }
                    return arc.ToString();
                }
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
                    StringBuilder circle = new StringBuilder("");
                    List<string> Layers = Brd.GetLayers(Layer);
                    foreach (var L in Layers)
                    {
                        circle.Append($"    (fp_circle (center {p1.X} {-p1.Y}) (end {p2.X} {-p2.Y}) (layer {L}) (width {Width}))\n");
                    }
                    return circle.ToString();
                }
                else
                {
                    StringBuilder arc = new StringBuilder("");
                    List<string> Layers = Brd.GetLayers(Layer);
                    foreach (var L in Layers)
                    {
                        arc.Append($"    (fp_arc (start {Math.Round(p1.X, Precision)} {Math.Round(-p1.Y, Precision)}) (end {Math.Round(p2.X, Precision)} {Math.Round(-p2.Y, Precision)}) (angle {Math.Round(Angle, Precision)}) (layer {L}) (width {Width}))\n");
                    }
                    return arc.ToString(); ;
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
            public Arcs(string filename, string cmfilename, string record, Type type, int offset) : base(filename, cmfilename, record, type, offset)
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
                X1 = (double)a.X1 * 25.4 / 10000000 - originX;
                Y1 = (double)a.Y1 * 25.4 / 10000000 - originY;
                Radius = (double)a.Radius * 25.4 / 10000000;
                StartAngle = a.StartAngle;
                EndAngle = a.EndAngle % 360;
                Width = (double)a.Width * 25.4 / 10000000;

                bool InComponent = Component != -1;
                double Angle;

                if (EndAngle < StartAngle)
                    EndAngle += 360;

                Angle = -(EndAngle - StartAngle);
                double X = X1 + Radius * Math.Cos(StartAngle * Math.PI / 180);
                double Y = Y1 + Radius * Math.Sin(StartAngle * Math.PI / 180);
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

                        X = X - XC;
                        Y = Y - YC;

                        double radius = Math.Sqrt(X * X + Y * Y);
                        // start angle in radians
                        double start_angle = StartAngle * Math.PI / 180;
                        double end_angle = EndAngle * Math.PI / 180;
                        double X2 = Radius * Math.Cos(end_angle);
                        double Y2 = Radius * Math.Sin(end_angle);
                        X = Radius * Math.Cos(start_angle);
                        Y = Radius * Math.Sin(start_angle);

                        tracks.Append($"# arc start {Math.Round(X1, Precision)},{Math.Round(Y1, Precision)} end  {Math.Round(X1, Precision)},{Math.Round(Y1, Precision)} angle {Math.Round(Angle, 0)}\n");
                        // generate arc segments at 5° increments
                        for (double angle = start_angle; angle < end_angle; angle += 2 * Math.PI / 72)
                        {
                            X1 = Radius * Math.Cos(angle);
                            Y1 = Radius * Math.Sin(angle);
                            tracks.Append($"  (segment (start {Math.Round(XC + X, Precision)} {Math.Round(-(YC + Y), Precision)}) (end {Math.Round(XC + X1, Precision)} {Math.Round(-(YC + Y1), Precision)}) (width {Math.Round(Width, Precision)}) (layer {layer}) (net {net}))\n");
                            //Line Line = new Line(X1, Y1, X2, Y2, Layer, Width, true);
                            //Segments.Add(Line);
                            X = X1;
                            Y = Y1;
                        }
                        // do last segment
                        if (X != X2 || Y != Y2)
                        {
                            tracks.Append($"  (segment (start {Math.Round(X + XC, Precision)} {Math.Round(-(Y + YC), Precision)}) (end {Math.Round(X2 + XC, Precision)} {Math.Round(-(Y2 + YC), Precision)}) (width {Width}) (layer {layer}) (net {net}))\n");
                        }
                        tracks.Append("# end arc\n");
                    }
                    else
                    {
                        // only add if not part of board outline
                        if ((layer != "Edge.Cuts") || !Brd.CheckExistingArc(X1, Y1, X, Y, Angle))
                        {
                            List<string> Layers = Brd.GetLayers(layer);
                            foreach (var L in Layers)
                            {
                                arcs.Append($"  (gr_arc (start {Math.Round(X1, Precision)} {Math.Round(-Y1, Precision)}) (end {Math.Round(X, Precision)} {Math.Round(-Y, Precision)}) (angle {Math.Round(Angle, Precision)}) (layer {L}) (width {Width}))\n");
                            }
                        }
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
