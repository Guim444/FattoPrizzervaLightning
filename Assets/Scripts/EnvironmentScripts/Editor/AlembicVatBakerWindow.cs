using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Formats.Alembic.Importer;

public sealed class AlembicVatBakerWindow : EditorWindow
{
    private const string VatShaderName = "FattoPrizzerva/VAT/URP Lit";

    [SerializeField] private GameObject sourceAlembic;
    [SerializeField] private Material visualMaterialOverride;
    [SerializeField] private string outputFolder = "Assets/Generated/VAT";
    [SerializeField, Min(1f)] private float framesPerSecond = 15f;
    [SerializeField, Min(0f)] private float sampleStart;
    [SerializeField, Min(0.01f)] private float sampleEnd = 2f;
    [SerializeField] private int maximumTextureWidth = 4096;
    [SerializeField] private bool bakeNormals = true;
    [SerializeField] private bool enableDepthNormalsPass = true;
    [SerializeField] private bool interpolateFrames = true;
    [SerializeField] private float playbackSpeed = 1f;
    [SerializeField, Range(0f, 1f)] private float randomPhase = 1f;

    private Vector2 scroll;

    [MenuItem("Tools/Fatto Prizzerva/Alembic VAT Baker")]
    private static void Open()
    {
        var window = GetWindow<AlembicVatBakerWindow>("Alembic VAT Baker");
        window.minSize = new Vector2(430f, 540f);
        window.TryUseCurrentSelection();
    }

