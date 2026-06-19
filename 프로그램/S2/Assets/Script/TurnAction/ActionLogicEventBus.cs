using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 행동 처리 중 발생한 논리 이벤트를 현재 활성 핸들러에게 전달하는 정적 통로다.
/// 각 시스템은 자신이 처리할 이벤트만 받아 후속 논리와 연출 이벤트를 추가한다.
/// </summary>
public static class ActionLogicEventBus
{
    // 현재 씬에서 활성화된 논리 이벤트 핸들러 목록이다.
    private static readonly List<IActionLogicEventHandler> handlers = new();

    /// <summary>
    /// 논리 이벤트 핸들러를 등록한다.
    /// </summary>
    public static void Register(IActionLogicEventHandler handler)
    {
        if (handler == null || handlers.Contains(handler))
        {
            return;
        }

        handlers.Add(handler);
    }

    /// <summary>
    /// 논리 이벤트 핸들러 등록을 해제한다.
    /// </summary>
    public static void Unregister(IActionLogicEventHandler handler)
    {
        if (handler == null)
        {
            return;
        }

        handlers.Remove(handler);
    }

    /// <summary>
    /// 지정한 논리 이벤트를 처리 가능한 모든 핸들러에게 전달한다.
    /// </summary>
    public static void Dispatch(IActionLogicEvent logicEvent, ActionResolutionContext context)
    {
        if (logicEvent == null || context == null)
        {
            return;
        }

        for (int i = 0; i < handlers.Count; i++)
        {
            IActionLogicEventHandler handler = handlers[i];
            if (handler == null || !handler.CanHandle(logicEvent))
            {
                continue;
            }

            try
            {
                handler.Handle(logicEvent, context);
            }
            catch (Exception exception)
            {
                Debug.LogError($"{nameof(ActionLogicEventBus)}: {logicEvent.GetType().Name} 처리 중 예외가 발생했습니다.\n{exception}");
            }
        }
    }
}

/// <summary>
/// 행동 처리 문맥 안에서 전달되는 논리 이벤트의 공통 표식이다.
/// </summary>
public interface IActionLogicEvent
{
}

/// <summary>
/// 행동 논리 이벤트를 처리하는 시스템의 공통 규약이다.
/// </summary>
public interface IActionLogicEventHandler
{
    /// <summary>
    /// 지정한 논리 이벤트를 이 핸들러가 처리할 수 있는지 확인한다.
    /// </summary>
    bool CanHandle(IActionLogicEvent logicEvent);

    /// <summary>
    /// 논리 이벤트를 처리하고 필요하면 후속 논리 이벤트나 연출 이벤트를 문맥에 추가한다.
    /// </summary>
    void Handle(IActionLogicEvent logicEvent, ActionResolutionContext context);
}
