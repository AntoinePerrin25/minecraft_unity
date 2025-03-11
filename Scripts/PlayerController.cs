using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 6.0f;
    public float runSpeed = 10.0f;
    public float jumpForce = 8.0f;
    public float gravity = 20.0f;
    public float mouseSensitivity = 2.0f;
    public float interactionRange = 5.0f;
    
    [Header("Block Interaction")]
    public float breakCooldown = 0.25f;
    public float placeCooldown = 0.25f;
    public GameObject blockHighlight;
    
    private CharacterController controller;
    private Camera playerCamera;
    private Vector3 moveDirection = Vector3.zero;
    private float rotationX = 0.0f;
    private bool isGrounded;
    
    private float breakTimer;
    private float placeTimer;
    
    // For block interaction
    private Vector3? highlightedBlockPos;
    private Vector3? adjacentBlockPos;
    private BlockType selectedBlockType = BlockType.Dirt; // Default block to place
    private BlockHighlight blockHighlightScript;
    
    void Start()
    {
        controller = GetComponent<CharacterController>();
        playerCamera = GetComponentInChildren<Camera>();
        
        if (playerCamera == null)
        {
            Debug.LogError("No camera found as child of player!");
        }
        
        // Lock and hide cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        breakTimer = 0f;
        placeTimer = 0f;

        // Get the BlockHighlight script
        if (blockHighlight != null)
        {
            blockHighlightScript = blockHighlight.GetComponent<BlockHighlight>();
            blockHighlight.SetActive(false);
        }
    }
    
    void Update()
    {
        // Detect if player is grounded
        isGrounded = controller.isGrounded;
        
        // Handle player movement
        HandleMovement();
        
        // Handle camera rotation
        HandleCameraRotation();
        
        // Handle block interaction
        HandleBlockInteraction();
        
        // Handle block selection (will be replaced by hotbar later)
        HandleBlockSelection();
    }
    
    void HandleMovement()
    {
        // Get input axes
        float moveHorizontal = Input.GetAxis("Horizontal");
        float moveVertical = Input.GetAxis("Vertical");
        
        // Calculate movement vector based on input
        Vector3 moveInput = transform.right * moveHorizontal + transform.forward * moveVertical;
        
        // Determine if player is sprinting
        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
        
        // Apply movement
        if (isGrounded)
        {
            moveDirection = moveInput * currentSpeed;
            
            // Handle jumping
            if (Input.GetButtonDown("Jump"))
            {
                moveDirection.y = jumpForce;
            }
        }
        else
        {
            // In air, maintain horizontal velocity
            moveDirection.x = moveInput.x * currentSpeed;
            moveDirection.z = moveInput.z * currentSpeed;
        }
        
        // Apply gravity
        moveDirection.y -= gravity * Time.deltaTime;
        
        // Move the character
        controller.Move(moveDirection * Time.deltaTime);
    }
    
    void HandleCameraRotation()
    {
        // Get mouse inputs
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        
        // Rotate the camera vertically (look up/down)
        rotationX -= mouseY;
        rotationX = Mathf.Clamp(rotationX, -90f, 90f);
        playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
        
        // Rotate the player horizontally (turn left/right)
        transform.rotation *= Quaternion.Euler(0, mouseX, 0);
    }
    
    void HandleBlockInteraction()
    {
        // Reset highlight position
        highlightedBlockPos = null;
        adjacentBlockPos = null;
        
        // Hide block highlight by default
        if (blockHighlight != null)
        {
            blockHighlight.SetActive(false);
        }
        
        // Raycast to detect blocks
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit, interactionRange))
        {
            // Get chunk from hit object
            ChunkMeshGenerator chunk = hit.collider.GetComponent<ChunkMeshGenerator>();
            if (chunk != null)
            {
                // Calculate the block position that was hit
                Vector3 hitPoint = hit.point - hit.normal * 0.01f; // Small offset inside the block
                Vector3Int blockPos = new Vector3Int(
                    Mathf.FloorToInt(hitPoint.x),
                    Mathf.FloorToInt(hitPoint.y),
                    Mathf.FloorToInt(hitPoint.z)
                );
                
                // Store the position of the highlighted block
                highlightedBlockPos = blockPos;
                
                // Calculate adjacent block position (for placing)
                Vector3 adjacentPoint = hit.point + hit.normal * 0.01f; // Small offset outside the block
                Vector3Int adjBlockPos = new Vector3Int(
                    Mathf.FloorToInt(adjacentPoint.x),
                    Mathf.FloorToInt(adjacentPoint.y),
                    Mathf.FloorToInt(adjacentPoint.z)
                );
                
                // Store the position of the adjacent block (for block placement)
                adjacentBlockPos = adjBlockPos;
                
                // Update block highlight position
                if (blockHighlight != null)
                {
                    Vector3 highlightPosition = blockPos + new Vector3(0.5f, 0.5f, 0.5f); // Center of block
                    blockHighlight.transform.position = highlightPosition;
                    blockHighlight.SetActive(true);
                    
                    // Set the face direction if we have the script
                    if (blockHighlightScript != null)
                    {
                        blockHighlightScript.SetPosition(highlightPosition);
                    }
                }
                
                // Break block
                if (Input.GetMouseButton(0) && breakTimer <= 0)
                {
                    BreakBlock(blockPos);
                    breakTimer = breakCooldown;
                }
                
                // Place block
                if (Input.GetMouseButton(1) && placeTimer <= 0)
                {
                    PlaceBlock(adjBlockPos, selectedBlockType);
                    placeTimer = placeCooldown;
                }
            }
        }
        
        // Update cooldown timers
        if (breakTimer > 0)
            breakTimer -= Time.deltaTime;
            
        if (placeTimer > 0)
            placeTimer -= Time.deltaTime;
    }
    
    void HandleBlockSelection()
    {
        // Temporary block selection via number keys
        // This will be replaced by proper hotbar later
        if (Input.GetKeyDown(KeyCode.Alpha1))
            selectedBlockType = BlockType.Dirt;
        else if (Input.GetKeyDown(KeyCode.Alpha2))
            selectedBlockType = BlockType.Grass;
        else if (Input.GetKeyDown(KeyCode.Alpha3))
            selectedBlockType = BlockType.Stone;
        else if (Input.GetKeyDown(KeyCode.Alpha4))
            selectedBlockType = BlockType.Cobblestone;
        else if (Input.GetKeyDown(KeyCode.Alpha5))
            selectedBlockType = BlockType.Wood;
        else if (Input.GetKeyDown(KeyCode.Alpha6))
            selectedBlockType = BlockType.Leaves;
    }
    
    void BreakBlock(Vector3 blockPos)
    {
        // Find the chunk containing this block
        int chunkX = Mathf.FloorToInt(blockPos.x / 16);
        int chunkZ = Mathf.FloorToInt(blockPos.z / 16);
        
        // Find world generator using non-deprecated method
        ChunkGenerator worldGenerator = FindAnyObjectByType<ChunkGenerator>();
        
        if (worldGenerator != null)
        {
            // Call a method to break the block at this position
            StartCoroutine(worldGenerator.BreakBlockAt(new Vector3Int(
                Mathf.FloorToInt(blockPos.x), 
                Mathf.FloorToInt(blockPos.y),
                Mathf.FloorToInt(blockPos.z)
            )));
        }
    }
    
    void PlaceBlock(Vector3? position, BlockType blockType)
    {
        // Don't place if there's no valid position
        if (!position.HasValue)
            return;
            
        Vector3 blockPos = position.Value;
        
        // Don't place blocks where the player is standing
        Bounds playerBounds = controller.bounds;
        Vector3 blockCenter = new Vector3(
            blockPos.x + 0.5f,
            blockPos.y + 0.5f,
            blockPos.z + 0.5f
        );
        
        // Create a block bounds
        Bounds blockBounds = new Bounds(blockCenter, Vector3.one);
        
        // Check if player is inside the block position
        if (playerBounds.Intersects(blockBounds))
            return;
            
        // Find world generator using non-deprecated method
        ChunkGenerator worldGenerator = FindAnyObjectByType<ChunkGenerator>();
        
        if (worldGenerator != null)
        {
            // Call a method to place the block at this position
            StartCoroutine(worldGenerator.PlaceBlockAt(new Vector3Int(
                Mathf.FloorToInt(blockPos.x), 
                Mathf.FloorToInt(blockPos.y),
                Mathf.FloorToInt(blockPos.z)
            ), blockType));
        }
    }
}
