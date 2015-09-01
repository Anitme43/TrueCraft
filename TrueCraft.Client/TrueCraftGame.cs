using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using TrueCraft.API;
using TrueCraft.Client.Rendering;
using System.Linq;
using System.ComponentModel;
using TrueCraft.Core.Networking.Packets;
using TrueCraft.API.World;
using System.Collections.Concurrent;
using TrueCraft.Client.Input;
using TrueCraft.Core;
using TrueCraft.Client.Graphics;
using TrueCraft.Client.Graphics.OpenGL;
using TKOpenGL = OpenTK.Graphics.OpenGL;
using TrueCraft.Client.Events;
using TrueCraft.Client.Maths;

namespace TrueCraft.Client
{
    public class TrueCraftGame : Game
    {
        private MultiplayerClient Client { get; set; }
        private IPEndPoint EndPoint { get; set; }
        private ChunkRenderer ChunkConverter { get; set; }
        private DateTime NextPhysicsUpdate { get; set; }
        private List<Mesh> ChunkMeshes { get; set; }
        public ConcurrentBag<Action> PendingMainThreadActions { get; set; }
        private ConcurrentBag<Lazy<Mesh>> IncomingChunks { get; set; }
        private TextureMapper TextureMapper { get; set; }
        private Camera Camera { get; set; }
        private Maths.BoundingFrustum CameraFrustum { get; set; }
        private ShaderProgram BasicEffect { get; set; }

        public TrueCraftGame(MultiplayerClient client, IPEndPoint endPoint)
            : base(UserSettings.Local.WindowResolution.Width,
                UserSettings.Local.WindowResolution.Height,
                "TrueCraft", UserSettings.Local.IsFullscreen)
        {
            Client = client;
            EndPoint = endPoint;
            NextPhysicsUpdate = DateTime.MinValue;
            ChunkMeshes = new List<Mesh>();
            IncomingChunks = new ConcurrentBag<Lazy<Mesh>>();
            PendingMainThreadActions = new ConcurrentBag<Action>();
            Camera = new Camera(Window.Width / Window.Height, 70.0f, 0.25f, 1000.0f);
            // UpdateCamera();
        }

        private void UpdateCamera()
        {
            Camera.Position = new Maths.Vector3(
                (float)Client.Position.X,
                (float)(Client.Position.Y + (Client.Size.Height / 2)),
                (float)Client.Position.Z);

            Camera.Pitch = Client.Pitch;
            Camera.Yaw = Client.Yaw;

            CameraFrustum = Camera.GetFrustum();
            Camera.ApplyTo(BasicEffect);
        }

        protected override void OnLoad(object sender, EventArgs e)
        {
            // Initializes various runtime components.
            base.OnLoad(sender, e);

            Initialize();
            LoadContent();
            CreateShaders();
        }

        private void CreateShaders()
        {
            var vertex =
                Shader.FromFile(ShaderType.Vertex, "Content/Shaders/vertex.glsl");
            vertex.Compile();

            var fragment =
                Shader.FromFile(ShaderType.Fragment, "Content/Shaders/fragment.glsl");
            fragment.Compile();

            var basicEffect = new ShaderProgram(vertex, fragment);
            basicEffect.Link();
            
            var texture = default(Texture2D);
            if(TextureMapper.TryGetTexture("terrain.png", out texture))
            {
                basicEffect.MakeCurrent();
                texture.Bind();

                var textureUniform = basicEffect.GetUniform<Texture>("un_Diffuse");
                textureUniform.SetValue(texture);
            }

            BasicEffect = basicEffect;
        }

