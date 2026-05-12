using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Dialogue Node", menuName = "Dialogue/Node")]
public class DialogueNode : ScriptableObject
{
    public string speakerName;
    
    [TextArea(3, 10)]
    [Tooltip("Use standard TextMeshPro tags like <color=red>text</color> or <size=150%>text</size> for emphasis. Use <shake>text</shake> for shaking text.")]
    public string dialogueText;
    
    [Header("Voice Settings")]
    [Tooltip("Leave empty to use the default beeps in DialogueManager, or assign specific clips here for this character's voice.")]
    public AudioClip[] characterBeeps;
    
    [Header("Options")]
    public List<DialogueOption> options;

    [System.Serializable]
    public class DialogueOption
    {
        public string optionText;
        [Tooltip("The next dialogue node to play. Leave empty to end dialogue.")]
        public DialogueNode nextNode;
        [Tooltip("Optional events to trigger when this option is selected (e.g., start quest, give item).")]
        public UnityEvent onOptionSelected;
    }
}
