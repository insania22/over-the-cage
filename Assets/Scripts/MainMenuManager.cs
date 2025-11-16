// MainMenuManager.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("메인 메뉴 UI 요소")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private GameObject settingsPanel; // 설정 패널 (처음엔 비활성화)

    [Header("설정창 UI 요소")]
    [SerializeField] private Slider cameraSensitivitySlider; // 카메라 감도 슬라이더
    [SerializeField] private Slider musicVolumeSlider;       // 음악 볼륨 슬라이더
    [SerializeField] private Button settingsBackButton;      // 설정창 뒤로 가기 버튼

    void Awake()
    {
        Debug.Log($"-- MainMenuManager.Awake() 시작! ({gameObject.name})", this);

        // UI 요소들이 Inspector에 잘 연결되었는지 확인하는 로그 (디버깅용)
        CheckUIElements();

        // --- 1. 버튼 클릭 이벤트 리스너 등록 ---
        startButton.onClick.AddListener(OnStartButtonClicked);
        settingsButton.onClick.AddListener(OnSettingsButtonClicked);
        settingsBackButton.onClick.AddListener(OnSettingsBackButtonClicked);

        // --- 2. 슬라이더 값 변경 이벤트 리스너 등록 ---
        // SettingsManager 인스턴스가 존재할 때만 리스너를 등록합니다.
        if (SettingsManager.Instance != null)
        {
            Debug.Log("-- MainMenuManager: SettingsManager.Instance 유효. 슬라이더 리스너 등록.");
            cameraSensitivitySlider.onValueChanged.AddListener(SettingsManager.Instance.SetCameraSensitivity);
            musicVolumeSlider.onValueChanged.AddListener(SettingsManager.Instance.SetMusicVolume);
        }
        else
        {
            // 이 에러가 계속 뜬다면, SettingsManager.cs의 Awake()와 Script Execution Order 설정을 재확인!
            Debug.LogError($"MainMenuManager: SettingsManager.Instance를 찾을 수 없습니다! 슬라이더 기능 작동 불가. (현재 Instance: {SettingsManager.Instance})", this);
        }
        Debug.Log($"-- MainMenuManager.Awake() 완료! ({gameObject.name})", this);
    }

    void Start()
    {
        Debug.Log($"--- MainMenuManager.Start() 시작! ({gameObject.name})", this);
        // 시작 시 설정 패널은 닫고, 설정값을 UI에 반영
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
        LoadSettingsToUI();

        // 메인 메뉴에서는 마우스 커서를 보이게 설정합니다.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Debug.Log($"--- MainMenuManager.Start() 완료! ({gameObject.name})", this);
    }

    // 'Start Game' 버튼 클릭 시
    void OnStartButtonClicked()
    {
        Debug.Log("Start Game Clicked! Loading Intro Animation Scene...");
        // SceneTransitionManager의 static 메서드를 통해 씬 전환 요청
        SceneTransitionManager.LoadScene("IntroAnimationScene");
    }

    // 'Settings' 버튼 클릭 시
    void OnSettingsButtonClicked()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true); // 설정 패널 열기
        }
        LoadSettingsToUI(); // 최신 설정값 다시 UI에 반영
        Debug.Log("Settings Clicked! Opening Settings Panel.");
    }

    // 설정창 'Back' 버튼 클릭 시
    void OnSettingsBackButtonClicked()
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SaveSettings(); // 설정 저장
        }
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false); // 설정 패널 닫기
        }
        Debug.Log("Back Clicked! Closing Settings Panel and Saving Settings.");
    }

    // SettingsManager에서 현재 설정값을 가져와 UI 슬라이더에 반영하는 함수
    void LoadSettingsToUI()
    {
        Debug.Log($"---- MainMenuManager.LoadSettingsToUI() 시작! ({gameObject.name})", this);
        if (SettingsManager.Instance != null)
        {
            Debug.Log("---- MainMenuManager: SettingsManager.Instance 유효. UI 슬라이더 업데이트.");
            // SettingsManager로부터 값을 가져와 슬라이더의 현재 값으로 설정
            cameraSensitivitySlider.value = SettingsManager.Instance.CameraSensitivity;
            musicVolumeSlider.value = SettingsManager.Instance.MusicVolume;

            // 슬라이더의 최소/최대 값 설정 (Inspector에서 설정되어 있다면 중복 불필요)
            cameraSensitivitySlider.minValue = 0.5f; // 최소 감도 예시
            cameraSensitivitySlider.maxValue = 5.0f;  // 최대 감도 예시
            musicVolumeSlider.minValue = 0.0f;        // 최소 볼륨 (음소거)
            musicVolumeSlider.maxValue = 1.0f;        // 최대 볼륨
        }
        else
        {
            Debug.LogError($"---- MainMenuManager: SettingsManager.Instance가 로드되지 않았습니다! UI에 설정을 로드할 수 없습니다. (현재 Instance: {SettingsManager.Instance})", this);
        }
        Debug.Log($"---- MainMenuManager.LoadSettingsToUI() 완료! ({gameObject.name})", this);
    }

    // UI 요소 연결 상태 확인 (디버깅용)
    private void CheckUIElements()
    {
        if (startButton == null) Debug.LogError("MainMenuManager: StartButton 필드가 Inspector에 연결되지 않았습니다!", this);
        if (settingsButton == null) Debug.LogError("MainMenuManager: SettingsButton 필드가 Inspector에 연결되지 않았습니다!", this);
        if (settingsPanel == null) Debug.LogError("MainMenuManager: SettingsPanel 필드가 Inspector에 연결되지 않았습니다!", this);
        if (cameraSensitivitySlider == null) Debug.LogError("MainMenuManager: CameraSensitivitySlider 필드가 Inspector에 연결되지 않았습니다!", this);
        if (musicVolumeSlider == null) Debug.LogError("MainMenuManager: MusicVolumeSlider 필드가 Inspector에 연결되지 않았습니다!", this);
        if (settingsBackButton == null) Debug.LogError("MainMenuManager: SettingsBackButton 필드가 Inspector에 연결되지 않았습니다!", this);
    }

    // 씬이 파괴될 때 이벤트 리스너를 해제하여 메모리 누수를 방지
    void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(OnStartButtonClicked);
        if (settingsButton != null) settingsButton.onClick.RemoveListener(OnSettingsButtonClicked);
        if (settingsBackButton != null) settingsBackButton.onClick.RemoveListener(OnSettingsBackButtonClicked);

        if (SettingsManager.Instance != null) // SettingsManager가 파괴되기 전에 미리 체크
        {
            if (cameraSensitivitySlider != null) cameraSensitivitySlider.onValueChanged.RemoveListener(SettingsManager.Instance.SetCameraSensitivity);
            if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.RemoveListener(SettingsManager.Instance.SetMusicVolume);
        }
    }
}