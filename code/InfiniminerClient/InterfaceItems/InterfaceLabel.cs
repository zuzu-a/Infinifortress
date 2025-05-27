using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Microsoft.Xna.Framework.Design;

namespace InterfaceItems
{
    class InterfaceLabel : InterfaceElement
    {
        public InterfaceLabel()
        {
        }

        public InterfaceLabel(Infiniminer.InfiniminerGame gameInstance)
        {
            uiFont = gameInstance.Content.Load<SpriteFont>(gameInstance.font);
        }

        public InterfaceLabel(Infiniminer.InfiniminerGame gameInstance, Infiniminer.PropertyBag pb)
        {
            uiFont = gameInstance.Content.Load<SpriteFont>(gameInstance.font);
            _P = pb;
        }

        public override void Render(GraphicsDevice graphicsDevice)
        {
            if (visible&&text!="")
            {
                SpriteBatch spriteBatch = new SpriteBatch(graphicsDevice);
                spriteBatch.Begin();

                spriteBatch.DrawString(uiFont, text, new Vector2(size.X, size.Y), Color.White);
                spriteBatch.End();
            }
        }
    }
}