        private void Initialize()
        {
            ChunkConverter = new ChunkRenderer(this, Client.World.World.BlockRepository);
            Client.ChunkLoaded += Client_ChunkLoaded;
            //Client.ChunkModified += (sender, e) => ChunkConverter.Enqueue(e.Chunk, true);
            ChunkConverter.MeshCompleted += ChunkConverter_MeshGenerated;
            ChunkConverter.Start();
            Client.PropertyChanged += HandleClientPropertyChanged;
            Client.Connect(EndPoint);
            LoadContent();
            TKOpenGL.GL.ClearColor(System.Drawing.Color.CornflowerBlue);
            TKOpenGL.GL.Enable(TKOpenGL.EnableCap.DepthTest);

            var vao = TKOpenGL.GL.GenVertexArray();
            TKOpenGL.GL.BindVertexArray(vao);
        }

        void Client_ChunkLoaded(object sender, ChunkEventArgs e)
        {
            ChunkConverter.Enqueue(e.Chunk);
        }

        void ChunkConverter_MeshGenerated(object sender, RendererEventArgs<ReadOnlyChunk> e)
        {
            IncomingChunks.Add(e.Result);
        }

        void HandleClientPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "Position":
                    var sorter = new ChunkRenderer.ChunkSorter(new Coordinates3D(
                        (int)Client.Position.X, 0, (int)Client.Position.Z));
                    PendingMainThreadActions.Add(() => ChunkMeshes.Sort(sorter));
                    break;
            }
        }

        private void LoadContent()
        {
            // Ensure we have default textures loaded.
            TextureMapper.LoadDefaults();

            // Load any custom textures if needed.
            TextureMapper = new TextureMapper();
            if (UserSettings.Local.SelectedTexturePack != TexturePack.Default.Name)
                TextureMapper.AddTexturePack(TexturePack.FromArchive(Path.Combine(TexturePack.TexturePackPath, UserSettings.Local.SelectedTexturePack)));
        }

        protected override void OnUpdate(object sender, FrameEventArgs e)
        {
            Action action;
            if (PendingMainThreadActions.TryTake(out action))
                action();

            if (NextPhysicsUpdate < DateTime.UtcNow && Client.LoggedIn)
            {
                IChunk chunk;
                var adjusted = Client.World.World.FindBlockPosition(new Coordinates3D((int)Client.Position.X, 0, (int)Client.Position.Z), out chunk);
                if (chunk != null)
                {
                    if (chunk.GetHeight((byte)adjusted.X, (byte)adjusted.Z) != 0)
                        Client.Physics.Update();
                }
                // NOTE: This is to make the vanilla server send us chunk packets
                // We should eventually make some means of detecing that we're on a vanilla server to enable this
                // It's a waste of bandwidth to do it on a TrueCraft server
                Client.QueuePacket(new PlayerGroundedPacket { OnGround = true });
                Client.QueuePacket(new PlayerPositionAndLookPacket(Client.Position.X, Client.Position.Y,
                    Client.Position.Y + MultiplayerClient.Height, Client.Position.Z, Client.Yaw, Client.Pitch, false));
                NextPhysicsUpdate = DateTime.UtcNow.AddMilliseconds(1000 / 20);
            }
        }

        protected override void OnRender(object sender, FrameEventArgs e)
        {
            // We lazily initialize meshes on the main thread.
            Lazy<Mesh> mesh;
            while (IncomingChunks.TryTake(out mesh))
                ChunkMeshes.Add(mesh.Value);

            // Clear the backbuffer.
            TKOpenGL.GL.Clear(
                TKOpenGL.ClearBufferMask.ColorBufferBit |
                TKOpenGL.ClearBufferMask.DepthBufferBit);

            // Bind our texture to the first texture unit.
            TextureMapper.GetTexture("terrain.png").Bind(0);

            // Make our basic shader program current and update our camera.
            BasicEffect.MakeCurrent();
            UpdateCamera();

            // Draw the opaque portion of the chunk meshes.
            foreach (var chunk in ChunkMeshes)
                chunk.Draw(0);
        }

        protected override void OnUnload(object sender, EventArgs e)
        {
            Client.ChunkLoaded -= Client_ChunkLoaded;
            ChunkConverter.Dispose();

            // Cleans up various runtime components (and should be run last).
            base.OnUnload(sender, e);
        }
    }
}
