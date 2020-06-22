using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ConvertToKicad
{
    public partial class ConvertPCBDoc
    {
        // class for a shape based 3d model
        // creation of a .step file equivalent or .wrl not implemented
        // any volunteers?
        class ShapeBasedModel : PCBObject
        {
            public string ID { get; set; }
            public string Checksum { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public double ROTX { get; set; }
            public double ROTY { get; set; }
            public double ROTZ { get; set; }
            public double DZ { get; set; }
            public double MinZ { get; set; }
            public double MaxZ { get; set; }
            public string Type { get; set; } // 0 = extruded 1 = Cone 2 = Cylinder 3 = sphere
            public double Rotation { get; set; }
            public double CylinderRadius { get; set; }
            public double CylinderHeight { get; set; }
            public double SphereRadius { get; set; }
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
                    Colour = GetUInt32(GetString(line, "BODYCOLOR3D="));
                    Opacity = GetDouble(GetString(line, "BODYOPACITY3D="));

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
                    CheckThreadAbort(Ex);
                }
            }

            public string ToString(double x, double y, double ModuleRotation)
            {
                //OutputError($"Shape based model not supported # ID {ID} X={X + x} Y={Y + y} Rotation={Rotation + ModuleRotation} ROTX={ROTX} ROTY={ROTY} ROTZ={ROTZ} Type={Type}\n#{Line}\n");
                return ""; // $"# ID {ID} X={X + x} Y={Y + y} Rotation={Rotation + ModuleRotation} ROTX={ROTX} ROTY={ROTY} ROTZ={ROTZ} Type={Type}\n#{Line}\n";
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
    }
}
