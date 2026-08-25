using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UJect.Init.CommonImpl;
using UJect.Init.Reflection;
using UJect.Init.Roslyn;
using UnityEditor;
using UnityEngine;

namespace UJect.Init.Editor
{
    public class DiDebugWindow : EditorWindow
    {
        [MenuItem("UJect/Debug Window")]
        public static void OpenDebugWindow()
        {
            var window = EditorWindow.GetWindow<DiDebugWindow>(title: "DI Debug Window");
            window.Show();
        }

        private readonly CustomPopup<Type> initImplPopup = new(
            () => TypeCache.GetTypesDerivedFrom<IUJectInitImpl>().ToArray(),
            TypeToGUIContent
        );

        private readonly CustomPopup<Type> roslynRegistryPopup = new(
            () =>
            {
                var types = TypeCache.GetTypesDerivedFrom<IDiMethodCollectionRegistry>()
                    .OrderBy(t => t.FullName?.Contains("Assembly-CSharp"))
                    .ThenBy(t => t.FullName)
                    .ToArray();
                return types;
            },
            TypeToGUIContent);

        private static GUIContent TypeToGUIContent(int index, Type t)
        {
            const int SMALL_STRING_SIZE = 64;
            var nameString = t.FullName;
            var fullString = $"[{index}] {nameString}";
            var smallString = fullString;
            if (smallString.Length > SMALL_STRING_SIZE)
            {
                smallString = smallString[..(SMALL_STRING_SIZE-3)] + "...";
            }
            return new GUIContent(smallString, fullString);
        }

        [SerializeField] private int selectedImplIndex = 0;
        [SerializeField] private int selectedRoslynRegistryIndex = 0;

        [SerializeField] private string? implError;
        [SerializeField] private string? bindingError;
        [SerializeField] private string? resolveError;

        [NonSerialized] private bool implNeedsInit = true;
        [NonSerialized] private IUJectInitImpl? currentImpl = null;
        private DiContainer? diContainer = null;
        [NonSerialized] private bool initialized = false;

        private void OnEnable()
        {
            implNeedsInit |= initImplPopup.TryInit(ref selectedImplIndex);
            if (initImplPopup.TryGetValue(ref selectedImplIndex, out _, out var selectedImpl) && selectedImpl == typeof(RoslynImpl))
            {
                implNeedsInit |= roslynRegistryPopup.TryInit(ref selectedRoslynRegistryIndex);
            }
        }

        private bool TryRegenerateCurrentImpl(out IUJectInitImpl uJectInitImpl)
        {
            implError = null;
            uJectInitImpl = null!;
            if (!initImplPopup.TryGetValue(ref selectedImplIndex, out _, out var selectedImplType))
            {
                implError = $"Failed to create {nameof(IUJectInitImpl)} because no concrete types were found.";
                return false;
            }

            if (selectedImplType == typeof(ReflectionDiBindImpl))
            {
                uJectInitImpl = new ReflectionDiBindImpl();
                return true;
            }

            if (selectedImplType == typeof(RoslynImpl))
            {
                if (roslynRegistryPopup.TryGetValue(ref selectedRoslynRegistryIndex, out _, out var selectedRoslynRegistryType))
                {
                    try
                    {
                        uJectInitImpl = new RoslynImpl((IDiMethodCollectionRegistry)Activator.CreateInstance(selectedRoslynRegistryType));
                        return true;
                    }
                    catch (Exception ex)
                    {
                        implError = $"Failed to create {nameof(IDiMethodCollectionRegistry)} of type {selectedRoslynRegistryType.FullName}";
                    }
                }
                else
                {
                    implError = $"Failed to create {nameof(RoslynImpl)} because no Roslyn-generated registries were found. Have you imported the dlls?";
                }
            }

            return false;
        }

        private void OnGUI()
        {
            implNeedsInit |= initImplPopup.Draw(ref selectedImplIndex);
            var implForceRefreshCache = false;
            if (implNeedsInit)
            {
                bindingError = null;
                resolveError = null;
                diContainer = null;
                if (TryRegenerateCurrentImpl(out var impl))
                {
                    currentImpl = impl;
                    implNeedsInit = false;
                    implForceRefreshCache = true;
                }
            }

            TryDrawError("Implementation Error", ref implError);
            TryDrawError("Binding Error", ref bindingError);
            TryDrawError("Resolve Error", ref resolveError);
            if (currentImpl != null)
            {
                DrawImpl(currentImpl, implForceRefreshCache);
            }

            if (diContainer != null)
            {
                EditorGUILayout.LabelField("Dependency Links:", EditorStyles.boldLabel);
                using (var _ = new EditorGUI.IndentLevelScope())
                {
                    foreach (var rootKey in diContainer.DependencyTree.RootKeys)
                    {
                        DrawKey(rootKey, diContainer.DependencyTree);
                    }
                }
            }
        }

