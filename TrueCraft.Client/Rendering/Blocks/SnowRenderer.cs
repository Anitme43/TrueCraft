﻿using System;
using TrueCraft.Core.Logic.Blocks;
using TrueCraft.API.Logic;
using TrueCraft.Client.Graphics;
using TrueCraft.Client.Maths;

namespace TrueCraft.Client.Rendering.Blocks
{
    public class SnowRenderer : BlockRenderer
    {
        static SnowRenderer()
        {
            BlockRenderer.RegisterRenderer(SnowfallBlock.BlockID, new SnowRenderer());
            for (int i = 0; i < Texture.Length; i++)
                Texture[i] *= new Vector2(16f / 256f);
        }

        private static Vector2 TextureMap = new Vector2(2, 4);
        private static Vector2[] Texture =
        {
            TextureMap + Vector2.UnitX + Vector2.UnitY,
            TextureMap + Vector2.UnitY,
            TextureMap,
            TextureMap + Vector2.UnitX,
        };

        public override Vertex[] Render(BlockDescriptor descriptor, Vector3 offset,
             Tuple<int, int> textureMap, int indiciesOffset, out ushort[] indicies)
        {
            var overhead = new Vector3(0.5f, 0.5f, 0.5f);
            var cube = CreateUniformCube(overhead, Texture, indiciesOffset, out indicies, Color.White);
            var heightMultiplier = new Vector3(1, ((descriptor.Metadata + 1) / 16f), 1);
            for (int i = 0; i < cube.Length; i++)
            {
                if (cube[i].Position.Y > 0)
                {
                    cube[i].Position *= heightMultiplier;
                }
                cube[i].Position += offset;
                cube[i].Position -= overhead;
            }
            return cube;
        }
    }
}