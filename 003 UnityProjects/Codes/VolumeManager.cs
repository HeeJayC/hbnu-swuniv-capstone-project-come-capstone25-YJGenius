using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class VolumeManager : MonoBehaviour
{
    public AudioMixer audioMixer;

    public Slider masterSlider;
    public Slider bgmSlider;
    public Slider sfxSlider;

    private void Start()
    {
        // 시작 시 슬라이더 → Mixer 반영
        SetMasterVolume(masterSlider.value);
        SetBGMVolume(bgmSlider.value);
        SetSFXVolume(sfxSlider.value);

        // 값 변경 시 자동 반영
        masterSlider.onValueChanged.AddListener(SetMasterVolume);
        bgmSlider.onValueChanged.AddListener(SetBGMVolume);
        sfxSlider.onValueChanged.AddListener(SetSFXVolume);
    }

    public void SetMasterVolume(float value)
    {
        audioMixer.SetFloat("Volume", value);
    }

    public void SetBGMVolume(float value)
    {
        audioMixer.SetFloat("Volume", value);
    }

    public void SetSFXVolume(float value)
    {
        audioMixer.SetFloat("Volume", value);
    }
}
