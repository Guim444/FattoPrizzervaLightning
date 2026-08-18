using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public sealed class DualVatAssemblerWindow : EditorWindow
{
    private const string VatShaderName = "FattoPrizzerva/Vegetation VAT/Dual Wind URP Lit";
    private const string DefaultOutputFolder = "Assets/Prefabs/Generated/VAT";
    private const string LegacyOutputFolder = "Assets/Generated/VAT";

    [SerializeField] private GameObject weakWindVatPrefab;
    [SerializeField] private GameObject strongWindVatPrefab;
    [SerializeField] private string outputFolder = DefaultOutputFolder;
    [SerializeField] private bool enableDepthNormalsPass = true;

    [MenuItem("Tools/Fatto Prizzerva/Vegetation VAT/2. Assemble Dual Wind")]
    private static void Open()
    {
        var window = GetWindow<DualVatAssemblerWindow>("Vegetation Dual Wind VAT");
        window.minSize = new Vector2(440f, 300f);
    }

    private void OnEnable()
    {
        if (string.IsNullOrWhiteSpace(outputFolder)
            || string.Equals(outputFolder, LegacyOutputFolder, StringComparison.Ordinal))
        {
            outputFolder = DefaultOutputFolder;
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Combinar viento débil + fuerte de vegetación", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Primero genera las VAT slow y fast con Vegetation VAT Baker. Después asigna aquí los dos prefabs. " +
            "Deben pertenecer a la misma vegetación y compartir exactamente topología, UV y submeshes.",
            MessageType.Info);

        weakWindVatPrefab = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("VAT viento débil", "Prefab VAT generado desde el Alembic slow."),
            weakWindVatPrefab,
            typeof(GameObject),
            false);
        strongWindVatPrefab = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("VAT viento fuerte", "Prefab VAT generado desde el Alembic fast."),
            strongWindVatPrefab,
            typeof(GameObject),
            false);
        enableDepthNormalsPass = EditorGUILayout.Toggle(
            new GUIContent(
                "Depth Normals (SSAO/Fog)",
                "Hace que el VAT escriba la profundidad y normales que utilizan SSAO y la niebla volumétrica."),
            enableDepthNormalsPass);
        outputFolder = EditorGUILayout.TextField("Carpeta de salida", outputFolder);

        EditorGUILayout.Space(12f);
        using (new EditorGUI.DisabledScope(
                   weakWindVatPrefab == null
                   || strongWindVatPrefab == null
                   || EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Generar prefab VAT dual", GUILayout.Height(38f)))
                Assemble();
        }

        EditorGUILayout.HelpBox(
            "El prefab resultante usa una sola malla y un solo renderer. WindStateManager controla " +
            "la mezcla global _VAT_WindBlend: 0 = débil, 1 = fuerte.",
            MessageType.None);
    }

    private void Assemble()
    {
        try
        {
            if (!TryGetVatParts(weakWindVatPrefab, "débil", out Mesh weakMesh, out MeshRenderer weakRenderer, out Material weakMaterial))
                return;
            if (!TryGetVatParts(strongWindVatPrefab, "fuerte", out Mesh strongMesh, out _, out Material strongMaterial))
                return;

            ValidateTopology(weakMesh, strongMesh);
            ValidateVatMaterial(weakMaterial, "débil");
            ValidateVatMaterial(strongMaterial, "fuerte");

            string treeFolderName = GetTreeFolderName(weakWindVatPrefab.name);
            string folder = EnsureAssetFolder($"{outputFolder.TrimEnd('/', '\\')}/{treeFolderName}/Dual");
            string safeName = treeFolderName;

            Mesh dualMesh = Instantiate(weakMesh);
            dualMesh.name = safeName + "_DualVAT_Mesh";
            Bounds combinedBounds = weakMesh.bounds;
            combinedBounds.Encapsulate(strongMesh.bounds.min);
            combinedBounds.Encapsulate(strongMesh.bounds.max);
            dualMesh.bounds = combinedBounds;
            string meshPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{dualMesh.name}.asset");
            AssetDatabase.CreateAsset(dualMesh, meshPath);

            var dualMaterial = new Material(weakMaterial)
            {
                name = safeName + "_DualVAT_Material",
                enableInstancing = true
            };
            dualMaterial.SetTexture("_VAT_FastPositionTex", strongMaterial.GetTexture("_VAT_PositionTex"));
            dualMaterial.SetTexture("_VAT_FastNormalTex", strongMaterial.GetTexture("_VAT_NormalTex"));
            CopyFloat(strongMaterial, dualMaterial, "_VAT_TextureWidth", "_VAT_FastTextureWidth");
            CopyFloat(strongMaterial, dualMaterial, "_VAT_TextureHeight", "_VAT_FastTextureHeight");
            CopyFloat(strongMaterial, dualMaterial, "_VAT_RowsPerFrame", "_VAT_FastRowsPerFrame");
            CopyFloat(strongMaterial, dualMaterial, "_VAT_FrameCount", "_VAT_FastFrameCount");
            CopyFloat(strongMaterial, dualMaterial, "_VAT_FPS", "_VAT_FastFPS");
            CopyFloat(strongMaterial, dualMaterial, "_VAT_HasNormals", "_VAT_FastHasNormals");
            dualMaterial.SetFloat("_VAT_DualMode", 1f);
            dualMaterial.SetShaderPassEnabled("DepthNormals", enableDepthNormalsPass);
            string materialPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{dualMaterial.name}.mat");
            AssetDatabase.CreateAsset(dualMaterial, materialPath);

            var root = new GameObject(safeName + "_DualVAT");
            string prefabPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{root.name}.prefab");
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh = dualMesh;
                MeshRenderer renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = dualMaterial;
                renderer.shadowCastingMode = weakRenderer.shadowCastingMode;
                renderer.receiveShadows = weakRenderer.receiveShadows;
                renderer.lightProbeUsage = weakRenderer.lightProbeUsage;
                renderer.reflectionProbeUsage = weakRenderer.reflectionProbeUsage;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            EditorUtility.DisplayDialog(
                "VAT dual generada",
                $"Prefab: {prefabPath}\n\n0 = viento débil\n1 = viento fuerte",
                "Aceptar");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Error al combinar VAT", exception.Message, "Cerrar");
        }
    }

    private static bool TryGetVatParts(
        GameObject prefab,
        string label,
        out Mesh mesh,
        out MeshRenderer renderer,
        out Material material)
    {
        mesh = null;
        renderer = null;
        material = null;

        MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
        MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
        if (filters.Length != 1 || renderers.Length != 1 || filters[0].sharedMesh == null)
        {
            EditorUtility.DisplayDialog(
                "Vegetation Dual Wind VAT",
                $"El prefab {label} debe contener exactamente una MeshFilter y un MeshRenderer.",
                "Cerrar");
            return false;
        }

        mesh = filters[0].sharedMesh;
        renderer = renderers[0];
        material = renderer.sharedMaterial;
        if (material == null)
        {
            EditorUtility.DisplayDialog("Vegetation Dual Wind VAT", $"El prefab {label} no tiene material.", "Cerrar");
            return false;
        }

        return true;
    }

    private static void ValidateVatMaterial(Material material, string label)
    {
        if (material.shader == null || material.shader.name != VatShaderName)
            throw new InvalidOperationException($"El material {label} no utiliza el shader VAT de Fatto Prizzerva.");

        string[] requiredProperties =
        {
            "_VAT_PositionTex",
            "_VAT_TextureWidth",
            "_VAT_TextureHeight",
            "_VAT_RowsPerFrame",
            "_VAT_FrameCount",
            "_VAT_FPS"
        };

        foreach (string property in requiredProperties)
        {
            if (!material.HasProperty(property))
                throw new InvalidOperationException($"Al material {label} le falta la propiedad {property}.");
        }

        if (material.GetTexture("_VAT_PositionTex") == null)
            throw new InvalidOperationException($"El material {label} no tiene textura de posiciones VAT.");
    }

    private static void ValidateTopology(Mesh weakMesh, Mesh strongMesh)
    {
        if (weakMesh.vertexCount != strongMesh.vertexCount || weakMesh.subMeshCount != strongMesh.subMeshCount)
        {
            throw new InvalidOperationException(
                "Las VAT slow y fast no tienen la misma cantidad de vértices/submeshes. No se pueden mezclar.");
        }

        for (int subMesh = 0; subMesh < weakMesh.subMeshCount; subMesh++)
        {
            int[] weakIndices = weakMesh.GetIndices(subMesh);
            int[] strongIndices = strongMesh.GetIndices(subMesh);
            if (weakIndices.Length != strongIndices.Length)
                throw new InvalidOperationException($"El submesh {subMesh} no comparte topología.");

            for (int i = 0; i < weakIndices.Length; i++)
            {
                if (weakIndices[i] != strongIndices[i])
                {
                    throw new InvalidOperationException(
                        $"El orden de índices difiere en el submesh {subMesh}. " +
                        "Las exportaciones Alembic deben conservar el mismo orden de vértices.");
                }
            }
        }
    }

    private static void CopyFloat(Material source, Material destination, string sourceProperty, string destinationProperty)
    {
        destination.SetFloat(destinationProperty, source.GetFloat(sourceProperty));
    }

    private static string EnsureAssetFolder(string requestedFolder)
    {
        string normalized = requestedFolder.Replace('\\', '/').TrimEnd('/');
        if (string.IsNullOrWhiteSpace(normalized) || !normalized.StartsWith("Assets/", StringComparison.Ordinal))
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
        return string.IsNullOrWhiteSpace(safe) ? "Tree" : safe;
    }

    private static string GetTreeFolderName(string sourceName)
    {
        string cleanName = Regex.Replace(sourceName, @"[\s_\-]*VAT$", string.Empty, RegexOptions.IgnoreCase);
        cleanName = Regex.Replace(cleanName, @"[\s_\-]*Dual$", string.Empty, RegexOptions.IgnoreCase);
        cleanName = Regex.Replace(
            cleanName,
            @"[\s_\-]*ANIM[\s_\-]*(slow|fast|weak|strong)([\s_\-]*V\d+)?$",
            string.Empty,
            RegexOptions.IgnoreCase);
        cleanName = Regex.Replace(cleanName, @"[\s_\-]+$", string.Empty);

        string safe = MakeSafeFileName(cleanName).Replace(' ', '_').Replace('-', '_');
        string[] words = safe.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i].ToLowerInvariant();
            words[i] = char.ToUpperInvariant(word[0]) + word.Substring(1);
        }
        return words.Length > 0 ? string.Join("_", words) : "Tree";
    }
}
