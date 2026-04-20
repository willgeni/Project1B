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
using System.Collections.Generic;

namespace ModelDisplay1
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        private Model myModel;  // Holds the spaceship model
        private Texture2D shipTexture;  // Holds the spaceship texture

        float aspectRatio;  // Holds the aspect ratio of the display for efficency

        // Create and set the position of the camera in world space, for our view matrix.
        Vector3 cameraPosition = new Vector3(0.0f, 0.0f, -1500.0f);

        BufferPool _bufferPool;
        Simulation _physicsSimulation;
        PhysicsMaterialRegistry _materialRegistry;
        PhysicsObject _playerShip;

        private Model ringModel;
        private Texture2D ringTexture;
        private List<RaceRing> courseRings = new();
        private int currentRingIndex = 0;
        private int ringsMissed = 0;
        private int finalScore = 0;

        // UI Variables
        private SpriteFont uiFont;
        private float raceTimer = 0f;
        private bool raceFinished = false;
        private Model skyboxModel;
        private Texture2D spaceTexture;

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
            uiFont = Content.Load<SpriteFont>("UIFont");
            skyboxModel = Content.Load<Model>("Models\\sphere");
            spaceTexture = Content.Load<Texture2D>("Models\\space");
            shipTexture = Content.Load<Texture2D>("Models\\candycruiser_tex");

            myModel = Content.Load<Model>("Models\\CandyShip");
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
        
            ringModel = Content.Load<Model>("Models\\Ring");
            ringTexture = Content.Load<Texture2D>("Models\\RingTexture");

            for (int i  = 0; i < 7; i++)
            {
                float xOffset = (float)Math.Sin(i) * 10f;
                float yOffset = (float)Math.Cos(i) * 1.25f;

                // Space rings out along the Z axis
                Vector3 spawnPosition = new Vector3(xOffset, yOffset, -(i + 1) * 45f);
                bool usedMeshFallback;
                var ringPhysics = PhysicsShapeFactory.CreateStaticPhysicsObject(
                    ringModel,
                    _physicsSimulation,
                    _bufferPool,
                    _materialRegistry,
                    new System.Numerics.Vector3(spawnPosition.X, spawnPosition.Y, spawnPosition.Z),
                    true, // Force mesh so we can fly through hole in the middle
                    out usedMeshFallback
                );

                courseRings.Add(new RaceRing(ringPhysics, spawnPosition, 5f, ringTexture));
            }

            // Highlight the first ring
            if (courseRings.Count > 0)
            {
                courseRings[0].IsNext = true;
            }
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
                Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();
            // Get some input.
            UpdateInput();

            _physicsSimulation.Timestep(1f / 60f); // 60 FPS timestep

            if (!raceFinished)
            {
                raceTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

                if (currentRingIndex >= courseRings.Count)
                {
                    raceFinished = true;
                    // Base score of 5k
                    // -50 per 10 seconds & -500 per missed ring
                    finalScore = (int)Math.Max(0, 5000 - (raceTimer * 5) - (ringsMissed * 500));
                }
            }

            if (currentRingIndex < courseRings.Count)
            {
                var currentRing = courseRings[currentRingIndex];
                // Ship's current position
                var shipPose = _physicsSimulation.Bodies.
                    GetBodyReference(_playerShip.BodyHandle.Value).Pose;
                Vector3 shipPos = new Vector3(shipPose.Position.X, shipPose.Position.Y, shipPose.Position.Z);

                if (shipPos.Z < currentRing.Position.Z)
                {
                    // Crossed ring's threshold
                    Vector2 shipXY = new Vector2(shipPos.X, shipPos.Y);
                    Vector2 ringXY = new Vector2(currentRing.Position.X, currentRing.Position.Y);

                    float distanceXY = Vector2.Distance(shipXY, ringXY);

                    if (distanceXY < currentRing.Radius)
                    {
                        // Perfect hit
                        currentRing.WasCollected = true;
                    } else
                    {
                        // Missed
                        ringsMissed++;
                        currentRing.WasCollected = true; // Set to true so it disappears
                    }

                    currentRing.IsNext = false;
                    currentRingIndex++;

                    if (currentRingIndex < courseRings.Count)
                    {
                        courseRings[currentRingIndex].IsNext = true;
                    }
                }
            }

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
            float rotationSpeed = 0.0025f; // Adjust to make the ship more/less sensitive

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
                    ApplyThrust(shipBody, currentGamePadState.Triggers.Right * 5f);
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

            shipBody.ApplyAngularImpulse(-shipBody.Velocity.Angular * 0.005f);

            // Thrust with keyboard
            if (currentKeyState.IsKeyDown(Keys.W))
            {
                ApplyThrust(shipBody, 1f);
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
            System.Numerics.Vector3 localForward = new(0, 0, -1);
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
            GraphicsDevice.Clear(Color.Black);
            
            // Ship's current position & orientation from physics engine
            Matrix shipWorld = _playerShip.GetWorldMatrix();
            Vector3 shipPosition = shipWorld.Translation;
            Vector3 shipForward = shipWorld.Forward;
            Vector3 shipUp = shipWorld.Up;

            // Get a behind-the-ship camera position
            Vector3 cameraPos = shipPosition - (shipForward * 5) + (shipUp * 2);
            Vector3 cameraTarget = shipPosition + (shipForward * 10);

            // Draw the skybox
            // Disable depth buffer and culling so we see inside of the skybox
            GraphicsDevice.DepthStencilState = DepthStencilState.None;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;

            foreach (ModelMesh mesh in skyboxModel.Meshes)
            {
                foreach (BasicEffect effect in skyboxModel.Meshes[0].Effects)
                {
                    effect.LightingEnabled = false;
                    effect.TextureEnabled = true;
                    effect.Texture = spaceTexture;

                    effect.DiffuseColor = Vector3.One;
                    effect.EmissiveColor = Vector3.One;

                    effect.World = Matrix.CreateScale(50f) * Matrix.CreateTranslation(cameraPos);
                    effect.View = Matrix.CreateLookAt(cameraPos, cameraTarget, shipUp);
                    effect.Projection = Matrix.CreatePerspectiveFieldOfView(
                        MathHelper.ToRadians(45.0f), aspectRatio,
                        1.0f, 10000.0f);
                }
                mesh.Draw();
            }

            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            // Draw the ship
            foreach (ModelMesh mesh in myModel.Meshes)
            {
                // This is where the mesh orientation is set, as well 
                // as our camera and projection.
                foreach (BasicEffect effect in mesh.Effects)
                {
                    effect.EnableDefaultLighting();
                    effect.TextureEnabled = true;
                    effect.Texture = shipTexture;

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

            // Draw the rings
            Matrix viewMatrix = Matrix.CreateLookAt(cameraPos, cameraTarget, shipUp);
            Matrix projMatrix = Matrix.CreatePerspectiveFieldOfView(
                        MathHelper.ToRadians(45.0f), aspectRatio,
                        1.0f, 10000.0f);
            foreach (var ring in courseRings)
            {
                ring.Draw(viewMatrix, projMatrix, cameraPos);
            }

            // HUD Drawing
            
            _spriteBatch.Begin();
            _spriteBatch.DrawString(uiFont, $"Time: {Math.Round(raceTimer, 2)}s", new Vector2(20, 20), Color.White);
            _spriteBatch.DrawString(uiFont, $"Rings Missed: {ringsMissed}", new Vector2(20, 50), Color.Red);
            if (raceFinished) 
            {
                _spriteBatch.DrawString(uiFont, "RACE COMPLETE!", new Vector2(500, 300), Color.Yellow);
                _spriteBatch.DrawString(uiFont, $"FINAL SCORE: {finalScore}", new Vector2(500, 340), Color.Cyan);
            }
            _spriteBatch.End();

            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;
            

            base.Draw(gameTime);
        }
    }
}
