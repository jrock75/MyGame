using System;
using UnityEngine;

namespace MyGame.MyClient
{
    public sealed class UnityInputSource : IInputSource, IDisposable
    {
        private readonly PlayerInputActions actions;

        public UnityInputSource()
        {
            actions = new PlayerInputActions();
            actions.Enable();
        }

        public Vector2 ReadMove()
        {
            return actions.Player.Move.ReadValue<Vector2>();
        }

        public void Dispose()
        {
            actions.Disable();
            actions.Dispose();
        }
    }
}