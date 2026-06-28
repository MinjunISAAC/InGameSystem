using System;
using System.Collections.Generic;

namespace InGameSystem
{
    [Serializable]
    public class WaveData
    {
        public int Wave;
        public float StartTime;   // accumulated play time at which this wave begins
        public float Duration;    // seconds the wave lasts
        public float SpawnDelay;  // seconds between individual enemy spawns
        public List<int> EnemyIds = new();
    }
}
