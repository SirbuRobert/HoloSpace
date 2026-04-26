using UnityEngine;

public class ModelActionBridge : MonoBehaviour
{
    public void RotatePlacedModel()
    {
        GetController()?.RotateModel();
    }

    public void ScaleUpPlacedModel()
    {
        GetController()?.ScaleUp();
    }

    public void ScaleDownPlacedModel()
    {
        GetController()?.ScaleDown();
    }

    public void ResetPlacedModel()
    {
        GetController()?.ResetModel();
    }

    private ModelController GetController()
    {
        if (ARPlacementManager.PlacedObject == null)
        {
            Debug.LogWarning("No placed model found yet.");
            return null;
        }

        ModelController controller =
            ARPlacementManager.PlacedObject.GetComponent<ModelController>();

        if (controller == null)
        {
            Debug.LogWarning("Placed model does not have ModelController.");
        }

        return controller;
    }
}