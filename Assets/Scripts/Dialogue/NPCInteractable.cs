using UnityEngine;

[RequireComponent(typeof(Collider))]
public class NPCInteractable : MonoBehaviour, IInteractable
{
    [Header("Dialogue Configuration")]
    [Tooltip("The initial dialogue node that will be triggered when the player interacts with this NPC.")]
    public DialogueNode startingNode;

    public void Interact()
    {
        if (startingNode != null)
        {
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.StartDialogue(startingNode, this.transform);
            }
            else
            {
                Debug.LogError("DialogueManager not found in scene!");
            }
        }
        else
        {
            Debug.LogWarning($"NPC {gameObject.name} has no starting dialogue node.");
        }
    }
}
