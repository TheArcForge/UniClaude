using UnityEngine;

/// <summary>
/// Example player health system that demonstrates integration with HealthBarController.
/// Shows how to connect game mechanics with the UI using ArcForge patterns.
/// </summary>
public class PlayerHealthExample : MonoBehaviour
{
    [Header("Health System")]
    [SerializeField]
    [Tooltip("Reference to the health bar controller")]
    private HealthBarController _healthBarController;
    
    [SerializeField]
    [Tooltip("Health regeneration rate per second")]
    private float _healthRegenRate = 5f;
    
    [SerializeField]
    [Tooltip("Delay before health starts regenerating after taking damage")]
    private float _regenDelay = 3f;
    
    [Header("Testing")]
    [SerializeField]
    [Tooltip("Damage amount for testing (press Space)")]
    private float _testDamageAmount = 10f;
    
    [SerializeField]
    [Tooltip("Heal amount for testing (press H)")]
    private float _testHealAmount = 15f;

    private float _timeSinceLastDamage = 0f;

    void Start()
    {
        if (_healthBarController == null)
        {
            _healthBarController = FindObjectOfType<HealthBarController>();
            if (_healthBarController == null)
            {
                Debug.LogError("HealthBarController not found. Please assign it in the inspector or ensure one exists in the scene.");
                return;
            }
        }

        // Subscribe to health change events
        _healthBarController.OnHealthChanged += OnPlayerHealthChanged;
        
        // Set initial health
        _healthBarController.CurrentHealth = _healthBarController.MaxHealth;
    }

    void Update()
    {
        HandleInput();
        HandleHealthRegeneration();
    }

    void OnDestroy()
    {
        // Clean up event subscription
        if (_healthBarController != null)
        {
            _healthBarController.OnHealthChanged -= OnPlayerHealthChanged;
        }
    }

    /// <summary>
    /// Handles player input for testing health system
    /// </summary>
    private void HandleInput()
    {
        // Test damage with Space
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TakeDamage(_testDamageAmount);
        }
        
        // Test healing with H
        if (Input.GetKeyDown(KeyCode.H))
        {
            Heal(_testHealAmount);
        }
        
        // Full heal with F
        if (Input.GetKeyDown(KeyCode.F))
        {
            _healthBarController.FullHeal();
        }
        
        // Increase max health with Plus/Equals
        if (Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.Equals))
        {
            _healthBarController.MaxHealth += 25f;
            Debug.Log($"Max health increased to {_healthBarController.MaxHealth}");
        }
        
        // Decrease max health with Minus
        if (Input.GetKeyDown(KeyCode.Minus))
        {
            _healthBarController.MaxHealth = Mathf.Max(25f, _healthBarController.MaxHealth - 25f);
            Debug.Log($"Max health decreased to {_healthBarController.MaxHealth}");
        }
    }

    /// <summary>
    /// Handles automatic health regeneration
    /// </summary>
    private void HandleHealthRegeneration()
    {
        if (_healthBarController == null) return;

        _timeSinceLastDamage += Time.deltaTime;

        // Start regenerating health after delay
        if (_timeSinceLastDamage >= _regenDelay && 
            !_healthBarController.IsFullHealth() && 
            !_healthBarController.IsDead())
        {
            float regenAmount = _healthRegenRate * Time.deltaTime;
            _healthBarController.Heal(regenAmount);
        }
    }

    /// <summary>
    /// Applies damage to the player
    /// </summary>
    /// <param name="damage">Amount of damage to apply</param>
    public void TakeDamage(float damage)
    {
        if (_healthBarController == null) return;
        
        _healthBarController.TakeDamage(damage);
        _timeSinceLastDamage = 0f; // Reset regen timer
        
        Debug.Log($"Player took {damage} damage. Health: {_healthBarController.CurrentHealth:F1}/{_healthBarController.MaxHealth}");
    }

    /// <summary>
    /// Heals the player
    /// </summary>
    /// <param name="healAmount">Amount of health to restore</param>
    public void Heal(float healAmount)
    {
        if (_healthBarController == null) return;
        
        _healthBarController.Heal(healAmount);
        
        Debug.Log($"Player healed for {healAmount}. Health: {_healthBarController.CurrentHealth:F1}/{_healthBarController.MaxHealth}");
    }

    /// <summary>
    /// Called when player health changes
    /// </summary>
    /// <param name="currentHealth">Current health value</param>
    /// <param name="maxHealth">Maximum health value</param>
    /// <param name="percentage">Health percentage (0.0 to 1.0)</param>
    private void OnPlayerHealthChanged(float currentHealth, float maxHealth, float percentage)
    {
        // Handle health-based game logic here
        if (_healthBarController.IsDead())
        {
            HandlePlayerDeath();
        }
        else if (percentage <= 0.25f) // Low health warning
        {
            HandleLowHealth();
        }
    }

    /// <summary>
    /// Handles player death
    /// </summary>
    private void HandlePlayerDeath()
    {
        Debug.Log("Player has died!");
        // Add death logic here (disable controls, show death screen, etc.)
    }

    /// <summary>
    /// Handles low health state
    /// </summary>
    private void HandleLowHealth()
    {
        Debug.Log("Player health is critically low!");
        // Add low health effects here (screen flash, warning sounds, etc.)
    }
}
