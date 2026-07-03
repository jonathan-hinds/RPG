using System;
using UnityEngine;

namespace RPGClone.Player
{
    public interface IMMOPlayerLocomotionSource
    {
        float CurrentPlanarSpeed { get; }
        Vector3 CurrentPlanarVelocity { get; }
        float VerticalVelocity { get; }
        bool IsGrounded { get; }
        bool IsAirborne { get; }
        bool HasGroundContact { get; }
        Vector2 CurrentLocalPlanarVelocity { get; }

        event Action Jumped;
        event Action BecameAirborne;
        event Action Landed;
    }
}