        private static void TryDrawError(string errorLabel, ref string? errorString)
        {
            if (string.IsNullOrEmpty(errorString)) return;

            using var _ = new EditorGUILayout.VerticalScope("box");
            EditorGUILayout.LabelField(errorLabel, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(errorString, MessageType.Error);
            var clear = GUILayout.Button("Clear error");
            if (clear)
            {
                errorString = null;
            }
        }

        private void DrawKey(InjectionKey key, IDependencyTree dependencyTree)
        {
            if (!string.IsNullOrEmpty(key.InjectedResourceName))
            {
                EditorGUILayout.LabelField($"{key.InjectedResourceType.Name} \"{key.InjectedResourceName}\"");
            }
            else
            {
                EditorGUILayout.LabelField(key.InjectedResourceType.Name);
            }

            var deps = dependencyTree.DependsOn(key);

            using (var _ = new EditorGUI.IndentLevelScope())
            {
                foreach (var dep in deps)
                {
                    DrawKey(dep, dependencyTree);
                }
            }
        }

        private void DrawImpl(IUJectInitImpl uJectInitImpl, bool implForceRefreshCache)
        {
            using var _ = new EditorGUILayout.VerticalScope("box");
            EditorGUILayout.LabelField(uJectInitImpl.GetType().Name, EditorStyles.boldLabel);
            using (var indentScope = new EditorGUI.IndentLevelScope())
            {
                if (roslynRegistryPopup.Draw(ref selectedRoslynRegistryIndex))
                {
                    // We changed selected registry, so we need to reinitialize
                    implNeedsInit = true;
                    return;
                }

                if (!uJectInitImpl.IsReadyToCollect)
                {
                    EditorGUILayout.HelpBox("Not yet ready", MessageType.Error);
                    return;
                }

                if (!initialized)
                {
                    initialized |= GUILayout.Button("Initialize");
                }

                if (!initialized)
                {
                    EditorGUILayout.HelpBox("Initialize to begin debugging.", MessageType.Info);
                    return;
                }

                var bindMethodsByAttributeType = uJectInitImpl.CollectBindMethodsByAttributeType(implForceRefreshCache);

                EditorGUILayout.LabelField("DI Bind Attributes:");
                using (var attributeIndentScope = new EditorGUI.IndentLevelScope())
                {
                    foreach (var kvp in bindMethodsByAttributeType)
                    {
                        bool run = false;

                        using (var h = new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(kvp.Key.Name);
                            run = GUILayout.Button("Run For Attribute");
                        }

                        if (run)
                        {
                            try
                            {
                                bindingError = null;
                                diContainer = new DiContainer("TestContainer");
                                uJectInitImpl.CollectBindMethodsByAttributeType()[kvp.Key].RunBindMethods(diContainer);

                                try
                                {
                                    resolveError = null;
                                    diContainer.TryResolveAll();
                                }
                                catch (Exception resolveEx)
                                {
                                    Debug.LogException(resolveEx);
                                    resolveError = resolveEx.ToString();
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogException(ex);
                                bindingError = ex.ToString();
                            }
                        }
                    }
                }
            }

            var runAll = GUILayout.Button("Run All");
            if (runAll)
            {
                try
                {
                    bindingError = null;
                    diContainer = new DiContainer("TestContainer");
                    uJectInitImpl.RunBindMethods(diContainer);

                    try
                    {
                        resolveError = null;
                        diContainer.TryResolveAll();
                    }
                    catch (Exception resolveEx)
                    {
                        Debug.LogException(resolveEx);
                        resolveError = resolveEx.ToString();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    bindingError = ex.ToString();
                }
            }
        }

        private class CustomPopup<T>
        {
            [NonSerialized] private bool isInitialized = false;
            [NonSerialized] private T[] options;
            [NonSerialized] private GUIContent[] optionLabels;

            public delegate IEnumerable<T> FetchOptionsDelegate();

            public delegate GUIContent ConvertToGUIContentDelegate(int index, T option);

            private FetchOptionsDelegate fetchOptions;
            private ConvertToGUIContentDelegate convertToGUIContent;

            public CustomPopup(FetchOptionsDelegate fetchOptions, ConvertToGUIContentDelegate convertToGUIContent)
            {
                this.fetchOptions = fetchOptions;
                this.convertToGUIContent = convertToGUIContent;
            }

            public bool TryInit(ref int selectedIndex)
            {
                if (isInitialized) return false;

                options = fetchOptions().ToArray();
                optionLabels = options.Select((o, i) => convertToGUIContent(i, o)).ToArray();
                isInitialized = true;

                var newIndex = Mathf.Clamp(selectedIndex, 0, options.Length);
                selectedIndex = newIndex;
                return newIndex != selectedIndex;
            }

            public bool TryGetValue(ref int index, out bool changed, out T value)
            {
                changed = TryInit(ref index);
                if (options.Length > 0)
                {
                    value = options[index];
                    return true;
                }

                value = default;
                return false;
            }

            public bool Draw(ref int selectedIndex)
            {
                var changed = TryInit(ref selectedIndex);
                var newIndex = EditorGUILayout.Popup(selectedIndex, optionLabels);
                changed |= (selectedIndex != newIndex);
                selectedIndex = newIndex;
                return changed;
            }
        }
    }
}