    private void OnEnable()
    {
        if (sourceAlembic == null)
            TryUseCurrentSelection();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Alembic → Vertex Animation Texture", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Arrastra el GameObject importado desde un único archivo .abc. Para esta primera versión, " +
            "el Alembic debe contener una sola malla y mantener la misma topología durante toda la animación.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        sourceAlembic = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Alembic source", "Arrastra aquí el asset .abc desde Project, no el prefab con Slow y Fast."),
            sourceAlembic,
            typeof(GameObject),
            true);
        if (EditorGUI.EndChangeCheck())
            UseSourceDuration();

        visualMaterialOverride = (Material)EditorGUILayout.ObjectField(
            new GUIContent(
                "Material visual",
                "Opcional. Usa el material del prefab original cuando el .abc no tiene asignado el material final."),
            visualMaterialOverride,
            typeof(Material),
            false);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Usar selección"))
                TryUseCurrentSelection();
            if (GUILayout.Button("Usar duración completa"))
                UseSourceDuration();
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Muestreo", EditorStyles.boldLabel);
        framesPerSecond = EditorGUILayout.FloatField("FPS", Mathf.Max(1f, framesPerSecond));
        sampleStart = EditorGUILayout.FloatField("Inicio (s)", Mathf.Max(0f, sampleStart));
        sampleEnd = EditorGUILayout.FloatField("Fin (s)", Mathf.Max(sampleStart + 0.01f, sampleEnd));
        maximumTextureWidth = EditorGUILayout.IntPopup(
            "Ancho máximo",
            maximumTextureWidth,
            new[] { "1024", "2048", "4096", "8192" },
            new[] { 1024, 2048, 4096, 8192 });
        bakeNormals = EditorGUILayout.Toggle(
            new GUIContent("Hornear normales", "Mejora la iluminación. Desactívalo para una prueba rápida Unlit."),
            bakeNormals);
        enableDepthNormalsPass = EditorGUILayout.Toggle(
            new GUIContent(
                "Depth Normals (SSAO/Fog)",
                "Incluye los árboles VAT en la textura de profundidad y normales usada por SSAO y niebla volumétrica."),
            enableDepthNormalsPass);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Reproducción generada", EditorStyles.boldLabel);
        interpolateFrames = EditorGUILayout.Toggle("Interpolar frames", interpolateFrames);
        playbackSpeed = EditorGUILayout.FloatField("Velocidad", playbackSpeed);
        randomPhase = EditorGUILayout.Slider(
            new GUIContent("Fase por posición", "0 sincroniza todas las instancias; 1 reparte la fase por posición mundial."),
            randomPhase,
            0f,
            1f);

        EditorGUILayout.Space(8f);
        outputFolder = EditorGUILayout.TextField("Carpeta de salida", outputFolder);

        DrawEstimate();

        EditorGUILayout.Space(12f);
        using (new EditorGUI.DisabledScope(sourceAlembic == null || EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Generar VAT + material + prefab", GUILayout.Height(38f)))
                Bake();
        }

        EditorGUILayout.HelpBox(
            "La salida utiliza RGBAHalf para posiciones y RGBA32 para normales, sin mipmaps ni compresión. " +
            "El shader anima en GPU usando _Time y soporta GPU Instancing.",
            MessageType.None);

        EditorGUILayout.EndScrollView();
    }

    private void DrawEstimate()
    {
        if (sourceAlembic == null)
            return;

        int frameCount = CalculateFrameCount();
        EditorGUILayout.Space(5f);
        EditorGUILayout.LabelField("Frames estimados", frameCount.ToString());

        AlembicStreamPlayer player = sourceAlembic.GetComponentInChildren<AlembicStreamPlayer>(true);
        if (player != null)
            EditorGUILayout.LabelField("Duración del asset", $"{player.Duration:0.###} s");
    }

    private void TryUseCurrentSelection()
    {
        GameObject selected = Selection.activeObject as GameObject;
        if (selected == null || selected.GetComponentInChildren<AlembicStreamPlayer>(true) == null)
            return;

        sourceAlembic = selected;
        UseSourceDuration();
        Repaint();
    }

    private void UseSourceDuration()
    {
        if (sourceAlembic == null)
            return;

        AlembicStreamPlayer[] players = sourceAlembic.GetComponentsInChildren<AlembicStreamPlayer>(true);
        if (players.Length == 1 && players[0].Duration > 0f)
        {
            sampleStart = 0f;
            sampleEnd = players[0].Duration;
        }
    }

    private int CalculateFrameCount()
    {
        float duration = Mathf.Max(0.01f, sampleEnd - sampleStart);
        return Mathf.Max(2, Mathf.FloorToInt(duration * Mathf.Max(1f, framesPerSecond)) + 1);
    }

    private void Bake()
    {
        if (!ValidateBeforeBake(out string validationError))
        {
            EditorUtility.DisplayDialog("Alembic VAT Baker", validationError, "Cerrar");
            return;
        }

        GameObject preview = null;
        try
        {
            preview = CreatePreviewInstance(sourceAlembic);
            AlembicStreamPlayer[] players = preview.GetComponentsInChildren<AlembicStreamPlayer>(true);
            if (players.Length != 1)
                throw new InvalidOperationException($"Se esperaba exactamente un AlembicStreamPlayer, pero se encontraron {players.Length}.");

            AlembicStreamPlayer player = players[0];
            player.enabled = true;

            float duration = player.Duration;
            if (duration <= 0f)
                throw new InvalidOperationException("El Alembic no informa de una duración válida.");

            float clampedStart = Mathf.Clamp(sampleStart, 0f, duration);
            float clampedEnd = Mathf.Clamp(sampleEnd, clampedStart + 0.0001f, duration);
            int frameCount = Mathf.Max(
                2,
                Mathf.FloorToInt((clampedEnd - clampedStart) * Mathf.Max(1f, framesPerSecond)) + 1);

            player.UpdateImmediately(clampedStart);
            MeshFilter filter = FindSingleAnimatedMesh(preview);
            Mesh sourceMesh = filter.sharedMesh;
            MeshRenderer sourceRenderer = filter.GetComponent<MeshRenderer>();
            if (sourceRenderer == null)
                throw new InvalidOperationException("La malla Alembic no tiene MeshRenderer.");

            int vertexCount = sourceMesh.vertexCount;
            if (vertexCount <= 0)
                throw new InvalidOperationException("La malla Alembic no tiene vértices.");

            int textureWidth = Mathf.NextPowerOfTwo(Mathf.Min(vertexCount, maximumTextureWidth));
            int rowsPerFrame = Mathf.CeilToInt(vertexCount / (float)textureWidth);
            int textureHeight = rowsPerFrame * frameCount;
            int supportedMaxSize = Mathf.Min(SystemInfo.maxTextureSize, 16384);

            if (textureHeight > supportedMaxSize)
            {
                throw new InvalidOperationException(
                    $"La VAT necesitaría una textura {textureWidth}x{textureHeight}, pero el máximo disponible es {supportedMaxSize}. " +
                    "Reduce FPS/duración o aumenta el ancho máximo.");
            }

            long texelCountLong = (long)textureWidth * textureHeight;
            if (texelCountLong > int.MaxValue)
                throw new InvalidOperationException("La textura VAT es demasiado grande para generarla en memoria.");

            int texelCount = (int)texelCountLong;
            var positionPixels = new Color[texelCount];
            Color32[] normalPixels = bakeNormals ? new Color32[texelCount] : null;
            var firstFramePositions = new Vector3[vertexCount];
            var firstFrameNormals = new Vector3[vertexCount];
            var boundsMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var boundsMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            int expectedSubMeshCount = sourceMesh.subMeshCount;
            int[] expectedIndexCounts = Enumerable.Range(0, expectedSubMeshCount)
                .Select(sourceMesh.GetIndexCount)
                .Select(value => checked((int)value))
                .ToArray();

            for (int frame = 0; frame < frameCount; frame++)
            {
                float normalizedFrame = frameCount > 1 ? frame / (float)(frameCount - 1) : 0f;
                float time = Mathf.Lerp(clampedStart, clampedEnd, normalizedFrame);
                EditorUtility.DisplayProgressBar(
                    "Horneando Alembic VAT",
                    $"Muestreando frame {frame + 1}/{frameCount}",
                    frame / (float)frameCount);

                player.UpdateImmediately(time);
                filter = FindSingleAnimatedMesh(preview);
                Mesh sampledMesh = filter.sharedMesh;
                ValidateTopology(sampledMesh, vertexCount, expectedSubMeshCount, expectedIndexCounts, frame);

                Vector3[] positions = sampledMesh.vertices;
                Vector3[] normals = bakeNormals ? sampledMesh.normals : Array.Empty<Vector3>();
                if (bakeNormals && normals.Length != vertexCount)
                    throw new InvalidOperationException($"El frame {frame} no contiene normales para todos los vértices.");

                Matrix4x4 meshToRoot = preview.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Matrix4x4 normalToRoot = meshToRoot.inverse.transpose;
                int frameBaseTexel = frame * rowsPerFrame * textureWidth;

                for (int vertex = 0; vertex < vertexCount; vertex++)
                {
                    Vector3 position = meshToRoot.MultiplyPoint3x4(positions[vertex]);
                    int texel = frameBaseTexel + vertex;
                    positionPixels[texel] = new Color(position.x, position.y, position.z, 1f);

                    boundsMin = Vector3.Min(boundsMin, position);
                    boundsMax = Vector3.Max(boundsMax, position);

                    if (frame == 0)
                        firstFramePositions[vertex] = position;

                    if (bakeNormals)
                    {
                        Vector3 normal = normalToRoot.MultiplyVector(normals[vertex]).normalized;
                        normalPixels[texel] = EncodeNormal(normal);
                        if (frame == 0)
                            firstFrameNormals[vertex] = normal;
                    }
                }
            }

            string safeName = MakeSafeFileName(sourceAlembic.name);
            string treeFolderName = GetTreeFolderName(sourceAlembic.name);
            string clipFolderName = GetClipFolderName(sourceAlembic.name);
            string folder = EnsureAssetFolder($"{outputFolder.TrimEnd('/', '\\')}/{treeFolderName}/{clipFolderName}");
            string positionPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{safeName}_VAT_Positions.asset");
            string normalPath = bakeNormals
                ? AssetDatabase.GenerateUniqueAssetPath($"{folder}/{safeName}_VAT_Normals.asset")
                : null;
            string meshPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{safeName}_VAT_Mesh.asset");

            Texture2D positionTexture = CreatePositionTexture(
                safeName,
                textureWidth,
                textureHeight,
                positionPixels,
                positionPath);
            Texture2D normalTexture = bakeNormals
                ? CreateNormalTexture(safeName, textureWidth, textureHeight, normalPixels, normalPath)
                : null;

            Mesh bakedMesh = Instantiate(sourceMesh);
            bakedMesh.name = safeName + "_VAT_Mesh";
            bakedMesh.vertices = firstFramePositions;
            if (bakeNormals)
                bakedMesh.normals = firstFrameNormals;

            var vatVertexIndices = new List<Vector2>(vertexCount);
            for (int i = 0; i < vertexCount; i++)
                vatVertexIndices.Add(new Vector2(i, 0f));
            bakedMesh.SetUVs(2, vatVertexIndices);

            Vector3 boundsSize = boundsMax - boundsMin;
            Vector3 safetyMargin = Vector3.Max(boundsSize * 0.02f, Vector3.one * 0.01f);
            bakedMesh.bounds = new Bounds(
                (boundsMin + boundsMax) * 0.5f,
                boundsSize + safetyMargin * 2f);
            AssetDatabase.CreateAsset(bakedMesh, meshPath);

            Shader vatShader = Shader.Find(VatShaderName);
            if (vatShader == null)
                throw new InvalidOperationException($"No se encuentra el shader '{VatShaderName}'. Reimporta el proyecto y vuelve a intentarlo.");

            Material[] sourceMaterials = visualMaterialOverride != null
                ? new[] { visualMaterialOverride }
                : sourceRenderer.sharedMaterials;
            int materialCount = Mathf.Max(1, sourceMaterials.Length);
            var generatedMaterials = new Material[materialCount];
            for (int i = 0; i < materialCount; i++)
            {
                Material sourceMaterial = i < sourceMaterials.Length ? sourceMaterials[i] : null;
                generatedMaterials[i] = CreateVatMaterial(
                    sourceMaterial,
                    vatShader,
                    safeName,
                    i,
                    positionTexture,
                    normalTexture,
                    textureWidth,
                    textureHeight,
                    rowsPerFrame,
                    frameCount,
                    framesPerSecond,
                    folder);
            }

            string prefabPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{safeName}_VAT.prefab");
            var generatedRoot = new GameObject(safeName + "_VAT");
            try
            {
                MeshFilter generatedFilter = generatedRoot.AddComponent<MeshFilter>();
                generatedFilter.sharedMesh = bakedMesh;
                MeshRenderer generatedRenderer = generatedRoot.AddComponent<MeshRenderer>();
                generatedRenderer.sharedMaterials = generatedMaterials;
                generatedRenderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
                generatedRenderer.receiveShadows = sourceRenderer.receiveShadows;
                generatedRenderer.lightProbeUsage = sourceRenderer.lightProbeUsage;
                generatedRenderer.reflectionProbeUsage = sourceRenderer.reflectionProbeUsage;
                PrefabUtility.SaveAsPrefabAsset(generatedRoot, prefabPath);
            }
            finally
            {
                DestroyImmediate(generatedRoot);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Object prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);

            double positionMiB = textureWidth * (double)textureHeight * 8d / (1024d * 1024d);
            double normalMiB = bakeNormals ? textureWidth * (double)textureHeight * 4d / (1024d * 1024d) : 0d;
            EditorUtility.DisplayDialog(
                "VAT generado",
                $"Prefab: {prefabPath}\n\n" +
                $"Vértices: {vertexCount:N0}\nFrames: {frameCount}\nTextura: {textureWidth}x{textureHeight}\n" +
                $"Memoria aproximada de texturas: {positionMiB + normalMiB:0.0} MiB",
                "Aceptar");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Error al generar VAT", exception.Message, "Cerrar");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (preview != null)
                DestroyImmediate(preview);
        }
    }

    private bool ValidateBeforeBake(out string error)
    {
        if (sourceAlembic == null)
        {
            error = "Selecciona un asset Alembic.";
            return false;
        }

        int playerCount = sourceAlembic.GetComponentsInChildren<AlembicStreamPlayer>(true).Length;
        if (playerCount != 1)
        {
            error = $"El origen debe contener exactamente un AlembicStreamPlayer. Se encontraron {playerCount}. " +
                    "Arrastra el .abc individual en lugar del prefab de árbol con Slow y Fast.";
            return false;
        }

        if (sampleEnd <= sampleStart)
        {
            error = "El tiempo final debe ser mayor que el inicial.";
            return false;
        }

        if (framesPerSecond < 1f)
        {
            error = "FPS debe ser igual o superior a 1.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(outputFolder) || !outputFolder.Replace('\\', '/').StartsWith("Assets/", StringComparison.Ordinal))
        {
            error = "La carpeta de salida debe estar dentro de Assets/.";
            return false;
        }

        error = null;
        return true;
    }

    private static GameObject CreatePreviewInstance(GameObject source)
    {
        GameObject instance = null;
        if (EditorUtility.IsPersistent(source))
            instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (instance == null)
            instance = Instantiate(source);

        instance.name = source.name + "_VAT_BakePreview";
        instance.hideFlags = HideFlags.HideAndDontSave;
        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        instance.transform.localScale = Vector3.one;
        instance.SetActive(true);
        return instance;
    }

    private static MeshFilter FindSingleAnimatedMesh(GameObject root)
    {
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true)
            .Where(candidate => candidate != null && candidate.sharedMesh != null)
            .ToArray();

        if (filters.Length != 1)
        {
            throw new InvalidOperationException(
                $"Esta versión requiere exactamente una MeshFilter animada. Se encontraron {filters.Length}. " +
                "Si este Alembic contiene varias mallas, hornéalas por separado.");
        }

        return filters[0];
    }

    private static void ValidateTopology(
        Mesh mesh,
        int vertexCount,
        int subMeshCount,
        IReadOnlyList<int> indexCounts,
        int frame)
    {
        if (mesh.vertexCount != vertexCount || mesh.subMeshCount != subMeshCount)
        {
            throw new InvalidOperationException(
                $"La topología cambia en el frame {frame}. VAT Soft requiere el mismo número de vértices y submeshes en todos los frames.");
        }

        for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
        {
            if (mesh.GetIndexCount(subMesh) != (uint)indexCounts[subMesh])
            {
                throw new InvalidOperationException(
                    $"La cantidad de índices cambia en el frame {frame}, submesh {subMesh}. La topología no es compatible con esta VAT.");
            }
        }
    }

    private static Texture2D CreatePositionTexture(
        string safeName,
        int width,
        int height,
        Color[] pixels,
        string path)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBAHalf, false, true)
        {
            name = safeName + "_VAT_Positions",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            anisoLevel = 0
        };
        texture.SetPixels(pixels);
        texture.Apply(false, false);
        AssetDatabase.CreateAsset(texture, path);
        return texture;
    }

    private static Texture2D CreateNormalTexture(
        string safeName,
        int width,
        int height,
        Color32[] pixels,
        string path)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
        {
            name = safeName + "_VAT_Normals",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            anisoLevel = 0
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        AssetDatabase.CreateAsset(texture, path);
        return texture;
    }

    private Material CreateVatMaterial(
        Material source,
        Shader shader,
        string safeName,
        int materialIndex,
        Texture2D positionTexture,
        Texture2D normalTexture,
        int textureWidth,
        int textureHeight,
        int rowsPerFrame,
        int frameCount,
        float fps,
        string folder)
    {
        var material = new Material(shader)
        {
            name = safeName + (materialIndex == 0 ? "_VAT_Material" : $"_VAT_Material_{materialIndex}"),
            enableInstancing = true
        };

        CopyVisualProperties(source, material);
        material.SetTexture("_VAT_PositionTex", positionTexture);
        material.SetTexture("_VAT_NormalTex", normalTexture != null ? normalTexture : Texture2D.grayTexture);
        material.SetFloat("_VAT_TextureWidth", textureWidth);
        material.SetFloat("_VAT_TextureHeight", textureHeight);
        material.SetFloat("_VAT_RowsPerFrame", rowsPerFrame);
        material.SetFloat("_VAT_FrameCount", frameCount);
        material.SetFloat("_VAT_FPS", fps);
        material.SetFloat("_VAT_PlaybackSpeed", playbackSpeed);
        material.SetFloat("_VAT_TimeOffset", 0f);
        material.SetFloat("_VAT_RandomPhase", randomPhase);
        material.SetFloat("_VAT_Interpolate", interpolateFrames ? 1f : 0f);
        material.SetFloat("_VAT_HasNormals", normalTexture != null ? 1f : 0f);
        material.SetShaderPassEnabled("DepthNormals", enableDepthNormalsPass);

        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{material.name}.mat");
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void CopyVisualProperties(Material source, Material destination)
    {
        if (source == null)
            return;

        string textureProperty = source.HasProperty("_BaseMap")
            ? "_BaseMap"
            : source.HasProperty("_MainTex") ? "_MainTex" : null;
        if (textureProperty != null)
        {
            destination.SetTexture("_BaseMap", source.GetTexture(textureProperty));
            destination.SetTextureScale("_BaseMap", source.GetTextureScale(textureProperty));
            destination.SetTextureOffset("_BaseMap", source.GetTextureOffset(textureProperty));
        }

        string colorProperty = source.HasProperty("_BaseColor")
            ? "_BaseColor"
            : source.HasProperty("_Color") ? "_Color" : null;
        if (colorProperty != null)
            destination.SetColor("_BaseColor", source.GetColor(colorProperty));

        float cutoff = source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : 0.5f;
        bool alphaClip = source.HasProperty("_AlphaClip") && source.GetFloat("_AlphaClip") > 0.5f;
        destination.SetFloat("_Cutoff", cutoff);
        destination.SetFloat("_AlphaClip", alphaClip ? 1f : 0f);
        if (source.HasProperty("_Cull"))
            destination.SetFloat("_Cull", source.GetFloat("_Cull"));
        destination.renderQueue = alphaClip ? (int)UnityEngine.Rendering.RenderQueue.AlphaTest : -1;
    }

    private static Color32 EncodeNormal(Vector3 normal)
    {
        normal = normal.normalized * 0.5f + Vector3.one * 0.5f;
        return new Color32(
            FloatToByte(normal.x),
            FloatToByte(normal.y),
            FloatToByte(normal.z),
            255);
    }

    private static byte FloatToByte(float value)
    {
        return (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
    }

    private static string EnsureAssetFolder(string requestedFolder)
    {
        string normalized = requestedFolder.Replace('\\', '/').TrimEnd('/');
        if (string.IsNullOrEmpty(normalized) || !normalized.StartsWith("Assets/", StringComparison.Ordinal))
            throw new InvalidOperationException("La carpeta de salida debe estar dentro de Assets/.");

        string[] parts = normalized.Split('/');
        string current = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }

        return current;
    }

    private static string MakeSafeFileName(string value)
    {
        string invalidCharacters = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
        string safe = Regex.Replace(value, $"[{invalidCharacters}]", "_");
        return string.IsNullOrWhiteSpace(safe) ? "Alembic" : safe;
    }

    private static string GetTreeFolderName(string sourceName)
    {
        string withoutClip = Regex.Replace(
            sourceName,
            @"[\s_\-]*ANIM[\s_\-]*(slow|fast|weak|strong)([\s_\-]*V\d+)?$",
            string.Empty,
            RegexOptions.IgnoreCase);
        withoutClip = Regex.Replace(withoutClip, @"[\s_\-]+$", string.Empty);
        if (string.IsNullOrWhiteSpace(withoutClip))
            withoutClip = sourceName;

        string safe = MakeSafeFileName(withoutClip).Replace(' ', '_').Replace('-', '_');
        string[] words = safe.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i].ToLowerInvariant();
            words[i] = char.ToUpperInvariant(word[0]) + word.Substring(1);
        }
        return words.Length > 0 ? string.Join("_", words) : "Tree";
    }

    private static string GetClipFolderName(string sourceName)
    {
        if (Regex.IsMatch(sourceName, @"(^|[\s_\-])(fast|strong)([\s_\-]|$)", RegexOptions.IgnoreCase))
            return "Fast";
        if (Regex.IsMatch(sourceName, @"(^|[\s_\-])(slow|weak)([\s_\-]|$)", RegexOptions.IgnoreCase))
            return "Slow";
        return "Clip";
    }
}
