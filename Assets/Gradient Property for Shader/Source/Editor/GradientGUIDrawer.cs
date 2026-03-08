using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using GPFS.Editor;
#endif

#if UNITY_EDITOR
public class GradientGUIDrawer : MaterialPropertyDrawer
{
	private readonly int resolution = 256;
	private static readonly Dictionary<string, Texture2D> lastKnownTextures = new Dictionary<string, Texture2D>();
	private static readonly Dictionary<string, Gradient> gradientCache = new Dictionary<string, Gradient>();

	public override void OnGUI(Rect rect, MaterialProperty property, GUIContent label, MaterialEditor editor)
	{
		if (property.targets.Length != 1) return;
		var target = (Material)property.targets[0];
		string path = AssetDatabase.GetAssetPath(target);

		string materialKey = $"{target.GetInstanceID()}_{property.name}";

		string textureName = $"{property.name} Texture";

		Gradient gradient = null;
		bool textureChanged = false;

		if (property.targets.Length == 1)
		{
#if UNITY_2021_1_OR_NEWER
			var currentTexture = target.HasTexture(property.name) ? target.GetTexture(property.name) as Texture2D : null;
#else
			var currentTexture = target.GetTexture(property.name) as Texture2D;
#endif

			if (lastKnownTextures.TryGetValue(materialKey, out Texture2D lastTexture))
			{
				if (lastTexture != currentTexture)
				{
					textureChanged = true;
					lastKnownTextures[materialKey] = currentTexture;
				}
			}
			else
			{
				lastKnownTextures[materialKey] = currentTexture;
				textureChanged = true;
			}

			if (!gradientCache.TryGetValue(materialKey, out gradient))
			{
				if (currentTexture != null && currentTexture.name.StartsWith(textureName))
					gradient = Decode(currentTexture.name, textureName);
				else
				{
					var textureAsset = LoadTexture(path, textureName);
					if (textureAsset != null && textureAsset.name.StartsWith(textureName))
						gradient = Decode(textureAsset.name, textureName);
				}

				gradient ??= GetNewGradient();
				gradientCache[materialKey] = gradient;
			}

			EditorGUI.showMixedValue = false;
		}
		else
			EditorGUI.showMixedValue = true;

		float fieldWidth = EditorGUIUtility.fieldWidth;
		float spacing = 2;

		Rect labelRect = new Rect(rect.x, rect.y, fieldWidth * 3, rect.height);
		Rect gradientRect = new Rect(rect.x + labelRect.width, rect.y, rect.width - fieldWidth - spacing - labelRect.width, rect.height);
		Rect buttonRect = new Rect(rect.x + rect.width - fieldWidth, rect.y, fieldWidth, rect.height);

		GUI.Label(labelRect, label);
		EditorGUI.BeginChangeCheck();
		gradient = EditorGUI.GradientField(gradientRect, gradient);

		if (GUI.Button(buttonRect, "Clean"))
		{
			GradientUtility.ApplyClean(target, property);
			gradientCache.Remove(materialKey);
			Debounce.Cancel(materialKey);
			editor.Repaint();
			return;
		}

		if (textureChanged) editor.Repaint();

		if (!EditorGUI.EndChangeCheck()) return;

		gradientCache[materialKey] = gradient;

		bool commit =
			Event.current.type == EventType.MouseUp ||
			Event.current.type == EventType.KeyUp ||
			Event.current.type == EventType.ExecuteCommand;

		Debounce.Request(
			key: materialKey,
			seconds: 0.1,
			action: () => ApplyGradient(
				materialKey,
				target,
				path,
				property.name,
				textureName
			),
			force: commit
		);

		EditorGUI.showMixedValue = false;
	}

	private Gradient GetNewGradient()
	{
		var colorKeys = new GradientColorKey[2];
		var alphaKeys = new GradientAlphaKey[2];
		colorKeys[0] = new GradientColorKey(Color.black, 0f);
		alphaKeys[0] = new GradientAlphaKey(1, 0f);
		colorKeys[1] = new GradientColorKey(Color.white, 1f);
		alphaKeys[1] = new GradientAlphaKey(1, 1f);

		return new Gradient { colorKeys = colorKeys, alphaKeys = alphaKeys };
	}


