using UnityEngine;

// 도깨비 환도 본체의 표시 계층을 담당한다.
// 위치/회전은 Motion에 맡기고, 여기서는 Animator, VFX, 표시/숨김, 정렬 순서만 처리한다.
public class PlayerWeaponVisualPresentation : MonoBehaviour
{
    [Header("Enter VFX")]
    // 최소 장전 시간이 끝났을 때 한 번 재생할 충전 완료 VFX다.
    [SerializeField] private VfxId aimingChargedVfxId = VfxId.None;
    // 근접 공격 위치로 이동할 때 한 번 재생할 검 반짝임 VFX다.
    [SerializeField] private VfxId meleeMoveVfxId = VfxId.None;
    // 투척 시작 순간에 한 번 재생할 VFX다. 실제 투척 판정은 ThrownWeapon이 담당한다.
    [SerializeField] private VfxId throwStartVfxId = VfxId.None;

    [Header("Visibility VFX")]
    // 검 본체가 숨겨지는 순간 호출할 전역 VFX ID다.
    [SerializeField] private VfxId vanishVfxId = VfxId.WeaponVanish;
    // 검 본체가 다시 보이는 순간 호출할 전역 VFX ID다.
    [SerializeField] private VfxId reappearVfxId = VfxId.WeaponReappear;

    private PlayerWeaponContext context;
    private bool wasVisualVisible;
    private WeaponAnimState? currentAnimState;

    private static readonly int AnimStateHash = Animator.StringToHash("AnimState");
    private static readonly int RestartAnimationHash = Animator.StringToHash("RestartAnimation");

    public bool IsWeaponVisible => ResolveWeaponVisible();

    public void Initialize(PlayerWeaponContext newContext)
    {
        context = newContext;
        wasVisualVisible = ResolveWeaponVisible();
    }

    public void ApplyLegacySettings(
        VfxId newAimingChargedVfxId,
        VfxId newMeleeMoveVfxId,
        VfxId newThrowStartVfxId,
        VfxId newVanishVfxId,
        VfxId newReappearVfxId)
    {
        aimingChargedVfxId = newAimingChargedVfxId;
        meleeMoveVfxId = newMeleeMoveVfxId;
        throwStartVfxId = newThrowStartVfxId;
        vanishVfxId = newVanishVfxId;
        reappearVfxId = newReappearVfxId;
    }

    public void ApplyData(PlayerWeaponVisualData visualData)
    {
        if (visualData == null)
        {
            return;
        }

        if (visualData.aiming != null)
        {
            aimingChargedVfxId = visualData.aiming.chargedVfxId;
        }

        if (visualData.melee != null)
        {
            meleeMoveVfxId = visualData.melee.moveVfxId;
        }

        if (visualData.throwVisual != null)
        {
            throwStartVfxId = visualData.throwVisual.startVfxId;
        }

        if (visualData.presentation != null)
        {
            vanishVfxId = visualData.presentation.vanishVfxId;
            reappearVfxId = visualData.presentation.reappearVfxId;
        }
    }

    public void HandleVisualStateChanged(WeaponVisualState previousState, WeaponVisualState nextState)
    {
        PlayAnimation(ToAnimState(nextState));
        PlayEnterVfx(nextState);
    }

    public void UpdateVisibility(WeaponVisualState state)
    {
        bool shouldShow = state != WeaponVisualState.HiddenBySlash;
        if (ResolveWeaponVisible() == shouldShow)
        {
            return;
        }

        SetWeaponVisible(shouldShow);
        // 현재 연출 방향에서는 숨김/재등장 VFX를 쓰지 않는다.
        // 검은 참격 중 잠깐 숨었다가 자연스럽게 다시 합류하는 쪽이 더 낫다.
        // PlayVisibilityEffect(shouldShow);
    }

    public void UpdateSorting() // PlayerWeaponVisualFSM에서 호출한다.
    {
        if (context?.WeaponRenderer == null || context.Player?.Anim == null)
        {
            return;
        }

        PlayerSide8 displaySide = context.Player.Anim.CurrentDisplaySide;
        // 정면(Down 계열)과 등 뒤(Up 계열)에서는 검을 몸 뒤에 둔다.
        // 좌우 측면은 검 실루엣이 보이도록 앞쪽에 둔다.
        context.WeaponRenderer.sortingOrder =
            displaySide is PlayerSide8.Down or PlayerSide8.DownLeft or PlayerSide8.DownRight
                or PlayerSide8.Up or PlayerSide8.UpLeft or PlayerSide8.UpRight
                ? -1
                : 1;
    }

    private void PlayEnterVfx(WeaponVisualState state)
    {
        // 상태 진입 순간에만 호출해야 하는 이펙트를 이곳에 모아둔다.
        // 위치/회전은 현재 검 Transform을 사용하므로, 실제 프리팹 배치는 VfxManager 쪽 테이블에서만 관리하면 된다.
        VfxId vfxId = state switch
        {
            WeaponVisualState.AimingCharged => aimingChargedVfxId,
            WeaponVisualState.FlyingOut => throwStartVfxId,
            WeaponVisualState.MeleeMove => meleeMoveVfxId,
            _ => VfxId.None,
        };

        if (vfxId != VfxId.None)
        {
            VfxManager.TryPlay(vfxId, transform.position, transform.rotation);
        }
    }

    private void PlayAnimation(WeaponAnimState animState)
    {
        if (context?.Animator == null || currentAnimState == animState)
        {
            return;
        }

        context.Animator.SetInteger(AnimStateHash, (int)animState);
        context.Animator.SetTrigger(RestartAnimationHash);
        currentAnimState = animState;
    }

    private bool ResolveWeaponVisible()
    {
        if (context?.VisualRoot != null && context.VisualRoot != gameObject)
        {
            return context.VisualRoot.activeSelf;
        }

        return context?.WeaponRenderer == null || context.WeaponRenderer.enabled;
    }

    private void SetWeaponVisible(bool visible)
    {
        if (context?.VisualRoot != null && context.VisualRoot != gameObject)
        {
            context.VisualRoot.SetActive(visible);
        }

        if (context?.WeaponRenderer != null)
        {
            context.WeaponRenderer.enabled = visible;
        }
    }

    private void PlayVisibilityEffect(bool appearing)
    {
        if (wasVisualVisible == appearing)
        {
            return;
        }

        wasVisualVisible = appearing;
        VfxId vfxId = appearing ? reappearVfxId : vanishVfxId;
        if (vfxId != VfxId.None)
        {
            VfxManager.TryPlay(vfxId, transform.position, transform.rotation);
        }
    }

    private static WeaponAnimState ToAnimState(WeaponVisualState visualState)
    {
        return visualState switch
        {
            WeaponVisualState.AimingMove => WeaponAnimState.AimingMove,
            WeaponVisualState.AimingCharged => WeaponAnimState.AimingCharged,
            WeaponVisualState.FlyingOut => WeaponAnimState.FlyingOut,
            WeaponVisualState.Returning => WeaponAnimState.Returning,
            WeaponVisualState.MeleeMove => WeaponAnimState.MeleeMove,
            WeaponVisualState.HiddenBySlash => WeaponAnimState.HiddenBySlash,
            _ => WeaponAnimState.Orbit,
        };
    }
}
