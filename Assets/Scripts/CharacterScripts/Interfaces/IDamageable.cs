public interface IDamageable
{
    float HP { get; set; }
    void TakeDamage(int dmg);
    void Die();
}
