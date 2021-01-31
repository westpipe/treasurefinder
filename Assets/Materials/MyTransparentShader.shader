Shader "Unlit/TransparentShader"
{
    Properties
    {
         _Color("Color", Color) = (1,1,1,.5)
    }
        SubShader
    {
         Tags {"Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent"}
         LOD 100

         ZWrite On
         Blend SrcAlpha OneMinusSrcAlpha

         Pass
         {
              Lighting Off
              Color[_Color]
         }
    }
}
