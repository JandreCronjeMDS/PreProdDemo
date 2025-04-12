using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RootkitTentacle : MonoBehaviour
{
    [Header("Tentacle Settings")]
    public LineRenderer lineRenderer;
    public int segments = 10;
    public float tentacleLength = 3f;
    public float waveHeight = 0.5f;
    public float waveSpeed = 1f;

    [Header("Animation")]
    public float swayAmount = 0.5f;
    public float swaySpeed = 0.5f;
    public Transform target; // Optional target to reach toward

    private Vector3[] positions;
    private Vector3 startPoint;

    void Start()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        // Initialize line renderer
        lineRenderer.positionCount = segments;
        positions = new Vector3[segments];

        // Store starting position
        startPoint = transform.position;
    }

    void Update()
    {
        AnimateTentacle();
    }

    void AnimateTentacle()
    {
        // Get endpoint (either toward target or in the tentacle's forward direction)
        Vector3 endPoint;
        if (target != null)
        {
            endPoint = target.position;
        }
        else
        {
            endPoint = transform.position + (transform.right * tentacleLength);
        }

        // Create a time-based sway using sine waves
        float timeOffset = Time.time * swaySpeed;

        // Generate the bezier curve points
        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)(segments - 1);

            // Create the basic bezier curve
            Vector3 point = Vector3.Lerp(transform.position, endPoint, t);

            // Add wave motion
            float waveOffset = Mathf.Sin((t * 4f) + (timeOffset * waveSpeed)) * waveHeight;

            // Perpendicular direction for the wave
            Vector3 perpendicular = Vector3.Cross(Vector3.forward, (endPoint - transform.position).normalized);

            // Add sway
            float sway = Mathf.Sin(timeOffset) * swayAmount * t; // More sway at the end

            // Combine wave and sway
            point += perpendicular * (waveOffset + sway);

            // Set the position
            positions[i] = point;
        }

        // Update the line renderer
        lineRenderer.SetPositions(positions);
    }
}