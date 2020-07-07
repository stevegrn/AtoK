using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;

namespace ConvertToKicad
{
    public partial class ConvertPCBDoc
    {
        // class for region objects
        class Region : PCBObject
        {
            public List<Point> Points { get; set; }
            int Net_no { get; set; }
            string Net_name { get; set; }
            string Layer { get; set; }
            bool InComponent { get; set; }
            public bool Keepout { get; set; }
            public bool PolygonCutout { get; set; }
            public bool BoardCutout { get; set; }
            short Flags { get; set; }
            public Int16 SubPolyIndex { get; set; }

            public void AddPoint(double X, double Y)
            {
                Point Point = new Point(X, Y);
                Points.Add(Point);
            }

            private Region()
            {

            }

            public Region(string layer, int net, Int16 flags, string line)
            {
                Int16 Index;
                if (!Int16.TryParse(GetString(line, "SUBPOLYINDEX="), out Index))
                    SubPolyIndex = -1;
                else
                    SubPolyIndex = Index;
                Layer = layer;
                Net_no = net;
                Net_name = GetNetName(Net_no);
                Points = new List<Point>();
                Flags = flags;
                Keepout = layer == "keepout";
                PolygonCutout = (ConvertPCBDoc.GetString(line, "|KIND=") == "1");
                BoardCutout   = (ConvertPCBDoc.GetString(line, "|ISBOARDCUTOUT=") == "TRUE");
                if (BoardCutout)
                    Layer = "Edge.Cuts";
            }

            public override string ToString()
            {
                StringBuilder ret = new StringBuilder("");
                string connectstyle = "";

                if (SubPolyIndex != -1)
                {
                    OutputError("Reject region");
                    return "";
                }

                double clearance = GetRuleValue(this, "Clearance", "PolygonClearance");
                if (Layer.Substring(0, 2) == "In")
                {
                    // this is an inner layer so use plane clearance
                    clearance = GetRuleValue(this, "PlaneClearance", "PlaneClearance");
                }

                if (Layer != "Edge.Cuts")
                {
                    List<string> Layers = Brd.GetLayers(Layer);
                    foreach (var L in Layers)
                    {
                        ret.Append($"  (zone (net {Net_no}) (net_name {Net_name}) (layer {L}) (hatch edge 0.508)");
                        ret.Append($"    (priority 100)\n");
                        ret.Append($"    (connect_pads {connectstyle} (clearance {clearance}))\n"); // TODO sort out these numbers properly
                        ret.Append($"    (min_thickness 0.2)\n");
                        if (Keepout)
                            ret.Append("(keepout(copperpour not_allowed))\n");
                        else if (PolygonCutout)
                            ret.Append("(keepout (tracks not_allowed) (vias allowed) (copperpour not_allowed))");
                        string fill = (Layer == "Edge.Cuts") ? "no" : "yes";
                        ret.Append($"    (fill {fill} (arc_segments 16) (thermal_gap 0.2) (thermal_bridge_width 0.3))\n");
                        var i = 0;
                        ret.Append("    (polygon (pts\n        ");
                        foreach (var Point in Points)
                        {
                            i++;
                            if ((i % 5) == 0)
                                ret.Append("\n        ");
                            ret.Append(Point.ToString());
                        }
                        ret.Append("\n      )\n    )\n  )\n");
                    }
                }
                else
                {
                    Point Start = new Point(0, 0);
                    Start = Points[0];
                    int i;
                    for (i = 0; i < Points.Count - 1; i++)
                    {
                        ret.Append($"  (gr_line (start {Points[i].X} {-Points[i].Y}) (end {Points[i + 1].X} {-Points[i + 1].Y}) (layer Edge.Cuts) (width 0.1))\n");
                    }
                    ret.Append($"  (gr_line (start {Points[i].X} {-Points[i].Y}) (end {Start.X} {-Start.Y}) (layer Edge.Cuts) (width 0.1))\n");
                }
                return ret.ToString();
            }

