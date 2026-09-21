using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 마우스 휠로 전투 카메라를 화면 중심 기준 확대·축소한다.
/// </summary>
public class CameraMouseZoom : MonoBehaviour
{
    [Header("Reference")]
    // 확대·축소할 직교 투영 전투 카메라다.
    [SerializeField] private Camera targetCamera;

    [Header("Zoom")]
    // 휠 한 눈금당 변경할 Orthographic Size다.
    [SerializeField] private float zoomStep = 0.5f;
    // 최대 확대 상태의 Orthographic Size다. 값이 작을수록 가까이 보인다.
    [SerializeField] private float minimumOrthographicSize = 2.5f;
    // 최대 축소 상태의 Orthographic Size다. 값이 클수록 넓게 보인다.
    [SerializeField] private float maximumOrthographicSize = 10.5f;

    /// <summary>필수 카메라 참조와 줌 범위를 검사한다.</summary>
    private void OnEnable()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
        }
    }

    /// <summary>휠 입력을 읽고 전투 연출 및 UI 조작과 겹치지 않을 때 줌을 변경한다.</summary>
    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !Application.isFocused ||
            !targetCamera.pixelRect.Contains(mouse.position.ReadValue()) ||
            (ActionPresentationQueue.Instance != null && ActionPresentationQueue.Instance.IsBusy))
        {
            return;
        }

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(scroll, 0f) || IsPointerOverUI(mouse.position.ReadValue()))
        {
            return;
        }

        // Input System의 기본값은 눈금당 1이며 Windows 원시 범위를 선택한 경우에만 120으로 나눈다.
        if (InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange &&
            (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer))
        {
            scroll /= 120f;
        }

        targetCamera.orthographicSize = Mathf.Clamp(
            targetCamera.orthographicSize - scroll * zoomStep,
            minimumOrthographicSize,
            maximumOrthographicSize);
    }

    /// <summary>같은 프레임에 포인터 이동과 휠 입력이 들어와도 현재 위치의 UI를 검사한다.</summary>
    private static bool IsPointerOverUI(Vector2 pointerPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return false;
        }

        PointerEventData pointerData = new(eventSystem) { position = pointerPosition };
        List<RaycastResult> results = new();
        eventSystem.RaycastAll(pointerData, results);
        foreach (RaycastResult result in results)
        {
            if (result.module is GraphicRaycaster)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>직접 연결한 카메라가 직교 투영인지 확인한다.</summary>
    public bool HasValidReference()
    {
        if (targetCamera == null || !targetCamera.orthographic)
        {
            Debug.LogError($"{nameof(CameraMouseZoom)} on {name}에는 직교 투영 카메라 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>줌 속도와 확대·축소 한계가 유효한 유한 값인지 확인한다.</summary>
    public bool HasValidData()
    {
        if (!float.IsFinite(zoomStep) || zoomStep <= 0f ||
            !float.IsFinite(minimumOrthographicSize) || minimumOrthographicSize <= 0f ||
            !float.IsFinite(maximumOrthographicSize) || maximumOrthographicSize < minimumOrthographicSize)
        {
            Debug.LogError($"{nameof(CameraMouseZoom)} on {name}의 줌 속도와 최소 크기는 양수여야 하며 최대 크기는 최소 크기 이상이어야 합니다.", this);
            return false;
        }

        return true;
    }
}
