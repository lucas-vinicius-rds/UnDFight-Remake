namespace UnDFight
{
    public enum FighterInputButton
    {
        LightPunch = 0,
        HeavyPunch = 1,
        LightKick = 2,
        HeavyKick = 3,
        Block = 4,
        Special = 5
    }

    public enum InputAssignmentMode
    {
        Auto = 0,
        KeyboardOnly = 1,
        Gamepad0 = 2,
        Gamepad1 = 3
    }

    public struct FighterInputState
    {
        public float MoveX;
        public float MoveY;
        public bool HoldBlock;
        public int LastTickSampled;
    }
}
