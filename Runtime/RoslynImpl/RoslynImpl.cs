using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UJect.Init.CommonImpl;
using UJect.Init.Reflection;
using UJect.Utilities;
using UnityEngine;

namespace UJect.Init.Roslyn
{
    /// <summary>
    /// Helper class for running DiBind methods via Roslyn generated classes (one per assembly).
    ///
    /// UJectAnalyzers.dll will generate a class named __DiBindMethodCollection per-assembly, instances of which are then collected via reflection.
    /// Once all instances are collected, they're used to create a standard IBindMethodCollection lookup.
    /// 
    /// This can be quicker at runtime for a large number of methods, at the cost of compile time.
    ///
    /// This class assumes you have added the UJect Roslyn Analyzer to the project, AND WILL NOT WORK OTHERWISE
    /// </summary>
    public class RoslynImpl : IUJectInitImpl
    {
        public bool IsReadyToCollect
        {
            get
            {
#if UNITY_EDITOR
                if (UnityEditor.EditorApplication.isCompiling) return false;
#endif
                return true;
            }
        }

        private readonly IDiMethodCollectionRegistry diRegistry;

        public RoslynImpl(IDiMethodCollectionRegistry diRegistry)
        {
            this.diRegistry = diRegistry;
        }

        private bool hasCachedMethods = false;
        private readonly List<IDiBindMethodCollection> cachedMethodCollections = new();
        private readonly Dictionary<Type, IBindMethodCollection> bindMethodCollectionsByAttributeType = new();

        private void TryInit(bool forceRefreshCache = false, IAssemblyFilter? assemblyFilter = null)
        {
            if (!forceRefreshCache && hasCachedMethods) return;
            hasCachedMethods = true;
            

            cachedMethodCollections.Clear();
            bindMethodCollectionsByAttributeType.Clear();

            var newMethodCollections = new HashSet<IDiBindMethodCollection>();
            diRegistry.CollectMethodCollections(newMethodCollections);
            cachedMethodCollections.AddRange(newMethodCollections);

            var methodListLookup = new Dictionary<Type, List<RunDiBindMethod>>();
            foreach (var diBindMethodCollection in cachedMethodCollections)
            {
                foreach (var byAttribute in diBindMethodCollection.MethodLookup)
                {
                    var attributeType = byAttribute.Key;
                    var assemblyAction = byAttribute.Value;

                    if (!methodListLookup.TryGetValue(attributeType, out List<RunDiBindMethod> methodList))
                    {
                        methodList = new List<RunDiBindMethod>();
                        methodListLookup[attributeType] = methodList;
                    }

                    methodList.Add(assemblyAction);
                }
            }

            foreach (var kvp in methodListLookup)
            {
                bindMethodCollectionsByAttributeType[kvp.Key] = new ActionListBindMethodCollection(kvp.Value);
            }
        }

        public void RunBindMethods(DiContainer diContainer) => RunBindMethods(diContainer, false, null);
        
        public void RunBindMethods(DiContainer diContainer, bool forceRefreshCache, IAssemblyFilter? assemblyFilter)
        {
            TryInit(forceRefreshCache, assemblyFilter);
            foreach (var diBindMethodCollection in cachedMethodCollections)
            {
                diBindMethodCollection.Run(diContainer);
            }
        }

        public IReadOnlyDictionary<Type, IBindMethodCollection> CollectBindMethodsByAttributeType(bool forceRefreshCache = false, IAssemblyFilter? assemblyFilter = null)
        {
            TryInit(forceRefreshCache, assemblyFilter);
            return bindMethodCollectionsByAttributeType;
        }

        private class ActionListBindMethodCollection : IBindMethodCollection
        {
            private readonly List<RunDiBindMethod> actionsToRun;
            public ActionListBindMethodCollection(List<RunDiBindMethod> actionsToRun) => this.actionsToRun = actionsToRun;

            public void RunBindMethods(DiContainer diContainer)
            {
                foreach (var action in actionsToRun)
                {
                    action(diContainer);
                }
            }
        }
    }
}