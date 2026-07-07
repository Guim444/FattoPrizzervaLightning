using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Video;

[DisallowMultipleComponent]
public class LoopingVideoSpriteReplacement : MonoBehaviour
{
    [Header("Video")]
    [SerializeField] private VideoClip videoClip;
    [SerializeField] private bool playOnAwake = true;
    [SerializeField] private bool keepSpriteUntilPrepared = true;
    [SerializeField, Min(0)] private int renderTextureWidth = 0;
    [SerializeField, Min(0)] private int renderTextureHeight = 0;

    [Header("Surface")]
    [SerializeField] private string surfaceName = "VideoSurface";
    [SerializeField] private Vector3 localOffset = Vector3.zero;
    [SerializeField] private Vector2 sizeMultiplier = Vector2.one;
    [SerializeField] private bool copySpriteRendererSorting = true;

    private SpriteRenderer _spriteRenderer;
    private Animator _animator;
    private VideoPlayer _videoPlayer;
    private MeshRenderer _surfaceRenderer;
    private RenderTexture _renderTexture;
    private Material _material;
    private Mesh _mesh;
    private bool _videoReady;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _animator = GetComponent<Animator>();

        if (videoClip == null)
        {
            Debug.LogWarning($"[{nameof(LoopingVideoSpriteReplacement)}] Falta asignar VideoClip en '{name}'.", this);
            return;
        }

        BuildSurface();
        ConfigureVideoPlayer();

        if (!keepSpriteUntilPrepared)
            HideSpriteFallback();

        if (playOnAwake)
            PrepareOrPlay();
    }

    private void OnEnable()
    {
        if (_videoReady && playOnAwake && _videoPlayer != null && !_videoPlayer.isPlaying)
            _videoPlayer.Play();
    }

    private void OnDisable()
    {
        if (_videoPlayer != null && _videoPlayer.isPlaying)
            _videoPlayer.Pause();
    }

    private void OnDestroy()
    {
        if (_videoPlayer != null)
        {
            _videoPlayer.prepareCompleted -= HandleVideoPrepared;
            _videoPlayer.errorReceived -= HandleVideoError;
            _videoPlayer.targetTexture = null;
        }

        if (_renderTexture != null)
        {
            _renderTexture.Release();
            Destroy(_renderTexture);
        }

        if (_material != null)
            Destroy(_material);

        if (_mesh != null)
            Destroy(_mesh);
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
        _surfaceRenderer.enabled = !keepSpriteUntilPrepared;
        _surfaceRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _surfaceRenderer.receiveShadows = false;
        _surfaceRenderer.lightProbeUsage = LightProbeUsage.Off;
        _surfaceRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        if (_spriteRenderer != null && copySpriteRendererSorting)
        {
            _surfaceRenderer.sortingLayerID = _spriteRenderer.sortingLayerID;
            _surfaceRenderer.sortingOrder = _spriteRenderer.sortingOrder;
            _surfaceRenderer.renderingLayerMask = _spriteRenderer.renderingLayerMask;
        }

        _mesh = CreateSpriteSizedQuad();
        meshFilter.sharedMesh = _mesh;

        _renderTexture = CreateRenderTexture();
        _material = CreateVideoMaterial(_renderTexture);
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

        Mesh mesh = new Mesh { name = $"{name}_VideoSurfaceMesh" };
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

        float aspect = GetVideoAspectRatio();
        return new Bounds(Vector3.zero, new Vector3(aspect, 1f, 0f));
    }

    private void ConfigureVideoPlayer()
    {
        _videoPlayer = GetComponent<VideoPlayer>();
        if (_videoPlayer == null)
            _videoPlayer = gameObject.AddComponent<VideoPlayer>();

        _videoPlayer.playOnAwake = false;
        _videoPlayer.isLooping = true;
        _videoPlayer.skipOnDrop = true;
        _videoPlayer.waitForFirstFrame = true;
        _videoPlayer.source = VideoSource.VideoClip;
        _videoPlayer.clip = videoClip;
        _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        _videoPlayer.targetTexture = _renderTexture;
        _videoPlayer.audioOutputMode = VideoAudioOutputMode.None;

        _videoPlayer.prepareCompleted += HandleVideoPrepared;
        _videoPlayer.errorReceived += HandleVideoError;
    }

    private void PrepareOrPlay()
    {
        if (_videoPlayer == null)
            return;

        if (_videoPlayer.isPrepared)
        {
            ShowVideoSurface();
            _videoPlayer.Play();
            return;
        }

        _videoPlayer.Prepare();
    }

    private void HandleVideoPrepared(VideoPlayer source)
    {
        ShowVideoSurface();

        if (playOnAwake)
            source.Play();
    }

    private void HandleVideoError(VideoPlayer source, string message)
    {
        if (_surfaceRenderer != null)
            _surfaceRenderer.enabled = false;

        Debug.LogWarning($"[{nameof(LoopingVideoSpriteReplacement)}] Error reproduciendo '{videoClip.name}' en '{name}': {message}", this);
    }

    private void ShowVideoSurface()
    {
        _videoReady = true;

        if (_surfaceRenderer != null)
            _surfaceRenderer.enabled = true;

        HideSpriteFallback();
    }

    private void HideSpriteFallback()
    {
        if (_animator != null)
            _animator.enabled = false;

        if (_spriteRenderer != null)
            _spriteRenderer.enabled = false;
    }

    private RenderTexture CreateRenderTexture()
    {
        int width = renderTextureWidth > 0 ? renderTextureWidth : GetClipWidthSafe();
        int height = renderTextureHeight > 0 ? renderTextureHeight : GetClipHeightSafe();

        RenderTexture texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
        {
            name = $"{name}_{videoClip.name}_RT",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false
        };
        texture.Create();
        return texture;
    }

    private Material CreateVideoMaterial(Texture texture)
    {
        Shader shader = ResolveVideoSurfaceShader();
        Material material = new Material(shader)
        {
            name = $"{name}_{videoClip.name}_MAT"
        };

        SetMaterialTexture(material, "_BaseMap", texture);
        SetMaterialTexture(material, "_MainTex", texture);

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

    private int GetClipWidthSafe()
        => videoClip != null && videoClip.width > 0 ? (int)videoClip.width : 512;

    private int GetClipHeightSafe()
        => videoClip != null && videoClip.height > 0 ? (int)videoClip.height : 512;

    private float GetVideoAspectRatio()
    {
        int height = GetClipHeightSafe();
        return height > 0 ? GetClipWidthSafe() / (float)height : 1f;
    }
}
