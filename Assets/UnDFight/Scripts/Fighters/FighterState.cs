namespace UnDFight
{
    public enum FighterState
    {
        Idle = 0,
        WalkForward = 1,
        WalkBackward = 2,
        Jump = 3,
        Crouch = 4,
        Block = 5,
        Attack = 6,
        Hit = 7,
        KnockDown = 8,
        GetUp = 9,
        Victory = 10,
        Defeat = 11,
        DashForward = 12,
        DashBackward = 13
    }

    public enum AttackType
    {
        None = 0,
        LightPunch = 1,
        HeavyPunch = 2,
        LightKick = 3,
        HeavyKick = 4,
        Special = 5,
        CrouchKick = 6,
        JumpAttack = 7,
        Throw = 8
    }
}
