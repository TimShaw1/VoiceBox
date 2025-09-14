using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TimShaw.VoiceBox.Editor
{

    [AttributeUsage(AttributeTargets.Field)]
    public class OptionsAttribute : PropertyAttribute
    {
        public string OptionsName { get; set; }

        public OptionsAttribute(string OptionsName)
        {
            this.OptionsName = OptionsName;
        }
    }

    [CustomPropertyDrawer(typeof(OptionsAttribute), true)]
    public class OptionAttributeDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.HelpBox(position, "Only works with string", MessageType.Error);
                return;
            }

            OptionsAttribute optionAttribute = attribute as OptionsAttribute;

            SerializedProperty options = property.serializedObject.FindProperty(optionAttribute.OptionsName);
            List<string> optionList = new List<string>();

            for (int i = 0; i < options.arraySize; i++)
            {
                SerializedProperty name = options.GetArrayElementAtIndex(i);
                optionList.Add(name.stringValue);
            }

            EditorGUI.BeginProperty(position, label, property);

            EditorGUI.BeginChangeCheck();
            int index = EditorGUI.Popup(position, property.name, optionList.IndexOf(property.stringValue), optionList.ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                property.stringValue = optionList[index];
            }

            EditorGUI.EndProperty();
        }
    }
}