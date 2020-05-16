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
        class Polygon : Object
        {
            List<Point> Points;
            int NetNo { get; set; }
            string NetName { get; set; }
            string Layer { get; set; }
            public bool InComponent { get; set; }
            public int Component { get; set; }
            public bool IsSplitPlane { get; set; }
            public double TrackWidth { get; set; }

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
                    TrackWidth = 0.1; // maybe should be smaller for split plane

                NetNo = net;
                NetName = GetNetName(NetNo);
                Points = new List<Point>();
                string[] coords = line.Split('|');
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
                StringBuilder ret = new StringBuilder("");
                string connectstyle = "";

                double clearance = GetRuleValue("Clearance", "PolygonClearance");
                if (Layer.Substring(0, 2) == "In")
                {
                    // this is an inner layer so use plane clearance
                    clearance = GetRuleValue("PlaneClearance", "PlaneClearance");
                }
                List<string> Layers = Brd.GetLayers(Brd.GetLayer(Layer));
                foreach (var L in Layers)
                {

                    ret.Append($"  (zone (net {NetNo}) (net_name {NetName}) (layer {L}) (tstamp 0) (hatch edge 0.508)");
                    ret.Append($"    (priority 100)\n");
                    ret.Append($"    (connect_pads {connectstyle} (clearance {clearance}))\n"); // TODO sort out these numbers properly
                    ret.Append($"    (min_thickness {TrackWidth})\n");
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
