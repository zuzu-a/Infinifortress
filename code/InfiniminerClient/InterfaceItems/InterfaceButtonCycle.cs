using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StateMasher;
using Infiniminer;
using Infiniminer.States;

namespace InterfaceItems
{
    internal class InterfaceButtonCycle : InterfaceElement
    {
        public string[] options;
        public int currentIndex = 0;
        public string value { get { return options[currentIndex]; } }
        private InfiniminerGame gameInstance;
        
        public InterfaceButtonCycle(InfiniminerGame gameInstance, PropertyBag playerInstance) : base(gameInstance, playerInstance)
        {
            this.color = Color.White;
            this.gameInstance = gameInstance;
        }

        public override void OnMouseDown(MouseButton button, int x, int y)
        {
            if (button == MouseButton.LeftButton && enabled && visible && size.Contains(x, y))
            {
                currentIndex = (currentIndex + 1) % options.Length;
            }
        }

        public override void Render(GraphicsDevice graphicsDevice)
        {
            if (!visible)
                return;

            SpriteBatch spriteBatch = new SpriteBatch(graphicsDevice);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            // Draw the label if we have one
            if (text != "")
            {
                spriteBatch.DrawString(uiFont, text, new Vector2(size.X, size.Y - 22), enabled ? color : Color.Gray);
            }

            // Draw the current value
            string displayText = value;
            Vector2 valueSize = uiFont.MeasureString(displayText);
            spriteBatch.DrawString(uiFont, displayText, 
                new Vector2(size.X + size.Width - valueSize.X, size.Y), 
                enabled ? color : Color.Gray);

            spriteBatch.End();
        }

        public void SetValue(string val)
        {
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i].ToLower() == val.ToLower())
                {
                    currentIndex = i;
                    break;
                }
            }
        }
    }
} 