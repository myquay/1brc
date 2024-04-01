using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace brc.Attempts.Lib04
{
    public class Utilities
    {
        public const byte sign = (byte)'-';
        public const byte dot = (byte)'.';
        public const byte digitOffset = (byte)'0';

        public static int FastParseTemp(ReadOnlySpan<byte> chunk)
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

        public static long GenerateKey(ReadOnlySpan<byte> chunk)
        {
            //Take the first 7 characters + length
            long key = chunk.Length << 64;

            for (int i = 0, l = chunk.Length; i < l && i < 7; i++)
                key += chunk[i] << (56 - i * 8);

            return key;
        }
    }
}