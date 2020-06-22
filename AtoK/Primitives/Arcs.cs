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
        class Arc : PCBObject
        {
          //  double X1 { get; set; }
          //  double Y1 { get; set; }
            double StartAngle { get; set; }
            double EndAngle { get; set; }
            double Radius;
          //  private readonly Layers Layer;
            private double Width;
            Int16 net;

            public static double Radians(double val)
            {
                return (Math.PI / 180) * val;
            }

            private Arc()
            {
                X1 = 0;
                Y1 = 0;
                StartAngle = 0;
                EndAngle = 0;
                Radius = 0;
                Layer = Layers.No_Layer;
                Width = 0;
                net = -1;
            }

            public Arc(double x1, double y1, double startangle, double endangle, double radius, Layers layer, double width)
            {
                // x1, y1 = centre 
                X1 = x1;
                Y1 = y1;
                StartAngle = startangle;
                EndAngle = endangle;
                Radius = radius;
                Layer = layer;
                Width = width;
                net = -1;
            }

            public Arc(double x1, double y1, double startangle, double endangle, double radius, Layers layer, double width, Int16 Net)
            {
                // x1, y1 = centre x2, y2 = start point
                X1 = x1;
                Y1 = y1;
                StartAngle = startangle;
                EndAngle = endangle;
                Radius = radius;
                Layer = layer;
                Width = width;
                net = Net;
            }

            public override string ToString()
            {
                // for some reason the arc in version 5.99 needs start middle end
                // rather than start end angle like for fp_arc ... go figure
                // X1, Y1 = centre 
                double Xs, Ys, Xm, Ym, Xe, Ye;

                Xs = Math.Round(X1 + Radius * Math.Cos(Radians(StartAngle)), Precision);
                Ys = Math.Round(Y1 + Radius * Math.Sin(Radians(StartAngle)), Precision);
                Xm = Math.Round(X1 + Radius * Math.Cos(Radians(StartAngle + (EndAngle - StartAngle) / 2)), Precision);
                Ym = Math.Round(Y1 + Radius * Math.Sin(Radians(StartAngle + (EndAngle - StartAngle) / 2)), Precision);
                Xe = Math.Round(X1 + Radius * Math.Cos(Radians(EndAngle)), Precision);
                Ye = Math.Round(Y1 + Radius * Math.Sin(Radians(EndAngle)), Precision);

                string netstr = (net == -1) ? "" : $"(net {net})";
                if(Brd.IsCopperLayer(Layer))
                    return $"  (arc (start {Xs} {-Ys}) (mid {Xm} {-Ym}) (end {Xe} {-Ye}) (layer {Brd.GetLayer(Layer)}) {netstr} (width {Width}))\n";
                else
                    return $"  (gr_arc (start {Math.Round(X1, Precision)} {Math.Round(-Y1, Precision)}) (end {Math.Round(Xs, Precision)} {Math.Round(-Ys, Precision)}) (angle {Math.Round(-(EndAngle - StartAngle), Precision)}) (layer {Brd.GetLayer(Layer)}) (width {Width}))\n";
            }

            public string ToString(double x, double y)
            {
                Int32 SA = Convert.ToInt32(StartAngle * 100);
                Int32 EA = Convert.ToInt32(EndAngle * 100);

                if (Math.Abs(EA - SA) == 36000)
                    // it's a circle
                    return $"    (fp_circle (center {Math.Round(X1 - x, Precision)} {Math.Round(-(Y1 - y), Precision)}) (end {Math.Round(X1+Radius - x, Precision)} {Math.Round(-(Y1 - y), Precision)}) (layer {Layer}) (width {Width}))\n";
                else
                {
                    double Xs, Ys, Xe, Ye;
                    Xs = Math.Round((X1-x) + Radius * Math.Cos(Radians(StartAngle)), Precision);
                    Ys = Math.Round((Y1-y) + Radius * Math.Sin(Radians(StartAngle)), Precision);
                    Xe = Math.Round((X1-x) + Radius * Math.Cos(Radians(EndAngle)), Precision);
                    Ye = Math.Round((Y1-y) + Radius * Math.Sin(Radians(EndAngle)), Precision);
                    Width = Math.Round(Width, Precision);
                    CheckMinMax(Xs, Ys);
                    CheckMinMax(Xe, Xe);
                    var arc = new StringBuilder("");
                    string n = (net == -1) ? "" : $"net {net}";
                    List<string> Layers = Brd.GetLayers(Brd.GetLayer(Layer));
                    foreach (var L in Layers)
                    {
                        arc.Append($"    (arc (start {Xs} {-Ys}) (end {Xe} {Ye}) (angle {Math.Round(EndAngle-StartAngle, Precision)}) (layer {L}) (width {Width}))\n");
                    }
                    return arc.ToString();
                }
            }

            public override string ToString(double x, double y, double ModuleRotation)
            {
                Point2D p1 = new Point2D(X1 - x, Y1 - y);

                double X2 = (X1 - x) + Radius * Math.Cos(Radians(StartAngle));
                double Y2 = (Y1 - y) + Radius * Math.Sin(Radians(StartAngle));
                Point2D p2 = new Point2D(X2, Y2);

                p1.Rotate(ModuleRotation);
                p2.Rotate(ModuleRotation);

                Int32 SA = Convert.ToInt32(StartAngle * 100);
                Int32 EA = Convert.ToInt32(EndAngle * 100);

                if (Math.Abs(EA - SA) == 36000)
                {
                    // it's a circle
                    StringBuilder circle = new StringBuilder("");
                    List<string> Layers = Brd.GetLayers(Brd.GetLayer(Layer));
                    foreach (var L in Layers)
                    {
                        circle.Append($"    (fp_circle (center {p1.X} {-p1.Y}) (end {p2.X} {-p2.Y}) (layer {L}) (width {Width}))\n");
                    }
                    return circle.ToString();
                }
                else
                {
                    StringBuilder arc = new StringBuilder("");
                    List<string> Layers = Brd.GetLayers(Brd.GetLayer(Layer));
                    foreach (var L in Layers)
                    {
                        arc.Append($"    (fp_arc (start {Math.Round(p1.X, Precision)} {Math.Round(-p1.Y, Precision)}) (end {Math.Round(p2.X, Precision)} {Math.Round(-p2.Y, Precision)}) (angle {Math.Round(-(EndAngle - StartAngle), Precision)}) (layer {L}) (width {Width}))\n");
                    }
                    return arc.ToString(); ;
                }
            }
        }

