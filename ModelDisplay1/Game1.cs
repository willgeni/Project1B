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

        Vector3 modelPosition = Vector3.Zero;  // The model position in 3 space 

        float modelRotation = 0.0f;  // The model rotation in 3 space

        // Create and set the position of the camera in world space, for our view matrix.
        Vector3 cameraPosition = new Vector3(0.0f, 0.0f, -1500.0f);

        Vector3 modelVelocity = Vector3.Zero;

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

            if (currentGamePadState.IsConnected)
            {
                // Rotate the model using the left thumbstick, and scale it down

                modelRotation -= currentGamePadState.ThumbSticks.Left.X * 0.10f;

                // Create some velocity if the right trigger is down.
                Vector3 modelVelocityAdd = Vector3.Zero;

                // Find out what direction we should be thrusting, 
                // using rotation.
                modelVelocityAdd.X = (float)Math.Sin(modelRotation);
                modelVelocityAdd.Z = (float)Math.Cos(modelRotation);

                // Now scale our direction by how hard the trigger is down.
                modelVelocityAdd *= currentGamePadState.Triggers.Right;

                if (currentGamePadState.Triggers.Right != 0f)
                {
                    engineon = true;
                }

                // Finally, add this vector to our velocity.
                modelVelocity += modelVelocityAdd;

                GamePad.SetVibration(PlayerIndex.One,
                    currentGamePadState.Triggers.Right,
                    currentGamePadState.Triggers.Right);


                // In case you get lost, press A to warp back to the center.
                if (currentGamePadState.Buttons.A == ButtonState.Pressed)
                {
                    modelPosition = Vector3.Zero;
                    modelVelocity = Vector3.Zero;
                    modelRotation = 0.0f;
                }
            }

            if (currentKeyState.IsKeyDown(Keys.A))
                modelRotation += 0.10f;

            if (currentKeyState.IsKeyDown(Keys.D))
                modelRotation -= 0.10f;


            if (currentKeyState.IsKeyDown(Keys.W))
            {
                var shipBody = _physicsSimulation.Bodies.GetBodyReference(_playerShip.BodyHandle.Value);

                shipBody.Awake = true;
                float thrustX = (float)Math.Sin(modelRotation);
                float thrustZ = (float)Math.Cos(modelRotation);

                shipBody.ApplyLinearImpulse(new System.Numerics.Vector3(thrustX * 10f, 0, thrustZ * 10f));
            }

            if (currentKeyState.IsKeyDown(Keys.X))
            {
                modelPosition = Vector3.Zero;
                modelVelocity = Vector3.Zero;
                modelRotation = 0.0f;
                soundHyperspaceActivation.Play();
            }

            if (engineon)
            {
                if (soundEngineInstance.State == SoundState.Stopped)
                {
                    soundEngineInstance.Volume = 0.75f;
                    soundEngineInstance.IsLooped = true;
                    soundEngineInstance.Play();
                }
                else
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

            foreach (ModelMesh mesh in myModel.Meshes)
            {
                // This is where the mesh orientation is set, as well 
                // as our camera and projection.
                foreach (BasicEffect effect in mesh.Effects)
                {
                    effect.EnableDefaultLighting();

                    effect.World = _playerShip.GetWorldMatrix();

                    effect.View = Matrix.CreateLookAt(cameraPosition,
                        Vector3.Zero, Vector3.Up);
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
