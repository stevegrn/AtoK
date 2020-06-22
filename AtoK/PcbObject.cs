using System;

namespace ConvertToKicad
{
    public partial class ConvertPCBDoc
    {
        enum PCBObjectType
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
            Coordinate
        }

        public class PCBObject : Object
        {
            PCBObjectType Type;
            public double X1 { get; set; }
            public double Y1 { get; set; }
            public double X2 { get; set; }
            public double Y2 { get; set; }
            public string Layer { get; set; }
            public int LayerNo { get; set; }
            public int Net { get; set; }
            public double Rotation { get; set; }
            public int Component { get; set; }
            public bool Designator { get; set; }
            public bool Comment { get; set; }

            public PCBObject()
            {
            }

          //  public override string ToString() => base.ToString();

            public bool All()
            {
                return true;
            }

            public bool IsArc()
            {
                return Type == Arc;
            }

            public bool IsClass()
            {
                return Type == Class;
            }

            public bool IsComponentArc()
            {
                return (Component != -1) && (Type == Class);
            }

            public bool IsComponentFill()
            {
                return (Component != -1) && (Type == Fill);
            }

            public bool IsComponentPad()
            {
                return (Component != -1) && (Type == Pad);
            }

            public bool IsComponentText()
            {
                return (Component != -1) && (Type == Text);
            }

            public bool IsComponentTrack()
            {
                return (Component != -1) && (Type == Track);
            }

            public bool IsComponentVia()
            {
                return (Component != -1) && (Type == Via);
            }

            public bool IsConnection()
            {
                return Type == Connection;
            }

            public bool IsCoordinate()
            {
                return Type == Coordinate;
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
                return (Type == IsComponentText) && Designator;
            }

            public bool IsComment()
            {
                return (Type == IsComponentText) && Comment;
            }

            public bool IsFill()
            {
                return Type == Fill;
            }

            public bool IsPad()
            {
                return Type == Pad;
            }

            public bool IsPoly()
            {
                return Type == Polygon;
            }

            public bool IsPolygon()
            {
                return Type == Polygon;
            }

            public bool IsRegion()
            {
                return Type == IsCopperRegion;
            }

            public bool IsSplitPlane()
            {
                return Brd.OrderedLayers[LayerNo].SplitPLane;
            }

            public bool IsText()
            {
                return Type == Text;
            }

            public bool IsTrack()
            {
                return Type == Track;
            }

            public bool IsVia()
            {
                return Type == Via;
            }

            public bool InComponent(int component)
            {
                return Component == component;
            }

        }
    }
}
