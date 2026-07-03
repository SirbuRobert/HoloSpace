using System;
using UnityEngine;

/// <summary>
/// Arbitru de selectie unica pentru SelectablePart-uri.
/// Asculta SelectablePart.GazeCompleted, deselecteaza partea curenta,
/// o selecteaza pe cea noua si anunta UI-ul prin SelectionChanged.
/// Conventie: o singura instanta in scena (un GameObject gol "SelectionManager").
/// </summary>
[DisallowMultipleComponent]
public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance { get; private set; }

    /// <summary>
    /// Emis de fiecare data cand selectia se schimba.
    /// Argumentul e noul SelectablePart sau null cand selectia e curatata.
    /// </summary>
    public static event Action<SelectablePart> SelectionChanged;

    /// <summary>Partea selectata curent, sau null daca nu exista selectie.</summary>
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
    /// API public, poate fi apelat si din alt cod (ex. test, hotkey).
    /// Deselecteaza partea curenta (daca e cazul) si o selecteaza pe cea noua.
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

    /// <summary>Curata selectia curenta (daca exista) si anunta UI-ul.</summary>
    public void ClearSelection()
    {
        if (CurrentSelection == null) return;

        CurrentSelection.SetSelected(false);
        CurrentSelection = null;
        SelectionChanged?.Invoke(null);
    }
}