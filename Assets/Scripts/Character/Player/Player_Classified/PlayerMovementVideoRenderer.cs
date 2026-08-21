using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Video;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
public class PlayerMovementVideoRenderer : MonoBehaviour
{
    [Header("Movement Clips")]
    [SerializeField] private VideoClip walkClip;
    [SerializeField] private VideoClip runClip;

    [Header("Surface")]
    [SerializeField] private string surfaceName = "PlayerMovementVideoSurface";
    [SerializeField] private Vector3 localOffset = Vector3.zero;
    [SerializeField] private Vector2 sizeMultiplier = Vector2.one;
    [SerializeField, Min(0)] private int renderTextureWidth = 0;
    [SerializeField, Min(0)] private int renderTextureHeight = 0;
    [SerializeField] private bool keepSpriteUntilPrepared = true;

    private PlayerController _player;
    private SpriteRenderer _spriteRenderer;
    private MeshRenderer _surfaceRenderer;
    private Material _material;
    private Mesh _mesh;
    private VideoSlot _walkSlot;
    private VideoSlot _runSlot;
    private VideoSlot _activeSlot;

    private sealed class VideoSlot
    {
        public VideoClip clip;
        public VideoPlayer player;
        public RenderTexture texture;
        public bool ready;
    }

    private void Awake()
    {
        if (!enabled)
            return;

        _player = GetComponent<PlayerController>();
        _spriteRenderer = GetComponent<SpriteRenderer>();

        if (walkClip == null && runClip == null)
        {
            enabled = false;
            return;
        }

        BuildSurface();
        _walkSlot = CreateSlot(walkClip, "Walk");
        _runSlot = CreateSlot(runClip, "Run");

        PrepareSlot(_walkSlot);
        PrepareSlot(_runSlot);
        ShowAnimatorSprite();
    }

    private void LateUpdate()
    {
        VideoSlot targetSlot = GetSlotForState(_player.currentState);

        if (targetSlot == null)
        {
            ShowAnimatorSprite();
            return;
        }

        if (!targetSlot.ready && keepSpriteUntilPrepared)
        {
            ShowAnimatorSprite();
            return;
        }

        ShowVideoSlot(targetSlot);
    }

    private void OnDisable()
    {
        PauseSlot(_walkSlot);
        PauseSlot(_runSlot);

        if (_surfaceRenderer != null)
            _surfaceRenderer.enabled = false;

        if (_spriteRenderer != null)
            _spriteRenderer.enabled = true;
    }

    private void OnDestroy()
    {
        ReleaseSlot(_walkSlot);
        ReleaseSlot(_runSlot);

        if (_material != null)
            Destroy(_material);

        if (_mesh != null)
            Destroy(_mesh);
    }

    private VideoSlot GetSlotForState(State state)
    {
        if (state == State.Moving)
            return _walkSlot;

        if (state == State.Running)
            return _runSlot;

        return null;
    }

    private void ShowVideoSlot(VideoSlot slot)
    {
        if (slot == null || slot.player == null || _surfaceRenderer == null) return;

        if (_activeSlot != slot)
        {
            PauseSlot(_activeSlot);
            SetMaterialTexture(_material, "_BaseMap", slot.texture);
            SetMaterialTexture(_material, "_MainTex", slot.texture);
            _activeSlot = slot;
        }

        if (_spriteRenderer != null)
            _spriteRenderer.enabled = false;

        _surfaceRenderer.enabled = true;

        if (!slot.player.isPlaying)
            slot.player.Play();
    }

    private void ShowAnimatorSprite()
    {
        PauseSlot(_activeSlot);
        _activeSlot = null;

        if (_surfaceRenderer != null)
            _surfaceRenderer.enabled = false;

        if (_spriteRenderer != null)
            _spriteRenderer.enabled = true;
    }

    private void BuildSurface()
    {
        GameObject surface = new GameObject(surfaceName);
        surface.layer = gameObject.layer;
        surface.transform.SetParent(transform, false);
        surface.transform.localPosition = localOffset;
        surface.transform.localRotation = Quaternion.identity;
        surface.transform.localScale = Vector3.one;

        MeshFilter meshFilter = surface.AddComponent<MeshFilter>();
        _surfaceRenderer = surface.AddComponent<MeshRenderer>();
        _surfaceRenderer.enabled = false;
        _surfaceRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _surfaceRenderer.receiveShadows = false;
        _surfaceRenderer.lightProbeUsage = LightProbeUsage.Off;
        _surfaceRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        if (_spriteRenderer != null)
        {
            _surfaceRenderer.sortingLayerID = _spriteRenderer.sortingLayerID;
            _surfaceRenderer.sortingOrder = _spriteRenderer.sortingOrder;
            _surfaceRenderer.renderingLayerMask = _spriteRenderer.renderingLayerMask;
        }

        _mesh = CreateSpriteSizedQuad();
        meshFilter.sharedMesh = _mesh;

        _material = CreateVideoMaterial();
        _surfaceRenderer.sharedMaterial = _material;
    }

