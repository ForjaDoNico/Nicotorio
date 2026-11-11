using System;
using Factory.Simulation;
using UnityEngine;

namespace Factory.Presentation
{
    /// <summary>
    /// Drives World.Tick() at a fixed WorldConfig.TicksPerSecond, independent of frame rate.
    /// Input handlers subscribe to BeforeTick to turn held input into per-tick commands.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class SimulationRunner : MonoBehaviour
    {
        public const float TickInterval = 1f / WorldConfig.TicksPerSecond;
        private const int MaxTicksPerFrame = 5; // after a hitch, drop time rather than spiral

        private World _world;
        private float _accumulator;

        /// <summary>Raised right before each Tick(), once per tick.</summary>
        public event Action BeforeTick;

        /// <summary>Raised right after each Tick().</summary>
        public event Action AfterTick;

        /// <summary>0..1 progress toward the next tick, for smoothing visuals between ticks.</summary>
        public float Alpha => Mathf.Clamp01(_accumulator / TickInterval);

        public void Init(World world)
        {
            _world = world;
        }

        private void Update()
        {
            if (_world == null) return;
            _accumulator += Time.deltaTime;
            int ticks = 0;
            while (_accumulator >= TickInterval && ticks < MaxTicksPerFrame)
            {
                BeforeTick?.Invoke();
                _world.Tick();
                AfterTick?.Invoke();
                _accumulator -= TickInterval;
                ticks++;
            }
            if (ticks == MaxTicksPerFrame) _accumulator = Mathf.Min(_accumulator, TickInterval);
        }
    }
}
