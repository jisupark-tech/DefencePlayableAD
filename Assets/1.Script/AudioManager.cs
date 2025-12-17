using UnityEngine;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Audio Settings")]
    [Range(0f, 1f)]
    public float masterVolume = 1f;
    [Range(0f, 1f)]
    public float sfxVolume = 1f;

    [Header("Gold Sound Effects")]
    public AudioClip goldSpawnSound;    // 골드 생성 시 사운드
    public AudioClip goldCollectSound;  // 골드 흡수 시 사운드

    [Header("Arrow Attack Effects")]
    public AudioClip arrowAttackSound;
    [Header("Audio Source Pool")]
    public int audioSourcePoolSize = 5; // 오디오소스 풀 크기

    private Queue<AudioSource> audioSourcePool;
    private List<AudioSource> allAudioSources;

    void Awake()
    {
        // 싱글톤 패턴
        if (Instance == null)
        {
            Instance = this;
            InitializeAudioSystem();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void InitializeAudioSystem()
    {
        audioSourcePool = new Queue<AudioSource>();
        allAudioSources = new List<AudioSource>();

        // AudioSource 풀 생성
        for (int i = 0; i < audioSourcePoolSize; i++)
        {
            GameObject audioObj = new GameObject($"AudioSource_{i}");
            audioObj.transform.SetParent(transform);

            AudioSource audioSource = audioObj.AddComponent<AudioSource>();

            // WebGL 최적화 설정
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.volume = sfxVolume * masterVolume;
            audioSource.spatialBlend = 0f; // 2D 사운드 (3D 계산 불필요)

            audioSourcePool.Enqueue(audioSource);
            allAudioSources.Add(audioSource);
        }
#if !PLAYABLE_AD 
        Debug.Log($"AudioManager initialized with {audioSourcePoolSize} audio sources");
#endif
    }

    // 골드 생성 사운드 재생
    public void PlayGoldSpawnSound(Vector3 position = default)
    {
        if (goldSpawnSound != null)
        {
            PlaySFX(goldSpawnSound, position);
        }
    }

    // 골드 수집 사운드 재생  
    public void PlayGoldCollectSound(Vector3 position = default)
    {
        if (goldCollectSound != null)
        {
            PlaySFX(goldCollectSound, position);
        }
    }

    //화살 공격 사운드 재새
    public void PlayArrowAttackSound(Vector3 position = default)
    {
        if(arrowAttackSound!=null)
        {
            PlaySFX(arrowAttackSound, position);
        }
    }
    // 일반적인 SFX 재생 메소드
    public void PlaySFX(AudioClip clip, Vector3 position = default, float volumeScale = 1f)
    {
        if (clip == null) return;

        AudioSource audioSource = GetPooledAudioSource();
        if (audioSource != null)
        {
            audioSource.clip = clip;
            audioSource.volume = sfxVolume * masterVolume * volumeScale;
            audioSource.transform.position = position;
            audioSource.Play();

            // 재생 완료 후 풀로 반환하는 코루틴 시작
            StartCoroutine(ReturnToPoolAfterPlay(audioSource, clip.length));
        }
    }

    AudioSource GetPooledAudioSource()
    {
        // 사용 가능한 AudioSource가 있으면 반환
        if (audioSourcePool.Count > 0)
        {
            return audioSourcePool.Dequeue();
        }

        // 풀이 비어있으면 재생 중이 아닌 AudioSource 찾기
        foreach (AudioSource source in allAudioSources)
        {
            if (!source.isPlaying)
            {
                return source;
            }
        }

        // 모든 AudioSource가 사용 중이면 첫 번째 것을 강제로 정지하고 사용
        if (allAudioSources.Count > 0)
        {
            AudioSource forcedSource = allAudioSources[0];
            forcedSource.Stop();
            return forcedSource;
        }

        return null;
    }

    System.Collections.IEnumerator ReturnToPoolAfterPlay(AudioSource audioSource, float clipLength)
    {
        // 클립 재생 시간만큼 대기
        yield return new WaitForSeconds(clipLength + 0.1f);

        // AudioSource를 풀로 반환
        if (!audioSource.isPlaying)
        {
            audioSourcePool.Enqueue(audioSource);
        }
    }

    // 볼륨 조절 메소드들
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        UpdateAllVolumes();
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        UpdateAllVolumes();
    }

    void UpdateAllVolumes()
    {
        foreach (AudioSource source in allAudioSources)
        {
            if (source.isPlaying)
            {
                source.volume = sfxVolume * masterVolume;
            }
        }
    }

    // 모든 사운드 정지
    public void StopAllSounds()
    {
        foreach (AudioSource source in allAudioSources)
        {
            source.Stop();
        }

        // 풀 초기화
        audioSourcePool.Clear();
        foreach (AudioSource source in allAudioSources)
        {
            audioSourcePool.Enqueue(source);
        }
    }

    // WebGL에서 사운드 활성화 체크 (사용자 상호작용 후)
    public void EnableAudio()
    {
        // WebGL에서는 사용자 상호작용 후에만 오디오 재생 가능
        AudioSource testSource = GetPooledAudioSource();
        if (testSource != null)
        {
            testSource.volume = 0f;
            testSource.Play();
            testSource.Stop();
            audioSourcePool.Enqueue(testSource);
        }
    }
}