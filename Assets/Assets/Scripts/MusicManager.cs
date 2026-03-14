using UnityEngine;
using System.Collections;

/// <summary>
/// Менеджер музыки. Управляет фоновой музыкой в игре.
/// Singleton — доступен через MusicManager.Instance
/// </summary>
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }
    
    [Header("Music Clips")]
    [Tooltip("Музыка для лобби/дома (играет всегда по кругу)")]
    [SerializeField] private AudioClip lobbyMusic;
    
    [Header("Settings")]
    [Tooltip("Громкость музыки (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 0.5f;
    
    [Tooltip("Ограничитель громкости звуков брейнротов (0-1). Умножается на громкость каждого звука взятия/других брейнротов — синхронизируй с громкостью музыки.")]
    [Range(0f, 1f)]
    [SerializeField] private float brainrotSoundsVolume = 1f;
    
    [Tooltip("Время плавного перехода между треками (секунды)")]
    [SerializeField] private float crossfadeDuration = 1.5f;
    
    [Tooltip("Зацикливать музыку")]
    [SerializeField] private bool loop = true;
    
    [Header("Нормализация (компрессия)")]
    [Tooltip("Группа микшера для музыки. Чтобы громкие части стали тише, а тихие — громче: создай Audio Mixer (правый клик в Project → Create → Audio Mixer), в группе музыки добавь эффект Compressor, затем перетащи эту группу сюда.")]
    [SerializeField] private UnityEngine.Audio.AudioMixerGroup musicMixerGroup;
    
    // Два AudioSource для плавного перехода (crossfade)
    private AudioSource audioSourceA;
    private AudioSource audioSourceB;
    private bool isPlayingA = true;
    
    private Coroutine crossfadeCoroutine;
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject); // Музыка продолжает играть между сценами (root для работы с дочерними объектами)
        
        // Создаём два AudioSource для crossfade
        audioSourceA = gameObject.AddComponent<AudioSource>();
        audioSourceB = gameObject.AddComponent<AudioSource>();
        
        SetupAudioSource(audioSourceA);
        SetupAudioSource(audioSourceB);
    }
    
    private void Start()
    {
        LoadSavedVolume();
        PlayLobbyMusic();
    }
    
    private void LoadSavedVolume()
    {
        if (GameStorage.Instance == null) return;
        float saved = GameStorage.Instance.GetMusicVolume();
        if (saved >= 0f)
            musicVolume = Mathf.Clamp01(saved);
    }
    
    /// <summary>Текущая громкость (0–1).</summary>
    public float GetVolume() => musicVolume;
    
    /// <summary>
    /// Совместимость с существующим кодом: больше не переключает музыку,
    /// так как бойовая музыка удалена. Метод оставлен пустым, чтобы не ломать вызовы.
    /// </summary>
    public void SetPlayerInFightZone(bool inZone)
    {
        // Ничего не делаем: всегда играет только lobbyMusic
    }
    
    private void SetupAudioSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = loop;
        source.volume = 0f;
        source.spatialBlend = 0f; // 2D звук (не зависит от позиции)
        if (musicMixerGroup != null)
            source.outputAudioMixerGroup = musicMixerGroup;
    }
    
    /// <summary>
    /// Воспроизводит музыку лобби
    /// </summary>
    public void PlayLobbyMusic()
    {
        if (lobbyMusic != null)
        {
            CrossfadeTo(lobbyMusic);
        }
    }
    
    /// <summary>
    /// Плавный переход к новому треку
    /// </summary>
    private void CrossfadeTo(AudioClip newClip)
    {
        if (crossfadeCoroutine != null)
        {
            StopCoroutine(crossfadeCoroutine);
        }
        
        crossfadeCoroutine = StartCoroutine(CrossfadeCoroutine(newClip));
    }
    
    private IEnumerator CrossfadeCoroutine(AudioClip newClip)
    {
        AudioSource fadeOut = isPlayingA ? audioSourceA : audioSourceB;
        AudioSource fadeIn = isPlayingA ? audioSourceB : audioSourceA;
        
        // Если уже играет этот же трек — ничего не делаем
        if (fadeOut.clip == newClip && fadeOut.isPlaying)
        {
            yield break;
        }
        
        // Запускаем новый трек
        fadeIn.clip = newClip;
        fadeIn.volume = 0f;
        fadeIn.Play();
        
        float elapsed = 0f;
        float startVolumeOut = fadeOut.volume;
        
        while (elapsed < crossfadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / crossfadeDuration;
            
            fadeOut.volume = Mathf.Lerp(startVolumeOut, 0f, t);
            fadeIn.volume = Mathf.Lerp(0f, musicVolume, t);
            
            yield return null;
        }
        
        // Финальные значения
        fadeOut.volume = 0f;
        fadeOut.Stop();
        fadeIn.volume = musicVolume;
        
        isPlayingA = !isPlayingA;
        crossfadeCoroutine = null;
    }
    
    /// <summary>
    /// Множитель громкости для всех звуков брейнротов (взятие в руки и т.д.). 0–1.
    /// </summary>
    public float GetBrainrotSoundsVolumeMultiplier()
    {
        return brainrotSoundsVolume;
    }
    
    /// <summary>
    /// Устанавливает ограничитель громкости звуков брейнротов (0–1). Синхронизация с музыкой.
    /// </summary>
    public void SetBrainrotSoundsVolume(float volume)
    {
        brainrotSoundsVolume = Mathf.Clamp01(volume);
    }
    
    /// <summary>
    /// Устанавливает громкость музыки
    /// </summary>
    public void SetVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        
        // Обновляем громкость активного источника
        AudioSource active = isPlayingA ? audioSourceA : audioSourceB;
        if (active.isPlaying)
        {
            active.volume = musicVolume;
        }
    }
    
    /// <summary>
    /// Останавливает музыку с плавным затуханием
    /// </summary>
    public void StopMusic()
    {
        if (crossfadeCoroutine != null)
        {
            StopCoroutine(crossfadeCoroutine);
        }
        
        StartCoroutine(FadeOutCoroutine());
    }
    
    private IEnumerator FadeOutCoroutine()
    {
        AudioSource active = isPlayingA ? audioSourceA : audioSourceB;
        float startVolume = active.volume;
        float elapsed = 0f;
        
        while (elapsed < crossfadeDuration)
        {
            elapsed += Time.deltaTime;
            active.volume = Mathf.Lerp(startVolume, 0f, elapsed / crossfadeDuration);
            yield return null;
        }
        
        active.Stop();
        active.volume = 0f;
    }
}
