using UnityEngine;

public class BlockHighlight : MonoBehaviour
{
    [Header("Highlight Settings")]
    public Color highlightColor = new Color(1f, 1f, 1f, 0.3f);
    public float pulseSpeed = 1.5f;
    public float minOpacity = 0.2f;
    public float maxOpacity = 0.4f;

    private Renderer highlightRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Vector3 currentPosition;
    private Vector3 faceNormal = Vector3.zero;

    void Awake()
    {
        // Get the renderer component
        highlightRenderer = GetComponent<Renderer>();
        
        // Initialize property block for efficient material property manipulation
        propertyBlock = new MaterialPropertyBlock();
    }

    void Update()
    {
        // Create a pulsing effect by changing the alpha over time
        float pulse = Mathf.Lerp(minOpacity, maxOpacity, (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f);
        
        Color pulsingColor = highlightColor;
        pulsingColor.a = pulse;
        
        // Update the material color with the pulsing effect
        propertyBlock.SetColor("_BaseColor", pulsingColor);
        highlightRenderer.SetPropertyBlock(propertyBlock);
    }

    public void SetPosition(Vector3 position)
    {
        currentPosition = position;
        transform.position = position;
    }

    public void SetFaceNormal(Vector3 normal)
    {
        // Store the normal of the face being looked at
        faceNormal = normal;
        
        // If you want to highlight just the face, you could adjust scale and position here
        // For example, for a specific face highlight (more advanced):
        
        // Reset scale to 1
        transform.localScale = Vector3.one;
        
        // Adjust position based on which face is being highlighted
        // This pushes the highlight slightly towards the face being looked at
        transform.position = currentPosition + normal * 0.01f;
        
        // Scale down on the axis perpendicular to the face normal
        if (Mathf.Abs(normal.x) > 0.5f)
        {
            // For X-facing faces, make the highlight flat on the X axis
            transform.localScale = new Vector3(1.01f, 1.01f, 1.01f);
        }
        else if (Mathf.Abs(normal.y) > 0.5f)
        {
            // For Y-facing faces, make the highlight flat on the Y axis
            transform.localScale = new Vector3(1.01f, 1.01f, 1.01f);

        }
        else if (Mathf.Abs(normal.z) > 0.5f)
        {
            // For Z-facing faces, make the highlight flat on the Z axis
            transform.localScale = new Vector3(1.01f, 1.01f, 1.01f);

        }
    }
}
