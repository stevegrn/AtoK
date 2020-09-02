using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ConvertToKicad
{
    public partial class ConvertPCBDoc
    {
        public enum PCBObjectType
        {
            Unknown,
            Pad,
            Net,
            Polygon,
            Region,
            Text,
            Track,
            Via,
            Arc,
            Class,
            Component,
            Connection,
            Coordinate,
            Fill,
            Designator,
            Comment,
            ComponentBody,
            Module,
            Dimension,
            Rule,
            ShapeBasedModel
        }

        public class PCBObject //: Object
        {
            public PCBObjectType ObjectType { get; set; }
            public double X1 { get; set; }
            public double Y1 { get; set; }
            public double X2 { get; set; }
            public double Y2 { get; set; }
            public Layers Layer { get; set; }
            public int LayerNo { get; set; }
            public int Net { get; set; }
            public double Rotation { get; set; }
            public int Component { get; set; }
            public string Designator { get; set; }
            public string Comment { get; set; }

            public PCBObject()
            {
            }

            public virtual string ToString() => base.ToString();

            public virtual string ToString(double x, double y, double rotation)
            {
                return "";
            }

            public bool All()
            {
                return true;
            }

            public bool IsArc()
            {
                return ObjectType == PCBObjectType.Arc;
            }

            public bool IsClass()
            {
                return ObjectType == PCBObjectType.Class;
            }

            public bool IsComponentArc()
            {
                return (Component != -1) && (ObjectType == PCBObjectType.Class);
            }

            public bool IsComponentFill()
            {
                return (Component != -1) && (ObjectType == PCBObjectType.Fill);
            }

            public bool IsComponentPad()
            {
                return (Component != -1) && (ObjectType == PCBObjectType.Pad);
            }

            public bool IsComponentText()
            {
                return (Component != -1) && (ObjectType == PCBObjectType.Text);
            }

            public bool IsComponentTrack()
            {
                return (Component != -1) && (ObjectType == PCBObjectType.Track);
            }

            public bool IsComponentVia()
            {
                return (Component != -1) && (ObjectType == PCBObjectType.Via);
            }

            public bool IsConnection()
            {
                return ObjectType == PCBObjectType.Connection;
            }

            public bool IsCoordinate()
            {
                return ObjectType == PCBObjectType.Coordinate;
            }

            public bool IsCopperRegion()
            {
                return false; // TODO
            }

            public bool IsCutoutRegion()
            {
                return false; // TODO
            }

            public bool IsDatumDimension()
            {
                return false; // TODO
            }

            public bool IsDesignator()
            {
                return IsComponentText() && Designator != ""; // TODO this doesn't look right
            }

            public bool IsComment()
            {
                return (IsComponentText()) && Comment != ""; // TODO this doesn't look right
            }

            public bool IsFill()
            {
                return ObjectType == PCBObjectType.Fill;
            }

            public bool IsPad()
            {
                return ObjectType == PCBObjectType.Pad;
            }

            public bool IsPoly()
            {
                return ObjectType == PCBObjectType.Polygon;
            }

            public bool IsPolygon()
            {
                return ObjectType == PCBObjectType.Polygon;
            }

            public bool IsRegion()
            {
                return ObjectType == PCBObjectType.Region;
            }

            public bool IsSplitPlane()
            {
                return Brd.OrderedLayers[LayerNo].SplitPlane;
            }

            public bool IsText()
            {
                return ObjectType == PCBObjectType.Text;
            }

            public bool IsTrack()
            {
                return ObjectType == PCBObjectType.Track;
            }

            public bool IsVia()
            {
                return ObjectType == PCBObjectType.Via;
            }

            class GFG : IComparer<string>
            {
                public int Compare(string x, string y)
                {

                    if (x == null || y == null)
                    {
                        return 0;
                    }

                    // "CompareTo()" method 
                    return x.CompareTo(y);

                }
            }

            public bool InComponent(string component)
            {
                bool match =  ModulesL[this.Component].Designator == component;
                if (match) Debugger.Break();
                return match;
                var Designators = new List<string>();
                GFG gg = new GFG();
                int i = 0;
                foreach(var Module in ModulesL)
                {
                    Designators.Add(Module.Designator);
                    if (component == Module.Designator)
                        return true;
                    i++;
                }
                Designators.Sort(gg);
                return false;
            }

        }
    }
}
