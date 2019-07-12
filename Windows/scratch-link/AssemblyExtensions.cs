using System;
using System.Reflection;

namespace scratch_link
{
    static class AssemblyExtensions
    {
        internal static T GetCustomAttribute<T>(this Assembly assembly) where T : Attribute
        {
            return assembly.GetCustomAttribute(typeof(T)) as T;
        }

    }
}
