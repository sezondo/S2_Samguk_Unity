using UnityEngine;

[CreateAssetMenu(menuName = "Scriptable/ArrowDataTable", fileName = "ArrowDataTable")]
public class ArrowDataTable : ScriptableObject
{
    // 게임 안에 존재하는 모든 화살 종류 목록이다.
    // 플레이어가 현재 사용할 수 있는 화살 목록은 PlayerLoadout에서 따로 관리한다.
    public ArrowData[] allArrowData;

    public ArrowData FindByTypeId(int arrowTypeId)
    {
        if (allArrowData == null)
        {
            return null;
        }

        foreach (ArrowData arrowData in allArrowData)
        {
            if (arrowData != null && arrowData.arrowTypeId == arrowTypeId)
            {
                return arrowData;
            }
        }

        return null;
    }
}
