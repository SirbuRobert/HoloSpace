using System;
using UnityEngine;

/// <summary>
/// Single-selection arbiter pentru SelectablePart-uri.
/// Ascultă SelectablePart.GazeCompleted, deselectează partea curentă,
/// selectează cea nouă, și anunță UI-ul prin SelectionChanged.
/// Convenție: o singură instanță în scenă (un GameObject gol "SelectionManager").
/// </summary>
[DisallowMultipleComponent]
public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance { get; private set; }

    /// <summary>
    /// Emis de fiecare dată când selecția se schimbă.
    /// Argumentul e noul SelectablePart sau null când selecția e curățată.
    /// </summary>
    public static event Action<SelectablePart> SelectionChanged;

    /// <summary>Partea selectată curent, sau null dacă nu există selecție.</summary>
    public SelectablePart CurrentSelection { get; private set; }

    [Header("Behaviour")]
    [Tooltip("Dacă e bifat, a doua privire pe aceeași parte o deselectează (toggle).")]
    [SerializeField] private bool toggleOnSameTarget = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                $"SelectionManager: altă instanță există deja pe '{Instance.name}'. " +
                $"Distrug duplicatul de pe '{name}'."
            );
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        SelectablePart.GazeCompleted += HandleGazeCompleted;
        SelectionChanged += part => Debug.Log(
        part == null ? "Selection cleared" : $"Selected: {part.DisplayName}"
    );
    }

    private void OnDisable()
    {
        SelectablePart.GazeCompleted -= HandleGazeCompleted;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void HandleGazeCompleted(SelectablePart part)
    {
        if (part == null) return;

        if (toggleOnSameTarget && part == CurrentSelection)
        {
            ClearSelection();
            return;
        }

        SelectPart(part);
    }

    /// <summary>
    /// API public — poate fi apelat și din alt cod (ex. test, hotkey).
    /// Deselectează partea curentă (dacă e cazul) și selectează noua.
    /// </summary>
    public void SelectPart(SelectablePart part)
    {
        if (part == CurrentSelection) return;

        if (CurrentSelection != null)
            CurrentSelection.SetSelected(false);

        CurrentSelection = part;

        if (CurrentSelection != null)
            CurrentSelection.SetSelected(true);

        SelectionChanged?.Invoke(CurrentSelection);
    }

    /// <summary>Curăță selecția curentă (dacă există) și anunță UI-ul.</summary>
    public void ClearSelection()
    {
        if (CurrentSelection == null) return;

        CurrentSelection.SetSelected(false);
        CurrentSelection = null;
        SelectionChanged?.Invoke(null);
    }
}