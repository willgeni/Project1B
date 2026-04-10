using Microsoft.VisualBasic.ApplicationServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Reflection;
using BepuPhysics;
using BepuUtilities;
using BepuUtilities.Memory;
using Matrix = Microsoft.Xna.Framework.Matrix;
using MathHelper = Microsoft.Xna.Framework.MathHelper;

namespace ModelDisplay1
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        private Model myModel;  // Holds the spaceship model

        float aspectRatio;  // Holds the aspect ratio of the display for efficency

        // Create and set the position of the camera in world space, for our view matrix.
        Vector3 cameraPosition = new Vector3(0.0f, 0.0f, -1500.0f);

        BufferPool _bufferPool;
        Simulation _physicsSimulation;
        PhysicsMaterialRegistry _materialRegistry;
        PhysicsObject _playerShip;

        SoundEffect soundEngine;
        SoundEffect soundHyperspaceActivation;


        SoundEffectInstance soundEngineInstance;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            _bufferPool = new BufferPool();
            _materialRegistry = new PhysicsMaterialRegistry();

            // Create simulation
            _physicsSimulation = Simulation.Create(
                _bufferPool,
                new SimpleNarrowPhaseCallbacks(),
                new SimplePostIntegratorCallbacks(),
                new SolveDescription(8, 1)
            );
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            myModel = Content.Load<Model>("Models\\lego spaceship");
            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 720;
            _graphics.ApplyChanges();

            aspectRatio = _graphics.GraphicsDevice.Viewport.AspectRatio;

            soundEngine = Content.Load<SoundEffect>("Audio\\engine_2");

            soundHyperspaceActivation = Content.Load<SoundEffect>("Audio\\hyperspace_activate");

            soundEngineInstance = soundEngine.CreateInstance();

            _playerShip = PhysicsShapeFactory.CreateDynamicPhysicsObject(
                myModel,
                _physicsSimulation,
                _bufferPool,
                _materialRegistry,
                new System.Numerics.Vector3(0, 0, 0),
                10f, // mass
                false,
                out bool usedFallback
            );
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
                Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();
            // Get some input.
            UpdateInput();

            _physicsSimulation.Timestep(1f / 60f); // 60 FPS timestep

            base.Update(gameTime);
        }

        protected void UpdateInput()
        {
            KeyboardState currentKeyState = Keyboard.GetState();
            GamePadState currentGamePadState = GamePad.GetState(PlayerIndex.One);

            bool engineon = false;
            var shipBody = _physicsSimulation.Bodies.GetBodyReference(_playerShip.BodyHandle.Value);
            shipBody.Awake = true;

            System.Numerics.Vector3 localAngularImpulse = System.Numerics.Vector3.Zero;
            float rotationSpeed = 2.0f; // Adjust to make the ship more/less sensitive

            // Yaw (Left/Right)
            if (currentKeyState.IsKeyDown(Keys.A)) localAngularImpulse.Y += rotationSpeed;
            if (currentKeyState.IsKeyDown(Keys.D)) localAngularImpulse.Y -= rotationSpeed;
            // Pitch (Up/Down)
            if (currentKeyState.IsKeyDown(Keys.Up)) localAngularImpulse.X += rotationSpeed;
            if (currentKeyState.IsKeyDown(Keys.Down)) localAngularImpulse.X -= rotationSpeed;
            // Roll
            if (currentKeyState.IsKeyDown(Keys.Q)) localAngularImpulse.Z += rotationSpeed;
            if (currentKeyState.IsKeyDown(Keys.E)) localAngularImpulse.Z -= rotationSpeed;

            if (currentGamePadState.IsConnected)
            {
                // Use thumbsticks for steering behavior (angular impulse)
                localAngularImpulse.Y -= currentGamePadState.ThumbSticks.Left.X * rotationSpeed;
                localAngularImpulse.X += currentGamePadState.ThumbSticks.Left.Y * rotationSpeed;
                localAngularImpulse.Z += (currentGamePadState.Triggers.Left - currentGamePadState.Triggers.Right) * rotationSpeed;

                // Thrust with triggers
                if (currentGamePadState.Triggers.Right > 0)
                {
                    ApplyThrust(shipBody, currentGamePadState.Triggers.Right * 15f);
                    engineon = true;
                }
            }

            // Apply angular impulse if there's any input
            if (localAngularImpulse != System.Numerics.Vector3.Zero)
            {
                // Transform local rotation intent into world space using the ship's current orientation
                var worldAngularImpulse = System.Numerics.Vector3.Transform(localAngularImpulse, shipBody.Pose.Orientation);
                shipBody.ApplyAngularImpulse(worldAngularImpulse);
            }

            // Thrust with keyboard
            if (currentKeyState.IsKeyDown(Keys.W))
            {
                ApplyThrust(shipBody, 15f);
                engineon = true;
            }

            // Hyperspace jump - reset position and velocity
            if (currentKeyState.IsKeyDown(Keys.X) || currentGamePadState.Buttons.Start == ButtonState.Pressed) 
            {
                shipBody.Pose.Position = System.Numerics.Vector3.Zero;
                shipBody.Velocity.Linear = System.Numerics.Vector3.Zero;
                shipBody.Velocity.Angular = System.Numerics.Vector3.Zero;
                shipBody.Pose.Orientation = System.Numerics.Quaternion.Identity;
                soundHyperspaceActivation.Play();
            }

            UpdateEngineSound(engineon);
        }

        // Helper method to apply thrust in the forward direction of the ship
        private void ApplyThrust(BepuPhysics.BodyReference body, float force)
        {
            System.Numerics.Vector3 localForward = new(0, 0, 1);
            System.Numerics.Vector3 worldForward = System.Numerics.Vector3.Transform(localForward, body.Pose.Orientation);
            body.ApplyLinearImpulse(worldForward * force);
        }

        // Helper method to manage engine sound based on whether the engine is on or off
        private void UpdateEngineSound(bool engineOn)
        {
            if (engineOn)
            {
                if (soundEngineInstance.State == SoundState.Stopped)
                {
                    soundEngineInstance.Volume = 0.75f;
                    soundEngineInstance.IsLooped = true;
                    soundEngineInstance.Play();
                } else
                {
                    soundEngineInstance.Resume();
                }
            }
            else
            {
                soundEngineInstance.Pause();
            }
        }


        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            
            // Ship's current position & orientation from physics engine
            Matrix shipWorld = _playerShip.GetWorldMatrix();
            Vector3 shipPosition = shipWorld.Translation;
            Vector3 shipForward = shipWorld.Forward;
            Vector3 shipUp = shipWorld.Up;

            // Get a behind-the-ship camera position
            Vector3 cameraPos = shipPosition - (shipForward * 500) + (shipUp * 200);
            Vector3 cameraTarget = shipPosition + (shipForward * 500);

            foreach (ModelMesh mesh in myModel.Meshes)
            {
                // This is where the mesh orientation is set, as well 
                // as our camera and projection.
                foreach (BasicEffect effect in mesh.Effects)
                {
                    effect.EnableDefaultLighting();
                    effect.World = shipWorld;

                    // Update view to follow the ship
                    effect.View = Matrix.CreateLookAt(cameraPos, cameraTarget, shipUp);
                    
                    effect.Projection = Matrix.CreatePerspectiveFieldOfView(
                        MathHelper.ToRadians(45.0f), aspectRatio,
                        1.0f, 10000.0f);
                }
                // Draw the mesh, using the effects set above.
                mesh.Draw();
            }
            base.Draw(gameTime);
        }
    }
}
