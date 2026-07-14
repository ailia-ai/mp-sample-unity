using UnityEngine;
using System.Collections;

public class VoicePlayer : MonoBehaviour
{

    [SerializeField] private AudioClip audioClip0_;
    [SerializeField] private AudioClip audioClip1_;
    [SerializeField] private AudioClip audioClip2_;

    private AudioSource audioSource_;

    // Use this for initialization
    void Start()
    {
        audioSource_ = gameObject.GetComponent<AudioSource>();
    }

    // Update is called once per frame
    void Update()
    {

    }

    // play
    public void Play(int voiceId)
    {
        switch (voiceId)
        {
            case 0:
                audioSource_.clip = audioClip0_;
                break;
            case 1:
                audioSource_.clip = audioClip1_;
                break;
            case 2:
                audioSource_.clip = audioClip2_;
                break;
            default:
                audioSource_.clip = audioClip0_;
                break;
        }
        audioSource_.Play();
    }

    // stop
    public void Stop()
    {
        audioSource_.Stop();
    }
}
