using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class DeathEffectController : MonoBehaviour
{
    [Header("References")]
    public Camera mainCamera;      // Main Camera
    public Transform playerModel;  // 캐릭터 Mesh Transform
    public Image fadeImage;        // Canvas 하위 검은 Image

    [Header("Death Effect (fade-in)")]
    public float fadeInTime = 2.0f;        // 천천히 어두워지는 시간
    [Range(0.1f, 1f)] public float slowMotionScale = 0.25f;
    public float scaleDistortion = 1.6f;   // 죽을 때 캐릭터 스케일 배율
    public float cameraZoomOut = 2.0f;     // 카메라 뒤로
    public float cameraTiltDeg = 25f;      // 카메라 아래로 숙임

    [Header("Restore (after teleport)")]
    public float restoreTime = 1.2f;       // 밝아지는 시간

    private Vector3 originalScale;
    private Vector3 originalCamLocalPos;
    private Quaternion originalCamLocalRot;
    private bool effectRunning = false;
    private System.Action onFinish;

    void Awake()
    {
        if (playerModel) originalScale = playerModel.localScale;
        if (mainCamera)
        {
            originalCamLocalPos = mainCamera.transform.localPosition;
            originalCamLocalRot = mainCamera.transform.localRotation;
        }

        if (fadeImage)
        {
            fadeImage.color = new Color(0f, 0f, 0f, 0f);
            fadeImage.raycastTarget = false;
            fadeImage.enabled = false; // 시작 때 가리지 않도록
        }
    }

    /// <summary>사망 연출 시작(암전/왜곡). 완료 시 onFinished 호출.</summary>
    public void BeginDeathEffect(System.Action onFinished)
    {
        if (effectRunning) return;
        effectRunning = true;
        onFinish = onFinished;
        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        Time.timeScale = slowMotionScale;
        if (fadeImage) fadeImage.enabled = true;

        float t = 0f;
        while (t < fadeInTime)
        {
            float k = t / Mathf.Max(0.0001f, fadeInTime);

            if (fadeImage)
                fadeImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 1f, k));

            if (playerModel)
                playerModel.localScale = Vector3.Lerp(originalScale, originalScale * scaleDistortion, k);

            if (mainCamera)
            {
                mainCamera.transform.localPosition =
                    Vector3.Lerp(originalCamLocalPos, originalCamLocalPos - Vector3.forward * cameraZoomOut, k);
                mainCamera.transform.localRotation =
                    Quaternion.Slerp(originalCamLocalRot, originalCamLocalRot * Quaternion.Euler(cameraTiltDeg, 0f, 0f), k);
            }

            t += Time.unscaledDeltaTime; // 슬로모션과 무관하게 진행
            yield return null;
        }

        // 완전 암전: GameManager가 스폰 이동을 수행할 시간
        onFinish?.Invoke();
    }

    /// <summary>스폰 이동이 끝난 후 호출: 화면/카메라/스케일 복원 시작.</summary>
    public void StartRestore()
    {
        StartCoroutine(RestoreRoutine());
    }

    private IEnumerator RestoreRoutine()
    {
        Time.timeScale = 1f;

        Vector3 distortedScale = playerModel ? playerModel.localScale : Vector3.one;
        Vector3 camPos = mainCamera ? mainCamera.transform.localPosition : Vector3.zero;
        Quaternion camRot = mainCamera ? mainCamera.transform.localRotation : Quaternion.identity;

        float r = 0f;
        while (r < restoreTime)
        {
            float k = r / Mathf.Max(0.0001f, restoreTime);

            if (fadeImage)
                fadeImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(1f, 0f, k));

            if (playerModel)
                playerModel.localScale = Vector3.Lerp(distortedScale, originalScale, k);

            if (mainCamera)
            {
                mainCamera.transform.localPosition = Vector3.Lerp(camPos, originalCamLocalPos, k);
                mainCamera.transform.localRotation = Quaternion.Slerp(camRot, originalCamLocalRot, k);
            }

            r += Time.unscaledDeltaTime;
            yield return null;
        }

        if (fadeImage)
        {
            fadeImage.color = new Color(0f, 0f, 0f, 0f);
            fadeImage.enabled = false;
        }
        if (playerModel) playerModel.localScale = originalScale;
        if (mainCamera)
        {
            mainCamera.transform.localPosition = originalCamLocalPos;
            mainCamera.transform.localRotation = originalCamLocalRot;
        }

        effectRunning = false;
    }
}
