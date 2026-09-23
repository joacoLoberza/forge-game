namespace ForjaGame.Combat
{
    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(int amount);
    }
}
