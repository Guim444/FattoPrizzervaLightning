Shader "Custom/OutlineEffect"
{
    // -------------------------------------------------------------------------
    // PROPERTIES
    // Exposed to the Unity Material Inspector.
    // -------------------------------------------------------------------------
    Properties
    {
        _OutlineColor     ("Outline Color",     Color)  = (0, 0, 0, 1)
        _OutlineThickness ("Outline Thickness", Range(0.0, 0.1)) = 0.02
        _EdgeThreshold    ("Edge Threshold",    Range(0.0, 1.0)) = 0.3
    }

    SubShader
    {
        // ---------------------------------------------------------------------
        // TAG BLOCK
        // "RenderType"="Opaque"  — tells Unity this belongs in the opaque
        //   render bucket (drawn before transparents, eligible for batching).
        // "Queue"="Geometry"     — rendered in the standard opaque geometry
        //   queue, not after-effects or overlay queues.
        // ---------------------------------------------------------------------
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        // =====================================================================
        // PASS 1 — OUTLINE PASS  (back-face extrusion)
        // =====================================================================
        // STRATEGY: render only the back faces of the mesh, push each vertex
        // outward along its normal by _OutlineThickness. Because front faces are
        // culled here, only the "halo" peeking out around the silhouette is
        // visible. This is the classic, GPU-cheap outline technique.
        // =====================================================================
        Pass
        {
            Name "OUTLINE"

            // ------------------------------------------------------------------
            // Cull FRONT — discard front-facing triangles so only the
            // enlarged back shell is rasterised.  The real mesh surface,
            // drawn in Pass 2, covers the interior exactly.
            // ------------------------------------------------------------------
            Cull Front

            // ------------------------------------------------------------------
            // ZWrite ON / ZTest LEqual — write depth normally so the outline
            // correctly occludes and is occluded by other geometry.
            // ------------------------------------------------------------------
            ZWrite On
            ZTest  LEqual

            CGPROGRAM
            #pragma vertex   vert_outline
            #pragma fragment frag_outline
            #include "UnityCG.cginc"

            // ------------------------------------------------------------------
            // Uniforms declared here mirror the Properties block above.
            // Unity matches them by name automatically.
            // ------------------------------------------------------------------
            float4 _OutlineColor;
            float  _OutlineThickness;
            float  _EdgeThreshold;

            struct appdata_outline
            {
                float4 vertex : POSITION;   // object-space position
                float3 normal : NORMAL;     // object-space normal
            };

            struct v2f_outline
            {
                float4 pos : SV_POSITION;
            };

            // ------------------------------------------------------------------
            // VERTEX SHADER — normal-extrusion outline
            //
            // HOW IT WORKS:
            //   1. Transform the normal into VIEW space.  View-space normals
            //      point directly at/away from the camera, so extruding in
            //      view space keeps the outline a consistent screen-space width
            //      regardless of mesh orientation or camera angle.
            //   2. Scale the extrusion amount by _EdgeThreshold.
            //      A lower threshold shrinks the perpendicular component of the
            //      push, so only normals that face the camera nearly head-on
            //      (i.e. silhouette normals) contribute significantly — this
            //      suppresses "inner crease" outlines on flat surfaces.
            //      A higher threshold restores full extrusion everywhere,
            //      revealing more interior-edge outlines.
            //   3. Push the clip-space position by the scaled normal offset.
            // ------------------------------------------------------------------
            v2f_outline vert_outline(appdata_outline v)
            {
                v2f_outline o;

                // Move to clip space first (standard MVP transform).
                float4 clipPos = UnityObjectToClipPos(v.vertex);

                // Transform normal to view space (no translation, use the
                // inverse-transpose of the model-view matrix for correctness
                // with non-uniform scale).
                float3 viewNormal = mul((float3x3)UNITY_MATRIX_IT_MV, v.normal);

                // Project the view normal into clip/NDC space so the offset is
                // measured in screen pixels rather than world units.
                // We only care about the XY screen-plane component (Z push
                // would move toward/away from camera, not outward).
                float2 screenNormal = normalize(
                    mul((float2x2)UNITY_MATRIX_P, viewNormal.xy)
                );

                // _EdgeThreshold controls how much of the raw normal is kept.
                // dot(viewNormal, forward) is near 1 for normals facing the
                // camera (flat surfaces) and near 0 for silhouette normals.
                // By lerping between full and attenuated thickness we make the
                // outline stronger at silhouette edges than at flat-face edges.
                float facing    = abs(normalize(viewNormal).z);          // 0=silhouette, 1=facing
                float edgeFactor = 1.0 - saturate(facing / max(_EdgeThreshold, 0.001));

                // Apply the 2-D offset in clip space.
                // Divide by clipPos.w converts from clip to NDC, keeping the
                // outline pixel-width consistent across depth.
                clipPos.xy += screenNormal * _OutlineThickness * edgeFactor
                              * clipPos.w;          // re-multiply by w to stay in clip space

                o.pos = clipPos;
                return o;
            }

            // ------------------------------------------------------------------
            // FRAGMENT SHADER — solid outline colour, no lighting.
            // This is the "Unlit" part of the brief: we output _OutlineColor
            // directly with no diffuse/specular/shadow computation.
            // ------------------------------------------------------------------
            float4 frag_outline(v2f_outline i) : SV_Target
            {
                return _OutlineColor;
            }

            ENDCG
        }

        // =====================================================================
        // PASS 2 — SURFACE PASS  (front-face, unlit fill)
        // =====================================================================
        // PURPOSE: restore the original mesh surface so the interior of the
        // object is covered and the outline only appears around the edge.
        //
        // For a "pure outline" look the surface can be invisible (alpha 0) yet
        // still write depth — this stops the back-face shell bleeding through
        // the front of the mesh from the camera's viewpoint.
        //
        // If you want to combine this shader with an albedo texture, replace
        // the frag_surface body with your own colour/texture lookup; the
        // outline pass above is independent.
        // =====================================================================
        Pass
        {
            Name "SURFACE"

            Cull  Back      // normal: discard back faces, render front faces
            ZWrite On
            ZTest  LEqual

            ColorMask RGBA

            CGPROGRAM
            #pragma vertex   vert_surface
            #pragma fragment frag_surface
            #include "UnityCG.cginc"

            struct appdata_surface
            {
                float4 vertex : POSITION;
            };

            struct v2f_surface
            {
                float4 pos : SV_POSITION;
            };

            v2f_surface vert_surface(appdata_surface v)
            {
                v2f_surface o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            // ------------------------------------------------------------------
            // Unlit surface: outputs a solid opaque fill.
            // Replace with float4(albedo, 1) or a texture sample if you want
            // a coloured body inside the outline.
            // ------------------------------------------------------------------
            float4 frag_surface(v2f_surface i) : SV_Target
            {
                // Solid opaque black fill — change to any colour you like,
                // or set alpha to 0 and add "Blend SrcAlpha OneMinusSrcAlpha"
                // above for a "ghost" outline-only look.

                return float4(0, 0, 0, 1);
            }

            ENDCG
        }
    }

    // Fallback to the built-in Unlit shader if the SubShader is unsupported.
    Fallback "Unlit/Color"
}
