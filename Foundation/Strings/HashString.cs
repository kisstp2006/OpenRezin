using System;
using System.Collections.Generic;
using System.Text;

namespace Foundation.Strings
{
    
    public struct HashString
    {
        public ulong handle = 0;
        public string? value = null;

        public HashString()
        {
        }

        public HashString(string str)
        {
            if (str == null) return;

            handle = ComputeHash(str);
            value = str;
        }

        private static ulong ComputeHash(string str)
        {
            const ulong offsetBasis = 14695981039346656037;

            const ulong prime = 1099511628211;

            ulong hash = offsetBasis;

            str = str.ToLowerInvariant();

            byte[] bytes = Encoding.UTF8.GetBytes(str);

            foreach (byte b in bytes)
            {
                hash ^= b;
                hash *= prime;
            }
            return hash;
        }

        public HashString(string str,int len)
        {
            if (str == null) return;

            str = str.Substring(0, len);

            handle = ComputeHash(str);
            value = str;



        }
        public HashString( HashString hstr)
        {
            handle = hstr.handle;
            value = hstr.value;
        }

    }
}

