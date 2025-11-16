// SettingsManager.cs
using UnityEngine;
// using System.Collections; // 현재 필요하지 않으므로 주석 처리 또는 삭제 가능

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; } // 싱글톤 인스턴스

    // PlayerPrefs에 저장될 키 이름들
    private const string CameraSensitivityKey = "CameraSensitivity";
    private const string MusicVolumeKey = "MusicVolume";

    // Inspector에서 설정할 수 있는 기본값들
    [SerializeField] private float defaultCameraSensitivity = 2.0f; // 기본 카메라 감도
    [SerializeField] private float defaultMusicVolume = 0.5f;     // 기본 음악 볼륨 (50%)

    // 현재 설정값들 (다른 스크립트에서 읽을 수 있음)
    public float CameraSensitivity { get; private set; }
    public float MusicVolume { get; private set; }

    void Awake()
    {
        // Debug.Log는 게임 실행 흐름을 파악하는 데 매우 중요합니다.
        // 어느 GameObject의 Awake()가 실행되는지 명확히 하기 위해 gameObject.name을 포함합니다.
        Debug.Log($"## SettingsManager.Awake() 시작! ({gameObject.name})", this);

        // 싱글톤 패턴: 인스턴스가 없는 경우에만 자신을 인스턴스로 설정
        if (Instance == null)
        {
            Instance = this; // 현재 이 스크립트의 인스턴스를 static 변수에 할당
            Debug.Log($"## SettingsManager.Instance 초기화됨! ({gameObject.name})", this);

            // 씬이 전환되어도 이 GameObject가 파괴되지 않도록 설정합니다.
            DontDestroyOnLoad(gameObject);

            // 게임 시작 시 저장된 설정값을 불러옵니다.
            LoadSettings();
        }
        else
        {
            // 이미 인스턴스가 존재한다면 (예: 다른 씬에서 넘어왔거나, 중복으로 생성된 경우)
            // 지금 생성된 GameObject는 파괴합니다.
            Debug.LogWarning($"## SettingsManager 인스턴스 중복! 현재 씬 ({gameObject.scene.name})의 '{gameObject.name}' 자신을 파괴! (이미 '{Instance.name}' 존재)", this);
            Destroy(gameObject);
        }
        Debug.Log($"## SettingsManager.Awake() 완료! Instance = {(Instance != null ? Instance.name : "NULL")}", this);
    }

    // --- 설정값 변경 메서드들 ---
    public void SetCameraSensitivity(float value)
    {
        CameraSensitivity = value;
        ApplySettings(); // 변경 즉시 적용 (필요하다면)
    }

    public void SetMusicVolume(float value)
    {
        MusicVolume = value;
        ApplySettings(); // 변경 즉시 적용 (필요하다면)
    }

    // --- 설정값 저장 및 로드 메서드들 ---
    public void SaveSettings()
    {
        PlayerPrefs.SetFloat(CameraSensitivityKey, CameraSensitivity);
        PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
        PlayerPrefs.Save(); // 디스크에 실제로 저장
        Debug.Log("게임 설정 저장됨!");
    }

    public void LoadSettings()
    {
        // PlayerPrefs에서 값을 불러옵니다. 저장된 값이 없으면 default 값 사용.
        CameraSensitivity = PlayerPrefs.GetFloat(CameraSensitivityKey, defaultCameraSensitivity);
        MusicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, defaultMusicVolume);
        Debug.Log($"게임 설정 불러옴! CameraSensitivity: {CameraSensitivity}, MusicVolume: {MusicVolume}");
        ApplySettings(); // 불러온 설정 적용
    }

    // --- 설정 적용 메서드 (실제로 게임에 반영) ---
    private void ApplySettings()
    {
        // AudioListener.volume을 통해 게임 내 모든 소리(음악, 효과음)의 전체 볼륨을 조절합니다.
        // 씬에 AudioListener 컴포넌트가 존재해야 합니다 (일반적으로 Main Camera에 기본으로 붙어있음).
        AudioListener.volume = MusicVolume;
        Debug.Log($"설정 적용됨: MusicVolume = {MusicVolume}");

        // 카메라 감도는 보통 PlayerController 스크립트에서 Start/Awake 또는 Update 시점에 이 값을 가져가 사용합니다.
        // 만약 실시간으로 적용하고 싶다면 PlayerController에 SetSensitivity(float newSens) 같은 메서드를 만들고 여기서 호출해야 합니다.
        // 예시: PlayerController playerController = FindObjectOfType<PlayerController>(); if(playerController != null) playerController.SetSensitivity(CameraSensitivity);
    }

    // 애플리케이션이 종료될 때 자동으로 설정값을 저장합니다.
    void OnApplicationQuit()
    {
        SaveSettings();
    }
}