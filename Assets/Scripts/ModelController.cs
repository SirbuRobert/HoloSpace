using UnityEngine;

public class ModelController : MonoBehaviour
{
    [Header("Target Model")]
    [SerializeField] private Transform modelRoot;

    [Header("Rotation")]
    [SerializeField] private float rotationStep = 90f;

    [Header("Scale")]
    [SerializeField] private float scaleStep = 0.1f;
    [SerializeField] private float minScale = 0.2f;
    [SerializeField] private float maxScale = 2f;

    private Vector3 initialScale;
    private Quaternion initialRotation;

    private void Start()
    {
        if (modelRoot == null)
            modelRoot = transform;

        initialScale = modelRoot.localScale;
        initialRotation = modelRoot.localRotation;
    }

    public void RotateModel()
    {
        modelRoot.Rotate(Vector3.up, rotationStep, Space.World);
    }

    public void ScaleUp()
    {
        float newScale = Mathf.Min(modelRoot.localScale.x + scaleStep, maxScale);
        modelRoot.localScale = Vector3.one * newScale;
    }

    public void ScaleDown()
    {
        float newScale = Mathf.Max(modelRoot.localScale.x - scaleStep, minScale);
        modelRoot.localScale = Vector3.one * newScale;
    }

    public void ResetModel()
    {
        modelRoot.localScale = initialScale;
        modelRoot.localRotation = initialRotation;
    }
}