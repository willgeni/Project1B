using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace ModelDisplay1
{
    public struct SimplePostIntegratorCallbacks : IPoseIntegratorCallbacks
    {
        public readonly AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;
        public readonly bool AllowSubstepsForUnconstrainedBodies => false;
        public readonly bool IntegrateVelocityForKinematics => false;
        public readonly void Initialize(Simulation simulation) { }
        public void PrepareForIntegration(float dt) { }
        public void IntegrateVelocity(Vector<int> bodyIndex, Vector3Wide position, QuaternionWide orientation, 
            BodyInertiaWide localInertia, Vector<int> integrationMask, int workerIndex, Vector<float> dt, 
            ref BodyVelocityWide velocity)
        {
            // Can add gravity here later
        }
    }

    public struct SimpleNarrowPhaseCallbacks : INarrowPhaseCallbacks
    {
        public void Initialize(Simulation simulation) { }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin) => false;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB) => false;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial) where TManifold : unmanaged, IContactManifold<TManifold>
        {
            pairMaterial.FrictionCoefficient = 1f;
            pairMaterial.SpringSettings = new SpringSettings(30, 1);
            pairMaterial.MaximumRecoveryVelocity = 2f;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold)
        {
            return true;
        }
        
        public void Dispose() { }
    }
}