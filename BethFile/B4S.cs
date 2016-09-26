using System;
using System.Linq;
using System.Text;

namespace BethFile
{
    public struct B4S : IEquatable<B4S>
    {
        public static readonly B4S TES4 = new B4S("TES4");
        public static readonly B4S GRUP = new B4S("GRUP");
        public static readonly B4S QUST = new B4S("QUST");
        public static readonly B4S CELL = new B4S("CELL");
        public static readonly B4S REFR = new B4S("REFR");
        public static readonly B4S WRLD = new B4S("WRLD");
        public static readonly B4S NAVM = new B4S("NAVM");
        public static readonly B4S WEAP = new B4S("WEAP");
        public static readonly B4S SCEN = new B4S("SCEN");
        public static readonly B4S PACK = new B4S("PACK");
        public static readonly B4S NPC_ = new B4S("NPC_");
        public static readonly B4S IDLE = new B4S("IDLE");
        public static readonly B4S GMST = new B4S("GMST");
        public static readonly B4S DLVW = new B4S("DLVW");
        public static readonly B4S DLBR = new B4S("DLBR");
        public static readonly B4S DIAL = new B4S("DIAL");
        public static readonly B4S CPTH = new B4S("CPTH");
        public static readonly B4S ACHR = new B4S("ACHR");
        public static readonly B4S BOOK = new B4S("BOOK");
        public static readonly B4S XXXX = new B4S("XXXX");
        public static readonly B4S HEDR = new B4S("HEDR");
        public static readonly B4S DOBJ = new B4S("DOBJ");

        private uint val;

        public unsafe B4S(string val)
        {
            if (val?.Length != 4 || val.Max() > 127)
            {
                this.val = 0;
                return;
            }

            fixed (char* pchar = val)
            fixed (B4S* pval = &this)
            {
                byte* pbyte = (byte*)pval;
                Encoding.ASCII.GetBytes(pchar, 4, pbyte, 4);
            }
        }

        private B4S(uint val)
        {
            this.val = val;
        }

        public static implicit operator uint(B4S val) => val.val;

        public static implicit operator B4S(uint val) => new B4S(val);

        public static bool operator ==(B4S first, B4S second) => first.val == second.val;
        public static bool operator !=(B4S first, B4S second) => first.val != second.val;

        public override bool Equals(object obj) => obj is B4S && this.val == ((B4S)obj).val;
        public bool Equals(B4S other) => this.val == other.val;
        public override int GetHashCode() => unchecked((int)this.val);

        public unsafe void CopyTo(UArraySegment<byte> seg)
        {
            fixed (byte* pbyte = &seg.Array[seg.Offset])
            {
                *((uint*)pbyte) = this.val;
            }
        }

        public override unsafe string ToString()
        {
            fixed (B4S* pval = &this)
            {
                return Encoding.ASCII.GetString((byte*)pval, 4);
            }
        }
    }
}
