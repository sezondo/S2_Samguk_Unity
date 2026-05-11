using UnityEngine;

public class PlayerArrowSwitcher : MonoBehaviour
{
    private void Awake()
    {
        // 활/화살 교체 시스템은 폐기됐다.
        // 기존 씬에 이 컴포넌트가 남아 있어도 해킹(E)과 일반 상호작용(F) 입력을 방해하지 않도록 비활성화한다.
        enabled = false;
    }
}
