using HoloKit;
using UnityEngine;

public class GazeInteractor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera arCamera;

    [Tooltip("Reticul-ul de gaze. Dacă e null, se creează automat un GameObject cu GazeReticle.")]
    [SerializeField] private GazeReticle reticle;

    [Header("Gaze Settings")]
    [SerializeField] private float gazeDistance = 10f;
    [SerializeField] private float gazeCompleteTime = 1.5f;
    [SerializeField] private LayerMask interactableLayers;

    private HoloKitCameraManager holoKitCamera;
    private Transform centerEyePose;

    private IGazeTarget currentTarget;
    private GameObject currentObject;
    private float gazeTimer;

    private void Start()
    {
        holoKitCamera = FindFirstObjectByType<HoloKitCameraManager>();
        if (holoKitCamera != null)
            centerEyePose = holoKitCamera.CenterEyePose;
        else
            Debug.LogWarning("[GazeInteractor] HoloKitCameraManager negăsit — fallback la arCamera.");

        // Cream reticulul automat daca nu e setat in Inspector
        if (reticle == null)
        {
            var go = new GameObject("GazeReticle");
            reticle = go.AddComponent<GazeReticle>();
            Debug.Log("[GazeInteractor] GazeReticle creat automat.");
        }
    }

    private void Update()
    {
        // Mono: ray din camera telefonului (comportament original)
        // Stereo: ray din CenterEyePose, mijlocul ochilor, compensat pentru decalajul HoloKit
        bool isStereo = holoKitCamera != null
                        && holoKitCamera.ScreenRenderMode == ScreenRenderMode.Stereo
                        && centerEyePose != null;

        Transform pose = isStereo ? centerEyePose : arCamera.transform;
        Ray ray = new Ray(pose.position, pose.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, gazeDistance, interactableLayers))
        {
            GameObject hitObject = hit.collider.gameObject;
            IGazeTarget target = hitObject.GetComponentInParent<IGazeTarget>();

            if (target != null)
            {
                HandleTarget(target, hitObject, hit.point, pose.position);
                return;
            }
        }

        ClearTarget();
    }

    private void HandleTarget(IGazeTarget target, GameObject hitObject,
                               Vector3 hitPoint, Vector3 eyePosition)
    {
        if (currentObject != hitObject)
        {
            ClearTarget();

            currentObject = hitObject;
            currentTarget = target;
            gazeTimer = 0f;

            currentTarget.OnGazeEnter();
        }

        gazeTimer += Time.deltaTime;

        float progress = Mathf.Clamp01(gazeTimer / gazeCompleteTime);
        currentTarget.OnGazeStay(progress);

        // Actualizam reticulul cu pozitia si progresul curent
        reticle?.ShowAt(hitPoint, eyePosition, progress);

        if (gazeTimer >= gazeCompleteTime)
        {
            currentTarget.OnGazeComplete();
            ClearTarget();
        }
    }

    private void ClearTarget()
    {
        if (currentTarget != null)
            currentTarget.OnGazeExit();

        reticle?.Hide();

        currentTarget = null;
        currentObject = null;
        gazeTimer = 0f;
    }
}
