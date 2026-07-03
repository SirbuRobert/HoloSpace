using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARPlacementManager : MonoBehaviour
{
    public static GameObject PlacedObject { get; private set; }

    [Header("AR References")]
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private ARPlaneManager planeManager;

    [Header("Placement")]
    [SerializeField] private GameObject objectToPlacePrefab;
    [SerializeField] private GameObject placementReticle;
    [SerializeField] private float gazeTimeToPlace = 1.5f;

    private static readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();

    private float gazeTimer = 0f;
    private bool canPlace = false;
    private bool hasPlacedObject = false;

    private Pose        currentPlacementPose;
    private GameObject  placedObject;

    // Obiect deja creat (GLB loaded) care va fi plasat pe suprafata in loc de prefab
    private GameObject  pendingExternalObject;

    private void Start()
    {
        if (placementReticle != null)
            placementReticle.SetActive(false);
    }

    private void Update()
    {
        if (hasPlacedObject)
            return;

        UpdatePlacementPose();
        UpdateGazePlacement();
    }

    private void UpdatePlacementPose()
    {
        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);

        bool hitPlane = raycastManager.Raycast(
            screenCenter,
            hits,
            TrackableType.PlaneWithinPolygon
        );

        canPlace = hitPlane;

        if (hitPlane)
        {
            currentPlacementPose = hits[0].pose;

            placementReticle.SetActive(true);
            placementReticle.transform.SetPositionAndRotation(
                currentPlacementPose.position,
                currentPlacementPose.rotation
            );
        }
        else
        {
            placementReticle.SetActive(false);
            gazeTimer = 0f;
        }
    }

    private void UpdateGazePlacement()
    {
        if (!canPlace)
            return;

        gazeTimer += Time.deltaTime;

        float progress = Mathf.Clamp01(gazeTimer / gazeTimeToPlace);

        float scale = Mathf.Lerp(0.06f, 0.11f, progress);
        placementReticle.transform.localScale = new Vector3(scale, scale, scale);

        if (gazeTimer >= gazeTimeToPlace)
        {
            PlaceObject();
        }
    }

    /// <summary>
    /// Distruge obiectul plasat curent (solar system) si intra in placement mode
    /// pentru un obiect deja creat la runtime (GLB importat).
    /// Acelasi flow ca plasarea initiala: utilizatorul dwell-gazeaza pe o suprafata.
    /// </summary>
    public void ResetForObject(GameObject externalObject)
    {
        // Distruge ce e plasat acum (solar system sau alt model)
        if (placedObject != null)
        {
            Destroy(placedObject);
            placedObject = null;
            PlacedObject = null;
        }

        // Distruge un model pending anterior (daca userul incarca al 2-lea inainte sa plaseze primul)
        if (pendingExternalObject != null)
        {
            Destroy(pendingExternalObject);
            pendingExternalObject = null;
        }

        // Ascundem noul obiect pana cand utilizatorul alege pozitia
        externalObject.SetActive(false);
        pendingExternalObject = externalObject;

        // Reactivam plane detection
        if (planeManager != null)
        {
            planeManager.enabled = true;
            foreach (var plane in planeManager.trackables)
                plane.gameObject.SetActive(true);
        }

        // Reset state: Update() reia placement flow-ul
        hasPlacedObject = false;
        gazeTimer       = 0f;
        canPlace        = false;

        if (placementReticle != null)
            placementReticle.SetActive(false);

        Debug.Log($"[ARPlacementManager] Reset pentru '{externalObject.name}' — privește o suprafață.");
    }

    private void PlaceObject()
    {
        if (pendingExternalObject != null)
        {
            // Plasam obiectul extern (GLB) pe suprafata aleasa
            pendingExternalObject.SetActive(true);
            pendingExternalObject.transform.SetPositionAndRotation(
                currentPlacementPose.position,
                currentPlacementPose.rotation
            );
            placedObject          = pendingExternalObject;
            pendingExternalObject = null;
        }
        else
        {
            // Comportament original: instantiem prefab-ul (solar system)
            placedObject = Instantiate(
                objectToPlacePrefab,
                currentPlacementPose.position,
                currentPlacementPose.rotation
            );
        }

        PlacedObject = placedObject;

        hasPlacedObject = true;
        placementReticle.SetActive(false);

        DisablePlaneDetection();
    }

    private void DisablePlaneDetection()
    {
        if (planeManager == null)
            return;

        planeManager.enabled = false;

        foreach (ARPlane plane in planeManager.trackables)
        {
            plane.gameObject.SetActive(false);
        }
    }
}