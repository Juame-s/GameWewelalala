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

    [Header("Billboard & Radial Settings")]
    public float radialMenuRadius = 150f;
    public Vector3 billboardOffset = new Vector3(0, 2f, 0);

    private Transform currentNPCTransform;

    private DialogueNode currentNode;
    private Coroutine typingCoroutine;
    private bool isTyping;
    private bool skipRequested;
    private float dialogueStartTime;
    
    private PlayerController playerController;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        else
        {
            Instance = this;
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        // --- FIX UI RAYCAST BLOCKING ---
        // 1. Prevent the text itself from accidentally covering and blocking the buttons
        if (dialogueBodyText != null) dialogueBodyText.raycastTarget = false;
        if (speakerNameText != null) speakerNameText.raycastTarget = false;
        
        // 2. Force the dialogue panel to have its own Canvas that renders on top of EVERYTHING
        Canvas panelCanvas = dialoguePanel.GetComponent<Canvas>();
        if (panelCanvas == null) panelCanvas = dialoguePanel.AddComponent<Canvas>();
        panelCanvas.overrideSorting = true;
        panelCanvas.sortingOrder = 32000; // Ridiculously high to beat all other UI and Physics
        
        // 3. Give it a dedicated GraphicRaycaster so it manages its own clicks
        // and completely ignores 3D/2D GameObjects that might be in the way.
        GraphicRaycaster gr = dialoguePanel.GetComponent<GraphicRaycaster>();
        if (gr == null) gr = dialoguePanel.AddComponent<GraphicRaycaster>();
        gr.blockingObjects = GraphicRaycaster.BlockingObjects.None;
        gr.ignoreReversedGraphics = true;
        
        // 4. Ensure the panel accepts interactions
        CanvasGroup cg = dialoguePanel.GetComponent<CanvasGroup>();
        if (cg == null) cg = dialoguePanel.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = true;
        cg.interactable = true;
        // -------------------------------

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
            // Billboard tracking
            if (currentNPCTransform != null && Camera.main != null)
            {
                Vector3 screenPos = Camera.main.WorldToScreenPoint(currentNPCTransform.position + billboardOffset);
                
                // If it's behind the camera, you might want to hide it, but for simplicity we just set position
                dialoguePanel.transform.position = screenPos;
            }

            // Skip typing effect (adding a tiny 0.1s delay so the interact button doesn't instantly skip)
            if (isTyping && Time.time - dialogueStartTime > 0.1f && (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E)))
            {
                skipRequested = true;
            }

            // Animate shaking text
            AnimateText();
        }
    }

    public void StartDialogue(DialogueNode startNode, Transform npcTransform = null)
    {
        if (startNode == null) return;

        currentNode = startNode;
        currentNPCTransform = npcTransform;
        dialoguePanel.SetActive(true);
        
        // Block player input but keep physics running
        if (playerController != null)
            playerController.SetDialogueMode(true);

        // Block camera rotation but keep camera following the player
        CameraFollow camFollow = Camera.main.GetComponent<CameraFollow>();
        if (camFollow != null)
            camFollow.IsInDialogue = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        DisplayNode(currentNode);
    }

    private void DisplayNode(DialogueNode node)
    {
        currentNode = node;
        dialogueStartTime = Time.time;
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
        List<GameObject> spawnedButtons = new List<GameObject>();

        if (currentNode.options == null || currentNode.options.Count == 0)
        {
            spawnedButtons.Add(CreateOptionButton("End Dialogue", null, null));
        }
        else
        {
            foreach (var option in currentNode.options)
            {
                spawnedButtons.Add(CreateOptionButton(option.optionText, option.nextNode, option.onOptionSelected));
            }
        }

        // Radial layout logic
        int count = spawnedButtons.Count;
        for (int i = 0; i < count; i++)
        {
            // Angle mapping: 
            // We want the options spread evenly.
            // If there's only 1 option, just put it in the center or right.
            float angleDeg = 0;
            if (count > 1)
            {
                // Start right, go counter-clockwise. You can adjust the offset (+90) if you want it to start at top.
                angleDeg = i * (360f / count); 
            }
            else
            {
                // 1 option, put it below the dialogue box maybe? Or right in the middle
                angleDeg = 270f; 
            }

            float angleRad = angleDeg * Mathf.Deg2Rad;
            Vector3 targetPos = new Vector3(Mathf.Cos(angleRad) * radialMenuRadius, Mathf.Sin(angleRad) * radialMenuRadius, 0);

            GameObject btn = spawnedButtons[i];
            RadialMenuOption radialOpt = btn.GetComponent<RadialMenuOption>();
            if (radialOpt == null)
            {
                radialOpt = btn.AddComponent<RadialMenuOption>();
            }

            // Stagger animation slightly
            radialOpt.AnimateAppearance(targetPos, i * 0.05f);
        }
    }

    private GameObject CreateOptionButton(string text, DialogueNode nextNode, UnityEngine.Events.UnityEvent onSelected)
    {
        GameObject buttonObj = Instantiate(optionButtonPrefab);
        buttonObj.transform.SetParent(optionsContainer, false);
        // Local scale and position will be handled by RadialMenuOption animation

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

        return buttonObj;
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
        currentNPCTransform = null;

        // Re-enable player input
        if (playerController != null)
            playerController.SetDialogueMode(false);

        // Re-enable camera rotation
        CameraFollow camFollow = Camera.main.GetComponent<CameraFollow>();
        if (camFollow != null)
            camFollow.IsInDialogue = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
