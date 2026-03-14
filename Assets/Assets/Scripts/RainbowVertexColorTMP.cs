using UnityEngine;
using TMPro;

/// <summary>
/// Вешается на 3D TextMeshPro. Делает vertex color переливающимся радужным градиентом (7 цветов).
/// </summary>
[RequireComponent(typeof(TextMeshPro))]
public class RainbowVertexColorTMP : MonoBehaviour
{
    [Header("Настройки радуги")]
    [Tooltip("Скорость переливания (смещение градиента по времени)")]
    [SerializeField] private float flowSpeed = 1f;
    
    [Tooltip("Смещение градиента по символам (0 = все символы в одной фазе, 1 = полный цикл на весь текст)")]
    [SerializeField] private float characterOffset = 0.15f;
    
    [Tooltip("Яркость цвета (0–1)")]
    [SerializeField] [Range(0.5f, 1f)] private float brightness = 1f;
    
    private static readonly Color[] RainbowColors = new Color[]
    {
        new Color(1f, 0f, 0f),     // Red
        new Color(1f, 0.5f, 0f),   // Orange
        new Color(1f, 1f, 0f),     // Yellow
        new Color(0f, 1f, 0f),     // Green
        new Color(0f, 0.3f, 1f),   // Blue
        new Color(0.29f, 0f, 0.51f), // Indigo
        new Color(0.58f, 0f, 0.83f)  // Violet
    };
    
    private const int RainbowCount = 7;
    private TMP_Text _text;
    
    private void Awake()
    {
        _text = GetComponent<TextMeshPro>();
        if (_text == null)
            _text = GetComponent<TMP_Text>();
    }
    
    private void LateUpdate()
    {
        if (_text == null) return;
        
        _text.ForceMeshUpdate();
        TMP_TextInfo textInfo = _text.textInfo;
        if (textInfo == null || textInfo.characterCount == 0) return;
        
        float t = Time.time * flowSpeed;
        
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo ch = textInfo.characterInfo[i];
            if (!ch.isVisible) continue;
            
            int materialIndex = ch.materialReferenceIndex;
            int vertexIndex = ch.vertexIndex;
            Color32[] vertexColors = textInfo.meshInfo[materialIndex].colors32;
            if (vertexColors == null || vertexIndex + 3 >= vertexColors.Length) continue;
            
            float phase = (t + i * characterOffset) % 1f;
            if (phase < 0f) phase += 1f;
            float segment = phase * RainbowCount;
            int idx = (int)segment % RainbowCount;
            int nextIdx = (idx + 1) % RainbowCount;
            float frac = segment - (int)segment;
            Color c = Color.Lerp(RainbowColors[idx], RainbowColors[nextIdx], frac);
            c *= brightness;
            c.a = 1f;
            Color32 c32 = c;
            
            vertexColors[vertexIndex + 0] = c32;
            vertexColors[vertexIndex + 1] = c32;
            vertexColors[vertexIndex + 2] = c32;
            vertexColors[vertexIndex + 3] = c32;
        }
        
        _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }
}