/*        public override string ToString(double x, double y, double ModuleRotation)
            {
                Point2D p1 = new Point2D(X1 - x, Y1 - y);

                double X2 = (X1-x) + Radius * Math.Cos(Radians(StartAngle));
                double Y2 = (Y1-y) + Radius * Math.Sin(Radians(StartAngle));
                Point2D p2 = new Point2D(X2, Y2);
                p1.Rotate(ModuleRotation);
                p2.Rotate(ModuleRotation);

                Int32 SA = Convert.ToInt32(StartAngle * 100);
                Int32 EA = Convert.ToInt32(EndAngle * 100);

                if (Math.Abs(EA - SA) == 36000)
                {
                    // it's a circle
                    StringBuilder circle = new StringBuilder("");
                    List<string> Layers = Brd.GetLayers(Brd.GetLayer(Layer));
                    foreach (var L in Layers)
                    {
                        circle.Append($"    (fp_circle (center {p1.X} {-p1.Y}) (end {p1.X+Radius} {-p1.Y}) (layer {L}) (width {Width}))\n");
                    }
                    return circle.ToString();
                }
                else
                {
                    StringBuilder arc = new StringBuilder("");
                    List<string> Layers = Brd.GetLayers(Brd.GetLayer(Layer));
                    foreach (var L in Layers)
                    {
                        arc.Append($"    (fp_arc (start {Math.Round(p1.X, Precision)} {Math.Round(-p1.Y, Precision)}) (end {Math.Round(p1.X+Radius, Precision)} {Math.Round(-p1.Y, Precision)}) (angle {Math.Round(EndAngle-StartAngle, Precision)}) (layer {L}) (width {Width}))\n");
                    }
                    return arc.ToString(); ;
                }
            }
        }
*/
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
                public Int32 X1;          // 13 Centre X
                public Int32 Y1;          // 17 Centre Y
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
                X1 = Math.Round((double)a.X1 * 25.4 / 10000000 - originX, Precision);
                Y1 = Math.Round((double)a.Y1 * 25.4 / 10000000 - originY, Precision);
                Radius = Math.Round((double)a.Radius * 25.4 / 10000000, Precision);
                StartAngle = a.StartAngle;
                EndAngle = a.EndAngle;
                Width = (double)a.Width * 25.4 / 10000000;

                bool InComponent = Component != -1;
                double Angle;

                if (EndAngle < StartAngle)
                    EndAngle += 360;

                Angle = (EndAngle - StartAngle);
                double X = X1 + Radius * Math.Cos(StartAngle * Math.PI / 180);
                double Y = Y1 + Radius * Math.Sin(StartAngle * Math.PI / 180);
                string layer = Brd.GetLayer(Layer);

                if (!InComponent)
                {
                    if (net > 0 && Brd.IsCopperLayer(Layer))
                    {
                        if (!Globals.PcbnewVersion)
                        {
                            // arcs with nets on copper layers allowed in 5.99
                            Arc Arc = new Arc(X1, Y1, StartAngle, EndAngle, Radius, Layer, Width, net);
                            ArcsL.Add(Arc);
                        }
                        else
                        {
                            //arcs.Append($"  (arc (start {Math.Round(X1, Precision)} {Math.Round(-Y1, Precision)}) (end {Math.Round(X, Precision)} {Math.Round(-Y, Precision)}) (angle {Math.Round(Angle, Precision)}) (layer {L}) (net {Net}) (width {Width}))\n");

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
                            double start_angle = Arc.Radians(StartAngle);
                            double end_angle = Arc.Radians(EndAngle);
                            double X2 = Radius * Math.Cos(end_angle);
                            double Y2 = Radius * Math.Sin(end_angle);
                            X = Radius * Math.Cos(start_angle);
                            Y = Radius * Math.Sin(start_angle);

                            // generate arc segments at 5° increments
                            for (double angle = start_angle; angle < end_angle; angle += 2 * Math.PI / 72)
                            {
                                X1 = Radius * Math.Cos(angle);
                                Y1 = Radius * Math.Sin(angle);
                                Line Line = new Line(XC + X, YC + Y, XC + X1, YC + Y1, layer, Width, net);
                                LinesL.Add(Line);
                                X = X1;
                                Y = Y1;
                            }
                            // do last segment
                            if (X != X2 || Y != Y2)
                            {
                                Line Line = new Line(X + XC, Y + YC, X2 + XC, Y2 + YC, layer, Width, net);
                                LinesL.Add(Line);
                            }
                        }
                    }
                    else
                    {
                        // only add if not part of board outline
                        if ((layer != "Edge.Cuts") || !Brd.CheckExistingArc(X1, Y1, X, Y, Angle))
                        {
                            List<string> Layers = Brd.GetLayers(layer);
                            foreach (var L in Layers)
                            {
                                Arc Arc = new Arc(X1, Y1, StartAngle, EndAngle, Radius, Brd.GetAltiumLayer(L), Width);
                                ArcsL.Add(Arc);
                            }
                        }
                    }
                }
                else
                {
                    Arc Arc = new Arc(X1, Y1, StartAngle, EndAngle, Radius, Layer, Width);
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
