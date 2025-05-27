using System;
using System.Collections.Generic;

using System.Text;
using System.Diagnostics;
using StateMasher;
using InterfaceItems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace Infiniminer.States
{
    class SettingsState : State
    {
        List<InterfaceElement> elements = new List<InterfaceElement>();
        Rectangle drawRect;
        
        // DPI-aware scaling
        float uiScale = 1.0f;
        int baseHeight;
        int sliderFullWidth;
        int elementSpacing;
        int columnWidth;
        
        Vector2 currentPos = new Vector2(0, 0);
        int originalY = 0;

        ClickRegion[] clkMenuSettings = new ClickRegion[2] {
            new ClickRegion(new Rectangle(0,713,255,42),"cancel"),
            new ClickRegion(new Rectangle(524,713,500,42),"accept")
        };

        protected string nextState = null;
        
        // Store original settings for comparison
        private Dictionary<string, string> originalSettings = new Dictionary<string, string>();

        private void CalculateUIScale()
        {
            // Base UI designed for 1024x768, scale up for higher resolutions
            float screenWidth = _SM.GraphicsDevice.Viewport.Width;
            float screenHeight = _SM.GraphicsDevice.Viewport.Height;
            
            // Calculate scale based on screen size (minimum scale of 1.0, max of 3.0 for 4K)
            uiScale = Math.Max(1.0f, Math.Min(3.0f, Math.Min(screenWidth / 1024f, screenHeight / 768f)));
            
            // Scale all UI elements
            baseHeight = (int)(18 * uiScale);
            sliderFullWidth = (int)(250 * uiScale);
            elementSpacing = (int)(20 * uiScale);
            columnWidth = (int)(350 * uiScale);
        }
        
        private void UpdateClickRegions()
        {
            // Calculate the background offset (since it's centered)
            int bgOffsetX = _SM.GraphicsDevice.Viewport.Width / 2 - (int)(1024 * uiScale) / 2;
            int bgOffsetY = _SM.GraphicsDevice.Viewport.Height / 2 - (int)(768 * uiScale) / 2;
            
            // Update click regions with proper scaling and offset
            clkMenuSettings[0] = new ClickRegion(new Rectangle(
                bgOffsetX + (int)(0 * uiScale), 
                bgOffsetY + (int)(713 * uiScale), 
                (int)(255 * uiScale), 
                (int)(42 * uiScale)), "cancel");
                
            clkMenuSettings[1] = new ClickRegion(new Rectangle(
                bgOffsetX + (int)(524 * uiScale), 
                bgOffsetY + (int)(713 * uiScale), 
                (int)(500 * uiScale), 
                (int)(42 * uiScale)), "accept");
        }

        public void addSpace(int amount)
        {
            currentPos.Y += (int)(amount * uiScale);
        }

        public void shiftColumn()
        {
            shiftColumn(columnWidth);
        }

        public void shiftColumn(int amount)
        {
            currentPos.X += amount;
            currentPos.Y = originalY;
        }

        public void addSliderAutomatic(string text, float minVal, float maxVal, float initVal, bool integerOnly)
        {
            int height = baseHeight + 8; // Increased base height
            if (text != "")
                height += elementSpacing + 8; // Added extra spacing for labels
            currentPos.Y += height;
            addSlider(new Rectangle((int)currentPos.X, (int)currentPos.Y, sliderFullWidth, baseHeight), true, true, text, minVal, maxVal, initVal, integerOnly);
        }

        public void addSlider(Rectangle size, bool enabled, bool visible, string text, float minVal, float maxVal, float initVal, bool integerOnly)
        {
            InterfaceSlider temp = new InterfaceSlider((_SM as InfiniminerGame), _P);
            temp.size=size;
            temp.enabled=enabled;
            temp.visible=visible;
            temp.text=text;
            temp.minVal=minVal;
            temp.maxVal=maxVal;
            temp.setValue(initVal);
            temp.integers=integerOnly;
            elements.Add(temp);
        }

        public void addButtonAutomatic(string text, string onText, string offText, bool clicked, Color col)
        {
            int height = baseHeight + 8; // Increased base height
            if (text != "")
                height += elementSpacing + 8; // Added extra spacing for labels
            currentPos.Y += height;
            addButton(new Rectangle((int)currentPos.X, (int)currentPos.Y, sliderFullWidth, baseHeight), true, true, text, onText, offText, clicked, col);
        }

        public void addButton(Rectangle size, bool enabled, bool visible, string text, string onText, string offText, bool clicked, Color col)
        {
            InterfaceButtonToggle temp = new InterfaceButtonToggle((_SM as InfiniminerGame), _P);
            temp.size = size;
            temp.enabled = enabled;
            temp.visible = visible;
            temp.color = col;
            temp.text = text;
            temp.onText = onText;
            temp.offText = offText;
            temp.clicked = clicked;
            elements.Add(temp);
        }

        public void addTextInputAutomatic(string text, string initVal, Color col)
        {
            int height = baseHeight + 8; // Increased base height
            if (text != "")
                height += elementSpacing + 8; // Added extra spacing for labels
            currentPos.Y += height;
            addTextInput(new Rectangle((int)currentPos.X, (int)currentPos.Y, sliderFullWidth, baseHeight), true, true, text, initVal, col);
        }

        public void addTextInput(Rectangle size, bool enabled, bool visible, string text, string initVal, Color col)
        {
            InterfaceTextInput temp = new InterfaceTextInput((_SM as InfiniminerGame), _P);
            temp.size = size;
            temp.enabled = enabled;
            temp.visible = visible;
            temp.text = text;
            temp.value = initVal;
            temp.color = col;
            elements.Add(temp);
        }

        public void addLabelAutomatic(string text)
        {
            currentPos.Y += elementSpacing;
            addLabel(new Rectangle((int)currentPos.X, (int)currentPos.Y, 0, 0), true, text);
        }

        public void addLabel(Rectangle size, bool visible, string text)
        {
            InterfaceLabel temp = new InterfaceLabel((_SM as InfiniminerGame), _P);
            temp.size = size;
            temp.visible = visible;
            temp.text = text;
            elements.Add(temp);
        }

        public void addButtonCycleAutomatic(string text, string[] options, string currentValue, Color col)
        {
            int height = baseHeight + 8; // Increased base height
            if (text != "")
                height += elementSpacing + 8; // Added extra spacing for labels
            currentPos.Y += height;
            addButtonCycle(new Rectangle((int)currentPos.X, (int)currentPos.Y, sliderFullWidth, baseHeight), true, true, text, options, currentValue, col);
        }

        public void addButtonCycle(Rectangle size, bool enabled, bool visible, string text, string[] options, string currentValue, Color col)
        {
            InterfaceButtonCycle temp = new InterfaceButtonCycle((_SM as InfiniminerGame), _P);
            temp.size = size;
            temp.enabled = enabled;
            temp.visible = visible;
            temp.text = text;
            temp.options = options;
            temp.color = col;
            temp.SetValue(currentValue);
            elements.Add(temp);
        }

        public override void OnEnter(string oldState)
        {
            _SM.IsMouseVisible = true;

            // Calculate DPI scaling first
            CalculateUIScale();

            // No longer loading background texture - using solid black instead
            drawRect = new Rectangle(_SM.GraphicsDevice.Viewport.Width / 2 - (int)(1024 * uiScale) / 2,
                                     _SM.GraphicsDevice.Viewport.Height / 2 - (int)(768 * uiScale) / 2,
                                     (int)(1024 * uiScale),
                                     (int)(768 * uiScale));

            // Update click regions after calculating background position
            UpdateClickRegions();

            //Read the data from file
            DatafileWriter dw = new DatafileWriter("client.config.txt");
            
            // Store original settings for immediate application
            originalSettings.Clear();
            foreach (var kvp in dw.Data)
            {
                originalSettings[kvp.Key] = kvp.Value;
            }

            currentPos = new Vector2((int)(200 * uiScale), (int)(100 * uiScale));
            originalY = (int)currentPos.Y;

            addLabelAutomatic("User Settings");
            if (_P.playerHandle.ToLower() == "player" || !dw.Data.ContainsKey("handle"))
                addTextInputAutomatic("Name", dw.Data.ContainsKey("handle") ? dw.Data["handle"] : "Player", Color.Red);
            else
                addTextInputAutomatic("Name", dw.Data.ContainsKey("handle") ? dw.Data["handle"] : "Player", Color.White);
            addSpace(16);

            addLabelAutomatic("Screen Settings");
                // Get current monitor resolution
                int monitorWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
                int monitorHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
                
                addTextInputAutomatic("Screen Width", dw.Data.ContainsKey("width") ? dw.Data["width"] : monitorWidth.ToString(), Color.White);
                addTextInputAutomatic("Screen Height", dw.Data.ContainsKey("height") ? dw.Data["height"] : monitorHeight.ToString(), Color.White);
                addButtonCycleAutomatic("Window Mode", 
                    new string[] { "Fullscreen", "Borderless", "Windowed" },
                    dw.Data.ContainsKey("windowmode") ? dw.Data["windowmode"] : "borderless", 
                    Color.White);
            addSpace(16);

            addLabelAutomatic("Sound Settings");
                addSliderAutomatic("Volume", 1f, 100f, dw.Data.ContainsKey("volume") ? float.Parse(dw.Data["volume"])*100 : 100f, true);
                addButtonAutomatic("Enable Sound", "On", "NoSound", dw.Data.ContainsKey("nosound") ? !bool.Parse(dw.Data["nosound"]) : true, Color.White);
            addSpace(16);

            shiftColumn();

            addLabelAutomatic("Mouse Settings");
            addButtonAutomatic("Invert Mouse", "Yes", "No", dw.Data.ContainsKey("yinvert") ? bool.Parse(dw.Data["yinvert"]) : false, Color.White);
                addSliderAutomatic("Mouse Sensitivity", 1f, 10f, dw.Data.ContainsKey("sensitivity") ? float.Parse(dw.Data["sensitivity"]) : 5f, true);
            addSpace(16);

            addLabelAutomatic("Misc Settings");
            addButtonAutomatic("Bloom", "Pretty", "Boring", dw.Data.ContainsKey("pretty") ? bool.Parse(dw.Data["pretty"]) : true, Color.White);
            addButtonAutomatic("Light", "Pretty", "Boring", dw.Data.ContainsKey("light") ? bool.Parse(dw.Data["light"]) : true, Color.White);
            addButtonAutomatic("Input Lag Fix", "Yes", "No", dw.Data.ContainsKey("inputlagfix") ? bool.Parse(dw.Data["inputlagfix"]) : true, Color.White);
            addButtonAutomatic("Show FPS", "Yes", "No", dw.Data.ContainsKey("showfps") ? bool.Parse(dw.Data["showfps"]) : true, Color.White);
            addSpace(16);
        }

        public override void OnLeave(string newState)
        {
            base.OnLeave(newState);
        }

        public override void OnMouseDown(MouseButton button, int x, int y)
        {
            base.OnMouseDown(button, x, y);
            
            foreach (InterfaceElement element in elements)
            {
                element.OnMouseDown(button, x, y);
            }
            
            string hitResult = ClickRegion.HitTest(clkMenuSettings, new Point(x, y));
            
            switch(hitResult)
            {
                case "cancel":
                    if (_P.playerHandle.ToLower() != "player")
                    {
                        nextState = "Infiniminer.States.ServerBrowserState";
                    }
                    else
                    {
                        _SM.Exit();
                    }
                    break;
                case "accept":
                    if (saveData() >= 1)
                    {
                        // Settings saved successfully
                        CrossPlatformServices.Instance.RestartApplication();
                    }
                    else
                    {
                        clearSuccessMessages();
                        addLabelAutomatic("Data refused to save!");
                        addLabelAutomatic("User has no write permissions to client.config.txt");
                        addLabelAutomatic("or client.config.txt is read-only!");
                        addLabelAutomatic("Use wordpad and edit client.config.txt manually to");
                        addLabelAutomatic("enter your name and settings.");
                    }
                    break;
            }
        }

        public override void OnMouseUp(MouseButton button, int x, int y)
        {
            base.OnMouseUp(button, x, y);
            foreach (InterfaceElement element in elements)
            {
                element.OnMouseUp(button, x, y);
            }
        }

        public void clearSuccessMessages()
        {
            // Remove any existing success message labels
            elements.RemoveAll(element => 
                element is InterfaceLabel label && 
                (label.text == "Settings saved successfully!" || 
                 label.text == "(Restart the game to apply all settings.)"));
        }

        public int saveData()
        {
            DatafileWriter dw = new DatafileWriter("client.config.txt");
            bool needsRestart = false;
            
            foreach (InterfaceElement element in elements)
            {
                switch (element.text)
                {
                    case "Name": 
                        string newHandle = (element as InterfaceTextInput).value;
                        dw.Data["handle"] = newHandle;
                        // Apply immediately
                        _P.playerHandle = newHandle;
                        break;
                        
                    case "Screen Width":
                        string newWidth = (element as InterfaceTextInput).value;
                        dw.Data["width"] = newWidth;
                        needsRestart = true;
                        break;
                        
                    case "Screen Height":
                        string newHeight = (element as InterfaceTextInput).value;
                        dw.Data["height"] = newHeight;
                        needsRestart = true;
                        break;
                        
                    case "Window Mode":
                        string windowMode = (element as InterfaceButtonCycle).value.ToLower();
                        dw.Data["windowmode"] = windowMode;
                        dw.Data["fullscreen"] = (windowMode == "fullscreen").ToString().ToLower();
                        dw.Data["borderless"] = (windowMode == "borderless").ToString().ToLower();
                        needsRestart = true;
                        break;
                        
                    case "Volume": 
                        float newVolume = (element as InterfaceSlider).value / 100;
                        dw.Data["volume"] = newVolume.ToString();
                        // Apply immediately
                        _P.volumeLevel = newVolume;
                        break;
                        
                    case "Enable Sound": 
                        bool soundEnabled = (element as InterfaceButtonToggle).clicked;
                        dw.Data["nosound"] = (!soundEnabled).ToString().ToLower();
                        needsRestart = true;
                        break;
                        
                    case "Invert Mouse": 
                        dw.Data["yinvert"] = (element as InterfaceButtonToggle).clicked.ToString().ToLower();
                        break;
                        
                    case "Mouse Sensitivity": 
                        float newSensitivity = (element as InterfaceSlider).value;
                        dw.Data["sensitivity"] = newSensitivity.ToString();
                        // Apply immediately
                        _P.mouseSensitivity = newSensitivity * 0.001f;
                        break;
                        
                    case "Bloom": 
                        dw.Data["pretty"] = (element as InterfaceButtonToggle).clicked.ToString().ToLower();
                        break;
                        
                    case "Light": 
                        dw.Data["light"] = (element as InterfaceButtonToggle).clicked.ToString().ToLower();
                        break;
                        
                    case "Input Lag Fix": 
                        dw.Data["inputlagfix"] = (element as InterfaceButtonToggle).clicked.ToString().ToLower();
                        break;
                        
                    case "Show FPS": 
                        dw.Data["showfps"] = (element as InterfaceButtonToggle).clicked.ToString().ToLower();
                        break;
                }
            }
            
            // Always try to write changes since we've updated all settings
            int result = dw.WriteChanges("client.config.txt");
            
            if (result >= 1)
            {
                clearSuccessMessages();
                addLabelAutomatic("Settings saved successfully!");
                if (needsRestart)
                {
                    addLabelAutomatic("(Restart the game to apply all settings.)");
                }
            }
            else
            {
                clearSuccessMessages();
                addLabelAutomatic("Failed to save settings!");
                addLabelAutomatic("Check if client.config.txt is read-only");
                addLabelAutomatic("or if you have write permissions.");
            }
            
            return result;
        }

        public override void OnCharEntered(EventInput.CharacterEventArgs e)
        {
            base.OnCharEntered(e);
            foreach (InterfaceElement element in elements)
            {
                element.OnCharEntered(e);
            }
        }

        public override void OnKeyDown(Keys key)
        {
            base.OnKeyDown(key);
            if (key == Keys.Escape)
            {
                nextState = "Infiniminer.States.ServerBrowserState";
            }
            else
            {
                foreach (InterfaceElement element in elements)
                {
                    element.OnKeyDown(key);
                }
            }
        }

        public override void OnKeyUp(Keys key)
        {
            base.OnKeyUp(key);
            foreach (InterfaceElement element in elements)
            {
                element.OnKeyUp(key);
            }
        }

        public override string OnUpdate(GameTime gameTime, KeyboardState keyState, MouseState mouseState)
        {
            return nextState;
        }

        public override void OnRenderAtUpdate(GraphicsDevice graphicsDevice, GameTime gameTime)
        {
            SpriteBatch spriteBatch = new SpriteBatch(graphicsDevice);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            
            // Draw solid black background instead of texture
            Texture2D blackTexture = new Texture2D(graphicsDevice, 1, 1, false, SurfaceFormat.Color);
            blackTexture.SetData(new Color[] { Color.Black });
            spriteBatch.Draw(blackTexture, drawRect, Color.Black);
            blackTexture.Dispose();
            
            // Draw custom button text
            SpriteFont uiFont = _SM.Content.Load<SpriteFont>(_SM.font);
            
            // Calculate button positions relative to background
            int bgOffsetX = _SM.GraphicsDevice.Viewport.Width / 2 - (int)(1024 * uiScale) / 2;
            int bgOffsetY = _SM.GraphicsDevice.Viewport.Height / 2 - (int)(768 * uiScale) / 2;
            
            // Cancel button text
            Vector2 cancelPos = new Vector2(
                bgOffsetX + (int)(127 * uiScale), // Center of cancel button
                bgOffsetY + (int)(734 * uiScale)  // Center vertically
            );
            string cancelText = "CANCEL";
            Vector2 cancelSize = uiFont.MeasureString(cancelText);
            spriteBatch.DrawString(uiFont, cancelText, 
                new Vector2(cancelPos.X - cancelSize.X/2, cancelPos.Y - cancelSize.Y/2), 
                Color.White);
            
            // Accept button text  
            Vector2 acceptPos = new Vector2(
                bgOffsetX + (int)(774 * uiScale), // Center of accept button
                bgOffsetY + (int)(734 * uiScale)  // Center vertically
            );
            string acceptText = "APPLY SETTINGS & RESTART";
            Vector2 acceptSize = uiFont.MeasureString(acceptText);
            spriteBatch.DrawString(uiFont, acceptText, 
                new Vector2(acceptPos.X - acceptSize.X/2, acceptPos.Y - acceptSize.Y/2), 
                Color.White);
            
            spriteBatch.End();
            
            // Render UI elements
            foreach (InterfaceElement element in elements)
            {
                element.Render(graphicsDevice);
            }
        }
    }
}
