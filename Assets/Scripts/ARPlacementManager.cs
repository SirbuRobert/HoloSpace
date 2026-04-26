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

    private Pose currentPlacementPose;
    private GameObject placedObject;

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

    private void PlaceObject()
    {
        placedObject = Instantiate(
            objectToPlacePrefab,
            currentPlacementPose.position,
            currentPlacementPose.rotation
        );

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