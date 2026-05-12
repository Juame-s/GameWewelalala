using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("UI References")]
    public GameObject dialoguePanel;
    public TextMeshProUGUI speakerNameText;
    public TextMeshProUGUI dialogueBodyText;
    public Transform optionsContainer;
    public GameObject optionButtonPrefab;
    public ScrollRect optionsScrollRect; // Included to support scrolling many options

    [Header("Typewriter Settings")]
    [Range(0.01f, 0.2f)]
    public float typeSpeed = 0.05f;
    public AudioClip[] beepSounds;
    private AudioSource audioSource;

    [Header("Text Effects")]
    public float shakeAmount = 2f;
    public float shakeSpeed = 10f;

    private DialogueNode currentNode;
    private Coroutine typingCoroutine;
    private bool isTyping;
    private bool skipRequested;
    
    private PlayerController playerController;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        dialoguePanel.SetActive(false);
    }

    private void Start()
    {
        playerController = FindObjectOfType<PlayerController>();
    }

    private void Update()
    {
        if (dialoguePanel.activeSelf)
        {
            // Skip typing effect
            if (isTyping && (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E)))
            {
                skipRequested = true;
            }

            // Animate shaking text
            AnimateText();
        }
    }

    public void StartDialogue(DialogueNode startNode)
    {
        if (startNode == null) return;

        currentNode = startNode;
        dialoguePanel.SetActive(true);
        
        // Disable player movement
        if (playerController != null)
            playerController.enabled = false;

        // Disable camera rotation and unlock mouse
        CameraFollow camFollow = Camera.main.GetComponent<CameraFollow>();
        if (camFollow != null)
            camFollow.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        DisplayNode(currentNode);
    }

    private void DisplayNode(DialogueNode node)
    {
        currentNode = node;
        ClearOptions();
        speakerNameText.text = node.speakerName;
        skipRequested = false;

        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }
        
        // Convert custom <shake> tags to TMPro <link=shake> tags for easy parsing
        string parsedText = node.dialogueText.Replace("<shake>", "<link=shake>").Replace("</shake>", "</link>");
        
        typingCoroutine = StartCoroutine(TypeDialogue(parsedText));
    }

    private IEnumerator TypeDialogue(string text)
    {
        isTyping = true;
        dialogueBodyText.text = text;
        dialogueBodyText.maxVisibleCharacters = 0;

        // Force a mesh update to get the correct character count including rich text tags
        dialogueBodyText.ForceMeshUpdate();

        int totalVisibleCharacters = dialogueBodyText.textInfo.characterCount;
        int counter = 0;

        while (counter <= totalVisibleCharacters)
        {
            if (skipRequested)
            {
                dialogueBodyText.maxVisibleCharacters = totalVisibleCharacters;
                break;
            }

            dialogueBodyText.maxVisibleCharacters = counter;

            // Determine which beeps to use (Node specific or default)
            AudioClip[] activeBeeps = (currentNode.characterBeeps != null && currentNode.characterBeeps.Length > 0) 
                ? currentNode.characterBeeps 
                : beepSounds;

            // Play beep sound for every character typed
            if (counter > 0 && activeBeeps != null && activeBeeps.Length > 0)
            {
                // Only beep if the current character is a letter/digit (not space or punctuation)
                char c = dialogueBodyText.textInfo.characterInfo[counter - 1].character;
                if (char.IsLetterOrDigit(c))
                {
                    AudioClip clip = activeBeeps[Random.Range(0, activeBeeps.Length)];
                    // Slightly randomize pitch for more natural sound variations
                    audioSource.pitch = Random.Range(0.9f, 1.1f);
                    audioSource.PlayOneShot(clip);
                }
            }

            counter++;
            yield return new WaitForSeconds(typeSpeed);
        }

        dialogueBodyText.maxVisibleCharacters = totalVisibleCharacters;
        isTyping = false;

        ShowOptions();
    }

    private void AnimateText()
    {
        dialogueBodyText.ForceMeshUpdate();
        TMP_TextInfo textInfo = dialogueBodyText.textInfo;

        if (textInfo.linkCount > 0)
        {
            bool meshModified = false;

            for (int i = 0; i < textInfo.linkCount; i++)
            {
                TMP_LinkInfo linkInfo = textInfo.linkInfo[i];
                if (linkInfo.GetLinkID() == "shake")
                {
                    for (int j = linkInfo.linkTextfirstCharacterIndex; j < linkInfo.linkTextfirstCharacterIndex + linkInfo.linkTextLength; j++)
                    {
                        if (!textInfo.characterInfo[j].isVisible)
                            continue;

                        int vertexIndex = textInfo.characterInfo[j].vertexIndex;
                        int materialIndex = textInfo.characterInfo[j].materialReferenceIndex;

                        Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;

                        Vector3 offset = new Vector3(Mathf.Sin(Time.time * shakeSpeed + j) * shakeAmount, Mathf.Cos(Time.time * shakeSpeed + j) * shakeAmount, 0);

                        vertices[vertexIndex + 0] += offset;
                        vertices[vertexIndex + 1] += offset;
                        vertices[vertexIndex + 2] += offset;
                        vertices[vertexIndex + 3] += offset;

                        meshModified = true;
                    }
                }
            }

            if (meshModified)
            {
                for (int i = 0; i < textInfo.materialCount; i++)
                {
                    if (textInfo.meshInfo[i].mesh != null)
                    {
                        textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
                        dialogueBodyText.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
                    }
                }
            }
        }
    }

    private void ShowOptions()
    {
        if (currentNode.options == null || currentNode.options.Count == 0)
        {
            CreateOptionButton("End Dialogue", null, null);
        }
        else
        {
            foreach (var option in currentNode.options)
            {
                CreateOptionButton(option.optionText, option.nextNode, option.onOptionSelected);
            }
        }
    }

    private void CreateOptionButton(string text, DialogueNode nextNode, UnityEngine.Events.UnityEvent onSelected)
    {
        GameObject buttonObj = Instantiate(optionButtonPrefab);
        buttonObj.transform.SetParent(optionsContainer, false);
        buttonObj.transform.localScale = Vector3.one;
        buttonObj.transform.localPosition = Vector3.zero;

        buttonObj.GetComponentInChildren<TextMeshProUGUI>().text = text;
        
        Button button = buttonObj.GetComponent<Button>();
        button.onClick.AddListener(() =>
        {
            onSelected?.Invoke();
            
            if (nextNode != null)
            {
                DisplayNode(nextNode);
            }
            else
            {
                EndDialogue();
            }
        });
    }

    private void ClearOptions()
    {
        foreach (Transform child in optionsContainer)
        {
            Destroy(child.gameObject);
        }
    }

    public void EndDialogue()
    {
        dialoguePanel.SetActive(false);
        currentNode = null;

        if (playerController != null)
            playerController.enabled = true;

        CameraFollow camFollow = Camera.main.GetComponent<CameraFollow>();
        if (camFollow != null)
            camFollow.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
