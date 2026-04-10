using BepuPhysics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ModelDisplay1
{
    public class RaceRing
    {
        public PhysicsObject PhysicsBody { get; private set; }
        public Vector3 Position { get; private set; }
        public float Radius { get; private set; }
        public bool IsNext { get; set; }
        public bool WasCollected { get; set; }

        public RaceRing(PhysicsObject physicsBody, Vector3 position, float radius)
        {
            PhysicsBody = physicsBody;
            Position = position;
            Radius = radius;
            IsNext = false;
            WasCollected = false;
        }

        public void Draw(Matrix view, Matrix projection, Vector3 cameraPosition)
        {
            if (WasCollected) return; // Don't draw if already collected
            foreach (ModelMesh mesh in PhysicsBody.Model.Meshes)
            {
                foreach (BasicEffect effect in mesh.Effects)
                {
                    effect.EnableDefaultLighting();
                    effect.World = Matrix.CreateTranslation(Position);
                    effect.View = view;
                    effect.Projection = projection;

                    // Accet color for the next ring to collect
                    if (IsNext)
                    {
                        effect.EmissiveColor = new Vector3(0, 1, 0); // Bright green glow
                        effect.DiffuseColor = new Vector3(0, 1, 0);
                    } else
                    {
                        effect.EmissiveColor = Vector3.Zero; // No glow
                        effect.DiffuseColor = new Vector3(0.8f, 0.8f, 0.8f); // Normal color
                    }
                }
                mesh.Draw();
            }
        }
    }
}