using System;
using System.Reflection;
using System.Collections.Generic;

namespace Alto.IO
{
    internal static class Debug
    {
        public static void Dump(params object[] items)
        {
            foreach (var item in items)
            {
                var type = item.GetType();

                Console.WriteLine(item);
            }
        }
    }
}