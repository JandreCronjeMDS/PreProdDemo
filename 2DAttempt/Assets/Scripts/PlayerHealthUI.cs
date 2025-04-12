using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHealthUI : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private Image damageFlashImage;
    [SerializeField] private float flashDuration = 0.2f;

    private void Start()
    {
        // Initialize the UI with player's health
        UpdateHealthUI(player.CurrentHealth, player.MaxHealth);

        // Subscribe to player health changes
        player.OnHealthChanged += UpdateHealthUI;

        // Reset flash image
        if (damageFlashImage != null)
        {
            damageFlashImage.color = new Color(1, 0, 0, 0);
        }
    }

    private void UpdateHealthUI(int currentHealth, int maxHealth)
    {
        // Update health slider
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }

        // Update health text
        if (healthText != null)
        {
            healthText.text = $"{currentHealth} / {maxHealth}";
        }

        // Flash damage overlay when health decreases
        if (damageFlashImage != null)
        {
            StartCoroutine(FlashDamageOverlay());
        }
    }

    private System.Collections.IEnumerator FlashDamageOverlay()
    {
        // Set the flash image to be visible
        damageFlashImage.color = new Color(1, 0, 0, 0.3f);

        // Fade it out
        float elapsedTime = 0;
        while (elapsedTime < flashDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(0.3f, 0, elapsedTime / flashDuration);
            damageFlashImage.color = new Color(1, 0, 0, alpha);
            yield return null;
        }

        // Ensure it's fully invisible
        damageFlashImage.color = new Color(1, 0, 0, 0);
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (player != null)
        {
            player.OnHealthChanged -= UpdateHealthUI;
        }
    }
}