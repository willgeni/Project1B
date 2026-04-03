using BepuPhysics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace ModelDisplay1
{
    public class PhysicsObject
    {
        public Model Model { get; private set; }
        public BodyHandle? BodyHandle { get; private set; }
        public StaticHandle? StaticHandle { get; private set; }
        private Simulation _simulation;
        private PhysicsMaterialRegistry _materials;
        // Wireframe for debugging/visualization
        public List<System.Numerics.Vector3> DebugVertices { get; private set; }
        public List<int> DebugIndices { get; private set; }
        // Constructor for Dynamic bodies (the ship)
        public PhysicsObject(Model model, BodyHandle handle, Simulation sim, PhysicsMaterialRegistry mat)
        {
            Model = model;
            BodyHandle = handle;
            _simulation = sim;
            _materials = mat;
        }
        // Constructor for Static bodies (the rings)
        public PhysicsObject(Model model, StaticHandle handle, Simulation sim, PhysicsMaterialRegistry mat)
        {
            Model = model;
            StaticHandle = handle;
            _simulation = sim;
            _materials = mat;
        }

        public void SetPhysicsShapeData(List<System.Numerics.Vector3> vertices, List<int> indices)
        {
            DebugVertices = vertices;
            DebugIndices = indices;
        }

        public Matrix GetWorldMatrix()
        {
            BepuPhysics.RigidPose pose;
            if (BodyHandle.HasValue)
                pose = _simulation.Bodies[BodyHandle.Value].Pose;
            else
                pose = _simulation.Statics[StaticHandle.Value].Pose;

            Matrix rotation = Matrix.CreateFromQuaternion(new Quaternion(pose.Orientation.X, pose.Orientation.Y, pose.Orientation.Z, pose.Orientation.W));
            Matrix translation = Matrix.CreateTranslation(new Vector3(pose.Position.X, pose.Position.Y, pose.Position.Z));

            return rotation * translation;
        }
    }
}