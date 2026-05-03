using UnityEngine;

public static class PlayerAnimFacingUtil
{
    /// <summary>
    /// 8방향(로직용) -> 3방향(애니용) + 좌우 플립 여부
    /// - Down/DownRight/DownLeft : Front
    /// - Up/UpRight/UpLeft       : Back
    /// - Right/Left              : Side
    /// - Left 계열 Side는 flipX=true로 처리
    /// </summary>
    public static void ToAnim3(PlayerSide8 side8, out PlayerAnimDir3 animDir, out bool flipX)
    {
        flipX = false;

        animDir = side8 switch
        {
            PlayerSide8.Up or PlayerSide8.UpRight or PlayerSide8.UpLeft => PlayerAnimDir3.Back,
            PlayerSide8.Right => PlayerAnimDir3.Side,
            PlayerSide8.Left => PlayerAnimDir3.Side,
            _ => PlayerAnimDir3.Front,
        };

        if (animDir == PlayerAnimDir3.Side)
        {
            flipX = side8 == PlayerSide8.Left;
        }
    }
}
