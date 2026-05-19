using System;
using System.Collections.Generic;

namespace UJect.Init.CommonImpl
{
    /// <summary>
    /// A collection of bind methods
    /// </summary>
    public interface IBindMethodCollection
    {
        /// <summary>
        /// Run all bind methods in the given collection against the provided <paramref name="diContainer"/>
        /// </summary>
        /// <param name="diContainer">The DiContainer to bind to</param>
        void RunBindMethods(DiContainer diContainer);
    }
    
    public interface IUJectInitImpl : IBindMethodCollection
    {
        /// <summary>
        /// Collect bind methods grouped by attribute type. If you're not using custom attribute types, you can use <see cref="IBindMethodCollection.RunBindMethods"/> directly.
        /// </summary>
        IReadOnlyDictionary<Type, IBindMethodCollection> CollectBindMethodsByAttributeType();
    }

}