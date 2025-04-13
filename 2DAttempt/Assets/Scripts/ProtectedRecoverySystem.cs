using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ProtectedRecoverySystem : MonoBehaviour
{
    [Header("References")]
    public GameObject[] instancePrefabs; // Change to array of prefabs instead of single prefab
    public GameObject[] connectionPrefabs; // Change to array of connection prefabs
    public TextMeshProUGUI statusText;
    public Image statusPanel;

    [Header("Layout")]
    public int maxLives = 5;
    public float spaceBetweenInstances = 80f;
    public Vector2 startPosition = new Vector2(50f, 50f);

    [Header("Colors")]
    public Color normalColor = new Color(0.2f, 0.8f, 1f); // Cyan
    public Color criticalColor = new Color(1f, 0.3f, 0.3f); // Red
    public Color inactiveColor = new Color(0.3f, 0.3f, 0.3f); // Gray

    private int currentLives;
    private GameObject[] instances;
    private GameObject[] connections;

    void Start()
    {
        // Validate prefabs
        if (instancePrefabs == null || instancePrefabs.Length == 0)
        {
            Debug.LogError("No instance prefabs assigned!");
            return;
        }

        currentLives = maxLives;
        instances = new GameObject[maxLives];
        connections = new GameObject[maxLives - 1];

        // Create all instances
        for (int i = 0; i < maxLives; i++)
        {
            // Instantiate instance (use first prefab if multiple are assigned)
            Vector2 position = startPosition + new Vector2(i * spaceBetweenInstances, 0);
            instances[i] = Instantiate(instancePrefabs[0], transform);
            instances[i].GetComponent<RectTransform>().anchoredPosition = position;

            // Set instance number (optional, only if exists)
            Transform numberTextTransform = instances[i].transform.Find("InstanceNumber");
            if (numberTextTransform != null)
            {
                TextMeshProUGUI numberText = numberTextTransform.GetComponent<TextMeshProUGUI>();
                if (numberText != null)
                    numberText.text = (i + 1).ToString();
            }

            // Create connection (except for last instance)
            if (i < maxLives - 1 && connectionPrefabs.Length > 0)
            {
                connections[i] = Instantiate(connectionPrefabs[0], transform);
                connections[i].GetComponent<RectTransform>().anchoredPosition =
                    position + new Vector2(spaceBetweenInstances / 2, 0);
            }
        }

        UpdateVisuals();
    }

    public void LoseLife()
    {
        if (currentLives > 0)
        {
            currentLives--;
            UpdateVisuals();

            if (currentLives == 1)
            {
                StartCoroutine(FlashWarningText());
            }
        }
    }

    public void GainLife()
    {
        if (currentLives < maxLives)
        {
            currentLives++;
            UpdateVisuals();
        }
    }

    void UpdateVisuals()
    {
        // Validate arrays
        if (instances == null || connections == null) return;

        // Update instances
        for (int i = 0; i < maxLives; i++)
        {
            if (instances[i] == null) continue;

            bool isActive = i < currentLives;
            bool isCritical = currentLives == 1 && i == 0;

            Color themeColor = isCritical ? criticalColor : normalColor;

            // Update hexagon frame
            UpdateImageColor(instances[i], "HexFrame", isActive ? themeColor : inactiveColor, isActive);

            // Update inner shield
            UpdateImageColor(instances[i], "InnerShield", new Color(themeColor.r, themeColor.g, themeColor.b, 0.2f), isActive);

            // Update scan lines
            UpdateImageColor(instances[i], "ScanLines", new Color(themeColor.r, themeColor.g, themeColor.b, 0.5f), isActive);

            // Update checkmark
            UpdateImageColor(instances[i], "Checkmark", themeColor, isActive);

            // Update data scan effects
            UpdateDataScanEffects(instances[i], themeColor, isActive);

            // Update instance number
            UpdateNumberText(instances[i], themeColor, isActive);
        }

        // Update connections
        UpdateConnections();

        // Update status text
        UpdateStatusText();
    }

    void UpdateImageColor(GameObject instance, string childName, Color color, bool active)
    {
        // Find the image component and update its color and active state
        Transform childTransform = instance.transform.Find(childName);
        if (childTransform != null)
        {
            Image image = childTransform.GetComponent<Image>();
            if (image != null)
            {
                image.gameObject.SetActive(active);
                image.color = color;
            }
        }
    }

    void UpdateDataScanEffects(GameObject instance, Color themeColor, bool isActive)
    {
        Transform dataScanEffectsTransform = instance.transform.Find("DataScanEffects");
        if (dataScanEffectsTransform != null)
        {
            dataScanEffectsTransform.gameObject.SetActive(isActive);

            foreach (Transform child in dataScanEffectsTransform)
            {
                Image scanPart = child.GetComponent<Image>();
                if (scanPart != null)
                    scanPart.color = new Color(themeColor.r, themeColor.g, themeColor.b, 0.3f);
            }
        }
    }

    void UpdateNumberText(GameObject instance, Color themeColor, bool isActive)
    {
        Transform numberTextTransform = instance.transform.Find("InstanceNumber");
        if (numberTextTransform != null)
        {
            TextMeshProUGUI numberText = numberTextTransform.GetComponent<TextMeshProUGUI>();
            if (numberText != null)
            {
                numberText.color = isActive ? themeColor : inactiveColor;
                numberText.color = new Color(numberText.color.r, numberText.color.g, numberText.color.b,
                                             isActive ? 1f : 0.5f);
            }
        }
    }

    void UpdateConnections()
    {
        for (int i = 0; i < maxLives - 1; i++)
        {
            if (connections[i] == null) continue;

            bool isActive = i < currentLives - 1;

            // Update line
            Transform lineTransform = connections[i].transform.Find("Line");
            if (lineTransform != null)
            {
                Image line = lineTransform.GetComponent<Image>();
                if (line != null)
                {
                    line.color = isActive ? normalColor : inactiveColor;
                    line.color = new Color(line.color.r, line.color.g, line.color.b,
                                          isActive ? 0.7f : 0.3f);
                }
            }

            // Update flow particle
            Transform flowParticleTransform = connections[i].transform.Find("FlowParticle");
            if (flowParticleTransform != null)
            {
                Image flowParticle = flowParticleTransform.GetComponent<Image>();
                if (flowParticle != null)
                    flowParticle.gameObject.SetActive(isActive);
            }
        }
    }

    void UpdateStatusText()
    {
        if (statusText == null || statusPanel == null) return;

        if (currentLives <= 1)
        {
            statusText.text = "WARNING: CRITICAL SYSTEM FAILURE IMMINENT";
            statusText.color = criticalColor;
            statusPanel.color = criticalColor;
        }
        else
        {
            statusText.text = $"PROTECTED RECOVERY INSTANCES: {currentLives}/{maxLives}";
            statusText.color = normalColor;
            statusPanel.color = normalColor;
        }
    }

    System.Collections.IEnumerator FlashWarningText()
    {
        while (currentLives == 1)
        {
            statusText.color = new Color(criticalColor.r, criticalColor.g, criticalColor.b, 1f);
            yield return new WaitForSeconds(0.5f);
            statusText.color = new Color(criticalColor.r, criticalColor.g, criticalColor.b, 0.5f);
            yield return new WaitForSeconds(0.5f);
        }
    }
}