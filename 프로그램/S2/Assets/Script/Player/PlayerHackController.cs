using System;
using UnityEngine;

public enum HackProgressState
{
    None,
    Ready,
    Progress,
    Complete,
    Cancel,
}

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerFSMManager))]
[RequireComponent(typeof(PlayerLoadout))]
[RequireComponent(typeof(PlayerContext))]
public class PlayerHackController : MonoBehaviour
{
    private PlayerInput input;
    private PlayerFSMManager fsm;
    private PlayerLoadout loadout;
    private PlayerWeaponThrow weaponThrow;

    private ThrownWeapon embeddedWeapon;
    private IHackable currentHackable;
    private float hackTimer;
    private bool returnRequested;

    public HackProgressState CurrentProgressState { get; private set; } = HackProgressState.None;
    public bool IsHackSessionActive => CurrentProgressState != HackProgressState.None;
    public bool HasEmbeddedWeapon => embeddedWeapon != null && embeddedWeapon.IsEmbeddedForHack;
    public ThrownWeapon EmbeddedWeapon => embeddedWeapon;

    public event Action<HackProgressState, HackProgressState> OnHackProgressStateChanged;

    private void Awake()
    {
        PlayerContext context = GetComponent<PlayerContext>();
        context.ResolveReferences();

        input = context.Input;
        fsm = context.Fsm;
        loadout = context.Loadout;
        weaponThrow = context.WeaponThrow;

        if (input == null || fsm == null || loadout == null || weaponThrow == null)
        {
            Debug.LogError($"{nameof(PlayerHackController)} on {name} is missing a required component.", this);
            enabled = false;
            return;
        }

        if (!HasValidData())
        {
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (fsm != null)
        {
            fsm.OnStateChanged += HandlePlayerStateChanged;
        }

        if (weaponThrow != null)
        {
            weaponThrow.EmbeddedHackWeaponRegistered += HandleEmbeddedHackWeaponRegistered;
            weaponThrow.WeaponRecovered += HandleWeaponRecovered;
        }
    }

    private void OnDisable()
    {
        if (fsm != null)
        {
            fsm.OnStateChanged -= HandlePlayerStateChanged;
        }

        if (weaponThrow != null)
        {
            weaponThrow.EmbeddedHackWeaponRegistered -= HandleEmbeddedHackWeaponRegistered;
            weaponThrow.WeaponRecovered -= HandleWeaponRecovered;
        }
    }

    private void Update()
    {
        if (CurrentProgressState == HackProgressState.None)
        {
            return;
        }

        if (fsm.IsState(PlayerState.Dead))
        {
            ClearSessionWithoutReturn();
            return;
        }

        if (CurrentProgressState is HackProgressState.Ready or HackProgressState.Progress)
        {
            if (input.WeaponThrowPressedThisFrame || IsTooFarFromEmbeddedWeapon())
            {
                CancelHack();
                return;
            }
        }

        if (fsm.IsState(PlayerState.Hacking) && CurrentProgressState == HackProgressState.Ready
            && input.HackPressedThisFrame)
        {
            StartHackProgress();
        }

        if (CurrentProgressState == HackProgressState.Progress)
        {
            UpdateHackProgress();
        }
    }

    private void HandleEmbeddedHackWeaponRegistered(ThrownWeapon weapon, IHackable hackable)
    {
        if (weapon == null || hackable == null)
        {
            return;
        }

        if (!HasValidHackableData(hackable))
        {
            return;
        }

        embeddedWeapon = weapon;
        currentHackable = hackable;
        hackTimer = 0f;
        returnRequested = false;
        ChangeProgressState(HackProgressState.Ready);
        currentHackable.OnHackReady();
        TryEnterHackingForEmbeddedWeapon();
    }

    private bool TryEnterHackingForEmbeddedWeapon()
    {
        if (!HasEmbeddedWeapon || CurrentProgressState == HackProgressState.None)
        {
            return false;
        }

        return fsm.RequestState(PlayerState.Hacking);
    }

    public void CancelHack()
    {
        if (CurrentProgressState == HackProgressState.None)
        {
            return;
        }

        ChangeProgressState(HackProgressState.Cancel);
        currentHackable?.OnHackCanceled();
        RequestEmbeddedWeaponReturn();
    }

    private void StartHackProgress()
    {
        hackTimer = 0f;
        ChangeProgressState(HackProgressState.Progress);
        currentHackable?.OnHackStarted();
    }

    private void UpdateHackProgress()
    {
        hackTimer += Time.deltaTime;
        if (hackTimer < ResolveHackDuration())
        {
            return;
        }

        ChangeProgressState(HackProgressState.Complete);
        currentHackable?.OnHackCompleted();
        RequestEmbeddedWeaponReturn();
    }

    private void RequestEmbeddedWeaponReturn()
    {
        if (returnRequested)
        {
            return;
        }

        returnRequested = true;
        embeddedWeapon?.BeginReturnFromHack();
    }

    private void HandlePlayerStateChanged(PlayerState previousState, PlayerState nextState)
    {
        if (nextState == PlayerState.Dead)
        {
            ClearSessionWithoutReturn();
            return;
        }

        if (nextState == PlayerState.WeaponReceiving)
        {
            TryEnterHackingForEmbeddedWeapon();
        }
    }

    private bool IsTooFarFromEmbeddedWeapon()
    {
        if (embeddedWeapon == null)
        {
            return false;
        }

        float maxDistance = ResolveMaxHackDistance();
        if (maxDistance <= 0f)
        {
            return false;
        }

        return Vector2.Distance(transform.position, embeddedWeapon.transform.position) > maxDistance;
    }

    private float ResolveHackDuration()
    {
        return currentHackable.HackData.hackDuration;
    }

    private float ResolveMaxHackDistance()
    {
        return loadout.PlayerData.hack.maxHackDistance;
    }

    private bool HasValidData()
    {
        PlayerData playerData = loadout.PlayerData;
        if (playerData == null)
        {
            Debug.LogError($"{nameof(PlayerHackController)} on {name} requires {nameof(PlayerData)}.", this);
            return false;
        }

        if (playerData.hack == null)
        {
            Debug.LogError($"{nameof(PlayerHackController)} on {name} requires {nameof(PlayerHackData)} in {playerData.name}.", this);
            return false;
        }

        if (playerData.hack.maxHackDistance <= 0f)
        {
            Debug.LogError($"{nameof(PlayerHackController)} on {name} requires maxHackDistance greater than 0 in {playerData.name}.", this);
            return false;
        }

        return true;
    }

    private bool HasValidHackableData(IHackable hackable)
    {
        HackableData hackData = hackable.HackData;
        if (hackData == null)
        {
            Debug.LogError($"{nameof(PlayerHackController)} on {name} received a hackable target without {nameof(HackableData)}.", this);
            return false;
        }

        if (hackData.hackDuration <= 0f)
        {
            Debug.LogError($"{nameof(PlayerHackController)} on {name} requires hackDuration greater than 0 in {hackData.name}.", this);
            return false;
        }

        return true;
    }

    private void ClearSessionWithoutReturn()
    {
        embeddedWeapon = null;
        currentHackable = null;
        hackTimer = 0f;
        returnRequested = false;
        ChangeProgressState(HackProgressState.None);
    }

    private void HandleWeaponRecovered(ThrownWeapon weapon)
    {
        if (embeddedWeapon != weapon)
        {
            return;
        }

        ClearSessionWithoutReturn();
    }

    private void ChangeProgressState(HackProgressState nextState)
    {
        if (CurrentProgressState == nextState)
        {
            return;
        }

        HackProgressState previousState = CurrentProgressState;
        CurrentProgressState = nextState;
        OnHackProgressStateChanged?.Invoke(previousState, nextState);
    }
}
