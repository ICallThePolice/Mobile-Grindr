using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SerializeReferenceMenuAttribute))]
public class SerializeReferenceMenuDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect labelRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.PropertyField(labelRect, property, label, true);

        if (property.propertyType == SerializedPropertyType.ManagedReference)
        {
            Rect buttonRect = new Rect(position.x + EditorGUIUtility.labelWidth, position.y, position.width - EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight);

            string buttonText = property.managedReferenceValue == null ? "Select Type..." : property.managedReferenceValue.GetType().Name;

            if (GUI.Button(buttonRect, buttonText, EditorStyles.popup))
            {
                ShowMenu(property);
            }
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, true);
    }

    private void ShowMenu(SerializedProperty property)
    {
        GenericMenu menu = new GenericMenu();
        Type fieldType = GetTypeFromManagedReferenceFullTypename(property.managedReferenceFieldTypename);

        if (fieldType != null)
        {
            var types = TypeCache.GetTypesDerivedFrom(fieldType).Where(t => !t.IsAbstract && !t.IsInterface);

            menu.AddItem(new GUIContent("Null"), false, () => AssignType(property, null));
            menu.AddSeparator("");

            foreach (var type in types)
            {
                menu.AddItem(new GUIContent(type.Name), false, () => AssignType(property, type));
            }
        }

        menu.ShowAsContext();
    }

    private void AssignType(SerializedProperty property, Type type)
    {
        property.serializedObject.Update();
        object instance = type == null ? null : Activator.CreateInstance(type);
        property.managedReferenceValue = instance;
        property.serializedObject.ApplyModifiedProperties();
    }

    private Type GetTypeFromManagedReferenceFullTypename(string managedReferenceFieldTypename)
    {
        string[] parts = managedReferenceFieldTypename.Split(' ');
        if (parts.Length == 2)
        {
            string assemblyName = parts[0];
            string typeName = parts[1];
            return Type.GetType($"{typeName}, {assemblyName}");
        }
        return null;
    }
}