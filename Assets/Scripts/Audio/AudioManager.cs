using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [SerializeField] AudioSource sfxSource;
    [SerializeField] AudioClip buttonClick;
    [SerializeField] Vector2 pitchRange = new Vector2(0.95f, 1.05f);
    public void PlayButtonSound()
    {
        sfxSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        sfxSource.clip = buttonClick;
        sfxSource.PlayOneShot(buttonClick);
    }
}