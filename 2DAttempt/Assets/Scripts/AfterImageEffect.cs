using System.Collections.Generic;
using UnityEngine;

public class AfterImageEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private SpriteRenderer playerSpriteRenderer;

    [Header("After Image Settings")]
    [SerializeField] private float imageSpawnRate = 0.05f; // Time between spawns
    [SerializeField] private float imageDuration = 0.5f; // How long each afterimage lasts
    [SerializeField] private int maxImages = 10; // Maximum number of afterimages active at once
    [SerializeField] private Color startColor = new Color(1f, 1f, 1f, 0.8f);
    [SerializeField] private Color endColor = new Color(1f, 1f, 1f, 0f);
    [SerializeField] private bool spawnDuringJump = true;
    [SerializeField] private bool spawnDuringDash = true;
    [Header("Rendering")]
    [SerializeField] private string sortingLayerName = "Player"; // Default to Player sorting layer
    [SerializeField] private int sortingOrderOffset = -1; // Just behind the player
    [SerializeField] private Material afterImageMaterial; // Optional custom material

    // Tracking variables
    private float lastImageTime;
    private Queue<AfterImageInstance> activeImages = new Queue<AfterImageInstance>();
    private Queue<GameObject> imagePool = new Queue<GameObject>();

    private void Start()
    {
        // Find references if not set
        if (playerController == null)
        {
            playerController = GetComponentInParent<PlayerController>();
        }

        if (playerSpriteRenderer == null)
        {
            playerSpriteRenderer = GetComponentInParent<SpriteRenderer>();
        }

        // Initialize object pool
        for (int i = 0; i < maxImages; i++)
        {
            imagePool.Enqueue(CreateImageObject());
        }
    }

    private void Update()
    {
        if (playerController == null || playerSpriteRenderer == null) return;

        // Check if we should create afterimages
        bool shouldCreateImage = false;

        // Get player state using reflection
        bool isJumping = GetFieldValue<bool>(playerController, "isJumping");
        bool isDashing = GetFieldValue<bool>(playerController, "isDashing");
        float moveInput = GetFieldValue<float>(playerController, "moveInput");

        // Create afterimages during dashing
        if (spawnDuringDash && isDashing)
        {
            shouldCreateImage = true;
        }

        // Create afterimages during jumping if player is also moving horizontally
        if (spawnDuringJump && isJumping && Mathf.Abs(moveInput) > 0.1f)
        {
            shouldCreateImage = true;
        }

        // Spawn new afterimage if conditions are met and cooldown has passed
        if (shouldCreateImage && Time.time > lastImageTime + imageSpawnRate)
        {
            SpawnAfterImage();
            lastImageTime = Time.time;
        }

        // Update active afterimages
        UpdateActiveImages();
    }

    private void SpawnAfterImage()
    {
        // Get an image object from the pool
        GameObject imageObj = GetImageFromPool();
        if (imageObj == null) return;

        // Position and orient the image to match the player
        imageObj.transform.position = transform.position;
        imageObj.transform.rotation = transform.rotation;
        imageObj.transform.localScale = transform.localScale;

        // Detach from parent to stay in place
        imageObj.transform.SetParent(null);

        // Set sprite and color
        SpriteRenderer imageRenderer = imageObj.GetComponent<SpriteRenderer>();
        if (imageRenderer != null)
        {
            // Copy sprite from player
            imageRenderer.sprite = playerSpriteRenderer.sprite;
            imageRenderer.flipX = playerSpriteRenderer.flipX;

            // Set initial color
            imageRenderer.color = startColor;

            // Ensure sorting is correct (in case it was changed)
            imageRenderer.sortingLayerName = sortingLayerName;
            imageRenderer.sortingOrder = playerSpriteRenderer.sortingOrder + sortingOrderOffset;
        }

        // Add to active images queue
        AfterImageInstance instance = new AfterImageInstance
        {
            gameObject = imageObj,
            spawnTime = Time.time,
            duration = imageDuration
        };

        // If queue is full, dequeue oldest image and return to pool
        if (activeImages.Count >= maxImages)
        {
            AfterImageInstance oldestImage = activeImages.Dequeue();
            oldestImage.gameObject.SetActive(false);
            oldestImage.gameObject.transform.SetParent(transform); // Reparent when returning to pool
            imagePool.Enqueue(oldestImage.gameObject);
        }

        // Add new image to queue
        activeImages.Enqueue(instance);
    }

    private void UpdateActiveImages()
    {
        // Store images that are still active after this update
        List<AfterImageInstance> stillActiveImages = new List<AfterImageInstance>();

        // Process all current active images
        int count = activeImages.Count;
        for (int i = 0; i < count; i++)
        {
            AfterImageInstance image = activeImages.Dequeue();

            // Calculate how far through the lifetime this image is (0 to 1)
            float normalizedAge = (Time.time - image.spawnTime) / image.duration;

            if (normalizedAge < 1.0f)
            {
                // Image is still active, update its color
                SpriteRenderer renderer = image.gameObject.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    // Lerp between start and end color
                    renderer.color = Color.Lerp(startColor, endColor, normalizedAge);
                }

                // Keep this image for the next frame
                stillActiveImages.Add(image);
            }
            else
            {
                // Image has expired, return to pool
                image.gameObject.SetActive(false);
                image.gameObject.transform.SetParent(transform); // Reparent when returning to pool
                imagePool.Enqueue(image.gameObject);
            }
        }

        // Add still-active images back to the queue
        foreach (var image in stillActiveImages)
        {
            activeImages.Enqueue(image);
        }
    }

    private GameObject CreateImageObject()
    {
        // Create new GameObject for afterimage
        GameObject imageObj = new GameObject("AfterImage");
        imageObj.transform.SetParent(transform);

        // Add sprite renderer
        SpriteRenderer renderer = imageObj.AddComponent<SpriteRenderer>();

        // Set sorting layer and order
        renderer.sortingLayerName = sortingLayerName;
        renderer.sortingOrder = playerSpriteRenderer != null ?
            playerSpriteRenderer.sortingOrder + sortingOrderOffset :
            0;

        // Set custom material if provided
        if (afterImageMaterial != null)
        {
            renderer.material = new Material(afterImageMaterial);
        }

        // Deactivate initially
        imageObj.SetActive(false);

        return imageObj;
    }

    private GameObject GetImageFromPool()
    {
        // Create new object if pool is empty
        if (imagePool.Count == 0)
        {
            return CreateImageObject();
        }

        // Get from pool and activate
        GameObject imageObj = imagePool.Dequeue();
        imageObj.SetActive(true);
        return imageObj;
    }

    // Helper method to get private fields from the player controller
    private T GetFieldValue<T>(object obj, string fieldName)
    {
        var field = obj.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);

        if (field != null)
        {
            return (T)field.GetValue(obj);
        }

        // Default values if field not found
        if (typeof(T) == typeof(bool)) return (T)(object)false;
        if (typeof(T) == typeof(float)) return (T)(object)0f;
        return default;
    }

    // Class to track active image instances
    private class AfterImageInstance
    {
        public GameObject gameObject;
        public float spawnTime;
        public float duration;
    }
}