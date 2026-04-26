using UnityEngine;

public class ModelButtonRelay : MonoBehaviour
{
    private ModelController GetController()
    {
        return GetComponentInParent<ModelController>();
    }

    public void Rotate()
    {
        GetController()?.RotateModel();
    }

    public void ScaleUp()
    {
        GetController()?.ScaleUp();
    }

    public void ScaleDown()
    {
        GetController()?.ScaleDown();
    }

    public void ResetModel()
    {
        GetController()?.ResetModel();
    }
}