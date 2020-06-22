using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ConvertToKicad
{
    public partial class ConvertPCBDoc
    {
        // class for component body objects
        class ComponentBody : PCBObject
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
            public double Opacity;
            public string BodyLayer;

            ComponentBody()
            {

            }

            public ComponentBody(string line)
            {
                string Ident = GetString(line, "IDENTIFIER=");
                try
                {
                    if (Ident != "")
                    {
                        Ident = Ident.Substring(1, Ident.Length - 1);
                        string[] chars = Ident.Split(',');
                        foreach (string c in chars)
                        {
                            Int32 x = Convert.ToInt32(c);
                            Identifier += Convert.ToChar(x);
                        }
                    }
                    else
                        Identifier = "";
                    ID = GetString(line, "MODELID=");
                    Checksum = GetUInt32(GetString(line, "MODEL.CHECKSUM="));
                    StandoffHeight = GetNumberInMM(GetString(line, "MODEL.3D.DZ="));
                    OverallHeight = GetNumberInMM(GetString(line, "OVERALLHEIGHT="));
                    X = Math.Round(GetNumberInMM(GetString(line, "MODEL.2D.X=")) - originX, Precision);
                    Y = Math.Round(GetNumberInMM(GetString(line, "MODEL.2D.Y=")) - originY, Precision);
                    Model2dRot = GetDouble(GetString(line, "MODEL.2D.ROTATION="));   // rotation of footprint
                    Model3dRotX = GetDouble(GetString(line, "MODEL.3D.ROTX="));       // rotation about x for 3d model
                    Model3dRotY = GetDouble(GetString(line, "MODEL.3D.ROTY="));       // rotation about y for 3d model
                    Model3dRotZ = GetDouble(GetString(line, "MODEL.3D.ROTZ="));       // rotation about z for 3d model
                    TextureRotation = GetDouble(GetString(line, "TEXTUREROTATION="));     // yet another rotation
                    BodyLayer = GetString(line, "BODYPROJECTION=");
                    string O = GetString(line, "BODYOPACITY3D=");
                    if (O == "")
                        O = "1.0";
                    Opacity = GetDouble(O);
                }
                catch(Exception Ex)
                {
                    CheckThreadAbort(Ex);
                }
            }

            public string ToString(double x, double y, double modulerotation)
            {
                string FileName = Mods.GetFilename(ID).ToLower();

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
                        ModRotZ = (Model3dRotZ + (360 - (Model2dRot - modulerotation))) % 360;
                    }
                    else
                    {
                        p.Rotate(-modulerotation);
                        ModRotZ = (Model3dRotZ + Model2dRot - modulerotation + 360) % 360;
                    }
                    if ((BodyLayer == "1" && ModulesL[CurrentModule].Layer == "F.Cu") || // 3d model layer
                        (BodyLayer == "0" && ModulesL[CurrentModule].Layer == "B.Cu"))
                    {
                        Model3dRotY = Model3dRotY - 180;
                    }

                    var ret = new StringBuilder("");

                    ret.Append($"    (model \"$(KIPRJMOD)/Models/{FileName}\"\n");
                    if (Opacity < 1)
                    {
                        if(!Globals.PcbnewVersion)
                            ret.Append($"        (opacity {Opacity})\n"); // in Pcbnew 5.99
                    }
                    ret.Append($"        (offset (xyz {p.X} {p.Y} {StandoffHeight}))\n");
                    ret.Append($"        (scale (xyz {1} {1} {1}))\n");
                    ret.Append($"        (rotate (xyz {-Model3dRotX} {-Model3dRotY} {-ModRotZ}))\n    )\n");
                    return ret.ToString();
                }

                return ""; // $"# ID={ID} Checksum = {Checksum} StandoffHeight={StandoffHeight} OverallHeight={OverallHeight} x={X - x} y={-(Y - y)} rotx={Model3dRotX} roty={Model3dRotY} rotz={Model3dRotZ}\n";
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
            public ComponentBodies(string filename, string cmfilename, string record, Type type, int offset) : base(filename, cmfilename, record, type, offset)
            {
                Binary_size = 1;
            }

            public override bool ProcessBinaryFile(byte[] data)
            {
                StartTimer();
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
                            ms.Seek(pos + 0x17, SeekOrigin.Begin);
                            UInt32 length = br.ReadUInt32();
                            char[] line = br.ReadChars((int)length);
                            string str = new string(line);
                            ProcessLine(str);
                            pos += 5 + next;
                        }
                    }
                    catch (Exception Ex)
                    {
                        CheckThreadAbort(Ex);
                    }
                }
                return true;
            }

            public override bool ProcessLine(string line)
            {
                base.ProcessLine();
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
    }
}
