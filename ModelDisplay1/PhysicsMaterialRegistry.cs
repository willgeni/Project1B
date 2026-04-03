using BepuPhysics;
using System.Collections.Generic;

namespace ModelDisplay1
{
    public struct PhysicsMaterial
    {
        public float SpringFrequency;
        public float SpringDamping;
        public float Friction;
    }

    public class PhysicsMaterialRegistry
    {
        // Dictionary mapping material IDs to physics materials
        public Dictionary<int, PhysicsMaterial> Materials = new();
        public PhysicsMaterial DefaultMaterial = new()
        {
            SpringFrequency = 30,
            SpringDamping = 1,
            Friction = 1
        };
    }
}