using UnityEngine;

public static class PlayerAnimFacingUtil
{
    /// <summary>
    /// 8방향(로직용) -> 5방향(애니용) + 좌우 플립 여부
    /// - Down  : 정면(Front), flipX=false
    /// - Up    : 후면(Back),  flipX=false
    /// - Right : Side,        flipX=false
    /// - UpRight : SideUp,    flipX=false
    /// - DownRight : SideDown,flipX=false
    /// - Left/UpLeft/DownLeft : 위 3개를 flipX=true로 처리
    /// </summary>
    public static void ToAnim5(PlayerSide8 side8, out PlayerAnimDir5 animDir, out bool flipX)
    {
        flipX = side8 == PlayerSide8.Left || side8 == PlayerSide8.UpLeft || side8 == PlayerSide8.DownLeft;

        // 좌측 계열은 우측 계열 클립을 flip해서 쓰므로, 그룹만 "우측 기준"으로 맞춰준다
        PlayerSide8 normalized = side8;
        if (flipX)
        {
            normalized = side8 switch
            {
                PlayerSide8.Left => PlayerSide8.Right,
                PlayerSide8.UpLeft => PlayerSide8.UpRight,
                PlayerSide8.DownLeft => PlayerSide8.DownRight,
                _ => side8
            };
        }

        animDir = normalized switch
        {
            PlayerSide8.Down => PlayerAnimDir5.Front,
            PlayerSide8.Up => PlayerAnimDir5.Back,
            PlayerSide8.UpRight => PlayerAnimDir5.SideUp,
            PlayerSide8.DownRight => PlayerAnimDir5.SideDown,
            _ => PlayerAnimDir5.Side, // Right 포함 + 혹시 모를 안전망
        };
    }
}
