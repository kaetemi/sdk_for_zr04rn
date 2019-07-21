using System;
using System.Collections.Generic;
using System.Text;

namespace ApiForZR04RN
{
    static class CommonUtility
    {
        public static T[] SubArray<T>(this T[] data, int index, int length)
        {
            T[] result = new T[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }

        public static string NullTerminate(this string data)
        {
            int i = data.IndexOf('\0');
            if (i >= 0)
                return data.Substring(0, i);
            return data;
        }

        public static string Passwordize(this string data)
        {
            return new string('*', data.Length);
        }

    }
}
