using System;
using System.Linq;
using System.Text;

namespace BethFile
{
    public struct B4S : IEquatable<B4S>
    {
        // get values by running this in C# Interactive:
        ////System.BitConverter.ToUInt32(System.Text.Encoding.ASCII.GetBytes("TES4"), 0)
        public const uint _TES4 = 877872468;
        public const uint _GRUP = 1347768903;
        public const uint _QUST = 1414747473;
        public const uint _CELL = 1280066883;
        public const uint _REFR = 1380336978;
        public const uint _WRLD = 1145852503;
        public const uint _NAVM = 1297498446;
        public const uint _WEAP = 1346454871;
        public const uint _SCEN = 1313162067;
        public const uint _PACK = 1262698832;
        public const uint _NPC_ = 1598246990;
        public const uint _IDLE = 1162626121;
        public const uint _GMST = 1414745415;
        public const uint _DLVW = 1465273412;
        public const uint _DLBR = 1380076612;
        public const uint _DIAL = 1279347012;
        public const uint _CPTH = 1213485123;
        public const uint _ACHR = 1380467521;
        public const uint _BOOK = 1263488834;
        public const uint _XXXX = 1482184792;
        public const uint _HEDR = 1380205896;
        public const uint _DOBJ = 1245859652;
        public const uint _DATA = 1096040772;
        public const uint _XESP = 1347634520;
        public const uint _ONAM = 1296125519;
        public const uint _OFST = 1414743631;
        public const uint _MGEF = 1178945357;
        public const uint _ARMO = 1330467393;
        public const uint _CONT = 1414418243;
        public const uint _LIGH = 1212631372;
        public const uint _LVLN = 1313625676;
        public const uint _RNAM = 1296125522;
        public const uint _IMAD = 1145130313;
        public const uint _FLST = 1414745158;
        public const uint _LCTN = 1314145100;
        public const uint _SMQN = 1313951059;
        public const uint _SMBN = 1312968019;
        public const uint _LGTM = 1297368908;

        public static readonly B4S TES4 = _TES4;
        public static readonly B4S GRUP = _GRUP;
        public static readonly B4S QUST = _QUST;
        public static readonly B4S CELL = _CELL;
        public static readonly B4S REFR = _REFR;
        public static readonly B4S WRLD = _WRLD;
        public static readonly B4S NAVM = _NAVM;
        public static readonly B4S WEAP = _WEAP;
        public static readonly B4S SCEN = _SCEN;
        public static readonly B4S PACK = _PACK;
        public static readonly B4S NPC_ = _NPC_;
        public static readonly B4S IDLE = _IDLE;
        public static readonly B4S GMST = _GMST;
        public static readonly B4S DLVW = _DLVW;
        public static readonly B4S DLBR = _DLBR;
        public static readonly B4S DIAL = _DIAL;
        public static readonly B4S CPTH = _CPTH;
        public static readonly B4S ACHR = _ACHR;
        public static readonly B4S BOOK = _BOOK;
        public static readonly B4S XXXX = _XXXX;
        public static readonly B4S HEDR = _HEDR;
        public static readonly B4S DOBJ = _DOBJ;
        public static readonly B4S DATA = _DATA;
        public static readonly B4S XESP = _XESP;
        public static readonly B4S ONAM = _ONAM;
        public static readonly B4S OFST = _OFST;
        public static readonly B4S MGEF = _MGEF;
        public static readonly B4S ARMO = _ARMO;
        public static readonly B4S CONT = _CONT;
        public static readonly B4S LIGH = _LIGH;
        public static readonly B4S LVLN = _LVLN;
        public static readonly B4S RNAM = _RNAM;
        public static readonly B4S IMAD = _IMAD;
        public static readonly B4S FLST = _FLST;
        public static readonly B4S LCTN = _LCTN;
        public static readonly B4S SMQN = _SMQN;
        public static readonly B4S SMBN = _SMBN;
        public static readonly B4S LGTM = _LGTM;

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
