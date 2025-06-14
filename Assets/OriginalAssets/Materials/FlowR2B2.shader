Shader "Unlit/FlowR2B2"  // 実験9のTraj条件で使用
{
    Properties
    {
        _Ratency ("Ratency", Float) = 0
        _Color ("Main Color", Color) = (1,1,1,1) // ★追加
    }

    CGINCLUDE

    float _Ratency;
    float4 _Color; // ★追加=

    // 一定の時間を超えた場合にループする関数
    // 単位は周期
    float loop_time(float time, float maxTime, float timeOffset = 0.0)
    {
        // timeOffsetは波動の発射タイミングをずらすために使用。
        time = time + timeOffset;

        time = fmod(time, maxTime);
        time += maxTime * step(time, -0.001);
        
        // 0を中心とした範囲にマッピング
        // 0 ~ maxTime を (0 - maxTime/2) ~ (0 + maxTime/2) の範囲に変換
        return time  - maxTime * 0.5;
    }

    float4 paint(float2 uv)
    {
        // _Ratencyに基づく明度係数を計算 (0→1.0, 7.2→0.0)
        float brightness = 1.0 - saturate(_Ratency / 7.2);

        // 円錐の頂点のUV座標は（0.25, 0.25）であり、その他の点が半径0.25の円上に存在するため、その距離に応じて色を変化させる
        // uv座標のx,y成分をそれぞれ0.25引き、2乗して足し合わせて16倍することで、円の半径に応じた距離を計算
        float dist = 16 * ((uv.x - 0.25) * (uv.x - 0.25) + (uv.y - 0.25) * (uv.y - 0.25));

        // distが1より大きい場合は白色を返す。（円錐の底面を白色にするための処理）
        float isOutside = step(1.0, dist);
        float4 whiteColor = float4(1, 0, 0, 1) * _Color;

        float spaceEachCone = 1 - dist; 
         
        // 時間ベースのオフセットを追加（_Timeは Unity の組み込み変数）
        // _Time.y は秒単位の時間
        float timeEachCone = (_Time.y - _Ratency) * 10.0; // 円錐ごとの時間オフセット(1が1周期を表す。)

        float color_r = 1 - 0.4 * abs(spaceEachCone - loop_time(timeEachCone, 10, 0)); 
        float color_b = 1 - 0.4 * abs(spaceEachCone - loop_time(timeEachCone, 10, 5)); 

        // Using step function for conditional coloring
        // step(a,b) returns 0 if b < a, and 1 if b >= a
        float useColor = step(0.0, max(color_r, color_b));
        
        // Combine the two cases using the step value as a mask
        float r = lerp(0, color_r, useColor);
        float b = lerp(0, color_b, useColor); 

        float useBlue = step(color_r, color_b);

        // 複数の範囲でuseBlueを0にする
        // 0.2以上0.3未満、0.4以上0.5未満、0.6以上0.7未満、0.8以上0.9未満
        float range1 = step(0.3, spaceEachCone) * (1.0 - step(0.5, spaceEachCone));
        float range2 = step(0.85, spaceEachCone) * (1.0 - step(0.95, spaceEachCone));

        float inAnyRange = max(range1, range2);
        useBlue = useBlue * (1.0 - inAnyRange);

        float4 normalColor = lerp(
            float4(brightness, brightness, brightness, 1) * _Color, // 通常の色
            float4(0, 0, 1, 1) * _Color, // 青色
            useBlue // 青色を使用するかどうかのマスク
        );

        
        //float4 normalColor = float4(r * brightness, 0, b * brightness, 1) * _Color;
    
        // distが1より大きい場合は白色、そうでなければ通常の色
        return lerp(normalColor, whiteColor, isOutside);
    }

    ENDCG



    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha // ★追加
            ZWrite Off                      // ★追加
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            // 構造体の定義
            struct appdata
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            // vert関数の出力からfrag関数の入力へ
            struct fin
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };


            fin vert(appdata v) // 構造体を使用した入出力
            {
                fin o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                return o;
            }

            float4 frag(fin IN) : SV_TARGET
            {
                return paint(IN.texcoord.xy);
            }
            ENDCG
        }
    }
}
