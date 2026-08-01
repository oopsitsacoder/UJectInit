using System.Reflection;

namespace UJect.Init.Reflection
{
    public interface IAssemblyFilter
    {
        bool ShouldProcessAssembly(Assembly assembly);
    }
}