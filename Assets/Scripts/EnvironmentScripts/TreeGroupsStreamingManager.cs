using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestiona desde un unico componente los grupos de arboles de LightingScene:
/// - Activa secuencialmente los arboles de la iglesia al entrar en su volumen.
/// - Mantiene activos solo los grupos cercanos del camino.
/// - Fuerza el apagado secuencial del camino al entrar en el segundo volumen.
/// </summary>
[DisallowMultipleComponent]
public sealed class TreeGroupsStreamingManager : MonoBehaviour
{
    [Header("Player")]
    [Tooltip("Automatically found through PlayerController on startup.")]
    [SerializeField] private Transform player;

    [Header("Trigger Volumes")]
    [Tooltip("When entered, activates the church groups in list order.")]
    [SerializeField] private Collider churchActivationTrigger;

    [Tooltip("When entered, sequentially deactivates and locks all path groups.")]
    [SerializeField] private Collider pathForceDeactivateTrigger;

    [Header("Church Tree Groups")]
    [SerializeField] private List<GameObject> churchTreeGroups = new List<GameObject>();

    [Tooltip("Delay between activating one group and the next.")]
    [SerializeField, Min(0f)] private float churchActivationInterval = 0.15f;

    [Header("Path Tree Groups")]
    [SerializeField] private List<GameObject> pathTreeGroups = new List<GameObject>();

    [Tooltip("An inactive group is activated when it enters this distance.")]
    [SerializeField, Min(0f)] private float pathActivationDistance = 60f;

    [Tooltip("An active group is deactivated after exceeding this distance. It must be greater than the activation distance.")]
    [SerializeField, Min(0f)] private float pathDeactivationDistance = 75f;

    [Tooltip("Minimum delay between two path group state changes.")]
    [SerializeField, Min(0f)] private float pathChangeInterval = 0.15f;

    [Tooltip("How often distances are recalculated.")]
    [SerializeField, Min(0.02f)] private float distanceCheckInterval = 0.2f;

    [Header("Initialization")]
    [Tooltip("Deactivates every group in Awake, before the first frame, then activates only the required groups.")]
    [SerializeField] private bool initializeAllGroupsInactive = true;

    [Tooltip("Keeps sequences running while Time.timeScale is 0.")]
    [SerializeField] private bool useUnscaledTime;

    private Coroutine churchActivationCoroutine;
    private Coroutine pathForcedDeactivationCoroutine;
    private bool churchActivationStarted;
    private bool pathForcedInactive;
    private bool wasInsideChurchActivationTrigger;
    private bool wasInsidePathForceTrigger;
    private float nextPlayerSearchTime;
    private float nextDistanceCheckTime;
    private float nextPathChangeTime;

    public bool PathForcedInactive => pathForcedInactive;

    private float CurrentTime => useUnscaledTime ? Time.unscaledTime : Time.time;

    private void Awake()
    {
        if (!initializeAllGroupsInactive)
            return;

        SetAllInactive(churchTreeGroups);
        SetAllInactive(pathTreeGroups);
    }

    private void Start()
    {
        FindPlayerIfNeeded();
        EvaluateTriggerVolumes();
        UpdatePathGroupsByDistance();
    }

    private void Update()
    {
        if (!FindPlayerIfNeeded())
            return;

        EvaluateTriggerVolumes();

        if (pathForcedInactive || pathForcedDeactivationCoroutine != null)
            return;

        float now = CurrentTime;
        if (now < nextDistanceCheckTime || now < nextPathChangeTime)
            return;

        nextDistanceCheckTime = now + distanceCheckInterval;

        if (UpdatePathGroupsByDistance())
            nextPathChangeTime = now + pathChangeInterval;
    }

    private bool FindPlayerIfNeeded()
    {
        if (player != null)
            return true;

        float now = CurrentTime;
        if (now < nextPlayerSearchTime)
            return false;

        nextPlayerSearchTime = now + 0.5f;

        PlayerController playerController = Object.FindFirstObjectByType<PlayerController>();
        if (playerController != null)
            player = playerController.transform;

        return player != null;
    }

