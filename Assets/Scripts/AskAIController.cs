using UnityEngine;

public class AskAIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MRPanelUI panel;
    [SerializeField] private OpenAIService aiService;

    [Header("Question template")]
    [TextArea(2, 4)]
    [SerializeField] private string questionTemplate =
        "Spune-mi pe scurt despre {0} în 2-3 propoziții.";

    [Header("UI Messages")]
    [SerializeField] private string loadingMessage = "Întreb AI...";

    public void AskAboutCurrentSelection()
    {
        SelectablePart current = SelectionManager.Instance != null
            ? SelectionManager.Instance.CurrentSelection
            : null;

        if (current == null)
        {
            Debug.LogWarning("[AskAIController] Nu există selecție — Ask AI ignorat.", this);
            return;
        }

        string question = string.Format(questionTemplate, current.DisplayName);
        Debug.Log($"[AskAIController] Întrebare: \"{question}\"", this);

        // Loading state imediat, ca utilizatorul sa vada ca se intampla ceva
        if (panel != null) panel.ShowDescription(loadingMessage);

        // Fallback daca serviciul nu e cablat inca (pre-API key)
        if (aiService == null)
        {
            Debug.LogWarning("[AskAIController] aiService nu e setat — folosesc placeholder.", this);
            if (panel != null)
                panel.ShowDescription($"[Placeholder] {current.DisplayName}: AI nu e configurat.");
            return;
        }

        // Capturez datele acum: daca selectia se schimba mid-request, le pastrez
        string partName = current.DisplayName;
        string partDescription = current.Description;

        aiService.Ask(question, partDescription,
            onSuccess: response =>
            {
                Debug.Log($"[AskAIController] Răspuns ({partName}): {response}", this);
                if (panel != null) panel.ShowDescription(response);
            },
            onError: error =>
            {
                Debug.LogError($"[AskAIController] Eroare AI: {error}", this);
                if (panel != null) panel.ShowDescription($"Eroare: {error}");
            }
        );
    }
}