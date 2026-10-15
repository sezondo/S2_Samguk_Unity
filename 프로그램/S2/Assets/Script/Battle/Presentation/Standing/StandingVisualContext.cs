using UnityEngine;

/// <summary>부위별 대기 표시가 사용하는 시각 컴포넌트를 모으는 참조 주머니다.</summary>
public sealed class StandingVisualContext : MonoBehaviour
{
    // 기존 전투 연출과 실제 스프라이트의 참조다.
    public ActorVisualController visual;
    public SpriteRenderer sourceRenderer;
    // 일반 대기·낮은 엄폐·벽 엄폐에 대응하는 확정 메시다.
    public StandingPoseMesh idle,lowCover,wallCover;
}