    private void EvaluateTriggerVolumes()
    {
        if (player == null)
            return;

        bool insideChurchTrigger = IsPointInsideCollider(churchActivationTrigger, player.position);
        bool insidePathForceTrigger = IsPointInsideCollider(pathForceDeactivateTrigger, player.position);

        // Reaching the church interior also guarantees that its trees are active.
        // This covers direct starts/teleports that never cross the approach volume.
        bool shouldActivateChurch = insideChurchTrigger || insidePathForceTrigger;

        if (shouldActivateChurch && !wasInsideChurchActivationTrigger && !churchActivationStarted)
        {
            churchActivationStarted = true;
            churchActivationCoroutine = StartCoroutine(ActivateChurchGroupsSequentially());
        }

        if (insidePathForceTrigger && !wasInsidePathForceTrigger && !pathForcedInactive)
        {
            pathForcedInactive = true;
            pathForcedDeactivationCoroutine = StartCoroutine(DeactivatePathGroupsSequentially());
        }

        wasInsideChurchActivationTrigger = shouldActivateChurch;
        wasInsidePathForceTrigger = insidePathForceTrigger;
    }

    private IEnumerator ActivateChurchGroupsSequentially()
    {
        bool hasChangedGroup = false;

        for (int i = 0; i < churchTreeGroups.Count; i++)
        {
            GameObject group = churchTreeGroups[i];
            if (group == null || group.activeSelf)
                continue;

            if (hasChangedGroup && churchActivationInterval > 0f)
                yield return WaitForInterval(churchActivationInterval);

            group.SetActive(true);
            hasChangedGroup = true;
        }

        churchActivationCoroutine = null;
    }

    private IEnumerator DeactivatePathGroupsSequentially()
    {
        bool hasChangedGroup = false;

        for (int i = 0; i < pathTreeGroups.Count; i++)
        {
            GameObject group = pathTreeGroups[i];
            if (group == null || !group.activeSelf)
                continue;

            if (hasChangedGroup && pathChangeInterval > 0f)
                yield return WaitForInterval(pathChangeInterval);

            group.SetActive(false);
            hasChangedGroup = true;
        }

        pathForcedDeactivationCoroutine = null;
    }

    /// <summary>
    /// Realiza como maximo un cambio. Los grupos lejanos se apagan antes y los
    /// cercanos se encienden empezando por el mas proximo.
    /// </summary>
    private bool UpdatePathGroupsByDistance()
    {
        if (player == null || pathForcedInactive)
            return false;

        GameObject farthestActiveGroup = null;
        float farthestActiveDistance = pathDeactivationDistance;
        GameObject nearestInactiveGroup = null;
        float nearestInactiveDistance = float.PositiveInfinity;

        for (int i = 0; i < pathTreeGroups.Count; i++)
        {
            GameObject group = pathTreeGroups[i];
            if (group == null)
                continue;

            float distance = Vector3.Distance(player.position, group.transform.position);

            if (group.activeSelf)
            {
                if (distance > farthestActiveDistance)
                {
                    farthestActiveDistance = distance;
                    farthestActiveGroup = group;
                }
            }
            else if (distance <= pathActivationDistance && distance < nearestInactiveDistance)
            {
                nearestInactiveDistance = distance;
                nearestInactiveGroup = group;
            }
        }

        if (farthestActiveGroup != null)
        {
            farthestActiveGroup.SetActive(false);
            return true;
        }

        if (nearestInactiveGroup != null)
        {
            nearestInactiveGroup.SetActive(true);
            return true;
        }

        return false;
    }

    private object WaitForInterval(float seconds)
    {
        return useUnscaledTime
            ? (object)new WaitForSecondsRealtime(seconds)
            : new WaitForSeconds(seconds);
    }

    private static bool IsPointInsideCollider(Collider volume, Vector3 point)
    {
        if (volume == null || !volume.enabled || !volume.gameObject.activeInHierarchy)
            return false;

        Vector3 closestPoint = volume.ClosestPoint(point);
        return (closestPoint - point).sqrMagnitude <= 0.0001f;
    }

    private static void SetAllInactive(List<GameObject> groups)
    {
        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i] != null)
                groups[i].SetActive(false);
        }
    }

    private void OnValidate()
    {
        if (pathDeactivationDistance < pathActivationDistance)
            pathDeactivationDistance = pathActivationDistance;
    }
}
