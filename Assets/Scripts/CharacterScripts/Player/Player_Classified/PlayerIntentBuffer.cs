using UnityEngine;

public class PlayerIntentBuffer
{
    public PlayerStateRequest Current { get; private set; }

    public void Add(PlayerStateRequest intent)
    {
        Current |= intent;
    }

    public bool Has(PlayerStateRequest intent)
    {
        return (Current & intent) != 0;
    }

    public void Clear()
    {
        Current = PlayerStateRequest.None;
    }
}
