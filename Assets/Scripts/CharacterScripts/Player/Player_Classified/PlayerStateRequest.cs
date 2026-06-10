[System.Flags]
public enum PlayerStateRequest
{
    None = 0,
    Attack = 1 << 0,
    Knockback = 1 << 1,
    Interact = 1 << 2,
    Jump = 1 << 3,
    Dash = 1 << 4,
    Idle = 1 << 5,
}
