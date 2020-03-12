using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ConvertToKicad
{
    public partial class ConvertPCBDoc
    {
        // advanced placer options document class - not implemented
        class AdvancedPlacerOptions : PcbDocEntry
        {
            public AdvancedPlacerOptions(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }
        }

        // design rule checker options document class - not implemented
        class DesignRuleCheckerOptions : PcbDocEntry
        {
            public DesignRuleCheckerOptions(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }
        }

        // pin swap options document class - not implemented
        class PinSwapOptions : PcbDocEntry
        {
            public PinSwapOptions(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }
        }

        // class for the differential pairs document entry in the pcbdoc file
        class DifferentialPairs : PcbDocEntry
        {
            public DifferentialPairs(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }

            public override bool ProcessLine(string line)
            {
                base.ProcessLine();
                return base.ProcessLine(line);
            }
        }

        // class for the embedded fonts document entry in the pcbdoc file (not implemented)
        class EmbeddedFonts : PcbDocEntry
        {
            public EmbeddedFonts(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }
        }

        // class for the shape based regions document entry in the pcbdoc file (not implemented)
        class ShapeBasedRegions : PcbDocEntry
        {
            public ShapeBasedRegions(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }
        }

        // class for the connections document entry in the pcbdoc file (not implemented)
        class Connections : PcbDocEntry
        {
            public Connections(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }
        }

        // class for the coordinates document entry in the pcbdoc file (not implemented)
        class Coordinates : PcbDocEntry
        {
            public Coordinates(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }
        }

        // class for the embeddeds document entry in the pcbdoc file (not implemented)
        class Embeddeds : PcbDocEntry
        {
            public Embeddeds(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }
        }

        // class for the embedded boards document entry in the pcbdoc file (not implemented)
        class EmbeddedBoards : PcbDocEntry
        {
            public EmbeddedBoards(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }
        }

        // class for the fromtos document entry in the pcbdoc file
        class FromTos : PcbDocEntry
        {
            public FromTos(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {

            }

            public override bool ProcessLine(string line)
            {
                return base.ProcessLine(line);
            }
        }

        // class for the modelsnoembeds document entry in the pcbdoc file (not implemented)
        class ModelsNoEmbeds : PcbDocEntry
        {
            public ModelsNoEmbeds(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }
        }

        // class for the textures document entry in the pcbdoc file (not implemented)
        class Textures : PcbDocEntry
        {
            public Textures(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }
        }

    }
}
