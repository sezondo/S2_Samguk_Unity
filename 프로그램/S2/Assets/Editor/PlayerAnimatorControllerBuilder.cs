using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class PlayerAnimatorControllerBuilder
{
    private const string ControllerPath = "Assets/Art/Character/Yujin/PlayerAniController.controller";
    private const string AnimStateParameter = "AnimState";
    private const string RestartAnimationParameter = "RestartAnimation";

    private readonly struct ClipEntry
    {
        public ClipEntry(string stateName, string clipPath, PlayerAnimState animState, Vector3 position)
        {
            StateName = stateName;
            ClipPath = clipPath;
            AnimState = animState;
            Position = position;
        }

        public string StateName { get; }
        public string ClipPath { get; }
        public PlayerAnimState AnimState { get; }
        public Vector3 Position { get; }
    }

    private static readonly ClipEntry[] ClipEntries =
    {
        new("Idle_Front", "Assets/Art/Character/Yujin/Sprite/Idle/Front/Idle_Front.anim", PlayerAnimState.IdleFront, new Vector3(280, 40, 0)),
        new("Idle_Side", "Assets/Art/Character/Yujin/Sprite/Idle/Side/Idle_Side.anim", PlayerAnimState.IdleSide, new Vector3(500, 40, 0)),
        new("Idle_Back", "Assets/Art/Character/Yujin/Sprite/Idle/Back/Idle_Back.anim", PlayerAnimState.IdleBack, new Vector3(720, 40, 0)),

        new("Run_Front", "Assets/Art/Character/Yujin/Sprite/Run/Front/Run_Front.anim", PlayerAnimState.RunFront, new Vector3(280, 110, 0)),
        new("Run_Side", "Assets/Art/Character/Yujin/Sprite/Run/Side/Run_Side.anim", PlayerAnimState.RunSide, new Vector3(500, 110, 0)),
        new("Run_Back", "Assets/Art/Character/Yujin/Sprite/Run/Back/Run_Back.anim", PlayerAnimState.RunBack, new Vector3(720, 110, 0)),

        new("Attack_Front01", "Assets/Art/Character/Yujin/Sprite/Attack/Front/Attack_Front01.anim", PlayerAnimState.AttackFront, new Vector3(280, 180, 0)),
        new("Attack_Side01", "Assets/Art/Character/Yujin/Sprite/Attack/Side/Attack_Side01.anim", PlayerAnimState.AttackSide, new Vector3(500, 180, 0)),
        new("Attack_Back01", "Assets/Art/Character/Yujin/Sprite/Attack/Back/Attack_Back01.anim", PlayerAnimState.AttackBack, new Vector3(720, 180, 0)),

        new("Dodge_Front", "Assets/Art/Character/Yujin/Sprite/Dodge/Front/Dodge_Front.anim", PlayerAnimState.DodgeFront, new Vector3(280, 250, 0)),
        new("Dodge_Side", "Assets/Art/Character/Yujin/Sprite/Dodge/Side/Dodge_Side.anim", PlayerAnimState.DodgeSide, new Vector3(500, 250, 0)),
        new("Dodge_Back", "Assets/Art/Character/Yujin/Sprite/Dodge/Back/Dodge_Back.anim", PlayerAnimState.DodgeBack, new Vector3(720, 250, 0)),

        new("Dead_Front", "Assets/Art/Character/Yujin/Sprite/Dead/Front/Dead_Front.anim", PlayerAnimState.DeadFront, new Vector3(280, 320, 0)),
        new("Dead_Side", "Assets/Art/Character/Yujin/Sprite/Dead/Side/Dead_Side.anim", PlayerAnimState.DeadSide, new Vector3(500, 320, 0)),
        new("Dead_Back", "Assets/Art/Character/Yujin/Sprite/Dead/Back/Dead_Back.anim", PlayerAnimState.DeadBack, new Vector3(720, 320, 0)),

        new("WeaponThrowReady_Front", "Assets/Art/Character/Yujin/Sprite/WeaponThrowReady/Front/WeaponThrowReady_Front.anim", PlayerAnimState.WeaponThrowReadyFront, new Vector3(280, 390, 0)),
        new("WeaponThrowReady_Side", "Assets/Art/Character/Yujin/Sprite/WeaponThrowReady/Side/WeaponThrowReady_Side.anim", PlayerAnimState.WeaponThrowReadySide, new Vector3(500, 390, 0)),
        new("WeaponThrowReady_Back", "Assets/Art/Character/Yujin/Sprite/WeaponThrowReady/Back/WeaponThrowReady_Back.anim", PlayerAnimState.WeaponThrowReadyBack, new Vector3(720, 390, 0)),

        new("WeaponThrow_Front", "Assets/Art/Character/Yujin/Sprite/WeaponThrow/Front/WeaponThrow_Front.anim", PlayerAnimState.WeaponThrowFront, new Vector3(280, 460, 0)),
        new("WeaponThrow_Side", "Assets/Art/Character/Yujin/Sprite/WeaponThrow/Side/WeaponThrow_Side.anim", PlayerAnimState.WeaponThrowSide, new Vector3(500, 460, 0)),
        new("WeaponThrow_Back", "Assets/Art/Character/Yujin/Sprite/WeaponThrow/Back/WeaponThrow_Back.anim", PlayerAnimState.WeaponThrowBack, new Vector3(720, 460, 0)),
    };

    [MenuItem("S2/Animation/Rebuild Player Animator Controller")]
    public static void RebuildPlayerAnimatorController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        Undo.RegisterCompleteObjectUndo(controller, "Rebuild Player Animator Controller");
        EnsureSingleBaseLayer(controller);
        RebuildParameters(controller);
        RebuildStateMachine(controller.layers[0].stateMachine);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(ControllerPath);
        Debug.Log($"{nameof(PlayerAnimatorControllerBuilder)} rebuilt {ControllerPath}.");
    }

    private static void EnsureSingleBaseLayer(AnimatorController controller)
    {
        if (controller.layers.Length == 0)
        {
            AnimatorControllerLayer layer = new()
            {
                name = "Base Layer",
                stateMachine = new AnimatorStateMachine()
            };
            AssetDatabase.AddObjectToAsset(layer.stateMachine, controller);
            controller.AddLayer(layer);
        }

        AnimatorControllerLayer[] layers = controller.layers;
        if (layers.Length > 1)
        {
            Array.Resize(ref layers, 1);
            controller.layers = layers;
        }
    }

    private static void RebuildParameters(AnimatorController controller)
    {
        while (controller.parameters.Length > 0)
        {
            controller.RemoveParameter(controller.parameters[0]);
        }

        controller.AddParameter(AnimStateParameter, AnimatorControllerParameterType.Int);
        controller.AddParameter(RestartAnimationParameter, AnimatorControllerParameterType.Trigger);
    }

    private static void RebuildStateMachine(AnimatorStateMachine stateMachine)
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            stateMachine.RemoveState(childState.state);
        }

        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
        {
            stateMachine.RemoveAnyStateTransition(transition);
        }

        stateMachine.entryPosition = new Vector3(40, 110, 0);
        stateMachine.anyStatePosition = new Vector3(40, 40, 0);
        stateMachine.exitPosition = new Vector3(960, 110, 0);

        AnimatorState defaultState = null;
        foreach (ClipEntry entry in ClipEntries)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(entry.ClipPath);
            if (clip == null)
            {
                Debug.LogWarning($"Missing animation clip: {entry.ClipPath}");
                continue;
            }

            AnimatorState state = stateMachine.AddState(entry.StateName, entry.Position);
            state.motion = clip;
            state.writeDefaultValues = true;

            AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(state);
            transition.hasExitTime = false;
            transition.duration = 0f;
            transition.offset = 0f;
            transition.exitTime = 0f;
            transition.canTransitionToSelf = true;
            transition.AddCondition(AnimatorConditionMode.Equals, (int)entry.AnimState, AnimStateParameter);
            transition.AddCondition(AnimatorConditionMode.If, 0f, RestartAnimationParameter);

            if (entry.StateName == "Idle_Front")
            {
                defaultState = state;
            }
        }

        if (defaultState != null)
        {
            stateMachine.defaultState = defaultState;
        }
    }
}
