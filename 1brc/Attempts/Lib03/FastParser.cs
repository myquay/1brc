using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace brc.Attempts.Lib03
{
    public class FastParser
    {

        public const byte sign = (byte)'-';
        public const byte dot = (byte)'.';
        public const byte digitOffset = (byte)'0';

        public static int TempAsInt(ReadOnlySpan<byte> chunk)
        {
            bool negative = chunk[0] == sign;
            int off = negative ? 1 : 0;

            int temp = chunk[off++] - digitOffset;
            
            if (chunk[off] != dot)
                temp = 10 * temp + chunk[off++] - digitOffset;
            off++; //Skip the '.' (Max 2 digits before '.')

            temp = 10 * temp + chunk[off] - digitOffset;
            return negative ? -temp : temp;
        }
    }
}