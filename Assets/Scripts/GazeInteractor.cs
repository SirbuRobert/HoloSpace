using UnityEngine;

public class GazeInteractor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera arCamera;

    [Header("Gaze Settings")]
    [SerializeField] private float gazeDistance = 10f;
    [SerializeField] private float gazeCompleteTime = 1.5f;
    [SerializeField] private LayerMask interactableLayers;

    private IGazeTarget currentTarget;
    private GameObject currentObject;
    private float gazeTimer;

    private void Update()
    {
        Ray ray = new Ray(arCamera.transform.position, arCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, gazeDistance, interactableLayers))
        {
            GameObject hitObject = hit.collider.gameObject;
            IGazeTarget target = hitObject.GetComponentInParent<IGazeTarget>();

            if (target != null)
            {
                HandleTarget(target, hitObject);
                return;
            }
        }

        ClearTarget();
    }

    private void HandleTarget(IGazeTarget target, GameObject hitObject)
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

        if (gazeTimer >= gazeCompleteTime)
        {
            currentTarget.OnGazeComplete();
            gazeTimer = 0f;
        }
    }

    private void ClearTarget()
    {
        if (currentTarget != null)
        {
            currentTarget.OnGazeExit();
        }

        currentTarget = null;
        currentObject = null;
        gazeTimer = 0f;
    }
}