using System.Collections.Generic;

namespace UJect.Init.Reflection
{
    public class DefaultAssemblyFilter : AssemblyNameFilter
    {
        public static readonly DefaultAssemblyFilter Instance = new();
        public DefaultAssemblyFilter() : base(new HashSet<string>()
        {
            "mscorlib",
            "UnityEngine",
            "UnityEditor",
            "netstandard",
            "I18N",
            "System"
        }, new HashSet<string>()
        {
            "UnityEngine.",
            "UnityEditor.",
            "I18N.",
            "System.",
            "Mono.",
            "Unity.",
            "Bee.",
            "nunit.",
            "JetBrains.",
            "Newtonsoft."
        })
        {
        }
    }
}