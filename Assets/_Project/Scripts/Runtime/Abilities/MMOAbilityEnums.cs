namespace RPGClone.Abilities
{
    public enum MMOAbilityTargetType
    {
        Self = 0,
        Friendly = 1,
        Hostile = 2,
        AnyCharacter = 3,
        GroundArea = 4
    }

    public enum MMOAbilityEffectType
    {
        Damage = 0,
        Heal = 1,
        TemporaryStatModifier = 2,
        Charge = 3,
        PeriodicDamage = 4
    }

    public enum MMOAbilityAmountSource
    {
        Flat = 0,
        WeaponDamage = 1,
        AttackPower = 2,
        SpellPower = 3
    }

    public enum MMODamageSchool
    {
        Physical = 0,
        Fire = 1,
        Frost = 2,
        Nature = 3,
        Shadow = 4,
        Arcane = 5,
        Holy = 6
    }

    public enum MMOAbilityAreaTargetFilter
    {
        Hostile = 0,
        Friendly = 1,
        AnyCharacter = 2
    }
}
