using UnityEngine;

public class HackableTest : MonoBehaviour, IHackable
{
    [SerializeField] private HackableData hackData;

    public HackableData HackData => hackData;

    public void OnHackReady()
    {
        Debug.Log($"{name}: 해킹 준비됨", this);
    }

    public void OnHackStarted()
    {
        Debug.Log($"{name}: 해킹 시작", this);
    }

    public void OnHackCompleted()
    {
        Debug.Log($"{name}: 나 해킹됐어요", this);
    }

    public void OnHackCanceled()
    {
        Debug.Log($"{name}: 해킹 취소", this);
    }
}
