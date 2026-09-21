using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MPUIKIT.Editor
{
	public class EditorGUILayoutExtended : UnityEditor.Editor
	{
		// Unity renamed/removed the internals this editor used to reflect into (EditorGUI.DoFloatField and
		// EditorGUI.s_RecycledEditor). The lookups now return null, so resolving them in a static initializer
		// threw a TypeInitializationException that broke the whole MPImage inspector. Everything internal is
		// therefore resolved lazily inside a try/catch and the field falls back to a plain EditorGUI.FloatField.
		private static Type editorGUIType;
		private static Type RecycledTextEditorType;
		private static Type[] argumentTypes;
		private static MethodInfo doFloatFieldMethod;
		private static FieldInfo fieldInfo;
		private static object recycledEditor;
		private static GUIStyle style;

		private static bool reflectionInitialized;
		private static bool reflectionAvailable;

		private static void InitReflection()
		{
			if (reflectionInitialized) return;
			reflectionInitialized = true;

			try
			{
				editorGUIType = typeof(EditorGUI);

				RecycledTextEditorType =
					Assembly.GetAssembly(editorGUIType).GetType("UnityEditor.EditorGUI+RecycledTextEditor");
				if (RecycledTextEditorType == null) return;

				argumentTypes = new[]
				{
					RecycledTextEditorType, typeof(Rect), typeof(Rect), typeof(int), typeof(float),
					typeof(string), typeof(GUIStyle), typeof(bool)
				};

				doFloatFieldMethod = editorGUIType.GetMethod("DoFloatField",
					BindingFlags.NonPublic | BindingFlags.Static, null, argumentTypes, null);
				if (doFloatFieldMethod == null) return;

				fieldInfo = editorGUIType.GetField("s_RecycledEditor",
					BindingFlags.NonPublic | BindingFlags.Static);
				if (fieldInfo == null) return;

				recycledEditor = fieldInfo.GetValue(null);
				if (recycledEditor == null) return;

				style = EditorStyles.numberField;
				reflectionAvailable = true;
			}
			catch
			{
				reflectionAvailable = false;
			}
		}

		public static float FloatFieldExtended(Rect _position, float _value, Rect _dragHotZone)
		{
			InitReflection();

			if (reflectionAvailable)
			{
				try
				{
					int controlId = GUIUtility.GetControlID("EditorTextField".GetHashCode(), FocusType.Keyboard,
						_position);
					object[] parameters =
						{recycledEditor, _position, _dragHotZone, controlId, _value, "g7", style, true};
					return (float) doFloatFieldMethod.Invoke(null, parameters);
				}
				catch
				{
					reflectionAvailable = false;
				}
			}

			// Unity 6 fallback: a regular float field. Loses the drag-on-label nicety but keeps
			// the inspector functional.
			return EditorGUI.FloatField(_position, _value);
		}

//	public static float FloatField(GUIContent _content, float _value, float _inputBoxWidth, params GUILayoutOption[] _options)
//	{
//		Rect totalRect = EditorGUILayout.GetControlRect(_options);
//		float width;
//		if (_inputBoxWidth < 1) width = totalRect.width * Mathf.Clamp(_inputBoxWidth, 0.2f, 0.8f);
//		else width = Mathf.Clamp(_inputBoxWidth, totalRect.width * 0.2f, totalRect.width * 0.8f);
//		Rect labelRect = new Rect(totalRect.x, totalRect.y, totalRect.width - width - 8, totalRect.height);
//		Rect inputRect = new Rect(totalRect.x + totalRect.width - width, totalRect.y, width, totalRect.height);
//		
//		EditorGUI.LabelField(labelRect, _content);
//		return FloatFieldExtended(inputRect, _value, labelRect);
//	}

		public static float FloatField(GUIContent _content, float _value, float _labelwidth,
			params GUILayoutOption[] _options)
		{
			Rect totalRect = EditorGUILayout.GetControlRect(_options);
//		float width;
//		if (_labelwidth < 1) width = totalRect.width * Mathf.Clamp(_labelwidth, 0.2f, 0.8f);
//		else width = Mathf.Clamp(_labelwidth, totalRect.width * 0.2f, totalRect.width * 0.8f);

			Rect labelRect = new Rect(totalRect.x, totalRect.y, _labelwidth, totalRect.height);
			Rect inputRect = new Rect(totalRect.x + _labelwidth, totalRect.y, totalRect.width - _labelwidth,
				totalRect.height);

//		Rect labelRect = new Rect(totalRect.x, totalRect.y, totalRect.width - width - 8, totalRect.height);
//		Rect inputRect = new Rect(totalRect.x + totalRect.width - width, totalRect.y, width, totalRect.height);

			EditorGUI.LabelField(labelRect, _content);
			return FloatFieldExtended(inputRect, _value, labelRect);
		}

	}
}