using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DialogueLayoutManager : MonoBehaviour
{
    [Serializable]
    private struct TransformPose
    {
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Quaternion localRotation;
        [SerializeField] private Vector3 localScale;

        public static TransformPose Capture(Transform target)
        {
            return new TransformPose
            {
                localPosition = target.localPosition,
                localRotation = target.localRotation,
                localScale = target.localScale
            };
        }

        public void ApplyTo(Transform target)
        {
            target.SetLocalPositionAndRotation(localPosition, localRotation);
            target.localScale = localScale;
        }
    }

    [Serializable]
    private sealed class LayoutEntry
    {
        [SerializeField] private string label;
        [SerializeField] private Transform target;
        [SerializeField] private TransformPose gameplayPose;
        [SerializeField] private TransformPose dialoguePose;

        public Transform Target => target;

        public void ApplyGameplayPose()
        {
            if (target != null)
                gameplayPose.ApplyTo(target);
        }

        public void ApplyDialoguePose()
        {
            if (target != null)
                dialoguePose.ApplyTo(target);
        }

        public void CaptureGameplayPose()
        {
            if (target != null)
                gameplayPose = TransformPose.Capture(target);
        }

        public void CaptureDialoguePose()
        {
            if (target != null)
                dialoguePose = TransformPose.Capture(target);
        }
    }

    [Header("Dialogue Lighting")]
    [Tooltip("Luz auxiliar de LightingScene que se activa al entrar en la iglesia.")]
    [SerializeField] private GameObject auxiliaryLight;

    [Header("Dialogue Layout")]
    [Tooltip("Objetos de LightingScene que tienen una pose diferente durante el diálogo.")]
    [SerializeField] private List<LayoutEntry> entries = new List<LayoutEntry>();

    private bool _auxiliaryLightWasActive;
    private bool _hasCapturedAuxiliaryLightState;

    public int EntryCount => entries.Count;

    public void EnterChurchLighting()
    {
        if (auxiliaryLight == null)
        {
            Debug.LogWarning(
                $"[{nameof(DialogueLayoutManager)}] Auxiliar light no está asignada.",
                this);
            return;
        }

        if (!_hasCapturedAuxiliaryLightState)
        {
            _auxiliaryLightWasActive = auxiliaryLight.activeSelf;
            _hasCapturedAuxiliaryLightState = true;
        }

        auxiliaryLight.SetActive(true);
    }

    public void RestoreChurchLighting()
    {
        if (!_hasCapturedAuxiliaryLightState)
            return;

        if (auxiliaryLight != null)
            auxiliaryLight.SetActive(_auxiliaryLightWasActive);

        _hasCapturedAuxiliaryLightState = false;
    }

    [ContextMenu("Layout/Aplicar Gameplay")]
    public void ApplyGameplayLayout()
    {
        ApplyLayout(useDialoguePose: false);
    }

    [ContextMenu("Layout/Aplicar Dialogue")]
    public void ApplyDialogueLayout()
    {
        ApplyLayout(useDialoguePose: true);
    }

    [ContextMenu("Layout/Capturar actual como Gameplay")]
    public void CaptureCurrentAsGameplayLayout()
    {
        CaptureLayout(captureDialoguePose: false);
    }

    [ContextMenu("Layout/Capturar actual como Dialogue")]
    public void CaptureCurrentAsDialogueLayout()
    {
        CaptureLayout(captureDialoguePose: true);
    }

    private void ApplyLayout(bool useDialoguePose)
    {
        RecordTargetTransformsForUndo(
            useDialoguePose ? "Aplicar layout Dialogue" : "Aplicar layout Gameplay");

        foreach (LayoutEntry entry in entries)
        {
            if (entry == null)
                continue;

            if (useDialoguePose)
                entry.ApplyDialoguePose();
            else
                entry.ApplyGameplayPose();
        }

        Physics.SyncTransforms();
        MarkSceneDirtyInEditMode();
    }

    private void CaptureLayout(bool captureDialoguePose)
    {
        RecordManagerForUndo(
            captureDialoguePose ? "Capturar layout Dialogue" : "Capturar layout Gameplay");

        foreach (LayoutEntry entry in entries)
        {
            if (entry == null)
                continue;

            if (captureDialoguePose)
                entry.CaptureDialoguePose();
            else
                entry.CaptureGameplayPose();
        }
        MarkManagerDirtyInEditMode();
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void RecordTargetTransformsForUndo(string operationName)
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
            return;

        List<UnityEngine.Object> targets = new List<UnityEngine.Object>(entries.Count);
        foreach (LayoutEntry entry in entries)
        {
            if (entry != null && entry.Target != null)
                targets.Add(entry.Target);
        }

        if (targets.Count > 0)
            UnityEditor.Undo.RecordObjects(targets.ToArray(), operationName);
#endif
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void RecordManagerForUndo(string operationName)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.Undo.RecordObject(this, operationName);
#endif
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void MarkManagerDirtyInEditMode()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
            return;

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void MarkSceneDirtyInEditMode()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }
}
