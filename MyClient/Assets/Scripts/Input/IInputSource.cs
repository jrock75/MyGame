using UnityEngine;

namespace MyGame.MyClient
{
    public interface IInputSource
    {
        Vector2 ReadMove();
    }
}