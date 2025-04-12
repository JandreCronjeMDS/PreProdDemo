using System.Collections.Generic;
using UnityEngine;

public class TriangleSmokeEffect : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private PlayerController playerController; // Reference to player controller
    [SerializeField] private int particlesPerJump = 20; // Increased particle count
    [SerializeField] private float spawnRadius = 0.5f; // Wider spread
    [SerializeField] private Vector2 initialBurstForce = new Vector2(2f, -1f); // Initial burst force (x=horizontal spread, y=downward force)

    [Header("Triangle Settings")]
    [SerializeField] private float minSize = 0.2f;
    [SerializeField] private float maxSize = 0.5f;
    [Range(0, 1)][SerializeField] private float triangleSharpness = 0.6f;
    [SerializeField] private Material triangleMaterial;
    [SerializeField]
    private Color[] smokeColors = new Color[]
    {
        new Color(0.9f, 0.9f, 0.9f, 0.8f),
        new Color(0.85f, 0.85f, 0.85f, 0.7f),
        new Color(0.8f, 0.8f, 0.8f, 0.6f),
        new Color(0.75f, 0.75f, 0.75f, 0.5f)
    };

    [Header("Animation")]
    [SerializeField] private float minLifetime = 0.6f;
    [SerializeField] private float maxLifetime = 1.2f;
    [SerializeField] private float minSpeed = 1.5f;
    [SerializeField] private float maxSpeed = 4f;
    [SerializeField] private float rotationSpeed = 60f;
    [SerializeField] private float fadeSpeed = 1.2f;
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private bool useInitialExplosion = true; // Burst effect
    [SerializeField] private float explosionForce = 3f; // Force of the initial explosion

    // List to track active particles
    private List<SmokeParticle> activeParticles = new List<SmokeParticle>();
    private bool wasGrounded = true;
    private bool wasJumping = false;

    // Pool of triangle mesh instances
    private Queue<GameObject> trianglePool = new Queue<GameObject>();
    private int poolSize = 50;

    private void Start()
    {
        // Initialize pool
        for (int i = 0; i < poolSize; i++)
        {
            CreateTriangleForPool();
        }

        // If no spawn point specified, use the current transform position
        if (spawnPoint == null)
        {
            spawnPoint = transform;
        }
    }

    private void Update()
    {
        if (playerController == null) return;

        // Get player state (using reflection to access private fields)
        bool isGrounded = GetFieldValue<bool>(playerController, "isGrounded");
        bool isJumping = GetFieldValue<bool>(playerController, "isJumping");

        // Detect jump start (was on ground and now jumping)
        if (wasGrounded && isJumping && !wasJumping)
        {
            EmitSmokeParticles();
        }

        // Update all active particles
        for (int i = activeParticles.Count - 1; i >= 0; i--)
        {
            SmokeParticle particle = activeParticles[i];

            // Update particle
            if (UpdateParticle(particle))
            {
                // Particle is alive, update it
                continue;
            }
            else
            {
                // Particle is dead, return to pool
                ReturnToPool(particle);
                activeParticles.RemoveAt(i);
            }
        }

        // Track state for next frame
        wasGrounded = isGrounded;
        wasJumping = isJumping;
    }

    private void EmitSmokeParticles()
    {
        // Calculate initial burst center
        Vector3 burstCenter = spawnPoint.position;

        for (int i = 0; i < particlesPerJump; i++)
        {
            // Get random position around spawn point
            Vector2 randomOffset = Random.insideUnitCircle * spawnRadius;
            Vector3 spawnPos = burstCenter + new Vector3(randomOffset.x, randomOffset.y, 0);

            // Get direction based on position from center
            Vector3 directionFromCenter = (spawnPos - burstCenter).normalized;

            // Combine with downward force (prioritize downward for smoke puff effect)
            Vector3 direction;

            if (useInitialExplosion)
            {
                // Create an explosion-like effect
                direction = directionFromCenter * explosionForce;

                // Add downward bias
                direction.y -= Random.Range(0.5f, 1.5f);
            }
            else
            {
                // Direct control with initialBurstForce
                float randomAngle = Random.Range(-60f, 60f);
                direction = Quaternion.Euler(0, 0, randomAngle) * new Vector3(
                    Random.Range(-initialBurstForce.x, initialBurstForce.x),
                    initialBurstForce.y,
                    0
                );
            }

            // Random size for variety (more smaller particles for smoke look)
            float sizeVariation = Mathf.Pow(Random.value, 1.5f); // More small particles
            float size = Mathf.Lerp(minSize, maxSize, sizeVariation);

            // More speed variation
            float speedVariation = Random.value;
            float speed = Mathf.Lerp(minSpeed, maxSpeed, speedVariation);

            // Variable lifetime (smaller particles live shorter)
            float lifetime = Mathf.Lerp(minLifetime, maxLifetime, sizeVariation);

            // Get color (add more variation to smaller particles)
            Color baseColor = smokeColors[Random.Range(0, smokeColors.Length)];
            Color color = baseColor;
            color.a *= Random.Range(0.7f, 1.0f); // Vary transparency

            // Get triangle from pool
            GameObject triangleObj = GetTriangleFromPool();
            if (triangleObj == null) continue;

            // Position triangle
            triangleObj.transform.position = spawnPos;
            triangleObj.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0, 360f));

            // Start with a rapid expansion
            triangleObj.transform.localScale = Vector3.one * size * 0.2f;

            // Set color
            MeshRenderer renderer = triangleObj.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.material != null)
            {
                renderer.material.color = color;
            }

            // Create particle and add to active list
            SmokeParticle particle = new SmokeParticle(
                triangleObj,
                direction,
                speed,
                size,
                lifetime,
                Time.time,
                color
            );

            activeParticles.Add(particle);
        }
    }

    private bool UpdateParticle(SmokeParticle particle)
    {
        // Calculate lifetime progress (0 to 1)
        float age = Time.time - particle.startTime;
        float progress = age / particle.lifetime;

        // Check if particle is dead
        if (progress >= 1.0f)
        {
            return false;
        }

        // Update position with gravity simulation
        Vector3 movement = particle.direction * particle.speed * Time.deltaTime;

        // Add gravity effect - stronger at the end of lifetime
        float gravityEffect = Mathf.Lerp(0.05f, 0.2f, progress);
        movement.y -= gravityEffect * Time.deltaTime * 9.8f;

        // Update position
        particle.obj.transform.position += movement;

        // Gradually change direction for swirling effect
        if (progress > 0.3f)
        {
            // Add some sideways drift based on position - creates a billowing effect
            float driftAmount = Mathf.Sin(Time.time * 2f + particle.startTime * 10f) * 0.2f * Time.deltaTime;
            particle.direction += new Vector3(driftAmount, 0, 0);
        }

        // Update rotation (faster for smaller particles)
        float rotationMultiplier = 1.5f - (particle.size / maxSize);
        particle.obj.transform.Rotate(0, 0, rotationSpeed * rotationMultiplier * Time.deltaTime);

        // Update scale based on animation curve (quick initial expansion, then gradual fade)
        float scaleProgress = progress < 0.2f ? progress * 5f : 1f; // Fast initial expansion
        float scaleMultiplier = scaleCurve.Evaluate(scaleProgress) * (1f - (progress * 0.3f)); // Gradually shrink
        particle.obj.transform.localScale = Vector3.one * particle.size * scaleMultiplier;

        // Update transparency (fade out)
        MeshRenderer renderer = particle.obj.GetComponent<MeshRenderer>();
        if (renderer != null && renderer.material != null)
        {
            Color color = renderer.material.color;

            // Hold alpha steady at first, then fade out quickly
            if (progress < 0.6f)
            {
                color.a = particle.color.a * (1f - (progress * 0.2f));
            }
            else
            {
                color.a = particle.color.a * (1f - ((progress - 0.6f) * 2.5f));
            }

            renderer.material.color = color;
        }

        // Slow down over time
        particle.speed = Mathf.Lerp(particle.speed, 0.5f, Time.deltaTime * 2f);

        return true; // Particle is still alive
    }

    private GameObject CreateTriangleForPool()
    {
        // Create game object
        GameObject triangleObj = new GameObject("SmokeTriangle");
        triangleObj.transform.SetParent(transform);

        // Add mesh filter and renderer
        MeshFilter meshFilter = triangleObj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = triangleObj.AddComponent<MeshRenderer>();

        // Create a triangle mesh
        Mesh triangleMesh = CreateTriangleMesh(triangleSharpness);
        meshFilter.mesh = triangleMesh;

        // Set material
        if (triangleMaterial != null)
        {
            meshRenderer.material = new Material(triangleMaterial);
        }
        else
        {
            // Create default material if none provided
            Material defaultMat = new Material(Shader.Find("Sprites/Default"));
            defaultMat.color = Color.white;
            meshRenderer.material = defaultMat;
        }

        // Disable and add to pool
        triangleObj.SetActive(false);
        trianglePool.Enqueue(triangleObj);

        return triangleObj;
    }

    private Mesh CreateTriangleMesh(float sharpness)
    {
        Mesh mesh = new Mesh();

        // Get a random shape variation
        int shapeType = Random.Range(0, 4);

        if (shapeType == 0) // Regular triangle
        {
            // Create a triangle with custom pointiness
            Vector3[] vertices = new Vector3[3];
            vertices[0] = new Vector3(0, sharpness, 0);     // Top point
            vertices[1] = new Vector3(-0.5f, -0.5f, 0);     // Bottom left
            vertices[2] = new Vector3(0.5f, -0.5f, 0);      // Bottom right

            // Define the triangle
            int[] triangles = new int[] { 0, 1, 2 };

            // Define UVs
            Vector2[] uvs = new Vector2[3];
            uvs[0] = new Vector2(0.5f, 1);     // Top
            uvs[1] = new Vector2(0, 0);        // Bottom left
            uvs[2] = new Vector2(1, 0);        // Bottom right

            // Assign to mesh
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
        }
        else if (shapeType == 1) // Downward pointing triangle
        {
            // Create a downward pointing triangle
            Vector3[] vertices = new Vector3[3];
            vertices[0] = new Vector3(0, -sharpness, 0);    // Bottom point
            vertices[1] = new Vector3(-0.5f, 0.5f, 0);      // Top left
            vertices[2] = new Vector3(0.5f, 0.5f, 0);       // Top right

            // Define the triangle
            int[] triangles = new int[] { 0, 2, 1 };

            // Define UVs
            Vector2[] uvs = new Vector2[3];
            uvs[0] = new Vector2(0.5f, 0);     // Bottom
            uvs[1] = new Vector2(0, 1);        // Top left
            uvs[2] = new Vector2(1, 1);        // Top right

            // Assign to mesh
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
        }
        else if (shapeType == 2) // Right-pointing triangle
        {
            // Create a right-pointing triangle
            Vector3[] vertices = new Vector3[3];
            vertices[0] = new Vector3(sharpness, 0, 0);     // Right point
            vertices[1] = new Vector3(-0.5f, 0.5f, 0);      // Top left
            vertices[2] = new Vector3(-0.5f, -0.5f, 0);     // Bottom left

            // Define the triangle
            int[] triangles = new int[] { 0, 1, 2 };

            // Define UVs
            Vector2[] uvs = new Vector2[3];
            uvs[0] = new Vector2(1, 0.5f);     // Right
            uvs[1] = new Vector2(0, 0);        // Top left
            uvs[2] = new Vector2(0, 1);        // Bottom left

            // Assign to mesh
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
        }
        else // Left-pointing triangle
        {
            // Create a left-pointing triangle
            Vector3[] vertices = new Vector3[3];
            vertices[0] = new Vector3(-sharpness, 0, 0);    // Left point
            vertices[1] = new Vector3(0.5f, 0.5f, 0);       // Top right
            vertices[2] = new Vector3(0.5f, -0.5f, 0);      // Bottom right

            // Define the triangle
            int[] triangles = new int[] { 0, 1, 2 };

            // Define UVs
            Vector2[] uvs = new Vector2[3];
            uvs[0] = new Vector2(0, 0.5f);     // Left
            uvs[1] = new Vector2(1, 0);        // Top right
            uvs[2] = new Vector2(1, 1);        // Bottom right

            // Assign to mesh
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
        }

        // Recalculate normals
        mesh.RecalculateNormals();

        return mesh;
    }

    private GameObject GetTriangleFromPool()
    {
        // If pool is empty, create a new triangle
        if (trianglePool.Count == 0)
        {
            return CreateTriangleForPool();
        }

        // Get from pool and activate
        GameObject triangle = trianglePool.Dequeue();
        triangle.SetActive(true);

        return triangle;
    }

    private void ReturnToPool(SmokeParticle particle)
    {
        // Deactivate and return to pool
        particle.obj.SetActive(false);
        trianglePool.Enqueue(particle.obj);
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

    // Class to represent a single smoke particle
    private class SmokeParticle
    {
        public GameObject obj;
        public Vector3 direction;
        public float speed;
        public float size;
        public float lifetime;
        public float startTime;
        public Color color;

        public SmokeParticle(GameObject obj, Vector3 direction, float speed, float size, float lifetime, float startTime, Color color)
        {
            this.obj = obj;
            this.direction = direction;
            this.speed = speed;
            this.size = size;
            this.lifetime = lifetime;
            this.startTime = startTime;
            this.color = color;
        }
    }
}