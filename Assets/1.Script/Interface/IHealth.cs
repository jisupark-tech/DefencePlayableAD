using UnityEngine;

public interface IHealth
{
    int GetCurrentHealth();

    int GetMaxHealth();

    void TakeDamage(int damage);

    void Heal(int amount);

    bool IsDead();

    void OnHealthChanged(int currentHealth, int maxHealth);

    void OnDeath();
    float GetHealthRatio();
    bool CanTargetable();
}