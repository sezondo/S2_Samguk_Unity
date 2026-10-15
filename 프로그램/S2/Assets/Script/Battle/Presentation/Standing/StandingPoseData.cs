using System;
using UnityEngine;

/// <summary>캐릭터별 자세의 부위 경계·회전 중심·고정 접점을 보관한다.</summary>
public sealed class StandingPoseData : ScriptableObject
{
    // 사용자에게 표시할 캐릭터·자세 이름과 해당 원화다.
    public string displayName;
    public Texture2D source;
    // 기존 Animator가 표시하는 단일 원화와 비교할 기준 스프라이트다.
    public Sprite sprite;
    // 기준 비교 크기와 웅크림·벽 접점 정책이다.
    public float displayHeight = 2.95f;
    public bool wallCover;
    // 0은 고정, 1~8은 기존 부위별 아이들 관절과 대응한다.
    public Vector2[] anchors;
    // 기존 원화 좌표 850×1187 기준으로 저장한 부위와 접점 경계다.
    public Region[] regions;
    // 총·활·방패 등의 전체 실루엣과 주변 메시를 하나의 강체로 묶는다.
    public Region[] rigidRegions;
    // 확정 표시용 PNG는 이미 외곽 정리를 거쳤으므로 다시 깎지 않는다.
    public bool sourceEdgesCleaned;
    // 자세별 호흡 크기와 인물별 위상 차이, 고정할 하체 높이 비율이다.
    public float motionScale=1f,phaseOffset,fixedLowerFraction=.08f;
    // 발·무릎·벽 접점과 실제 무기 위의 검증 위치다. 정규화 원화 좌표를 쓴다.
    public Vector2[] fixedContacts;
    public RigidProbe[] rigidProbes;

    [Serializable]
    public sealed class RigidProbe
    {
        // 점이 따라야 하는 관절과 원화 내부의 위치다.
        public int bone;
        public Vector2 point;
    }

    [Serializable]
    public sealed class Region
    {
        // 적용할 관절 번호와 다각형 경계·연결 폭이다.
        public int bone;
        public Vector2[] polygon;
        public float feather = 18;
    }
}
