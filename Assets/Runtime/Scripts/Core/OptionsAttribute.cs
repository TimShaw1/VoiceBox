#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TimShaw.VoiceBox.Editor
{
    /// <summary>
    /// An attribute to create a dropdown list in the Unity Inspector for a string field.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class OptionsAttribute : PropertyAttribute
    {
        /// <summary>
        /// Gets or sets the name of the string array property that contains the options for the dropdown.
        /// </summary>
        public string OptionsName { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OptionsAttribute"/> class.
        /// </summary>
        /// <param name="OptionsName">The name of the string array property that contains the options.</param>
        public OptionsAttribute(string OptionsName)
        {
            this.OptionsName = OptionsName;
        }
    }

    /// <summary>
    /// A custom property drawer for the <see cref="OptionsAttribute"/>.
    /// </summary>
    [CustomPropertyDrawer(typeof(OptionsAttribute), true)]
    public class OptionAttributeDrawer : PropertyDrawer
    {
        /// <summary>
        /// Gets the height of the property in the inspector.
        /// </summary>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">The label of this property.</param>
        /// <returns>The height of the property.</returns>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        /// <summary>
        /// Draws the property inside the given rect.
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property GUI.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">The label of this property.</param>
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

#endif