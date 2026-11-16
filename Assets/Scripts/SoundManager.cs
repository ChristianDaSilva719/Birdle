using UnityEngine;
using System;

//Different types of sounds
public enum SoundType
{
    Peg_Hit,
    Peg_HitSpecial,
    Peg_Break,
    BlackHole_Eat,
    Level_Complete
}

[RequireComponent(typeof(AudioSource)), ExecuteInEditMode] //Auto adds an audio source component
public class SoundManager : MonoBehaviour
{
    [SerializeField] private SoundList[] soundList;
    private static SoundManager instance;
    private AudioSource audioSource;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if(PlayerPrefs.HasKey("Muted"))
        {
            if(PlayerPrefs.GetInt("Muted") == 1)
            {
                audioSource.mute = true;
            }
            else
            {
                audioSource.mute = false;
            }
        }
    }

    //Plays a sound based on sound type
    public static void PlaySound(SoundType _sound, float _volume = 1, bool _randomPitch = false)
    {
        AudioClip[] clips = instance.soundList[(int)_sound].Sounds; //Get list of sounds
        AudioClip randomClip = clips[UnityEngine.Random.Range(0, clips.Length)]; //Pick random from list
        if (_randomPitch)
        {
            instance.audioSource.pitch = UnityEngine.Random.Range(0.8f, 1.2f);
        }
        else
        {
            instance.audioSource.pitch = 1f;
        }
        instance.audioSource.PlayOneShot(randomClip, _volume); //plays random sound
    }
#if UNITY_EDITOR
    private void OnEnable()
    {
        string[] names = Enum.GetNames(typeof(SoundType));
        Array.Resize(ref soundList, names.Length);
        for (int i = 0; i < soundList.Length; i++)
        {
            soundList[i].name = names[i];
        }
    }
#endif
}

[Serializable]
public struct SoundList
{
    public AudioClip[] Sounds { get => sounds; }
    [HideInInspector] public string name;
    [SerializeField] private AudioClip[] sounds; //List of sounds for clip to choose from
}
