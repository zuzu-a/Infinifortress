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
    class CylinderDrawingComponent
    {
        private static Model cylModel;
        private static BasicEffect effect;
        private static GraphicsDevice graphicsDevice;

        public static void Load(ContentManager content, GraphicsDevice gDevice)
        {
            graphicsDevice = gDevice;
            cylModel = content.Load<Model>("Content/basketball");
            effect = new BasicEffect(gDevice);
        }

        private struct Cylinder
        {
            public Vector3 start;
            public Vector3 end;

            public Cylinder(Vector3 start, Vector3 end)
            {
                this.start = start;
                this.end = end;
            }
        }

        private static Queue<Cylinder> cylQueue = new Queue<Cylinder>();

        public static void AddLine(Vector3 start, Vector3 end)
        {
            cylQueue.Enqueue(new Cylinder(start, end));
        }

        public static void Draw(Matrix View, Matrix Projection)
        {
            if (cylQueue.Count > 0)
            {
                for (int i = 0; i < cylQueue.Count; i++)
                {
                    Cylinder cyl = cylQueue.Dequeue();
                    Matrix world = Matrix.Identity;
                    float distScale = .2f * 2;
                    float width = 1f;
                    float height = 1f;
                    float distance = Vector3.Distance(cyl.end, cyl.start);
                    Vector3 direction = Vector3.Normalize(cyl.end - cyl.start);

                    bool doFor = Math.Abs(Vector3.Dot(direction, Vector3.Up)) < .999f;

                    Matrix connector_matrix = Matrix.CreateScale(width, height, distance * distScale) *
                        Matrix.CreateWorld(Vector3.Zero, -direction, doFor ? Vector3.UnitX : Vector3.Up);
                    connector_matrix.Translation = cyl.start;

                    world = connector_matrix;
                    foreach (ModelMesh mesh in cylModel.Meshes)
                    {
                        foreach (BasicEffect effect in mesh.Effects)
                        {
                            effect.World = world;
                            //effect.EnableDefaultLighting(); 
                            effect.View = View;
                            effect.Projection = Projection;
                        }
                        mesh.Draw();
                    }

                }
            }
        }
    } 

    public class Particle
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Size;
        public Vector4 Color;
        public bool FlaggedForDeletion = false;
        public float Bounce = 0.0f;
        public DateTime Lifetime = DateTime.Now;
        public float SizeChange = 1.0f;//when deleting 
        public float Gravity = 8.0f;
        public TimeSpan Expirytime = TimeSpan.FromSeconds(20.0);
    }

    public class ParticleEngine
    {
        InfiniminerGame gameInstance;
        PropertyBag _P;
        List<Particle> particleList;
        Effect particleEffect;
        Random randGen;
        VertexBuffer vertexBuffer;

        public ParticleEngine(InfiniminerGame gameInstance)
        {
            this.gameInstance = gameInstance;
            particleEffect = gameInstance.Content.Load<Effect>("effect_particle");
            randGen = new Random();
            particleList = new List<Particle>();

            VertexPositionTextureShade[] vertices = GenerateVertices();
            vertexBuffer = new VertexBuffer(gameInstance.GraphicsDevice, typeof(VertexPositionTextureShade), vertices.Length, BufferUsage.WriteOnly);
            vertexBuffer.SetData(vertices);
        }

        private VertexPositionTextureShade[] GenerateVertices()
        {
            VertexPositionTextureShade[] cubeVerts = new VertexPositionTextureShade[36];

            // BOTTOM
            cubeVerts[0] = new VertexPositionTextureShade(new Vector3(-1, -1, -1), new Vector2(0, 0), 0.3);
            cubeVerts[1] = new VertexPositionTextureShade(new Vector3(1, -1, -1), new Vector2(0, 0), 0.3);
            cubeVerts[2] = new VertexPositionTextureShade(new Vector3(-1, 1, -1), new Vector2(0, 0), 0.3);
            cubeVerts[3] = new VertexPositionTextureShade(new Vector3(1, -1, -1), new Vector2(0, 0), 0.3);
            cubeVerts[4] = new VertexPositionTextureShade(new Vector3(1, 1, -1), new Vector2(0, 0), 0.3);
            cubeVerts[5] = new VertexPositionTextureShade(new Vector3(-1, 1, -1), new Vector2(0, 0), 0.3);

            // TOP
            cubeVerts[30] = new VertexPositionTextureShade(new Vector3(-1, -1, 1), new Vector2(0, 0), 1.0);
            cubeVerts[31] = new VertexPositionTextureShade(new Vector3(1, -1, 1), new Vector2(0, 0), 1.0);
            cubeVerts[32] = new VertexPositionTextureShade(new Vector3(-1, 1, 1), new Vector2(0, 0), 1.0);
            cubeVerts[33] = new VertexPositionTextureShade(new Vector3(1, -1, 1), new Vector2(0, 0), 1.0);
            cubeVerts[34] = new VertexPositionTextureShade(new Vector3(1, 1, 1), new Vector2(0, 0), 1.0);
            cubeVerts[35] = new VertexPositionTextureShade(new Vector3(-1, 1, 1), new Vector2(0, 0), 1.0);

            // LEFT
            cubeVerts[6] = new VertexPositionTextureShade(new Vector3(-1, -1, -1), new Vector2(0, 0), 0.7);
            cubeVerts[7] = new VertexPositionTextureShade(new Vector3(-1, -1, 1), new Vector2(0, 0), 0.7);
            cubeVerts[8] = new VertexPositionTextureShade(new Vector3(-1, 1, 1), new Vector2(0, 0), 0.7);
            cubeVerts[9] = new VertexPositionTextureShade(new Vector3(-1, -1, -1), new Vector2(0, 0), 0.7);
            cubeVerts[10] = new VertexPositionTextureShade(new Vector3(-1, 1, 1), new Vector2(0, 0), 0.7);
            cubeVerts[11] = new VertexPositionTextureShade(new Vector3(-1, 1, -1), new Vector2(0, 0), 0.7);

            // RIGHT
            cubeVerts[12] = new VertexPositionTextureShade(new Vector3(1, -1, -1), new Vector2(0, 0), 0.7);
            cubeVerts[13] = new VertexPositionTextureShade(new Vector3(1, -1, 1), new Vector2(0, 0), 0.7);
            cubeVerts[14] = new VertexPositionTextureShade(new Vector3(1, 1, 1), new Vector2(0, 0), 0.7);
            cubeVerts[15] = new VertexPositionTextureShade(new Vector3(1, -1, -1), new Vector2(0, 0), 0.7);
            cubeVerts[16] = new VertexPositionTextureShade(new Vector3(1, 1, 1), new Vector2(0, 0), 0.7);
            cubeVerts[17] = new VertexPositionTextureShade(new Vector3(1, 1, -1), new Vector2(0, 0), 0.7);

            // FRONT
            cubeVerts[18] = new VertexPositionTextureShade(new Vector3(-1, 1, -1), new Vector2(0, 0), 0.5);
            cubeVerts[19] = new VertexPositionTextureShade(new Vector3(-1, 1, 1), new Vector2(0, 0), 0.5);
            cubeVerts[20] = new VertexPositionTextureShade(new Vector3(1, 1, 1), new Vector2(0, 0), 0.5);
            cubeVerts[21] = new VertexPositionTextureShade(new Vector3(-1, 1, -1), new Vector2(0, 0), 0.5);
            cubeVerts[22] = new VertexPositionTextureShade(new Vector3(1, 1, 1), new Vector2(0, 0), 0.5);
            cubeVerts[23] = new VertexPositionTextureShade(new Vector3(1, 1, -1), new Vector2(0, 0), 0.5);

            // BACK
            cubeVerts[24] = new VertexPositionTextureShade(new Vector3(-1, -1, -1), new Vector2(0, 0), 0.5);
            cubeVerts[25] = new VertexPositionTextureShade(new Vector3(-1, -1, 1), new Vector2(0, 0), 0.5);
            cubeVerts[26] = new VertexPositionTextureShade(new Vector3(1, -1, 1), new Vector2(0, 0), 0.5);
            cubeVerts[27] = new VertexPositionTextureShade(new Vector3(-1, -1, -1), new Vector2(0, 0), 0.5);
            cubeVerts[28] = new VertexPositionTextureShade(new Vector3(1, -1, 1), new Vector2(0, 0), 0.5);
            cubeVerts[29] = new VertexPositionTextureShade(new Vector3(1, -1, -1), new Vector2(0, 0), 0.5);

            return cubeVerts;
        }

        private static bool ParticleExpired(Particle particle)
        {
            return particle.FlaggedForDeletion;
        }

        public void Update(GameTime gameTime)
        {
            if (_P == null)
                return;

            foreach (Particle p in particleList)
            {
                if (p.Bounce != 0.0f)
                {
                    if (gameInstance.propertyBag.blockEngine.BlockAtPoint(p.Position + ((float)(gameTime.ElapsedGameTime.TotalSeconds) * p.Velocity)) == BlockType.None)
                    {
                        p.Position += (float)gameTime.ElapsedGameTime.TotalSeconds * p.Velocity;
                        p.Velocity.Y -= p.Gravity * (float)gameTime.ElapsedGameTime.TotalSeconds;
                    }
                    else
                    {
                       
                        Vector3 nv = p.Velocity;//adjustment axis
                        //Vector3 fv = p.Velocity;//final velocity

                        nv.X = 0;
                        nv.Z = 0;
                        if (Math.Abs(p.Velocity.Y) > 2.0f)
                        if (gameInstance.propertyBag.blockEngine.BlockAtPoint(p.Position + ((float)(gameTime.ElapsedGameTime.TotalSeconds) * nv)) != BlockType.None)
                        {
                            p.Position.Y -= (float)gameTime.ElapsedGameTime.TotalSeconds * p.Velocity.Y / 2;
                            p.Velocity.Y = -p.Velocity.Y * p.Bounce;
                        }

                        nv.X = p.Velocity.X;
                        nv.Y = 0;
                        nv.Z = 0;
                        if (Math.Abs(p.Velocity.X) > 0.3f)
                        if (gameInstance.propertyBag.blockEngine.BlockAtPoint(p.Position + ((float)(gameTime.ElapsedGameTime.TotalSeconds) * nv)) != BlockType.None)
                        {
                            p.Position.X -= (float)gameTime.ElapsedGameTime.TotalSeconds * p.Velocity.X / 2;
                            p.Velocity.X = -p.Velocity.X * p.Bounce;
                        }

                        nv.X = 0;
                        nv.Y = 0;
                        nv.Z = p.Velocity.Z;
                        if (Math.Abs(p.Velocity.Z) > 0.3f)
                        if (gameInstance.propertyBag.blockEngine.BlockAtPoint(p.Position + ((float)(gameTime.ElapsedGameTime.TotalSeconds) * nv)) != BlockType.None)
                        {
                            p.Position.Z -= (float)gameTime.ElapsedGameTime.TotalSeconds * p.Velocity.Z / 2;
                            p.Velocity.Z = -p.Velocity.Z * p.Bounce;
                        }
                        //p.Position -= (float)gameTime.ElapsedGameTime.TotalSeconds * p.Velocity / 2;
                        //p.Velocity.Y -= (p.Velocity.Y * (float)gameTime.ElapsedGameTime.TotalSeconds) / 2;
                    }
                }
                else
                {
                    p.Position += (float)gameTime.ElapsedGameTime.TotalSeconds * p.Velocity;
                    p.Velocity.Y -= 8 * (float)gameTime.ElapsedGameTime.TotalSeconds;
                }

                if (p.Bounce != 0.0f)
                {
                    if (p.Lifetime < DateTime.Now)
                    {
                        if (p.Size - ((float)gameTime.ElapsedGameTime.TotalSeconds * p.SizeChange) > 0.005)
                            p.Size = p.Size - ((float)gameTime.ElapsedGameTime.TotalSeconds * p.SizeChange);
                        else
                            p.FlaggedForDeletion = true;//hit minimum size, removing
         
                        if (p.Lifetime < DateTime.Now - p.Expirytime)
                        {
                            p.FlaggedForDeletion = true;
                        }
                    }
                }
                else
                {
                    if (gameInstance.propertyBag.blockEngine.SolidAtPointForPlayer(p.Position) || p.Lifetime < DateTime.Now - p.Expirytime)
                    {
                        p.FlaggedForDeletion = true;
                    }
                }
            }
            particleList.RemoveAll(ParticleExpired);
        }

        public void CreateExplosionDebris(Vector3 explosionPosition)
        {
            for (int i = 0; i < 50; i++)
            {
                Particle p = new Particle();
                p.Color = new Vector4(0.35f,0.235f,0.156f,1.0f);
                p.Size = (float)(randGen.NextDouble() * 0.4 + 0.05);
                p.Position = explosionPosition;
                p.Position.Y += (float)randGen.NextDouble() - 0.5f;
                p.Velocity = new Vector3((float)randGen.NextDouble() * 8 - 4, (float)randGen.NextDouble() * 8, (float)randGen.NextDouble() * 8 - 4);
                p.Lifetime = DateTime.Now + TimeSpan.FromSeconds(5.0);
                p.Expirytime = TimeSpan.FromSeconds(0.0);
                particleList.Add(p);
            }
        }

        public void CreateDiggingDebris(Vector3 explosionPosition, BlockType block)
        {
            for (int i = 0; i < 10; i++)
            {
                Particle p = new Particle();
                p.Color = new Vector4(0.3f,0.3f,0.3f,1.0f);//0.35f,0.235f,0.156f,1.0f);//new Color(90, 60, 40);
                p.Size = (float)(randGen.NextDouble() * 0.05 + 0.01);
                p.Position = explosionPosition;
                p.Position.Y += (float)randGen.NextDouble() - 0.5f;
                p.Velocity = new Vector3((float)randGen.NextDouble() * 4 - 2, (float)randGen.NextDouble() * 3, (float)randGen.NextDouble() * 4 - 2);
                p.Lifetime = DateTime.Now+TimeSpan.FromSeconds(2.0);
                p.Bounce = 0.3f;
                p.SizeChange = 0.1f;

                switch (block)
                {
                    case BlockType.Water:
                        {
                            p.Lifetime = DateTime.Now + TimeSpan.FromSeconds(0.0 - randGen.NextDouble());
                            p.Color = new Vector4(0.1f + (float)((randGen.NextDouble() - 0.5f) * 0.025f), 0.4f + (float)((randGen.NextDouble() - 0.5f) * 0.025f), 0.70f + (float)((randGen.NextDouble() - 0.5f) * 0.025f), 1.0f);
                            p.SizeChange = 0.2f;
                            p.Bounce = 0.0f;
                            p.Position.Y += ((float)randGen.NextDouble() * 0.5f) + 0.75f;
                            p.Velocity = new Vector3(0.0f, -0.25f + (float)randGen.NextDouble(), 0.0f);
                            p.Gravity = 6.0f;
                        }
                        break;

                    case BlockType.Lava:
                        {
                            p.Lifetime = DateTime.Now + TimeSpan.FromSeconds(0.0 - randGen.NextDouble());
                            int ll = randGen.Next(0, 2);
                            if (ll == 0)
                            {
                                p.Color = new Vector4(0.95f + (float)((randGen.NextDouble() - 0.5f) * 0.025f), 0.7f + (float)((randGen.NextDouble() - 0.5f) * 0.025f), 0.05f + (float)((randGen.NextDouble() - 0.5f) * 0.025f), 1.0f);
                                p.Gravity = 6.0f;
                                p.Velocity = new Vector3(((float)randGen.NextDouble() - 0.5f) * 2.0f, 1.0f + (float)randGen.NextDouble() * 2.0f - 0.25f, ((float)randGen.NextDouble() - 0.5f) * 2.0f);
                            }
                            else if (ll == 1)
                            {
                                p.Color = new Vector4(0.3f + (float)((randGen.NextDouble() - 0.5f) * 0.01f), 0.01f + (float)((randGen.NextDouble() - 0.5f) * 0.025f), 0.0f, 1.0f);
                                p.Gravity = 2.0f + (float)randGen.NextDouble() - 0.5f;
                                p.Velocity = new Vector3(((float)randGen.NextDouble() - 0.5f) * 2.0f, 1.0f + (float)randGen.NextDouble() * 2.0f - 0.25f, ((float)randGen.NextDouble() - 0.5f) * 2.0f);
                            }
                            else if (ll == 2)
                            {
                                p.Gravity = 4.0f + (float)randGen.NextDouble() - 0.5f;
                                p.Color = new Vector4(0.8f + (float)((randGen.NextDouble() - 0.5f) * 0.025f), 0.02f + (float)((randGen.NextDouble() - 0.5f) * 0.015f), 0.01f, 1.0f);
                                p.Velocity = new Vector3(((float)randGen.NextDouble() - 0.5f) * 2.0f, (float)randGen.NextDouble() * 1.0f - 0.25f, ((float)randGen.NextDouble() - 0.5f) * 2.0f);
                            }
                            p.SizeChange = 0.2f;
                            p.Bounce = 0.0f;
                            p.Position.Y += 1.0f + (float)randGen.NextDouble() - 0.5f;
                        }
                        break;

                    case BlockType.Dirt:
                        p.Color = new Vector4(0.35f + (float)((randGen.NextDouble() - 0.5f) * 0.03f), 0.235f + (float)((randGen.NextDouble() - 0.5f) * 0.02f), 0.156f + (float)((randGen.NextDouble() - 0.5f) * 0.015f), 1.0f);
                        break;

                    case BlockType.Gold:
                        {
                            int ll = randGen.Next(0, 3);
                            if (ll == 2)
                            {
                                p.Color = new Vector4(0.35f + (float)((randGen.NextDouble() - 0.5f) * 0.03f), 0.235f + (float)((randGen.NextDouble() - 0.5f) * 0.02f), 0.156f + (float)((randGen.NextDouble() - 0.5f) * 0.015f), 1.0f);
                            }
                            else
                            {
                                p.Color = new Vector4(0.65f + (float)((randGen.NextDouble() - 0.5f) * 0.1f), 0.35f + (float)((randGen.NextDouble() - 0.5f) * 0.1f), 0.15f + (float)((randGen.NextDouble() - 0.5f) * 0.1f), 1.0f);
                                p.Gravity = 20.0f + (float)randGen.NextDouble() - 0.5f;
                                p.Bounce = 0.1f;
                            }
                        }
                        break;

                    case BlockType.Sand:
                        p.Color = new Vector4(0.9f + (float)((randGen.NextDouble() - 0.5f) * 0.025f), 0.7f + (float)((randGen.NextDouble() - 0.5f) * 0.025f), 0.3f + (float)((randGen.NextDouble() - 0.5f) * 0.025f), 1.0f);
                        break;

                    case BlockType.SolidRed:
                    case BlockType.SolidRed2:
                    case BlockType.ArtCaseR:
                    case BlockType.ResearchR:
                    case BlockType.BeaconRed:
                    case BlockType.MedicalR:
                    case BlockType.InhibitorR:
                    case BlockType.GlassR:
                    case BlockType.RadarRed:
                    case BlockType.BankRed:
                    case BlockType.ConstructionR:
                        p.Color = new Vector4(0.8f + (float)((randGen.NextDouble() - 0.5f) * 0.2f), 0.1f, 0.1f, 1.0f);
                        break;

                    case BlockType.SolidBlue:
                    case BlockType.SolidBlue2:
                    case BlockType.ArtCaseB:
                    case BlockType.ResearchB:
                    case BlockType.MedicalB:
                    case BlockType.BeaconBlue:
                    case BlockType.InhibitorB:
                    case BlockType.GlassB:
                    case BlockType.RadarBlue:
                    case BlockType.BankBlue:
                    case BlockType.ConstructionB:
                        p.Color = new Vector4(0.1f, 0.1f, 0.8f + (float)((randGen.NextDouble() - 0.5f) * 0.2f), 1.0f);
                        break;

                    case BlockType.Metal:
                        p.Color = Vector4.One * (0.2f + (float)(randGen.NextDouble() * 0.7f));
                        break;

                    case BlockType.Ore:
                        {
                            int ll = randGen.Next(0, 3);
                            if (ll == 2)
                            {
                                p.Color = new Vector4(0.45f + (float)((randGen.NextDouble() - 0.5f) * 0.03f), 0.255f + (float)((randGen.NextDouble() - 0.5f) * 0.02f), 0.186f + (float)((randGen.NextDouble() - 0.5f) * 0.015f), 1.0f);
                            }
                            else
                            {
                                p.Color = Vector4.One * (0.2f + (float)(randGen.NextDouble() * 0.7f));
                                p.Gravity = 20.0f + (float)randGen.NextDouble() - 0.5f;
                                p.Bounce = 0.4f;
                            }
                        }
                        break;

                    case BlockType.Highlight:
                        {
                            p.Lifetime = DateTime.Now + TimeSpan.FromSeconds(4.0 - randGen.NextDouble());
                            p.Color = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);
                            p.Bounce = 0.0f;
                            p.Position += new Vector3((float)(randGen.NextDouble() * 1.0f), (float)(randGen.NextDouble() * 1.0f), (float)(randGen.NextDouble() * 1.0f));
                            p.Velocity = new Vector3((float)(randGen.NextDouble() * 0.5f), (float)(randGen.NextDouble() * 0.5f), (float)(randGen.NextDouble() * 0.5f));
                            p.Gravity = 2.0f;
                        }
                        break;

                    case BlockType.DirtSign:
                    case BlockType.Grass:
                    case BlockType.Mud:
                        p.Color = new Vector4(0.35f + (float)((randGen.NextDouble() - 0.5f) * 0.03f), 0.235f + (float)((randGen.NextDouble() - 0.5f) * 0.02f), 0.156f + (float)((randGen.NextDouble() - 0.5f) * 0.015f), 1.0f);
                        break;

                    default:
                        p.Color = Vector4.One * (0.2f + (float)(randGen.NextDouble() * 0.7f));
                        break;
                }
                particleList.Add(p);
            }
        }

        public void CreateBlockDebris(Vector3 explosionPosition, BlockType block, float mag)
        {
            if (mag < 0.1f)
                return;

            if (block == BlockType.Water)
            {
                mag = mag / 6;
            }
            else if (block == BlockType.Lava)
            {
                mag = mag / 5;
                if (_P.blockEngine.BlockAtPoint(explosionPosition + new Vector3(0.0f,0.5f,0.0f)) == BlockType.Lava)
                {//would not be visible
                    return;
                }
            }

            for (int i = 0; i < (int)(5*mag); i++)
            {
                Particle p = new Particle();
               
                p.Size = (float)(randGen.NextDouble() * 0.1 + 0.02);
                p.Position = explosionPosition;
                p.Position.X += (float)randGen.NextDouble() - 0.5f;
                p.Position.Z += (float)randGen.NextDouble() - 0.5f;
                p.Position.Y += (float)randGen.NextDouble() - 0.5f;
                p.Velocity = new Vector3(0.0f, (float)randGen.NextDouble() * 2 - 1, 0.0f);
                p.Bounce = 0.2f;
                p.Lifetime = DateTime.Now + TimeSpan.FromSeconds(2.0 + randGen.NextDouble());//2
                p.SizeChange = 0.1f;

                switch (block)
                {
                    case BlockType.Water:
                        {
                            p.Lifetime = DateTime.Now + TimeSpan.FromSeconds(0.0 - randGen.NextDouble());
                            p.Color = new Vector4(0.1f + (float)((randGen.NextDouble() - 0.5f) * 0.025f), 0.4f + (float)((randGen.NextDouble() - 0.5f) * 0.025f), 0.70f + (float)((randGen.NextDouble() - 0.5f) * 0.025f), 1.0f);
                            p.SizeChange = 0.2f;
                            p.Bounce = 0.0f;
                            p.Position.Y += ((float)randGen.NextDouble() * 0.5f) + 0.75f;
                            p.Velocity = new Vector3(0.0f, -0.25f + (float)randGen.NextDouble(), 0.0f);
                            p.Gravity = 6.0f;
                        }
                        break;

                    case BlockType.Lava:
                        {
                            p.Lifetime = DateTime.Now + TimeSpan.FromSeconds(0.0 - randGen.NextDouble());
                            int ll = randGen.Next(0, 2);
                            if (ll == 0)
                            {
                                p.Color = new Vector4(0.95f + (float)((randGen.NextDouble() - 0.5f) * 0.025f), 0.7f + (float)((randGen.NextDouble() - 0.5f) * 0.025f), 0.05f + (float)((randGen.NextDouble() - 0.5f) * 0.025f), 1.0f);
                                p.Gravity = 6.0f;
                                p.Velocity = new Vector3(((float)randGen.NextDouble() - 0.5f) * 2.0f, 1.0f + (float)randGen.NextDouble() * 2.0f - 0.25f, ((float)randGen.NextDouble() - 0.5f) * 2.0f);
                            }
                            else if (ll == 1)
                            {
                                p.Color = new Vector4(0.3f + (float)((randGen.NextDouble() - 0.5f) * 0.01f), 0.01f + (float)((randGen.NextDouble() - 0.5f) * 0.025f), 0.0f, 1.0f);
                                p.Gravity = 2.0f + (float)randGen.NextDouble() - 0.5f;
                                p.Velocity = new Vector3(((float)randGen.NextDouble() - 0.5f) * 2.0f, 1.0f + (float)randGen.NextDouble() * 2.0f - 0.25f, ((float)randGen.NextDouble() - 0.5f) * 2.0f);
                            }
                            else if (ll == 2)
                            {
                                p.Gravity = 4.0f + (float)randGen.NextDouble() - 0.5f;
                                p.Color = new Vector4(0.8f + (float)((randGen.NextDouble() - 0.5f) * 0.025f), 0.02f + (float)((randGen.NextDouble() - 0.5f) * 0.015f), 0.01f, 1.0f);
                                p.Velocity = new Vector3(((float)randGen.NextDouble() - 0.5f) * 2.0f, (float)randGen.NextDouble() * 1.0f - 0.25f, ((float)randGen.NextDouble() - 0.5f) * 2.0f);
                            }
                            p.SizeChange = 0.2f;
                            p.Bounce = 0.0f;
                            p.Position.Y += 1.0f + (float)randGen.NextDouble() - 0.5f;
                        }
                        break;

                    case BlockType.Dirt:
                        p.Color = new Vector4(0.35f + (float)((randGen.NextDouble() - 0.5f) * 0.03f), 0.235f + (float)((randGen.NextDouble() - 0.5f) * 0.02f), 0.156f + (float)((randGen.NextDouble() - 0.5f) * 0.015f), 1.0f);
                        break;

                    case BlockType.Gold:
                        {
                            int ll = randGen.Next(0, 3);
                            if (ll == 2)
                            {
                                p.Color = new Vector4(0.35f + (float)((randGen.NextDouble() - 0.5f) * 0.03f), 0.235f + (float)((randGen.NextDouble() - 0.5f) * 0.02f), 0.156f + (float)((randGen.NextDouble() - 0.5f) * 0.015f), 1.0f);
                            }
                            else
                            {
                                p.Color = new Vector4(0.65f + (float)((randGen.NextDouble() - 0.5f) * 0.1f), 0.35f + (float)((randGen.NextDouble() - 0.5f) * 0.1f), 0.15f + (float)((randGen.NextDouble() - 0.5f) * 0.1f), 1.0f);
                                p.Gravity = 20.0f + (float)randGen.NextDouble() - 0.5f;
                                p.Bounce = 0.1f;
                            }  
                        }
                        break;

                    case BlockType.Sand:
                        p.Color = new Vector4(0.9f + (float)((randGen.NextDouble() - 0.5f) * 0.025f), 0.7f + (float)((randGen.NextDouble() - 0.5f) * 0.025f), 0.3f + (float)((randGen.NextDouble() - 0.5f) * 0.025f), 1.0f);
                        break;

                    case BlockType.SolidRed:
                    case BlockType.SolidRed2:
                    case BlockType.ArtCaseR:
                    case BlockType.ResearchR:
                    case BlockType.BeaconRed:
                    case BlockType.InhibitorR:
                    case BlockType.GlassR:
                    case BlockType.RadarRed:
                    case BlockType.ConstructionR:
                        p.Color = new Vector4(0.8f + (float)((randGen.NextDouble() - 0.5f) * 0.2f), 0.1f, 0.1f, 1.0f);
                        break;

                    case BlockType.SolidBlue:
                    case BlockType.SolidBlue2:
                    case BlockType.ArtCaseB:
                    case BlockType.ResearchB:
                    case BlockType.BeaconBlue:
                    case BlockType.InhibitorB:
                    case BlockType.GlassB:
                    case BlockType.RadarBlue:
                    case BlockType.ConstructionB:
                        p.Color = new Vector4(0.1f, 0.1f, 0.8f + (float)((randGen.NextDouble() - 0.5f) * 0.2f), 1.0f);
                        break;

                    case BlockType.Metal:
                         p.Color = Vector4.One * (0.2f + (float)(randGen.NextDouble() * 0.7f));
                        break;

                    case BlockType.Ore:
                        {
                            int ll = randGen.Next(0, 3);
                            if (ll == 2)
                            {
                                p.Color = new Vector4(0.45f + (float)((randGen.NextDouble() - 0.5f) * 0.03f), 0.255f + (float)((randGen.NextDouble() - 0.5f) * 0.02f), 0.186f + (float)((randGen.NextDouble() - 0.5f) * 0.015f), 1.0f);
                            }
                            else
                            {
                                p.Color = Vector4.One * (0.2f + (float)(randGen.NextDouble() * 0.7f));
                                p.Gravity = 20.0f + (float)randGen.NextDouble() - 0.5f;
                                p.Bounce = 0.4f;
                            }
                        }
                        break;

                    case BlockType.Highlight:
                        {
                            p.Lifetime = DateTime.Now + TimeSpan.FromSeconds(4.0 - randGen.NextDouble());
                            p.Color = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);
                            p.Bounce = 0.0f;
                            p.Position += new Vector3((float)(randGen.NextDouble() * 1.0f), (float)(randGen.NextDouble() * 1.0f), (float)(randGen.NextDouble() * 1.0f));
                            p.Velocity = new Vector3((float)(randGen.NextDouble() * 0.5f), (float)(randGen.NextDouble() * 0.5f), (float)(randGen.NextDouble() * 0.5f));
                            p.Gravity = 2.0f;
                        }
                        break;

                    case BlockType.DirtSign:
                    case BlockType.Grass:
                    case BlockType.Mud:
                        p.Color = new Vector4(0.35f + (float)((randGen.NextDouble() - 0.5f) * 0.03f), 0.235f + (float)((randGen.NextDouble() - 0.5f) * 0.02f), 0.156f + (float)((randGen.NextDouble() - 0.5f) * 0.015f), 1.0f);
                        break;

                    default:
                        p.Color = Vector4.One * (0.2f + (float)(randGen.NextDouble() * 0.7f));
                        break;
                }

                particleList.Add(p);
            }
        }

        public void CreateBloodSplatter(Vector3 playerPosition, Color color, float strength)
        {
            for (int i = 0; i < 30; i++)
            {
                Particle p = new Particle();
                p.Color = color.ToVector4();
                if (color == Color.Red)
                {
                    p.Color.X += (float)(randGen.NextDouble() * 0.5 - 0.3);
                }
                else
                {
                    p.Color.X += (float)(randGen.NextDouble() * 0.5 - 0.3);
                    p.Color.Z -= (float)(randGen.NextDouble() * 0.8 - 0.4);
                }
                p.Size = (float)(0.05 + (strength*0.15));
                p.Position = playerPosition;
                p.Position.Y -= (float)randGen.NextDouble();
                p.Velocity = new Vector3((float)randGen.NextDouble() * 4 - 2.0f, (float)randGen.NextDouble() * 2f, (float)randGen.NextDouble() * 4 - 2.0f);
                p.Lifetime = DateTime.Now + TimeSpan.FromSeconds(0.4 + (strength*1.25));
                p.Bounce = 0.01f;
                p.SizeChange = 0.2f - (strength*0.2f);
                if (p.SizeChange < 0.0f)
                    p.SizeChange = 0.015f;
                particleList.Add(p);
            }
        }

        public void CreateTrail(Vector3 Position, Color color)
        {
            for (int i = 0; i < 1; i++)
            {
                Particle p = new Particle();
                p.Color = color.ToVector4();
                
                p.Size = 0.15f;
                p.Position = Position;
                p.Gravity = -2.0f;
                //p.Position.Y += (float)randGen.NextDouble();
                p.Velocity = new Vector3((float)randGen.NextDouble() * 2 - 1.0f, 0.0f, (float)randGen.NextDouble() * 2 - 1.0f);
                p.Lifetime = DateTime.Now;
                p.Bounce = 0.5f;
                p.SizeChange = 0.2f;
                particleList.Add(p);
            }
        }

        public void CreateWingsTrail(Vector3 Position, Color color, Vector3 heading)
        {
            for (int i = 0; i < 8; i++)
            {
                Particle p = new Particle();
                p.Color = color.ToVector4();

                p.Size = 0.15f;
                p.Position = Position;
                p.Gravity = 0.2f;
                //p.Position.Y += (float)randGen.NextDouble();
                p.Velocity = new Vector3((float)randGen.NextDouble() * 2 - 1.0f, -1.0f, (float)randGen.NextDouble() * 2 - 1.0f);
                p.Velocity += -heading*0.3f;
                p.Lifetime = DateTime.Now;
                p.Bounce = 0.5f;
                p.SizeChange = 0.2f;
                particleList.Add(p);
            }
        }

        public void CreateTargetTrail(Vector3 Position, Vector4 color, Vector3 Heading)
        {
            for (int i = 0; i < 1; i++)
            {
                Particle p = new Particle();
                p.Color = color;// color.ToVector4();

                p.Size = 0.1f;
                p.Position = Position-(Heading * 0.7f);
                p.Gravity = 0;// -2.0f;
                //p.Position.Y += (float)randGen.NextDouble();
                p.Velocity = Heading;
                p.Lifetime = DateTime.Now;
                p.Bounce = 0.01f;
                p.SizeChange = 0.15f;
                particleList.Add(p);
            }
        }
        public void CreateHidden(Vector3 Position, Color color)
        {
            for (int i = 0; i < 25; i++)
            {
                Particle p = new Particle();
                p.Color = color.ToVector4();
                p.Position = Position;
                p.Position.X += (float)randGen.NextDouble() - 0.5f;
                p.Position.Z += (float)randGen.NextDouble() - 0.5f;
                p.Position.Y += (float)randGen.NextDouble() + 0.1f;

                p.Size = 0.15f;

                p.Gravity = -2.0f;
                //p.Position.Y += (float)randGen.NextDouble();
                p.Velocity = new Vector3((float)randGen.NextDouble() * 2 - 1.0f, 0.0f, (float)randGen.NextDouble() * 2 - 1.0f);
                p.Lifetime = DateTime.Now;
                p.Bounce = 0.5f;
                p.SizeChange = 0.2f;
                particleList.Add(p);
            }
        }

        public void Render(GraphicsDevice graphicsDevice)
        {
            // If we don't have _P, grab it from the current gameInstance.
            // We can't do this in the constructor because we are created in the property bag's constructor!
            if (_P == null)
                _P = gameInstance.propertyBag;

            foreach (Particle p in particleList)
            {
                Matrix worldMatrix = Matrix.CreateScale(p.Size / 2) * Matrix.CreateTranslation(p.Position);
                particleEffect.Parameters["xWorld"].SetValue(worldMatrix);
                particleEffect.Parameters["xView"].SetValue(_P.playerCamera.ViewMatrix);
                particleEffect.Parameters["xProjection"].SetValue(_P.playerCamera.ProjectionMatrix);
                particleEffect.Parameters["xColor"].SetValue(p.Color);
                particleEffect.CurrentTechnique.Passes[0].Apply();

                graphicsDevice.RasterizerState = RasterizerState.CullNone;
                graphicsDevice.SetVertexBuffer(vertexBuffer);
                graphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, vertexBuffer.VertexCount / 3);
            }
        }
    }
}