	private Texture2D GetTexture(string path, string name, FilterMode filterMode)
	{
		Texture2D textureAsset = LoadTexture(path, name);

		if (textureAsset == null)
			textureAsset = CreateTexture(path, name, filterMode);

		textureAsset.filterMode = filterMode;

		if (textureAsset.width != resolution)
		{
#if UNITY_2021_2_OR_NEWER
			textureAsset.Reinitialize(resolution, 1);
#else
            textureAsset.Resize(resolution, 1);
#endif
		}

		return textureAsset;
	}

	private Texture2D CreateTexture(string path, string name, FilterMode filterMode)
	{
		Texture2D textureAsset = new Texture2D(resolution, 1, TextureFormat.ARGB32, false)
		{
			name = name,
			wrapMode = TextureWrapMode.Clamp,
			filterMode = filterMode
		};
		AssetDatabase.AddObjectToAsset(textureAsset, path);
		AssetDatabase.SaveAssets();
		AssetDatabase.ImportAsset(path);

		return textureAsset;
	}

	private string Encode(Gradient gradient) => gradient == null ? null : JsonUtility.ToJson(new GradientProperty(gradient));

	private Gradient Decode(string name, string textureName)
	{
#pragma warning disable 0168
		string json = name.Substring(textureName.Length);
		try
		{
			GradientProperty gradientProperty = JsonUtility.FromJson<GradientProperty>(json);
			return gradientProperty?.ToGradient(new Gradient());
		}
		catch (Exception _)
		{
			return null;
		}
#pragma warning restore 0168
	}

	private Texture2D LoadTexture(string path, string name)
	{
		var textures = AssetDatabase.LoadAllAssetsAtPath(path);
		var texture = textures.FirstOrDefault(asset => asset?.name?.StartsWith(name) ?? false);
		return texture == null ? null : texture as Texture2D;
	}

	private void BakeGradient(Gradient gradient, Texture2D texture)
	{
		if (gradient == null)
			return;

		for (int x = 0; x < texture.width; x++)
		{
			Color color = gradient.Evaluate((float)x / (texture.width - 1));
			for (int y = 0; y < texture.height; y++)
				texture.SetPixel(x, y, color);
		}

		texture.Apply();
	}

	private void ApplyGradient(string key, Material target, string path, string propertyName, string textureName)
	{
		if (!gradientCache.TryGetValue(key, out var gradient)) return;
		if (!AssetDatabase.Contains(target)) return;

		var texture = GetTexture(
			path,
			textureName,
			gradient.mode == GradientMode.Blend
				? FilterMode.Bilinear
				: FilterMode.Point
		);

		Undo.RecordObject(texture, "Change Material Gradient");

		texture.name = textureName + Encode(gradient);
		BakeGradient(gradient, texture);

		target.SetTexture(propertyName, texture);
		EditorUtility.SetDirty(target);
	}


	[Serializable]
	private class GradientProperty
	{
		public GradientMode mode;
		public ColorKey[] colorKeys;
		public AlphaKey[] alphaKeys;

		public GradientProperty() { }

		public GradientProperty(Gradient source)
		{
			GetGradient(source);
		}

		public void GetGradient(Gradient source)
		{
			mode = source.mode;
			colorKeys = source.colorKeys.Select(key => new ColorKey(key)).ToArray();
			alphaKeys = source.alphaKeys.Select(key => new AlphaKey(key)).ToArray();
		}

		public Gradient ToGradient(Gradient gradient)
		{
			gradient.mode = mode;
			gradient.colorKeys = colorKeys.Select(key => key.ToGradientKey()).ToArray();
			gradient.alphaKeys = alphaKeys.Select(key => key.ToGradientKey()).ToArray();
			return gradient;
		}

		[Serializable]
		public struct ColorKey
		{
			public Color color;
			public float time;

			public ColorKey(GradientColorKey source)
			{
				color = default;
				time = default;
				GetGradientKey(source);
			}

			public void GetGradientKey(GradientColorKey source)
			{
				color = source.color;
				time = source.time;
			}

			public GradientColorKey ToGradientKey()
			{
				GradientColorKey key;
				key.color = color;
				key.time = time;
				return key;
			}
		}

		[Serializable]
		public struct AlphaKey
		{
			public float alpha, time;

			public AlphaKey(GradientAlphaKey source)
			{
				alpha = default;
				time = default;
				GetGradientKey(source);
			}

			public void GetGradientKey(GradientAlphaKey source)
			{
				alpha = source.alpha;
				time = source.time;
			}

			public GradientAlphaKey ToGradientKey()
			{
				GradientAlphaKey key;
				key.alpha = alpha;
				key.time = time;
				return key;
			}
		}
	}
}
#endif