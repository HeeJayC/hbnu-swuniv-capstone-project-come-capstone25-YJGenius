using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class SetVolume : MonoBehaviour
{
    // 오디오 믹서
    public AudioMixer audioMixer;

    // 슬라이더
    public Slider BgmSlider;
    public Slider SfxSlider;

    // 볼륨 조절
    public void SetBgmVolme()
    {
        // 로그 연산 값 전달
        audioMixer.SetFloat("BGM", Mathf.Log10(BgmSlider.value) * 20);
    }

    public void SetSFXVolme()
    {
        // 로그 연산 값 전달
        audioMixer.SetFloat("SFX", Mathf.Log10(SfxSlider.value) * 20);
    }
}