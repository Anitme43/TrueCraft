﻿using System;
using TrueCraft.Core.Logic;
using TrueCraft.API.Logic;
using System.Linq;
using TrueCraft.Client.Graphics;
using TrueCraft.Client.Maths;

namespace TrueCraft.Client.Rendering
{
    public class BlockRenderer
    {
        private static readonly BlockRenderer DefaultRenderer = new BlockRenderer();
        private static readonly BlockRenderer[] Renderers = new BlockRenderer[0x100];

        public static void RegisterRenderer(byte id, BlockRenderer renderer)
        {
            Renderers[id] = renderer;
        }

        public static Vertex[] RenderBlock(IBlockProvider provider, BlockDescriptor descriptor,
            Vector3 offset, int indiciesOffset, out uint[] indicies)
        {
            var textureMap = provider.GetTextureMap(descriptor.Metadata);
            if (textureMap == null)
                textureMap = new Tuple<int, int>(0, 0); // TODO: handle this better
            return Renderers[descriptor.ID].Render(descriptor, offset, textureMap, indiciesOffset, out indicies);
        }

        public virtual Vertex[] Render(BlockDescriptor descriptor, Vector3 offset,
            Tuple<int, int> textureMap, int indiciesOffset, out uint[] indicies)
        {
            var texCoords = new Vector2(textureMap.Item1, textureMap.Item2);
            var texture = new[]
            {
                texCoords + Vector2.UnitX + Vector2.UnitY,
                texCoords + Vector2.UnitY,
                texCoords,
                texCoords + Vector2.UnitX
            };
            for (int i = 0; i < texture.Length; i++)
                texture[i] *= new Vector2(16f / 256f);
            return CreateUniformCube(offset, texture, indiciesOffset, out indicies, Color.White);
        }

        protected Vertex[] CreateUniformCube(Vector3 offset, Vector2[] texture, int indiciesOffset, out uint[] indicies, Color color)
        {
            indicies = new uint[6 * 6];
            var verticies = new Vertex[4 * 6];
            uint[] _indicies;
            int textureIndex = 0;
            for (int _side = 0; _side < 6; _side++)
            {
                var side = (CubeFace)_side;
                var quad = CreateQuad(side, offset, texture, textureIndex % texture.Length, indiciesOffset, out _indicies, color);
                Array.Copy(quad, 0, verticies, _side * 4, 4);
                Array.Copy(_indicies, 0, indicies, _side * 6, 6);
                textureIndex += 4;
            }
            return verticies;
        }

        protected static Vertex[] CreateQuad(CubeFace face, Vector3 offset, Vector2[] texture, int textureOffset,
            int indiciesOffset, out uint[] indicies, Color color)
        {
            indicies = new uint[] { 0, 1, 3, 1, 2, 3 };
            for (int i = 0; i < indicies.Length; i++)
                indicies[i] += (uint)(((int)face * 4) + indiciesOffset);
            var quad = new Vertex[4];
            var unit = CubeMesh[(int)face];
            var normal = CubeNormals[(int)face];
            for (int i = 0; i < 4; i++)
            {
                quad[i] = new Vertex(offset + unit[i], normal, color, texture[textureOffset + i]);
            }
            return quad;
        }

        protected enum CubeFace
        {
            PositiveZ = 0,
            NegativeZ = 1,
            PositiveX = 2,
            NegativeX = 3,
            PositiveY = 4,
            NegativeY = 5
        }

        protected static readonly Vector3[][] CubeMesh;

        protected static readonly Vector3[] CubeNormals =
        {
            new Vector3(0, 0, 1),
            new Vector3(0, 0, -1),
            new Vector3(1, 0, 0),
            new Vector3(-1, 0, 0),
            new Vector3(0, 1, 0),
            new Vector3(0, -1, 0)
        };

        static BlockRenderer()
        {
            for (int i = 0; i < Renderers.Length; i++)
            {
                Renderers[i] = DefaultRenderer;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes().Where(t =>
                    typeof(BlockRenderer).IsAssignableFrom(t) && !t.IsAbstract && t != typeof(BlockRenderer)))
                {
                    Activator.CreateInstance(type); // This is just to call the static initializers
                }
            }

            CubeMesh = new Vector3[6][];

            CubeMesh[0] = new[] // Positive Z face
            {
                new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f)
            };

            CubeMesh[1] = new[] // Negative Z face
            {
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f)
            };

            CubeMesh[2] = new[] // Positive X face
            {
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, -0.5f)
            };

            CubeMesh[3] = new[] // Negative X face
            {
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f)
            };

            CubeMesh[4] = new[] // Positive Y face
            {
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f)
            };

            CubeMesh[5] = new[] // Negative Y face
            {
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, -0.5f, 0.5f)
            };
        }
    }
}