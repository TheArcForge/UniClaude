using UnityEngine;
using UnityEngine.UIElements;
using ArcForge.UI;
using ArcForge.UI.Components;
using ArcForge.Reactive;
using System.Collections.Generic;
using System;

/// <summary>
/// Health bar UI controller that displays and manages player health using ArcForge reactive patterns.
/// Follows the MonoBehaviour bridge pattern to connect game logic with UI components.
/// </summary>
public class HealthBarController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] 
    [Tooltip("UI Document containing the health bar container")]
    private UIDocument _doc;
    
    [Header("Health Settings")]
    [SerializeField]
    [Tooltip("Maximum health value")]
    private float _maxHealth = 100f;
    
    [SerializeField]
    [Tooltip("Starting health value")]
    private float _startingHealth = 100f;
    
    [Header("Visual Settings")]
    [SerializeField]
    [Tooltip("Label text for the health bar")]
    private string _label = "Health";
    
    [SerializeField]
    [Tooltip("Container name in UXML where health bar will be added")]
    private string _containerName = "health-container";

    /// <summary>
    /// Observable health value that drives the UI reactively
    /// </summary>
    private Observable<float> _currentHealth;
    
    /// <summary>
    /// Observable maximum health value for dynamic max health changes
    /// </summary>
    private Observable<float> _maxHealthObservable;
    
    /// <summary>
    /// List of disposable subscriptions for cleanup
    /// </summary>
    private List<IDisposable> _subscriptions = new List<IDisposable>();
    
    /// <summary>
    /// The health bar component instance
    /// </summary>
    private ProgressBar _healthBar;

    /// <summary>
    /// Gets or sets the current health value. Setting this value will update the UI automatically.
    /// </summary>
    public float CurrentHealth
    {
        get => _currentHealth?.Value ?? 0f;
        set => _currentHealth.Value = Mathf.Clamp(value, 0f, MaxHealth);
    }

    /// <summary>
    /// Gets or sets the maximum health value. Setting this value will update the UI automatically.
    /// </summary>
    public float MaxHealth
    {
        get => _maxHealthObservable?.Value ?? _maxHealth;
        set
        {
            _maxHealth = value;
            if (_maxHealthObservable != null)
            {
                _maxHealthObservable.Value = value;
                // Clamp current health to new max
                CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, value);
            }
        }
    }

    /// <summary>
    /// Gets the current health as a percentage (0.0 to 1.0)
    /// </summary>
    public float HealthPercentage => MaxHealth > 0 ? CurrentHealth / MaxHealth : 0f;

    /// <summary>
    /// Event invoked when health changes. Passes (currentHealth, maxHealth, percentage)
    /// </summary>
    public event System.Action<float, float, float> OnHealthChanged;

    void OnEnable()
    {
        InitializeUI();
    }

    void OnDisable()
    {
        CleanupUI();
    }

    /// <summary>
    /// Initializes the health bar UI components and reactive bindings
    /// </summary>
    private void InitializeUI()
    {
        if (_doc == null)
        {
            Debug.LogError($"UIDocument not assigned on {gameObject.name}");
            return;
        }

        var root = _doc.rootVisualElement;
        var container = root.Q(_containerName);
        
        if (container == null)
        {
            Debug.LogError($"Container '{_containerName}' not found in UXML");
            return;
        }

        // Initialize observables
        _currentHealth = new Observable<float>(_startingHealth);
        _maxHealthObservable = new Observable<float>(_maxHealth);

        // Create health bar with reactive bindings
        _healthBar = new ProgressBar(_currentHealth.Value, _maxHealthObservable.Value)
            .Label(_label)
            .Bind(_currentHealth);

        // Bind max health changes
        var maxHealthSubscription = _maxHealthObservable.OnChanged(newMax =>
        {
            // Recreate the progress bar with new max value
            container.Clear();
            _healthBar = new ProgressBar(_currentHealth.Value, newMax)
                .Label(_label)
                .Bind(_currentHealth);
            container.Add(_healthBar);
        });
        _subscriptions.Add(maxHealthSubscription);

        // Subscribe to health changes for events
        var healthSubscription = _currentHealth.OnChanged(newHealth =>
        {
            OnHealthChanged?.Invoke(newHealth, MaxHealth, HealthPercentage);
        });
        _subscriptions.Add(healthSubscription);

        // Add to container
        container.Add(_healthBar);
    }

    /// <summary>
    /// Cleans up UI components and disposes subscriptions
    /// </summary>
    private void CleanupUI()
    {
        // Dispose manual subscriptions
        foreach (var subscription in _subscriptions)
        {
            subscription?.Dispose();
        }
        _subscriptions.Clear();

        // Clear UI (bound subscriptions auto-dispose when component detaches)
        if (_doc?.rootVisualElement != null)
        {
            var container = _doc.rootVisualElement.Q(_containerName);
            container?.Clear();
        }

        _healthBar = null;
        _currentHealth = null;
        _maxHealthObservable = null;
    }

    /// <summary>
    /// Damages the player by the specified amount
    /// </summary>
    /// <param name="damage">Amount of damage to apply</param>
    public void TakeDamage(float damage)
    {
        CurrentHealth -= damage;
    }

    /// <summary>
    /// Heals the player by the specified amount
    /// </summary>
    /// <param name="healAmount">Amount of health to restore</param>
    public void Heal(float healAmount)
    {
        CurrentHealth += healAmount;
    }

    /// <summary>
    /// Sets health to full
    /// </summary>
    public void FullHeal()
    {
        CurrentHealth = MaxHealth;
    }

    /// <summary>
    /// Checks if the player is at full health
    /// </summary>
    /// <returns>True if current health equals max health</returns>
    public bool IsFullHealth()
    {
        return Mathf.Approximately(CurrentHealth, MaxHealth);
    }

    /// <summary>
    /// Checks if the player is dead (health at or below 0)
    /// </summary>
    /// <returns>True if health is 0 or below</returns>
    public bool IsDead()
    {
        return CurrentHealth <= 0f;
    }
}
