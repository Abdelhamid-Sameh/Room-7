Shader "Room 7/3D Text (depth tested)"
{
    // Same as Unity's built-in GUI/Text Shader, except it respects walls: ZTest LEqual instead of ZTest Always.
    Properties
    {
        _MainTex ("Font Texture", 2D) = "white" {}
        _Color ("Text Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Lighting Off Cull Off ZTest LEqual ZWrite Off Fog { Mode Off }
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Color [_Color]
            SetTexture [_MainTex] { combine primary, texture * primary }
        }
    }
}