    private Mesh CreateSpriteSizedQuad()
    {
        Bounds bounds = GetReferenceBounds();
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        extents.x *= Mathf.Max(0.01f, sizeMultiplier.x);
        extents.y *= Mathf.Max(0.01f, sizeMultiplier.y);

        Vector3 min = center - extents;
        Vector3 max = center + extents;

        Mesh mesh = new Mesh { name = $"{name}_MovementVideoSurfaceMesh" };
        mesh.vertices = new[]
        {
            new Vector3(min.x, min.y, 0f),
            new Vector3(min.x, max.y, 0f),
            new Vector3(max.x, max.y, 0f),
            new Vector3(max.x, min.y, 0f)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0f)
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        return mesh;
    }

    private Bounds GetReferenceBounds()
    {
        if (_spriteRenderer != null)
        {
            if (_spriteRenderer.drawMode == SpriteDrawMode.Simple && _spriteRenderer.sprite != null)
                return _spriteRenderer.sprite.bounds;

            return new Bounds(Vector3.zero, new Vector3(_spriteRenderer.size.x, _spriteRenderer.size.y, 0f));
        }

        return new Bounds(Vector3.zero, Vector3.one);
    }

    private VideoSlot CreateSlot(VideoClip clip, string slotName)
    {
        if (clip == null) return null;

        VideoSlot slot = new VideoSlot
        {
            clip = clip,
            texture = CreateRenderTexture(clip, slotName)
        };

        slot.player = gameObject.AddComponent<VideoPlayer>();
        slot.player.playOnAwake = false;
        slot.player.isLooping = true;
        slot.player.skipOnDrop = true;
        slot.player.waitForFirstFrame = true;
        slot.player.source = VideoSource.VideoClip;
        slot.player.clip = clip;
        slot.player.renderMode = VideoRenderMode.RenderTexture;
        slot.player.targetTexture = slot.texture;
        slot.player.audioOutputMode = VideoAudioOutputMode.None;
        slot.player.prepareCompleted += HandleVideoPrepared;
        slot.player.errorReceived += HandleVideoError;

        return slot;
    }

    private void PrepareSlot(VideoSlot slot)
    {
        if (slot?.player == null) return;
        slot.player.Prepare();
    }

    private void PauseSlot(VideoSlot slot)
    {
        if (slot?.player != null && slot.player.isPlaying)
            slot.player.Pause();
    }

    private void ReleaseSlot(VideoSlot slot)
    {
        if (slot == null) return;

        if (slot.player != null)
        {
            slot.player.prepareCompleted -= HandleVideoPrepared;
            slot.player.errorReceived -= HandleVideoError;
            slot.player.targetTexture = null;
        }

        if (slot.texture != null)
        {
            slot.texture.Release();
            Destroy(slot.texture);
        }
    }

    private RenderTexture CreateRenderTexture(VideoClip clip, string slotName)
    {
        int width = renderTextureWidth > 0 ? renderTextureWidth : GetClipWidthSafe(clip);
        int height = renderTextureHeight > 0 ? renderTextureHeight : GetClipHeightSafe(clip);

        RenderTexture texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
        {
            name = $"{name}_{slotName}_MovementVideo_RT",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false
        };
        texture.Create();
        return texture;
    }

    private Material CreateVideoMaterial()
    {
        Material material = new Material(ResolveVideoSurfaceShader())
        {
            name = $"{name}_MovementVideo_MAT"
        };

        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);

        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        return material;
    }

    private Shader ResolveVideoSurfaceShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null) return shader;

        shader = Shader.Find("Unlit/Transparent");
        if (shader != null) return shader;

        return Shader.Find("Sprites/Default");
    }

    private void SetMaterialTexture(Material material, string propertyName, Texture texture)
    {
        if (material == null || texture == null || !material.HasProperty(propertyName)) return;
        material.SetTexture(propertyName, texture);
    }

    private int GetClipWidthSafe(VideoClip clip)
        => clip != null && clip.width > 0 ? (int)clip.width : 512;

    private int GetClipHeightSafe(VideoClip clip)
        => clip != null && clip.height > 0 ? (int)clip.height : 512;

    private void HandleVideoPrepared(VideoPlayer source)
    {
        VideoSlot slot = ResolveSlot(source);
        if (slot != null)
            slot.ready = true;
    }

    private void HandleVideoError(VideoPlayer source, string message)
    {
        Debug.LogWarning($"[{nameof(PlayerMovementVideoRenderer)}] Error reproduciendo video de movimiento en '{name}': {message}", this);
    }

    private VideoSlot ResolveSlot(VideoPlayer source)
    {
        if (_walkSlot != null && _walkSlot.player == source)
            return _walkSlot;

        if (_runSlot != null && _runSlot.player == source)
            return _runSlot;

        return null;
    }
}
