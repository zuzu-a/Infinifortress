using System;
using System.Collections.Generic;
using System.Runtime.InteropServices; 
using System.Reflection;
using Infiniminer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace StateMasher
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    /// 
    public class StateMachine : Microsoft.Xna.Framework.Game
    {
        [DllImport("user32.dll")]
        public static extern int GetForegroundWindow(); 

        public GraphicsDeviceManager graphicsDeviceManager;
        public Infiniminer.PropertyBag propertyBag = null;
        public string font = "font_04b08";
        private string currentStateType = "";
        public string CurrentStateType
        {
            get { return currentStateType; }
        }

        private State currentState = null;
        private bool needToRenderOnEnter = false;

        private int frameCount = 0;
        private DateTime lastFPScheck = DateTime.Now;
        private double frameRate = 0;
        public double FrameRate
        {
            get { return frameRate; }
        }

        private MouseState msOld;
        private KeyboardState kbOld;
        private Dictionary<Keys, bool> keyRepeatTracker = new Dictionary<Keys, bool>();

        public StateMachine()
        {
            Content.RootDirectory = "Content";
            graphicsDeviceManager = new GraphicsDeviceManager(this);
            // EventInput system was removed during XNA 4.0 migration
            // We'll handle character input manually in Update()
        }

        protected void ChangeState(string newState)
        {
            // Call OnLeave for the old state.
            if (currentState != null)
                currentState.OnLeave(newState);

            // Instantiate and set the new state.
            Assembly a = Assembly.GetExecutingAssembly();
            Type t = a.GetType(newState);
            currentState = Activator.CreateInstance(t) as State;

            // Set up the new state.
            currentState._P = propertyBag;
            currentState._SM = this;
            currentState.OnEnter(currentStateType);
            currentStateType = newState;
            needToRenderOnEnter = true;
        }

        public bool WindowHasFocus()
        {
            return IsActive;
        }

        private char ConvertKeyToChar(Keys key, bool shiftPressed)
        {
            // Handle letters
            if (key >= Keys.A && key <= Keys.Z)
            {
                char letter = (char)('a' + (key - Keys.A));
                return shiftPressed ? char.ToUpper(letter) : letter;
            }
            
            // Handle numbers
            if (key >= Keys.D0 && key <= Keys.D9)
            {
                if (shiftPressed)
                {
                    switch (key)
                    {
                        case Keys.D1: return '!';
                        case Keys.D2: return '@';
                        case Keys.D3: return '#';
                        case Keys.D4: return '$';
                        case Keys.D5: return '%';
                        case Keys.D6: return '^';
                        case Keys.D7: return '&';
                        case Keys.D8: return '*';
                        case Keys.D9: return '(';
                        case Keys.D0: return ')';
                    }
                }
                else
                {
                    return (char)('0' + (key - Keys.D0));
                }
            }

            // Handle numpad numbers
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
            {
                return (char)('0' + (key - Keys.NumPad0));
            }

            // Handle special characters
            switch (key)
            {
                case Keys.Space: return ' ';
                case Keys.OemPeriod: return shiftPressed ? '>' : '.';
                case Keys.OemComma: return shiftPressed ? '<' : ',';
                case Keys.OemQuestion: return shiftPressed ? '?' : '/';
                case Keys.OemSemicolon: return shiftPressed ? ':' : ';';
                case Keys.OemQuotes: return shiftPressed ? '"' : '\'';
                case Keys.OemOpenBrackets: return shiftPressed ? '{' : '[';
                case Keys.OemCloseBrackets: return shiftPressed ? '}' : ']';
                case Keys.OemPipe: return shiftPressed ? '|' : '\\';
                case Keys.OemMinus: return shiftPressed ? '_' : '-';
                case Keys.OemPlus: return shiftPressed ? '+' : '=';
                case Keys.OemTilde: return shiftPressed ? '~' : '`';
                default: return '\0'; // Unsupported key
            }
        }

        protected override void Initialize()
        {
            base.Initialize();
        }

        protected override void LoadContent()
        {
            
        }

        protected override void UnloadContent()
        {
        }

        protected override void Update(GameTime gameTime)
        {
            if (lastFPScheck <= DateTime.Now - TimeSpan.FromMilliseconds(1000))
            {
                lastFPScheck = DateTime.Now;
                frameRate = frameCount;// / gameTime.ElapsedTotalTime.TotalSeconds;
                frameCount = 0;
            }

            if (currentState != null && propertyBag != null)
            {
                // Get current input states
                KeyboardState kbNew = Keyboard.GetState();
                MouseState msNew = Mouse.GetState();
                
                // Call OnUpdate.
                string newState = currentState.OnUpdate(gameTime, kbNew, msNew);
                if (newState != null)
                    ChangeState(newState);

                // Check for keyboard events
                Keys[] pressedKeys = kbNew.GetPressedKeys();
                Keys[] oldPressedKeys = kbOld.GetPressedKeys();
                
                // Check for key down events
                foreach (Keys key in pressedKeys)
                {
                    if (!kbOld.IsKeyDown(key))
                    {
                        currentState.OnKeyDown(key);
                    }
                }
                
                // Check for key up events  
                foreach (Keys key in oldPressedKeys)
                {
                    if (!kbNew.IsKeyDown(key))
                    {
                        currentState.OnKeyUp(key);
                    }
                }

                // Handle character input conversion for chat and text input
                foreach (Keys key in pressedKeys)
                {
                    if (!kbOld.IsKeyDown(key)) // Only on initial key press
                    {
                        char character = ConvertKeyToChar(key, kbNew.IsKeyDown(Keys.LeftShift) || kbNew.IsKeyDown(Keys.RightShift));
                        if (character != '\0')
                        {
                            EventInput.CharacterEventArgs charArgs = new EventInput.CharacterEventArgs(character, 0);
                            currentState.OnCharEntered(charArgs);
                        }
                    }
                }

                bool hasFocus = WindowHasFocus();
                
                // TEMPORARILY REMOVE FOCUS CHECK - Process input regardless of focus
                // if (hasFocus)
                {
                    if (msOld.LeftButton == ButtonState.Released && msNew.LeftButton == ButtonState.Pressed)
                        currentState.OnMouseDown(MouseButton.LeftButton, msNew.X, msNew.Y);
                    if (msOld.MiddleButton == ButtonState.Released && msNew.MiddleButton == ButtonState.Pressed)
                        currentState.OnMouseDown(MouseButton.MiddleButton, msNew.X, msNew.Y);
                    if (msOld.RightButton == ButtonState.Released && msNew.RightButton == ButtonState.Pressed)
                        currentState.OnMouseDown(MouseButton.RightButton, msNew.X, msNew.Y);
                    if (msOld.LeftButton == ButtonState.Pressed && msNew.LeftButton == ButtonState.Released)
                        currentState.OnMouseUp(MouseButton.LeftButton, msNew.X, msNew.Y);
                    if (msOld.MiddleButton == ButtonState.Pressed && msNew.MiddleButton == ButtonState.Released)
                        currentState.OnMouseUp(MouseButton.MiddleButton, msNew.X, msNew.Y);
                    if (msOld.RightButton == ButtonState.Pressed && msNew.RightButton == ButtonState.Released)
                        currentState.OnMouseUp(MouseButton.RightButton, msNew.X, msNew.Y);
                    if (msOld.ScrollWheelValue != msNew.ScrollWheelValue)
                        currentState.OnMouseScroll(msNew.ScrollWheelValue - msOld.ScrollWheelValue);
                }
                
                // Update old states
                msOld = msNew;
                kbOld = kbNew;
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            
            // Call OnRenderAtUpdate.
            if (currentState != null && propertyBag != null)
            {
                frameCount += 1;
                currentState.OnRenderAtUpdate(GraphicsDevice, gameTime);
            }

            // If we have one queued, call OnRenderAtEnter.
            if (currentState != null && needToRenderOnEnter && propertyBag != null)
            {
                needToRenderOnEnter = false;
                currentState.OnRenderAtEnter(GraphicsDevice);
            }
            
            base.Draw(gameTime);
        }
    }
}
