using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ColorChanger
{
    public static float VelocityParameter(float velocity)
    {
        // 位置が10フレームで0.01m変化する時を最小値、0.1m変化するときを最大値とする。
        float minDistance = 0.001f; // 距離の最小値（適宜調整）
        float maxDistance = 0.02f; // 距離の最大値（適宜調整）
        return Mathf.InverseLerp(minDistance, maxDistance, velocity);
    }
    

    // Lab→XYZ→RGB変換
    public static Color LabToRGB(float l, float a, float b)
    {
        // Lab→XYZ
        float y = (l + 16f) / 116f;
        float x = a / 500f + y;
        float z = y - b / 200f;

        float x3 = Mathf.Pow(x, 3);
        float y3 = Mathf.Pow(y, 3);
        float z3 = Mathf.Pow(z, 3);

        x = 0.95047f * (x3 > 0.008856f ? x3 : (x - 16f / 116f) / 7.787f);
        y = 1.00000f * (y3 > 0.008856f ? y3 : (y - 16f / 116f) / 7.787f);
        z = 1.08883f * (z3 > 0.008856f ? z3 : (z - 16f / 116f) / 7.787f);

        // XYZ→RGB
        float r = x * 3.2406f + y * -1.5372f + z * -0.4986f;
        float g = x * -0.9689f + y * 1.8758f + z * 0.0415f;
        float b_ = x * 0.0557f + y * -0.2040f + z * 1.0570f;

        // ガンマ補正
        r = r > 0.0031308f ? 1.055f * Mathf.Pow(r, 1f / 2.4f) - 0.055f : 12.92f * r;
        g = g > 0.0031308f ? 1.055f * Mathf.Pow(g, 1f / 2.4f) - 0.055f : 12.92f * g;
        b_ = b_ > 0.0031308f ? 1.055f * Mathf.Pow(b_, 1f / 2.4f) - 0.055f : 12.92f * b_;

        // 0-1にクランプ
        r = Mathf.Clamp01(r);
        g = Mathf.Clamp01(g);
        b_ = Mathf.Clamp01(b_);

        return new Color(r, g, b_, 1f);
    }
}
