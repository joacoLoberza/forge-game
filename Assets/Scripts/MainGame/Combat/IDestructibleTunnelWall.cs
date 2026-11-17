using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    public enum TunnelWallType
    {
        Web,
        WoodPlanks,
        BrokenBricks
    }

    /// <summary>
    /// Interfaz para paredes destruibles que bloquean los túneles de la mina y responden
    /// con multiplicadores de daño específicos según el tipo de arma equipada.
    /// </summary>
    public interface IDestructibleTunnelWall : IDamageable
    {
        TunnelWallType WallType { get; }
        WeaponType EffectiveWeapon { get; }
        void TakeWeaponDamage(int baseDamage, WeaponType? weaponType, GameObject attacker);
    }
}

