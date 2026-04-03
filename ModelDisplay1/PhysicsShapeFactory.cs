using BepuPhysics;
using BepuPhysics.Collidables;
using BepuUtilities;
using BepuUtilities.Memory;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace ModelDisplay1
{
    /// <summary>
    /// Factory for creating BepuPhysics shapes from MonoGame models.
    /// </summary>
    public static class PhysicsShapeFactory
    {
        /// <summary>
        /// Creates a dynamic physics body from a MonoGame model.
        /// Attempts compound convex first, falls back to convex hull of all vertices.
        /// </summary>
        /// <param name="model">The MonoGame model.</param>
        /// <param name="simulation">The BepuPhysics simulation.</param>
        /// <param name="bufferPool">The buffer pool for memory allocation.</param>
        /// <param name="materials">The physics material registry.</param>
        /// <param name="position">Initial position.</param>
        /// <param name="mass">Mass of the body.</param>
        /// <param name="forceSingleHull">If true, forces single convex hull instead of compound (mesh not supported for dynamic bodies).</param>
        /// <param name="usedFallback">True if fell back to single convex hull.</param>
        public static PhysicsObject CreateDynamicPhysicsObject(
            Model model,
            Simulation simulation,
            BufferPool bufferPool,
            PhysicsMaterialRegistry materials,
            Vector3 position,
            float mass,
            bool forceSingleHull,
            out bool usedFallback)
        {
            var verticesByMesh = ExtractVerticesByMesh(model);

            // Try compound convex (one hull per mesh part) unless forced to use single hull
            if (!forceSingleHull && verticesByMesh.Count > 1 && TryCreateCompoundConvex(verticesByMesh, simulation, bufferPool, position, mass, out var bodyHandle))
            {
                usedFallback = false;
                var physicsObject = new PhysicsObject(model, bodyHandle, simulation, materials);
                ExtractCompoundConvexWireframeData(verticesByMesh, out var wireVerts, out var wireIndices);
                physicsObject.SetPhysicsShapeData(wireVerts, wireIndices);
                return physicsObject;
            }

            // Fall back to single convex hull from all vertices
            // Note: BepuPhysics does not support mesh shapes for dynamic bodies
            var allVertices = ExtractAllVertices(model);
            bodyHandle = CreateConvexHullBody(allVertices, simulation, bufferPool, position, mass);
            var physicsObject2 = new PhysicsObject(model, bodyHandle, simulation, materials);
            ExtractConvexHullWireframeData(allVertices, out var wireVerts2, out var wireIndices2);
            physicsObject2.SetPhysicsShapeData(wireVerts2, wireIndices2);
            usedFallback = true;
            return physicsObject2;
        }

        /// <summary>
        /// Creates a static physics body from a MonoGame model.
        /// Attempts compound convex first, falls back to mesh shape.
        /// </summary>
        /// <param name="model">The MonoGame model.</param>
        /// <param name="simulation">The BepuPhysics simulation.</param>
        /// <param name="bufferPool">The buffer pool for memory allocation.</param>
        /// <param name="materials">The physics material registry.</param>
        /// <param name="position">Position of the static body.</param>
        /// <param name="forceMesh">If true, forces mesh shape instead of compound convex.</param>
        /// <param name="usedMesh">True if used mesh shape.</param>
        public static PhysicsObject CreateStaticPhysicsObject(
            Model model,
            Simulation simulation,
            BufferPool bufferPool,
            PhysicsMaterialRegistry materials,
            Vector3 position,
            bool forceMesh,
            out bool usedMesh)
        {
            var verticesByMesh = ExtractVerticesByMesh(model);

            // Try compound convex (one hull per mesh part) unless forced to use mesh
            if (!forceMesh && TryCreateStaticCompoundConvex(verticesByMesh, simulation, bufferPool, position, out var staticHandle))
            {
                usedMesh = false;
                var physicsObject = new PhysicsObject(model, staticHandle, simulation, materials);
                
                // Set wireframe data for compound convex visualization
                ExtractCompoundConvexWireframeData(verticesByMesh, out var wireVerts, out var wireIndices);
                physicsObject.SetPhysicsShapeData(wireVerts, wireIndices);
                
                return physicsObject;
            }

            // Fall back to mesh shape
            var triangles = ExtractTriangles(model);
            staticHandle = CreateMeshStatic(triangles, simulation, bufferPool, position);
            var physicsObject2 = new PhysicsObject( model, staticHandle, simulation, materials);
            
            // Set wireframe data for visualization
            ExtractMeshWireframeData(triangles, out var wireVerts2, out var wireIndices2);
            physicsObject2.SetPhysicsShapeData(wireVerts2, wireIndices2);
            
            usedMesh = true;
            return physicsObject2;
        }

        #region Vertex Extraction

        private static List<List<Vector3>> ExtractVerticesByMesh(Model model)
        {
            var result = new List<List<Vector3>>();

            foreach (var mesh in model.Meshes)
            {
                var meshVertices = new List<Vector3>();
                // Get the bone transform for this mesh
                var boneTransform = mesh.ParentBone.Transform;
                
                foreach (var part in mesh.MeshParts)
                {
                    var vertices = ExtractPartVertices(part);
                    // Transform each vertex by the bone transform
                    foreach (var vertex in vertices)
                    {
                        var transformed = Microsoft.Xna.Framework.Vector3.Transform(
                            new Microsoft.Xna.Framework.Vector3(vertex.X, vertex.Y, vertex.Z),
                            boneTransform);
                        meshVertices.Add(new Vector3(transformed.X, transformed.Y, transformed.Z));
                    }
                }
                if (meshVertices.Count >= 4)
                    result.Add(meshVertices);
            }

            return result;
        }

        private static List<Vector3> ExtractAllVertices(Model model)
        {
            var vertices = new List<Vector3>();

            foreach (var mesh in model.Meshes)
            {
                var boneTransform = mesh.ParentBone.Transform;
                
                foreach (var part in mesh.MeshParts)
                {
                    var partVertices = ExtractPartVertices(part);
                    foreach (var vertex in partVertices)
                    {
                        var transformed = Microsoft.Xna.Framework.Vector3.Transform(
                            new Microsoft.Xna.Framework.Vector3(vertex.X, vertex.Y, vertex.Z),
                            boneTransform);
                        vertices.Add(new Vector3(transformed.X, transformed.Y, transformed.Z));
                    }
                }
            }

            return vertices;
        }

        private static List<Triangle> ExtractTriangles(Model model)
        {
            var triangles = new List<Triangle>();

            foreach (var mesh in model.Meshes)
            {
                var boneTransform = mesh.ParentBone.Transform;
                
                foreach (var part in mesh.MeshParts)
                {
                    var vertices = ExtractPartVertices(part);
                    var indices = ExtractIndices(part);

                    // Transform vertices by bone transform
                    var transformedVertices = new Vector3[vertices.Length];
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        var transformed = Microsoft.Xna.Framework.Vector3.Transform(
                            new Microsoft.Xna.Framework.Vector3(vertices[i].X, vertices[i].Y, vertices[i].Z),
                            boneTransform);
                        transformedVertices[i] = new Vector3(transformed.X, transformed.Y, transformed.Z);
                    }

                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        triangles.Add(new Triangle(
                            transformedVertices[indices[i]],
                            transformedVertices[indices[i + 1]],
                            transformedVertices[indices[i + 2]]));
                    }
                }
            }

            return triangles;
        }

        private static int[] ExtractIndices(ModelMeshPart part)
        {
            var indexBuffer = part.IndexBuffer;
            var indexCount = part.PrimitiveCount * 3;
            var indices = new int[indexCount];

            if (indexBuffer.IndexElementSize == IndexElementSize.SixteenBits)
            {
                var shortIndices = new short[indexCount];
                indexBuffer.GetData(part.StartIndex * 2, shortIndices, 0, indexCount);
                for (int i = 0; i < indexCount; i++)
                    indices[i] = shortIndices[i];
            }
            else
            {
                indexBuffer.GetData(part.StartIndex * 4, indices, 0, indexCount);
            }

            return indices;
        }

        /// <summary>
        /// Extracts vertices and indices for wireframe rendering from a convex hull.
        /// </summary>
        public static void ExtractConvexHullWireframeData(
            List<Vector3> hullPoints,
            out List<Vector3> vertices,
            out List<int> indices)
        {
            vertices = new List<Vector3>(hullPoints);
            indices = new List<int>();

            // Simple triangulation from center for visualization
            if (hullPoints.Count >= 3)
            {
                // Create triangle fan from first vertex
                for (int i = 1; i < hullPoints.Count - 1; i++)
                {
                    indices.Add(0);
                    indices.Add(i);
                    indices.Add(i + 1);
                }
            }
        }

        /// <summary>
        /// Extracts wireframe data from triangles list.
        /// </summary>
        public static void ExtractMeshWireframeData(
            List<Triangle> triangles,
            out List<Vector3> vertices,
            out List<int> indices)
        {
            vertices = new List<Vector3>();
            indices = new List<int>();

            foreach (var tri in triangles)
            {
                int baseIndex = vertices.Count;
                vertices.Add(tri.A);
                vertices.Add(tri.B);
                vertices.Add(tri.C);
                indices.Add(baseIndex);
                indices.Add(baseIndex + 1);
                indices.Add(baseIndex + 2);
            }
        }

        /// <summary>
        /// Extracts wireframe data from multiple convex hulls for compound shapes.
        /// </summary>
        public static void ExtractCompoundConvexWireframeData(
            List<List<Vector3>> verticesByMesh,
            out List<Vector3> vertices,
            out List<int> indices)
        {
            vertices = new List<Vector3>();
            indices = new List<int>();

            foreach (var meshVertices in verticesByMesh)
            {
                int baseIndex = vertices.Count;
                vertices.AddRange(meshVertices);

                // Create triangle fan from first vertex of each hull
                if (meshVertices.Count >= 3)
                {
                    for (int i = 1; i < meshVertices.Count - 1; i++)
                    {
                        indices.Add(baseIndex);
                        indices.Add(baseIndex + i);
                        indices.Add(baseIndex + i + 1);
                    }
                }
            }
        }

        #endregion

        #region Dynamic Body Creation

        private static bool TryCreateCompoundConvex(
            List<List<Vector3>> verticesByMesh,
            Simulation simulation,
            BufferPool bufferPool,
            Vector3 position,
            float mass,
            out BodyHandle bodyHandle)
        {
            bodyHandle = default;

            try
            {
                using var compoundBuilder = new CompoundBuilder(bufferPool, simulation.Shapes, verticesByMesh.Count);

                foreach (var meshVertices in verticesByMesh)
                {
                    bufferPool.Take<Vector3>(meshVertices.Count, out var pointBuffer);
                    for (int i = 0; i < meshVertices.Count; i++)
                        pointBuffer[i] = meshVertices[i];

                    var hull = new ConvexHull(pointBuffer, bufferPool, out _);
                    compoundBuilder.Add(hull, new RigidPose(Vector3.Zero), 1f);
                    bufferPool.Return(ref pointBuffer);
                }

                compoundBuilder.BuildDynamicCompound(out var children, out var inertia, out var center);
                var compound = new Compound(children);
                var shapeIndex = simulation.Shapes.Add(compound);

                // Scale the inertia to the desired mass
                var inverseMassScale = verticesByMesh.Count / mass;
                Symmetric3x3.Scale(inertia.InverseInertiaTensor, inverseMassScale, out var scaledTensor);
                var scaledInertia = new BodyInertia
                {
                    InverseInertiaTensor = scaledTensor,
                    InverseMass = inertia.InverseMass * inverseMassScale
                };

                bodyHandle = simulation.Bodies.Add(BodyDescription.CreateDynamic(
                    new RigidPose(position),
                    scaledInertia,
                    new CollidableDescription(shapeIndex, 0.1f),
                    0.01f));

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static BodyHandle CreateConvexHullBody(
            List<Vector3> vertices,
            Simulation simulation,
            BufferPool bufferPool,
            Vector3 position,
            float mass)
        {
            bufferPool.Take<Vector3>(vertices.Count, out var pointBuffer);
            for (int i = 0; i < vertices.Count; i++)
                pointBuffer[i] = vertices[i];

            var hull = new ConvexHull(pointBuffer, bufferPool, out _);
            bufferPool.Return(ref pointBuffer);

            var shapeIndex = simulation.Shapes.Add(hull);
            var inertia = hull.ComputeInertia(mass);

            return simulation.Bodies.Add(BodyDescription.CreateDynamic(
                new RigidPose(position),
                inertia,
                new CollidableDescription(shapeIndex, 0.1f),
                0.01f));
        }

        #endregion

        #region Static Body Creation

        private static bool TryCreateStaticCompoundConvex(
            List<List<Vector3>> verticesByMesh,
            Simulation simulation,
            BufferPool bufferPool,
            Vector3 position,
            out StaticHandle staticHandle)
        {
            staticHandle = default;

            try
            {
                using var compoundBuilder = new CompoundBuilder(bufferPool, simulation.Shapes, verticesByMesh.Count);

                foreach (var meshVertices in verticesByMesh)
                {
                    bufferPool.Take<Vector3>(meshVertices.Count, out var pointBuffer);
                    for (int i = 0; i < meshVertices.Count; i++)
                        pointBuffer[i] = meshVertices[i];

                    var hull = new ConvexHull(pointBuffer, bufferPool, out _);
                    compoundBuilder.Add(hull, new RigidPose(Vector3.Zero), 1f);
                    bufferPool.Return(ref pointBuffer);
                }

                compoundBuilder.BuildKinematicCompound(out var children);
                var compound = new Compound(children);
                var shapeIndex = simulation.Shapes.Add(compound);

                staticHandle = simulation.Statics.Add(new StaticDescription
                {
                    Pose = new RigidPose(position),
                    Shape = shapeIndex,
                    Continuity = default
                });

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static StaticHandle CreateMeshStatic(
            List<Triangle> triangles,
            Simulation simulation,
            BufferPool bufferPool,
            Vector3 position)
        {
            bufferPool.Take<Triangle>(triangles.Count, out var triangleBuffer);
            for (int i = 0; i < triangles.Count; i++)
                triangleBuffer[i] = triangles[i];

            var mesh = new Mesh(triangleBuffer, Vector3.One, bufferPool);
            var meshIndex = simulation.Shapes.Add(mesh);

            return simulation.Statics.Add(new StaticDescription
            {
                Pose = new RigidPose(position),
                Shape = meshIndex,
                Continuity = default
            });
        }

        #endregion

        private static Vector3[] ExtractPartVertices(ModelMeshPart part)
        {
            var vertexBuffer = part.VertexBuffer;
            var vertexCount = part.NumVertices;
            var vertexDeclaration = vertexBuffer.VertexDeclaration;
            var vertexStride = vertexDeclaration.VertexStride;
            
            // Get raw vertex data
            var vertexData = new byte[vertexCount * vertexStride];
            vertexBuffer.GetData(part.VertexOffset * vertexStride, vertexData, 0, vertexData.Length);
            
            var vertices = new Vector3[vertexCount];
            
            // Find position element in vertex declaration
            var positionElement = Array.Find(vertexDeclaration.GetVertexElements(),
                e => e.VertexElementUsage == VertexElementUsage.Position);
            
            if (positionElement.VertexElementFormat == VertexElementFormat.Vector3)
            {
                for (int i = 0; i < vertexCount; i++)
                {
                    int offset = i * vertexStride + positionElement.Offset;
                    float x = BitConverter.ToSingle(vertexData, offset);
                    float y = BitConverter.ToSingle(vertexData, offset + 4);
                    float z = BitConverter.ToSingle(vertexData, offset + 8);
                    vertices[i] = new Vector3(x, y, z);
                }
            }
            
            return vertices;
        }
    }
}