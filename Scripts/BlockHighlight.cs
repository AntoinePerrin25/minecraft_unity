using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockHighlight : MonoBehaviour
{
    [Header("Visual Settings")]
    public Color highlightColor = new Color(1f, 1f, 1f, 0.3f);
    public float pulseSpeed = 1.5f;
    public float minAlpha = 0.2f;
    public float maxAlpha = 0.5f;
    public bool enablePulse = true;

    private Renderer blockRenderer;
    private Material highlightMaterial;
    private float pulseTimer = 0f;

    void Awake()
    {
        blockRenderer = GetComponent<Renderer>();
        
        // Create a new material instance to avoid affecting other objects using the same material
        if (blockRenderer != null && blockRenderer.material != null)
        {
            highlightMaterial = new Material(blockRenderer.material);
            highlightMaterial.color = highlightColor;
            blockRenderer.material = highlightMaterial;
        }
    }

    void Update()
    {
        if (enablePulse && highlightMaterial != null)
        {
            // Pulse the alpha value of the highlight
            pulseTimer += Time.deltaTime * pulseSpeed;
            if (pulseTimer > Mathf.PI * 2)
            {
                pulseTimer -= Mathf.PI * 2;
            }
            
            // Calculate alpha based on sine wave
            float alphaValue = Mathf.Lerp(minAlpha, maxAlpha, (Mathf.Sin(pulseTimer) + 1) * 0.5f);
            
            // Apply the new alpha to the material
            Color newColor = highlightMaterial.color;
            newColor.a = alphaValue;
            highlightMaterial.color = newColor;
        }
    }

    // Method to update the highlight position
    public void SetPosition(Vector3 position)
    {
        transform.position = position;
    }
}
