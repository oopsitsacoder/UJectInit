using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UJect.Init.CommonImpl;
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

        [NonSerialized] private readonly List<Type> initImplChoices = new();

        [SerializeField] private GUIContent[] initImplChoiceLabels;

        [SerializeField] private int selectedIndex = 0;

        [SerializeField] private string? implError;
        [SerializeField] private string? bindingError;
        [SerializeField] private string? resolveError;

        [NonSerialized] private bool implNeedsInit = true;
        private IUJectInitImpl? selectedImpl = null;
        private DiContainer? diContainer = null;

        private void OnEnable()
        {
            initImplChoices.Clear();
            initImplChoices.AddRange(TypeCache.GetTypesDerivedFrom<IUJectInitImpl>().OrderBy(t => t.FullName));
            initImplChoiceLabels = initImplChoices.Select(t => new GUIContent(t.Name, t.FullName)).ToArray();
        }

        private void OnGUI()
        {
            selectedIndex = Mathf.Clamp(selectedIndex, 0, initImplChoiceLabels.Length);
            var newIndex = EditorGUILayout.Popup(selectedIndex, initImplChoiceLabels);

            if (newIndex != selectedIndex || implNeedsInit)
            {
                implError = null;
                bindingError = null;
                resolveError = null;
                diContainer = null;
                
                selectedIndex = newIndex;
                implNeedsInit = false;
                var type = initImplChoices[selectedIndex];
                var field = type.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                if (field == null)
                {
                    implError = $"Could not get instance from impl {type}";
                }
                else
                {
                    var instanceObj = field.GetValue(null);
                    if (instanceObj is not IUJectInitImpl impl)
                    {
                        implError = $"Could not get {nameof(IUJectInitImpl)} from field";
                    }
                    else
                    {
                        selectedImpl = impl;
                    }
                }
            }
            
            TryDrawError("Implementation Error", ref implError);
            TryDrawError("Binding Error", ref bindingError);
            TryDrawError("Resolve Error", ref resolveError);

            if (selectedImpl != null)
            {
                DrawImpl(selectedImpl);
            }

            if (diContainer != null)
            {
                foreach (var rootKey in diContainer.DependencyTree.RootKeys)
                {
                    DrawKey(rootKey, diContainer.DependencyTree);
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

            EditorGUI.indentLevel++;
            foreach (var dep in deps)
            {
                DrawKey(dep, dependencyTree);
            }

            EditorGUI.indentLevel--;
        }


        private void DrawImpl(IUJectInitImpl uJectInitImpl)
        {
            if (uJectInitImpl == null)
            {
                EditorGUILayout.HelpBox("Null impl", MessageType.Error);
                return;
            }

            if (!uJectInitImpl.IsReadyToCollect)
            {
                EditorGUILayout.HelpBox("Null impl", MessageType.Error);
                return;
            }
            
            var bindMethodsByAttributeType = uJectInitImpl.CollectBindMethodsByAttributeType();

            using var _ = new EditorGUILayout.VerticalScope("box");
            EditorGUILayout.LabelField(uJectInitImpl.GetType().Name, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
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

            EditorGUI.indentLevel--;
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
    }
}