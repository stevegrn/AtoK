using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ConvertToKicad
{
    public partial class ConvertPCBDoc
    {
        // class for polygon objects
        class Polygon : PCBObject
        {
            public List<Point> Points { get; set; }
            int NetNo { get; set; }
            string NetName { get; set; }
            string Layer { get; set; }
            public bool InComponent { get; set; }
            public int Component { get; set; }
            public bool IsSplitPlane { get; set; }
            public double TrackWidth { get; set; }
            public Int16 PourIndex { get; set; }
            public double NeckWidthThreshold { get; set; }

            public int GetPriority(int pourindex)
            {
                return Brd.MaxPourIndex - pourindex;
            }

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
                Int32 net = 0;

                if ((param = GetString(line, "|NET=")) != "")
                {
                    net = Convert.ToInt32(param) + 1;
                }
                string Net = GetNetName(net);
                if ((param = GetString(line, "|COMPONENT=")) != "")
                {
                    Component = Convert.ToInt32(param);
                    InComponent = true;
                }
                if ((param = GetString(line, "|LAYER=")) != "")
                {
                    Layer = Brd.GetLayer(param);
                }
                IsSplitPlane = ((param = GetString(line, "|POLYGONTYPE=")) == "Split Plane");
                if (!IsSplitPlane && (param = GetString(line, "|TRACKWIDTH=")) != "")
                {
                    TrackWidth = Convert.ToDouble(GetNumberInMM(param));
                }
                else
                {
                    TrackWidth = 0.1; // maybe should be smaller for split plane
                    NeckWidthThreshold = 0.1;
                }
                if ((param = GetString(line, "|POURINDEX=")) != "")
                {
                    PourIndex = Convert.ToInt16(param);
                    if (PourIndex > Brd.MaxPourIndex)
                        Brd.MaxPourIndex = PourIndex;
                }

                if ((param = GetString(line, "|NECKWIDTHTHRESHOLD=")) != "")
                {
                    NeckWidthThreshold = GetNumberInMM(param.Trim(charsToTrim));
                }

                NetNo = net;
                NetName = GetNetName(NetNo);
                Points = new List<Point>();
                string[] coords = line.Split('|');
                // now add all the vertices
                for (var j = 0; j < coords.Length; j++)
                {
                    if (coords[j].StartsWith("KIND"))
                    {
                        var start = coords[j].IndexOf('=') + 1;
                        string type = coords[j].Substring(start);
                        j++;
                        start = coords[j].IndexOf('=') + 1;
                        string coord = coords[j].Substring(start);
                        // get start X
                        double VX = GetCoordinateX(coord.Trim(charsToTrim));
                        j++;
                        start = coords[j].IndexOf('=') + 1;
                        coord = coords[j].Substring(start);
                        // get start Y
                        double VY = GetCoordinateY(coord.Trim(charsToTrim));
                        j++;
                        start = coords[j].IndexOf('=') + 1;
                        coord = coords[j].Substring(start);
                        // get centre X
                        double CX = GetCoordinateX(coord.Trim(charsToTrim));
                        j++;
                        start = coords[j].IndexOf('=') + 1;
                        coord = coords[j].Substring(start);
                        // get centre Y
                        double CY = GetCoordinateY(coord.Trim(charsToTrim));
                        j++;
                        start = coords[j].IndexOf('=') + 1;
                        coord = coords[j].Substring(start);
                        // get start angle
                        double SA = GetDouble(coord.Trim(charsToTrim));
                        j++;
                        start = coords[j].IndexOf('=') + 1;
                        coord = coords[j].Substring(start);
                        // get end angle
                        double EA = GetDouble(coord.Trim(charsToTrim));
                        j++;
                        start = coords[j].IndexOf('=') + 1;
                        coord = coords[j].Substring(start);
                        // get radius
                        double R = GetNumberInMM(coord.Trim(charsToTrim));
                        if (type == "0")
                        {
                            // straight line
                            AddPoint(Math.Round(VX, Precision), Math.Round(VY, Precision));
                        }
                        else
                        {
                            // this is an arc so have to simulate arc with a number of line segments
                            // first normalise it so that the centre is at 0,0
                            // save the centre point
                            double XC = CX;
                            double YC = CY;

                            double X = VX - XC;
                            double Y = VY - YC;

                            // generate arc segments at 5° increments
                            if (EA < SA)
                                EA += 360;
                            // start point of arc
                            X = R * Math.Cos(Arc.Radians(SA));
                            Y = R * Math.Sin(Arc.Radians(SA));
                            // end point of arc
                            double X2 = R * Math.Cos(Arc.Radians(EA));
                            double Y2 = R * Math.Sin(Arc.Radians(EA));
                            bool clockwise = true;
                            double l1 = Math.Sqrt((VX - (X  + XC)) * (VX - (X  + XC)) + (VY - (Y  + YC)) * (VY - (Y  + YC)));
                            double l2 = Math.Sqrt((VX - (X2 + XC)) * (VX - (X2 + XC)) + (VY - (Y2 + YC)) * (VY - (Y2 + YC)));
                            if (l1 < l2)
                                clockwise = false;

                            // need to determine if this is clockwise or anticlockwise as the                   
                            // start and end angles are back to front
                            if (!clockwise)
                            {
                                // anticlockwise
                                for (double angle = SA; angle < EA; angle += 5)
                                {
                                    double X1 = R * Math.Cos(Arc.Radians(angle));
                                    double Y1 = R * Math.Sin(Arc.Radians(angle));
                                    AddPoint(X + XC, Y + YC);
                                    X = X1;
                                    Y = Y1;
                                }

                                // do last segment
                                if (X != X2 || Y != Y2)
                                {
                                    AddPoint(X2 + XC, Y2 + YC);
                                }
                            }
                            else
                            {
                                // clockwise
                                // start point of arc
                                X = R * Math.Cos(Arc.Radians(EA));
                                Y = R * Math.Sin(Arc.Radians(EA));
                                // end point of arc
                                X2 = R * Math.Cos(Arc.Radians(SA));
                                Y2 = R * Math.Sin(Arc.Radians(SA));

                                for (double angle = EA; angle > SA; angle -= 5)
                                {
                                    double X1 = R * Math.Cos(Arc.Radians(angle));
                                    double Y1 = R * Math.Sin(Arc.Radians(angle));
                                    AddPoint(X + XC, Y + YC);
                                    X = X1;
                                    Y = Y1;
                                }

                                // do last segment
                                if (X != X2 || Y != Y2)
                                {
                                    AddPoint(X2 + XC, Y2 + YC);
                                }
                            }
                        }
                    }
                }

            }

            public override string ToString()
            {
                var ret = new StringBuilder("");
                string connectstyle = "";

                double clearance = GetRuleValue(this, "Clearance", "PolygonClearance");
                if (Layer.Substring(0, 2) == "In")
                {
                    // this is an inner layer so use plane clearance
                    clearance = GetRuleValue(this, "PlaneClearance", "PlaneClearance");
                }
                List<string> Layers = Brd.GetLayers(Brd.GetLayer(Layer));
                foreach (var L in Layers)
                {

                    ret.Append($"  (zone (net {NetNo}) (net_name {NetName}) (layer {L}) (hatch edge 0.508)");
                    ret.Append($"    (priority {GetPriority(PourIndex)})\n");
                    ret.Append($"    (connect_pads {connectstyle} (clearance {clearance}))\n"); // TODO sort out these numbers properly
                    ret.Append($"    (min_thickness {NeckWidthThreshold})\n");
                    ret.Append("    (fill yes (arc_segments 16) (thermal_gap 0.2) (thermal_bridge_width 0.3))\n");
                    var i = 0;
                    ret.Append("    (polygon (pts\n        ");
                    foreach (var Point in Points)
                    {
                        i++;
                        if ((i % 5) == 0)
                            ret.Append("\n        ");
                        ret.Append(Point.ToString());
                        CheckMinMax(Point.X, Point.Y);
                    }
                    ret.Append("\n      )\n    )\n  )\n");
                    if (IsSplitPlane)
                        ret.Append("# Split Plane\n");
                }

                return ret.ToString();
            }

            public string ToString(double x, double y, double rotation)
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

        // class for the polygons document entry in the pcbdoc file
        class Polygons : PcbDocEntry
        {
            public Polygons(string filename, string cmfilename, string record, Type type, int offset) : base(filename, cmfilename, record, type, offset)
            {
            }

            public override bool ProcessLine(string line)
            {
                base.ProcessLine();
                Polygon Poly = new Polygon(line);
                if (Poly.InComponent)
                    ModulesL[Poly.Component].Polygons.Add(Poly);
                else
                    PolygonsL.Add(Poly);
                return true;
            }

        }
    }
}
