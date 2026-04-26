using UnityEngine;
using UnityEngine.Events;

public class GazeButton : MonoBehaviour, IGazeTarget
{
    [Header("Visual")]
    [SerializeField] private Renderer buttonRenderer;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor = Color.yellow;
    [SerializeField] private Color completeColor = Color.green;

    [Header("Animation")]
    [SerializeField] private Transform progressVisual;

    [Header("Action")]
    public UnityEvent onGazeClick;

    private Vector3 initialProgressScale;

    private void Awake()
    {
        if (buttonRenderer == null)
            buttonRenderer = GetComponentInChildren<Renderer>();

        if (progressVisual != null)
            initialProgressScale = progressVisual.localScale;

        SetColor(normalColor);
        ResetProgress();
    }

    public void OnGazeEnter()
    {
        SetColor(hoverColor);
    }

    public void OnGazeStay(float progress)
    {
        if (progressVisual != null)
        {
            progressVisual.localScale = initialProgressScale * Mathf.Lerp(0.2f, 1f, progress);
        }
    }

    public void OnGazeExit()
    {
        SetColor(normalColor);
        ResetProgress();
    }

    public void OnGazeComplete()
    {
        SetColor(completeColor);
        onGazeClick?.Invoke();
        ResetProgress();
    }

    private void SetColor(Color color)
    {
        if (buttonRenderer != null)
            buttonRenderer.material.color = color;
    }

    private void ResetProgress()
    {
        if (progressVisual != null)
            progressVisual.localScale = initialProgressScale * 0.2f;
    }
}