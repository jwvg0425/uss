﻿using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UssStyleModifier : MonoBehaviour
{
    private class ModifierData
    {
        public object modifier;
        public MethodInfo method;
        public Type acceptedComponent;
        public bool isArrayParameter;
    }

    public static bool hasError = false;
    public static bool applied = false;

    private static List<UssStyleDefinition> styles;
    private static Dictionary<string, ModifierData> modifiers;

    private static DateTime applyTime;

    static UssStyleModifier()
    {
        modifiers = new Dictionary<string, ModifierData>();

        // Load default modifiers
        LoadModifier<UssColorModifier>();
        LoadModifier<UssTextModifier>();
        LoadModifier<UssOutlineModifier>();
        LoadModifier<UssShadowModifier>();
        LoadModifier<UssPaddingModifier>();
        LoadModifier<UssOverflowModifier>();
    }
    public static void LoadModifier<T>()
        where T : new()
    {
        var obj = new T();

        foreach (var method in typeof(T).GetMethods())
        {
            var attrs = method.GetCustomAttributes(typeof(UssModifierKeyAttribute), true);
            if (attrs.Length == 0)
                continue;

            var key = ((UssModifierKeyAttribute)attrs[0]).key;
            if (modifiers.ContainsKey(key))
                throw new InvalidOperationException("Already has modifier with key: " + key);
            if (method.GetParameters().Length != 2)
                throw new InvalidOperationException("Invalid modifier format. Params.Length must be length of 2.");

            modifiers.Add(key, new ModifierData()
            {
                modifier = obj,
                method = method,
                acceptedComponent = method.GetParameters().First().ParameterType,
                isArrayParameter = method.GetParameters().Last().ParameterType.IsArray
            });
        }
    }
    public static void LoadUss(string uss)
    {
        try
        {
            UssValues.Reset();
            var result = UssParser.Parse(uss);
            styles = new List<UssStyleDefinition>(result.styles);
            foreach (var pair in result.values)
                UssValues.SetValue(pair.Key, pair.Value);

            applyTime = DateTime.Now;
            Apply(GameObject.Find("Canvas"));

            hasError = false;
            applied = true;

#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
#endif
        }
        catch(Exception e)
        {
            hasError = true;
            Debug.LogException(e);
        }
    }

    public static void Apply(GameObject g)
    {
        if (g == null)
            ;
        if (styles == null)
            throw new InvalidOperationException(".ucss file not loaded yet.");

        foreach (var style in styles)
        {
            if (CheckConditions(g, style.conditions) == false)
                continue;

            AddInspectorItem(g, style);

            foreach (var p in style.properties)
            {
                foreach (var m in modifiers)
                {
                    if (p.key != m.Key) continue;

                    var comp = g.GetComponent(m.Value.acceptedComponent);
                    if (comp == null) continue;

                    if (m.Value.isArrayParameter)
                    {
                        m.Value.method.Invoke(m.Value.modifier, new object[]{
                            comp, p.values
                        });
                    }
                    else
                    {
                        m.Value.method.Invoke(m.Value.modifier, new object[]{
                            comp, p.values[0]
                        });
                    }
                }
            }
        }

        for (int i = 0; i < g.transform.childCount; i++)
            Apply(g.transform.GetChild(i).gameObject);
    }

    private static bool CheckConditions(GameObject g, UssStyleCondition[] conditions)
    {
        foreach (var c in conditions)
        {
            if (c.target == UssStyleTarget.Name)
            {
                if (g.name != c.name)
                    return false;
            }
            else if (c.target == UssStyleTarget.Component)
            {
                if (g.GetComponent(c.name) == null)
                    return false;
            }
            else if (c.target == UssStyleTarget.Class)
            {
                var klass = g.GetComponent<UssClass>();
                if (klass == null)
                    return false;
                if (klass.classes.Split(' ').Contains(c.name) == false)
                    return false;
            }
        }

        return true;
    }

    private static void AddInspectorItem(GameObject g, UssStyleDefinition style)
    {
        var insp = g.GetComponent<UssInspector>();
        if (insp == null)
            insp = g.AddComponent<UssInspector>();

        if (insp.updatedAt != applyTime)
            insp.Clear();

        insp.applied.Add(style);
        insp.updatedAt = applyTime;
    }
}
