using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager instance;

    public float bgmVolume = 1f;
    public float sfxVolume = 1f;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
