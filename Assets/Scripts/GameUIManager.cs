// GameUIManager.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameUIManager : MonoBehaviour
{
    [Header("UI 패널들")]
    [SerializeField] private GameObject pausePanel;         // 일시정지 메뉴 (PausePanel)
    [SerializeField] private GameObject inGameSettingsPanel; // 인게임 설정 메뉴 (InGameSettingsPanel)

    [Header("일시정지 메뉴 버튼")]
    [SerializeField] private Button resumeButton;           // 게임 재개 버튼
    [SerializeField] private Button inGameSettingsButton;   // 인게임 설정 열기 버튼
    [SerializeField] private Button quitGameButton;         // 메인 메뉴로 나가기 버튼

    [Header("설정 메뉴 UI 요소")]
    [SerializeField] private Slider cameraSensitivitySlider;    // 카메라 감도 슬라이더
    [SerializeField] private Slider musicVolumeSlider;          // 음악 볼륨 슬라이더
    [SerializeField] private Button inGameSettingsBackButton;   // 설정 메뉴에서 뒤로 가기 버튼

    private bool isGamePaused = false; // 현재 게임이 일시정지 상태인지 추적

    void Awake()
    {
        Debug.Log($"-- GameUIManager.Awake() 시작! ({gameObject.name})", this);

        // UI 요소들이 Inspector에 잘 연결되었는지 확인하는 로그 (디버깅용)
        CheckUIElements();

        // --- 1. 각 버튼 클릭 이벤트 리스너 등록 ---
        resumeButton.onClick.AddListener(OnResumeButtonClicked);
        inGameSettingsButton.onClick.AddListener(OnInGameSettingsButtonClicked);
        quitGameButton.onClick.AddListener(OnQuitGameButtonClicked);
        inGameSettingsBackButton.onClick.AddListener(OnInGameSettingsBackButtonClicked);

        // --- 2. 슬라이더 값 변경 이벤트 리스너 등록 ---
        if (SettingsManager.Instance != null)
        {
            Debug.Log("-- GameUIManager: SettingsManager.Instance 유효. 슬라이더 리스너 등록.");
            cameraSensitivitySlider.onValueChanged.AddListener(SettingsManager.Instance.SetCameraSensitivity);
            musicVolumeSlider.onValueChanged.AddListener(SettingsManager.Instance.SetMusicVolume);
        }
        else
        {
            Debug.LogError($"GameUIManager: SettingsManager.Instance를 찾을 수 없습니다! 인게임 설정 슬라이더 기능 작동 불가. (현재 Instance: {SettingsManager.Instance})", this);
        }
        Debug.Log($"-- GameUIManager.Awake() 완료! ({gameObject.name})", this);
    }

    void Start()
    {
        Debug.Log($"--- GameUIManager.Start() 시작! ({gameObject.name})", this);
        // 시작 시 모든 UI 패널은 비활성화 상태로 시작
        CloseAllPanels();
        // 게임 시간은 정상적으로 흐르도록 설정
        Time.timeScale = 1f;
        // 게임 플레이 시에는 마우스 커서를 숨기고 중앙에 잠금
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Debug.Log($"--- GameUIManager.Start() 완료! ({gameObject.name})", this);
    }

    void Update()
    {
        // ESC 키 입력을 감지합니다.
        if (Input.GetKeyDown(KeyCode.Escape)) // UnityEngine.Input 사용 (New Input System 아님)
        {
            if (isGamePaused) // 게임이 일시정지 상태라면
            {
                // 설정창이 열려 있다면, 설정창만 닫고 일시정지 메뉴로 돌아갑니다.
                if (inGameSettingsPanel != null && inGameSettingsPanel.activeSelf)
                {
                    OpenPausePanel();
                }
                else // 일시정지 메뉴가 열려 있다면 게임을 재개합니다.
                {
                    ResumeGame();
                }
            }
            else // 게임이 플레이 중이라면
            {
                PauseGame(); // 게임을 일시정지합니다.
            }
        }
    }

    // --- UI 패널 활성/비활성 관련 함수들 ---
    void CloseAllPanels()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (inGameSettingsPanel != null) inGameSettingsPanel.SetActive(false);
    }

    void PauseGame()
    {
        isGamePaused = true;
        OpenPausePanel();
        Time.timeScale = 0f; // 게임 시간 정지
        Cursor.lockState = CursorLockMode.None; // 마우스 커서 잠금 해제
        Cursor.visible = true; // 마우스 커서 보이게
    }

    void ResumeGame()
    {
        isGamePaused = false;
        CloseAllPanels();
        Time.timeScale = 1f; // 게임 시간 재개
        Cursor.lockState = CursorLockMode.Locked; // 마우스 커서 잠금
        Cursor.visible = false; // 마우스 커서 숨기기
    }

    void OpenPausePanel()
    {
        if (pausePanel != null) pausePanel.SetActive(true);
        if (inGameSettingsPanel != null) inGameSettingsPanel.SetActive(false);
    }

    void OpenInGameSettingsPanel()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (inGameSettingsPanel != null) inGameSettingsPanel.SetActive(true);
        LoadSettingsToUI(); // 최신 설정값을 UI 슬라이더에 반영
    }

    // --- 버튼 클릭 이벤트 핸들러 함수들 ---
    void OnResumeButtonClicked() { ResumeGame(); }
    void OnInGameSettingsButtonClicked() { OpenInGameSettingsPanel(); }

    void OnQuitGameButtonClicked()
    {
        if (SettingsManager.Instance != null) { SettingsManager.Instance.SaveSettings(); }
        Time.timeScale = 1f; // 씬 전환 전 게임 시간 재설정 (매우 중요!)
        SceneTransitionManager.LoadScene("MainMenu");
    }

    void OnInGameSettingsBackButtonClicked()
    {
        if (SettingsManager.Instance != null) { SettingsManager.Instance.SaveSettings(); }
        OpenPausePanel(); // 설정창 닫고 일시정지 메뉴로 돌아감
    }

    // --- UI에 설정값 로드 및 슬라이더 범위 설정 함수 ---
    void LoadSettingsToUI()
    {
        Debug.Log($"---- GameUIManager.LoadSettingsToUI() 시작! ({gameObject.name})", this);
        if (SettingsManager.Instance != null)
        {
            Debug.Log("---- GameUIManager: SettingsManager.Instance 유효. UI 슬라이더 업데이트.");
            cameraSensitivitySlider.value = SettingsManager.Instance.CameraSensitivity;
            musicVolumeSlider.value = SettingsManager.Instance.MusicVolume;

            // 슬라이더의 최소/최대 값 설정 (Inspector에서 설정되어 있다면 중복 불필요)
            cameraSensitivitySlider.minValue = 0.5f;
            cameraSensitivitySlider.maxValue = 5.0f;
            musicVolumeSlider.minValue = 0.0f;
            musicVolumeSlider.maxValue = 1.0f;
        }
        else
        {
            Debug.LogError($"---- GameUIManager: SettingsManager.Instance가 로드되지 않았습니다! UI에 설정을 로드할 수 없습니다. (현재 Instance: {SettingsManager.Instance})", this);
        }
        Debug.Log($"---- GameUIManager.LoadSettingsToUI() 완료! ({gameObject.name})", this);
    }

    // UI 요소 연결 상태 확인 (디버깅용)
    private void CheckUIElements()
    {
        if (pausePanel == null) Debug.LogError("GameUIManager: PausePanel 필드가 Inspector에 연결되지 않았습니다!", this);
        if (inGameSettingsPanel == null) Debug.LogError("GameUIManager: InGameSettingsPanel 필드가 Inspector에 연결되지 않았습니다!", this);
        if (resumeButton == null) Debug.LogError("GameUIManager: ResumeButton 필드가 Inspector에 연결되지 않았습니다!", this);
        if (inGameSettingsButton == null) Debug.LogError("GameUIManager: InGameSettingsButton 필드가 Inspector에 연결되지 않았습니다!", this);
        if (quitGameButton == null) Debug.LogError("GameUIManager: QuitGameButton 필드가 Inspector에 연결되지 않았습니다!", this);
        if (cameraSensitivitySlider == null) Debug.LogError("GameUIManager: CameraSensitivitySlider 필드가 Inspector에 연결되지 않았습니다!", this);
        if (musicVolumeSlider == null) Debug.LogError("GameUIManager: MusicVolumeSlider 필드가 Inspector에 연결되지 않았습니다!", this);
        if (inGameSettingsBackButton == null) Debug.LogError("GameUIManager: InGameSettingsBackButton 필드가 Inspector에 연결되지 않았습니다!", this);
    }

    // 씬 파괴 시 이벤트 리스너 해제 (메모리 누수 방지)
    void OnDestroy()
    {
        if (resumeButton != null) resumeButton.onClick.RemoveListener(OnResumeButtonClicked);
        if (inGameSettingsButton != null) inGameSettingsButton.onClick.RemoveListener(OnInGameSettingsButtonClicked);
        if (quitGameButton != null) quitGameButton.onClick.RemoveListener(OnQuitGameButtonClicked);
        if (inGameSettingsBackButton != null) inGameSettingsBackButton.onClick.RemoveListener(OnInGameSettingsBackButtonClicked);

        if (SettingsManager.Instance != null)
        {
            if (cameraSensitivitySlider != null) cameraSensitivitySlider.onValueChanged.RemoveListener(SettingsManager.Instance.SetCameraSensitivity);
            if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.RemoveListener(SettingsManager.Instance.SetMusicVolume);
        }
    }
}