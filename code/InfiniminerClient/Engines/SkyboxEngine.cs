using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace Infiniminer
{
    public class SkyplaneEngine
    {
        InfiniminerGame gameInstance;
        PropertyBag _P;
        Texture2D texNoise;
        VertexPositionTexture[] vertices;
        Effect effect;
        float effectTime = 0;

        public SkyplaneEngine(InfiniminerGame gameInstance)
        {
            this.gameInstance = gameInstance;

            // Load the effect file.
            effect = gameInstance.Content.Load<Effect>("effect_skyplane");

            // Load our noise texture.
            texNoise = gameInstance.Content.Load<Texture2D>("blocks/tex_block_magma");

            // Create our vertices.
            vertices = new VertexPositionTexture[6];
            vertices[0] = new VertexPositionTexture(new Vector3(-210, 100, -210), new Vector2(0, 0));
            vertices[1] = new VertexPositionTexture(new Vector3(274, 100, -210), new Vector2(1, 0));
            vertices[2] = new VertexPositionTexture(new Vector3(274, 100, 274), new Vector2(1, 1));
            vertices[3] = new VertexPositionTexture(new Vector3(-210, 100, -210), new Vector2(0, 0));
            vertices[4] = new VertexPositionTexture(new Vector3(274, 100, 274), new Vector2(1, 1));
            vertices[5] = new VertexPositionTexture(new Vector3(-210, 100, 274), new Vector2(0, 1));
        }

        public void Update(GameTime gameTime)
        {
            effectTime = (float)gameTime.TotalGameTime.TotalSeconds;
        }

        public void Render(GraphicsDevice graphicsDevice)
        {
            // If we don't have _P, grab it from the current gameInstance.
            // We can't do this in the constructor because we are created in the property bag's constructor!
            if (_P == null)
                _P = gameInstance.propertyBag;

            // Draw the skybox.
            Matrix viewMatrix = _P.playerCamera.ViewMatrix;
            Matrix projectionMatrix = _P.playerCamera.ProjectionMatrix;

            effect.CurrentTechnique = effect.Techniques["Skyplane"];
            effect.Parameters["xWorld"].SetValue(Matrix.Identity);
            effect.Parameters["xView"].SetValue(viewMatrix);
            effect.Parameters["xProjection"].SetValue(projectionMatrix);
            effect.Parameters["xTexture"].SetValue(texNoise);
            effect.Parameters["xTime"].SetValue(effectTime);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                graphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
                graphicsDevice.RasterizerState = RasterizerState.CullNone;
                graphicsDevice.DepthStencilState = DepthStencilState.None;
                graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length / 3);
                graphicsDevice.DepthStencilState = DepthStencilState.Default;
            }
        }
    }
}