            // inside component region
            // presently this is not allowed (V5.1.2)
            public string ToString(double x, double y, double ModuleRotation)
            {
                // not allowed so just return
                return "";
/*
                StringBuilder ret = new StringBuilder("");

                double clearance = GetRuleValue("Clearance", "PolygonClearance");
                if (Layer.Substring(0, 2) == "In")
                {
                    // this is an inner layer so use plane clearance
                    clearance = GetRuleValue("PlaneClearance", "PlaneClearance");
                }

                string connectstyle = ""; // TODO get connect style from rules
                ret.Append($"  (zone (net {Net_no}) (net_name {Net_name}) (layer {Layer}) (tstamp 0) (hatch edge 0.508)");
                ret.Append($"    (priority 100)\n");
                ret.Append($"    (connect_pads {connectstyle} (clearance {clearance}))\n"); // TODO sort out these numbers properly
                ret.Append($"    (min_thickness 0.2)\n");
                if (Keepout)
                    ret.Append("(keepout(copperpour not_allowed))\n");
                else if (PolygonCutout)
                    ret.Append("(keepout (tracks not_allowed) (vias allowed) (copperpour not_allowed))");
                string fill = (Layer == "Edge.Cuts") ? "no" : "yes";
                ret.Append($"    (fill {fill} (arc_segments 16) (thermal_gap 0.2) (thermal_bridge_width 0.3))\n");
                var i = 0;
                ret.Append("    (polygon (pts\n        ");
                foreach (var Point in Points)
                {
                    i++;
                    if ((i % 5) == 0)
                        ret.Append("\n        ");
                    ret.Append(Point.ToString());
                }
                ret.Append("\n      )\n    )\n  )\n");
                
                ret.Append($"  (zone (net {Net_no}) (net_name {Net_name}) (layer {Layer}) (tstamp 0) (hatch edge 0.508)");
                ret.Append($"    (priority 100)\n");
                ret.Append($"    (connect_pads {connectstyle} (clearance {clearance}))\n"); // TODO sort out these numbers properly
                ret.Append($"    (min_thickness 0.2)\n");
                ret.Append("    (fill yes (arc_segments 16) (thermal_gap 0.2) (thermal_bridge_width 0.3))\n");
                 
                var i = 0;
                ret.Append("    (fp_poly (pts\n        ");
                foreach (var Point in Points)
                {
                    i++;
                    if ((i % 5) == 0)
                        ret.Append("\n        ");
                    ret.Append(Point.ToString(x, y, ModuleRotation));
                }
                //                ret += "\n      )\n    )\n  )\n";
                ret.Append($" ) ( layer {Brd.GetLayer(Layer)}) (width 0)\n    )\n");

                return ret.ToString();
*/
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
            public Regions(string filename, string cmfilename, string record, Type type, int offset) : base(filename, cmfilename, record, type, offset)
            {
                Binary_size = 1;
                Points = new List<Point>();
            }

            public override bool ProcessLine(byte[] record)
            {
                Layers Layer;
                Int16 Component;
                bool InComponent;
                base.ProcessLine();

                using (MemoryStream ms = new MemoryStream(record))
                {
                    // Use the memory stream in a binary reader.
                    using (BinaryReader br = new BinaryReader(ms))
                    {
                        try
                        {
                            ms.Seek(0, SeekOrigin.Begin);
                            Layer = (Layers)br.ReadByte();
                            ms.Seek(1, SeekOrigin.Begin);
                            short Flags = (short)br.ReadInt16();
                            ms.Seek(3, SeekOrigin.Begin);
                            int net = (int)br.ReadInt16()+1;
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
                            Region r = new Region(l, net, Flags, str);
                            if(r.SubPolyIndex > 0)
                            {
                                // Rejected region
                                return true;
                            }
                            while (DataLen-- > 0)
                            {
                                double X = Math.Round(ToMM(br.ReadDouble()) - originX, Precision);
                                double Y = Math.Round(ToMM(br.ReadDouble()) - originY, Precision);
                                r.AddPoint(X, Y);
                            }

                            if (!InComponent)
                            {
                                RegionsL.Add(r);
                            }
                            else
                            {
                                if (!r.Keepout && !r.PolygonCutout)
                                    ModulesL[Component].Regions.Add(r);
                                else
                                    // until keepouts are allowed in components
                                    // just add as a board region
                                    RegionsL.Add(r);
                            }
                        }
                        catch (Exception Ex)
                        {
                            CheckThreadAbort(Ex);
                        }
                    }
                }

                return true;
            }

            public override bool ProcessBinaryFile(byte[] data)
            {
                StartTimer();
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
                    CheckThreadAbort(Ex);
                }
                return true;
            }
        }
    }
}
