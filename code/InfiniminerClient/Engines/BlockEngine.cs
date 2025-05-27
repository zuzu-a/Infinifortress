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
    [Serializable]
    public struct VertexPositionTextureShade : IVertexType
    {
        Vector3 pos;
        Vector2 tex;
        float shade;

        public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration
        (
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
            new VertexElement(20, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 1)
        );

        public static readonly VertexElement[] VertexElements = new VertexElement[]
        { 
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
            new VertexElement(20, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 1)
        };

        public VertexPositionTextureShade(Vector3 position, Vector2 uv, double shade)
        {
            pos = position;
            tex = uv;
            this.shade = (float)shade;
        }

        public Vector3 Position { get { return pos; } set { pos = value; } }
        public Vector2 Tex { get { return tex; } set { tex = value; } }
        public float Shade { get { return shade; } set { shade = value; } }
        public static int SizeInBytes { get { return sizeof(float) * 6; } }

        VertexDeclaration IVertexType.VertexDeclaration { get { return VertexDeclaration; } }
    }

    public class IMTexture
    {
        public Texture2D Texture = null;
        public Color LODColor = Color.Black;

        public IMTexture(Texture2D texture)
        {
            Texture = texture;
            LODColor = Color.Black;

            // If this is a null texture, use a black LOD color.
            if (Texture == null)
                return;

            // Calculate the load color dynamically.
            float r = 0, g = 0, b = 0;
            Color[] pixelData = new Color[texture.Width * texture.Height];
            texture.GetData<Color>(pixelData);
            for (int i = 0; i < texture.Width; i++)
                for (int j = 0; j < texture.Height; j++)
                {
                    r += pixelData[i + j * texture.Width].R;
                    g += pixelData[i + j * texture.Width].G;
                    b += pixelData[i + j * texture.Width].B;
                }
            r /= texture.Width * texture.Height;
            g /= texture.Width * texture.Height;
            b /= texture.Width * texture.Height;
            LODColor = new Color(r / 256, g / 256, b / 256);
        }
    }

    public class BlockEngine
    {
        public BlockType[,,] blockList = null;
        public BlockType[, , ,] blockListAttach = null;//attach item/player/action
        public BlockType[, ,] blockTexList = null;
        public BlockType[, ,] downloadList = null;

        Dictionary<uint,bool>[,] faceMap = null;
        //SortedDictionary<RegionSort, int> sortedRegions = new SortedDictionary<RegionSort, int>();
        SortedDictionary<float, uint> sortedRegions = new SortedDictionary<float, uint>();
        public int[] RegionList = null;
        public Dictionary<Vector3, Beacon> beaconList = new Dictionary<Vector3, Beacon>();
       
        BlockTexture[,] blockTextureMap = null;
        public IMTexture[] blockTextures = null;
        Effect basicEffect;
        InfiniminerGame gameInstance;
        DynamicVertexBuffer[,] vertexBuffers = null;
        public bool[,] vertexListDirty = null;
        public double[, , ,] Light = null;
        public BloomComponent bloomPosteffect;

        
        OcclusionQuery[] query = null;
        public int[] queryActive = null;
        public int[] queryCount = null;

        public int occCount = 0;
        public bool occComplete = true;
        public int[] RegionOcc = null;

        public bool occActive = false;
        public void MakeRegionDirty(int texture, int region)
        {
            vertexListDirty[texture, region] = true;
        }

        public const int MAPSIZE = 64;
        const int REGIONSIZE = 16;
        const int REGIONRATIO = MAPSIZE / REGIONSIZE;
        const int NUMREGIONS = REGIONRATIO * REGIONRATIO * REGIONRATIO;

        public void DownloadComplete()
        {
            for (ushort i = 0; i < MAPSIZE; i++)
                for (ushort j = 0; j < MAPSIZE; j++)
                    for (ushort k = 0; k < MAPSIZE; k++)
                        if (downloadList[i, j, k] != BlockType.None)
                            AddBlock(i, j, k, downloadList[i, j, k]);
        }

        public BlockEngine(InfiniminerGame gameInstance)
        {
            this.gameInstance = gameInstance;

            // Initialize the block list.
            downloadList = new BlockType[MAPSIZE, MAPSIZE, MAPSIZE];
            blockList = new BlockType[MAPSIZE, MAPSIZE, MAPSIZE];
            blockTexList = new BlockType[MAPSIZE, MAPSIZE, MAPSIZE];
            RegionOcc = new int[NUMREGIONS + 1];
            Light = new double[MAPSIZE, MAPSIZE, MAPSIZE, 6];

            for (ushort i = 0; i < MAPSIZE; i++)
                for (ushort j = 0; j < MAPSIZE; j++)
                    for (ushort k = 0; k < MAPSIZE; k++)
                    {
                        downloadList[i, j, k] = BlockType.None;
                        blockList[i, j, k] = BlockType.None;
                        blockTexList[i, j, k] = BlockType.None;
                        for (int f = 0; f < 6; f++)
                        {
                            Light[i, j, k, f] = 0.7f;
                        }
                    }

            // Initialize the face lists.
            faceMap = new Dictionary<uint,bool>[(byte)BlockTexture.MAXIMUM, NUMREGIONS];
            for (BlockTexture blockTexture = BlockTexture.None; blockTexture < BlockTexture.MAXIMUM; blockTexture++)
                for (int r = 0; r < NUMREGIONS; r++)
                {
                    faceMap[(byte)blockTexture, r] = new Dictionary<uint, bool>();
                    //make its own declaration at some point
                }

            // Initialize the texture map.
            blockTextureMap = new BlockTexture[(byte)BlockType.MAXIMUM, 6];
            for (BlockType blockType = BlockType.None; blockType < BlockType.MAXIMUM; blockType++)
                for (BlockFaceDirection faceDir = BlockFaceDirection.XIncreasing; faceDir < BlockFaceDirection.MAXIMUM; faceDir++)
                    blockTextureMap[(byte)blockType,(byte)faceDir] = BlockInformation.GetTexture(blockType, faceDir);

            // Load the textures we'll use.
            blockTextures = new IMTexture[(byte)BlockTexture.MAXIMUM];
            blockTextures[(byte)BlockTexture.None] = new IMTexture(null);
            blockTextures[(byte)BlockTexture.Dirt] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_dirt"));
            blockTextures[(byte)BlockTexture.Mud] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_dirt"));
            blockTextures[(byte)BlockTexture.Grass] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_grass"));
            blockTextures[(byte)BlockTexture.GrassSide] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_grass_side"));
            blockTextures[(byte)BlockTexture.Sand] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_sand"));
            blockTextures[(byte)BlockTexture.Rock] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_rock"));
            blockTextures[(byte)BlockTexture.Ore] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_ore"));
            blockTextures[(byte)BlockTexture.Gold] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_silver"));
            blockTextures[(byte)BlockTexture.Diamond] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_diamond"));
            blockTextures[(byte)BlockTexture.HomeRed] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_home_red"));
            blockTextures[(byte)BlockTexture.HomeBlue] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_home_blue"));
            blockTextures[(byte)BlockTexture.SolidRed] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_red"));
            blockTextures[(byte)BlockTexture.SolidBlue] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_blue"));
            blockTextures[(byte)BlockTexture.SolidRed2] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_red2"));//placeholder texture
            blockTextures[(byte)BlockTexture.SolidBlue2] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_blue2"));//placeholder texture
            blockTextures[(byte)BlockTexture.Ladder] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_ladder"));
            blockTextures[(byte)BlockTexture.LadderTop] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_ladder_top"));
            blockTextures[(byte)BlockTexture.Spikes] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_spikes"));
            blockTextures[(byte)BlockTexture.Jump] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_jump"));
            blockTextures[(byte)BlockTexture.JumpTop] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_jump_top"));
            blockTextures[(byte)BlockTexture.Explosive] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_explosive"));
            blockTextures[(byte)BlockTexture.Metal] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_metal"));
            blockTextures[(byte)BlockTexture.DirtSign] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_dirt_sign"));
            blockTextures[(byte)BlockTexture.BankTopRed] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_bank_top_red"));
            blockTextures[(byte)BlockTexture.BankLeftRed] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_bank_left_red"));
            blockTextures[(byte)BlockTexture.BankFrontRed] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_bank_front_red"));
            blockTextures[(byte)BlockTexture.BankRightRed] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_bank_right_red"));
            blockTextures[(byte)BlockTexture.BankBackRed] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_bank_back_red"));
            blockTextures[(byte)BlockTexture.BankTopBlue] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_bank_top_blue"));
            blockTextures[(byte)BlockTexture.BankLeftBlue] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_bank_left_blue"));
            blockTextures[(byte)BlockTexture.BankFrontBlue] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_bank_front_blue"));
            blockTextures[(byte)BlockTexture.BankRightBlue] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_bank_right_blue"));
            blockTextures[(byte)BlockTexture.BankBackBlue] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_bank_back_blue"));
            blockTextures[(byte)BlockTexture.Forge] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_forge"));
            blockTextures[(byte)BlockTexture.ForgeSide] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_forge_side"));
            blockTextures[(byte)BlockTexture.ArtCaseR] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_artcase_red"));
            blockTextures[(byte)BlockTexture.ArtCaseB] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_artcase_blue"));
            blockTextures[(byte)BlockTexture.InhibitorR] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_inhibitorr"));
            blockTextures[(byte)BlockTexture.InhibitorB] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_inhibitorb"));
            blockTextures[(byte)BlockTexture.ResearchR] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_researchr"));
            blockTextures[(byte)BlockTexture.ResearchB] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_researchb"));
            blockTextures[(byte)BlockTexture.RadarRed] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_radarr"));
            blockTextures[(byte)BlockTexture.RadarBlue] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_radarb"));
           
            //base block
            blockTextures[(byte)BlockTexture.BaseTopRed] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_bank_top_red"));
            blockTextures[(byte)BlockTexture.BaseLeftRed] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_bank_left_red"));
            blockTextures[(byte)BlockTexture.BaseFrontRed] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_bank_front_red"));
            blockTextures[(byte)BlockTexture.BaseRightRed] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_bank_right_red"));
            blockTextures[(byte)BlockTexture.BaseBackRed] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_bank_back_red"));
            blockTextures[(byte)BlockTexture.BaseTopBlue] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_bank_top_blue"));
            blockTextures[(byte)BlockTexture.BaseLeftBlue] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_bank_left_blue"));
            blockTextures[(byte)BlockTexture.BaseFrontBlue] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_bank_front_blue"));
            blockTextures[(byte)BlockTexture.BaseRightBlue] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_bank_right_blue"));
            blockTextures[(byte)BlockTexture.BaseBackBlue] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_bank_back_blue"));
            //base block
            blockTextures[(byte)BlockTexture.TeleSideA] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_teleporter_a"));
            blockTextures[(byte)BlockTexture.TeleSideB] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_teleporter_b"));
            blockTextures[(byte)BlockTexture.TeleTop] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_teleporter_top"));
            blockTextures[(byte)BlockTexture.TeleBottom] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_teleporter_bottom"));
            blockTextures[(byte)BlockTexture.Lava] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_lava"));
            blockTextures[(byte)BlockTexture.Water] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_trans_water"));
            blockTextures[(byte)BlockTexture.Road] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_road"));
            blockTextures[(byte)BlockTexture.RoadTop] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_road_top"));
            blockTextures[(byte)BlockTexture.RoadBottom] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_road_bottom"));
            blockTextures[(byte)BlockTexture.BeaconRed] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_beacon_top_red"));
            blockTextures[(byte)BlockTexture.BeaconBlue] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_beacon_top_blue"));
            blockTextures[(byte)BlockTexture.TransRed] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_trans_red"));
            blockTextures[(byte)BlockTexture.TransBlue] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_trans_blue"));
            blockTextures[(byte)BlockTexture.GlassR] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_glass_red"));
            blockTextures[(byte)BlockTexture.GlassB] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_glass_blue"));
            blockTextures[(byte)BlockTexture.ForceR] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_force_red"));
            blockTextures[(byte)BlockTexture.ForceB] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_force_blue"));

            blockTextures[(byte)BlockTexture.MedicalR] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_medicalr"));
            blockTextures[(byte)BlockTexture.MedicalB] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_medicalb"));
            blockTextures[(byte)BlockTexture.Refinery] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_refinery"));
            blockTextures[(byte)BlockTexture.RefinerySide] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_refinery_side"));
            blockTextures[(byte)BlockTexture.Maintenance] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_maintenance"));
            blockTextures[(byte)BlockTexture.Generator] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_generator"));
            blockTextures[(byte)BlockTexture.Controller] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_controller"));
            blockTextures[(byte)BlockTexture.Pipe] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_pipe"));
            blockTextures[(byte)BlockTexture.Pump] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_pump"));
            blockTextures[(byte)BlockTexture.Barrel] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_barrel_side"));
            blockTextures[(byte)BlockTexture.BarrelTop] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_barrel_top"));
            blockTextures[(byte)BlockTexture.Spring] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_spring"));
            blockTextures[(byte)BlockTexture.MagmaVent] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_magmavent"));
            blockTextures[(byte)BlockTexture.MagmaBurst] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_magma"));
            blockTextures[(byte)BlockTexture.Fire] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_fire"));
            blockTextures[(byte)BlockTexture.StealthBlockR] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_dirt_trans"));
            blockTextures[(byte)BlockTexture.StealthBlockB] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_dirt_trans"));
            blockTextures[(byte)BlockTexture.Trap] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_dirt"));
            blockTextures[(byte)BlockTexture.TrapB] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_dirt"));
            blockTextures[(byte)BlockTexture.TrapR] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_dirt"));
            blockTextures[(byte)BlockTexture.TrapVis] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_trapVis"));
            blockTextures[(byte)BlockTexture.Magma] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_magma"));
            blockTextures[(byte)BlockTexture.Lever] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_mechanism"));
            blockTextures[(byte)BlockTexture.Plate] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_metal"));
            blockTextures[(byte)BlockTexture.Hinge] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_hinge"));
            blockTextures[(byte)BlockTexture.ConstructionR] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_constructionr"));
            blockTextures[(byte)BlockTexture.ConstructionB] = new IMTexture(gameInstance.Content.Load<Texture2D>("blocks/tex_block_constructionb"));
            // Load our effects.
            basicEffect = gameInstance.Content.Load<Effect>("effect_basic");

            // Build vertex lists.
            vertexBuffers = new DynamicVertexBuffer[(byte)BlockTexture.MAXIMUM, NUMREGIONS];
            vertexListDirty = new bool[(byte)BlockTexture.MAXIMUM, NUMREGIONS];

            for (int i = 0; i < (byte)BlockTexture.MAXIMUM; i++)
                for (int j = 0; j < NUMREGIONS; j++)
                    vertexListDirty[i, j] = true;

            if (occActive)
            {
                query = new OcclusionQuery[NUMREGIONS + 1];
                queryActive = new Int32[NUMREGIONS + 1];
                queryCount = new Int32[NUMREGIONS + 1];
                occCount = 0;
                occComplete = false;

                for (int o = 0; o < NUMREGIONS; o++)
                {
                    query[o] = new OcclusionQuery(gameInstance.GraphicsDevice);
                    queryActive[o] = -1;
                    queryCount[o] = 0;
                    RegionOcc[o] = 0;
                }
            }

            // Initialize any graphics stuff.
            // vertexDeclaration = new VertexDeclaration(VertexPositionTextureShade.VertexElements); // Not needed in XNA 4.0/MonoGame

            // Initialize the bloom engine.
            if (gameInstance.RenderPretty)
            {
                bloomPosteffect = new BloomComponent();
                bloomPosteffect.Load(gameInstance.GraphicsDevice, gameInstance.Content);
            }
            else
                bloomPosteffect = null;
        }

        // Returns true if we are solid at this point.
        public bool SolidAtPoint(Vector3 point)
        {
            return BlockAtPoint(point) != BlockType.None; 
        }

        public bool SolidAtPointForPlayer(Vector3 point)
        {
            return !BlockPassibleForPlayer(BlockAtPoint(point));
        }

        public bool BlockPassibleForPlayer(BlockType blockType)
        {
            if (blockType == BlockType.None)
                return true;
            if (gameInstance.propertyBag.playerTeam == PlayerTeam.Red && (blockType == BlockType.TransRed || blockType == BlockType.StealthBlockR || blockType == BlockType.TrapB))
                return true;
            if (gameInstance.propertyBag.playerTeam == PlayerTeam.Blue && (blockType == BlockType.TransBlue || blockType == BlockType.StealthBlockB|| blockType == BlockType.TrapR))
                return true;
            if (blockType == BlockType.Fire)
                return true;
            if (blockType == BlockType.Water)
                return true;
            return false;
        }

        public BlockType BlockAtPoint(Vector3 point)
        {
            ushort x = (ushort)point.X;
            ushort y = (ushort)point.Y;
            ushort z = (ushort)point.Z;
            if (x < 0 || y < 0 || z < 0 || x >= MAPSIZE || y >= MAPSIZE || z >= MAPSIZE)
                return BlockType.None;
            return blockList[x, y, z]; 
        }

        public bool RayCollision(Vector3 startPosition, Vector3 src, int searchGranularity)
        {//light ray expires when it hits wall, returns true if it hits nothing
            Vector3 testPos = startPosition;

            //float distance = Vector3.Distance(cyl.end, cyl.start);
            Vector3 dir = Vector3.Normalize(src - startPosition);
            //src -= dir * 2;
            for(int imax = 10;imax > 0;imax--)
            {
                testPos += dir;// / searchGranularity;
                BlockType testBlock = BlockAtPoint(testPos);

                if ((int)testPos.X == (int)src.X && (int)testPos.Y == (int)src.Y && (int)testPos.Z == (int)src.Z)
                    return true;

                if(((int)testPos.X != (int)startPosition.X || (int)testPos.Y != (int)startPosition.Y || (int)testPos.Z != (int)startPosition.Z))
                if (testBlock != BlockType.None)
                {
                    return false;
                }
            }
            return true;
        }

        public bool RayCollision(Vector3 startPosition, Vector3 rayDirection, float distance, int searchGranularity, ref Vector3 hitPoint, ref Vector3 buildPoint)
        {
            Vector3 testPos = startPosition;
            Vector3 buildPos = startPosition;
            for (int i=0; i<searchGranularity; i++)
            {
                testPos += rayDirection * distance / searchGranularity;
                BlockType testBlock = BlockAtPoint(testPos);
                if (testBlock != BlockType.None && testBlock != BlockType.Water)
                {
                    hitPoint = testPos;
                    buildPoint = buildPos;
                    return true;
                }
                buildPos = testPos;
            }
            return false;
        }

        public bool RayCollision(Vector3 startPosition, Vector3 rayDirection, float distance, int searchGranularity, ref Vector3 hitPoint, ref Vector3 buildPoint, bool passablecheck)
        {
            Vector3 testPos = startPosition;
            Vector3 buildPos = startPosition;
            for (int i = 0; i < searchGranularity; i++)
            {
                testPos += rayDirection * distance / searchGranularity;
                BlockType testBlock = BlockAtPoint(testPos);
                if (!BlockPassibleForPlayer(testBlock))
                {
                    hitPoint = testPos;
                    buildPoint = buildPos;
                    return true;
                }
                buildPos = testPos;
            }
            return false;
        }

        public bool RayCollision(Vector3 startPosition, Vector3 rayDirection, float distance, int searchGranularity, ref Vector3 hitPoint, ref Vector3 buildPoint, BlockType block)
        {
            Vector3 testPos = startPosition;
            Vector3 buildPos = startPosition;
            for (int i = 0; i < searchGranularity; i++)
            {
                testPos += rayDirection * distance / searchGranularity;
                BlockType testBlock = BlockAtPoint(testPos);
                if (testBlock != BlockType.None && testBlock != block && testBlock != (gameInstance.propertyBag.playerTeam == PlayerTeam.Red ? BlockType.TransRed : BlockType.TransBlue))
                {
                    hitPoint = testPos;
                    buildPoint = buildPos;
                    return true;
                }
                buildPos = testPos;
            }
            return false;
        }
        private void BuildShape(Vector3 shapePosition, Vector3 shapeSize)
        {
            VertexPositionNormalTexture[] boxVertices = new VertexPositionNormalTexture[36];
            VertexPositionNormalTexture[] shapeVertices = new VertexPositionNormalTexture[36];

            Vector3 topLeftFront = shapePosition +
                new Vector3(-1.0f, 1.0f, -1.0f) * shapeSize;
            Vector3 bottomLeftFront = shapePosition +
                new Vector3(-1.0f, -1.0f, -1.0f) * shapeSize;
            Vector3 topRightFront = shapePosition +
                new Vector3(1.0f, 1.0f, -1.0f) * shapeSize;
            Vector3 bottomRightFront = shapePosition +
                new Vector3(1.0f, -1.0f, -1.0f) * shapeSize;
            Vector3 topLeftBack = shapePosition +
                new Vector3(-1.0f, 1.0f, 1.0f) * shapeSize;
            Vector3 topRightBack = shapePosition +
                new Vector3(1.0f, 1.0f, 1.0f) * shapeSize;
            Vector3 bottomLeftBack = shapePosition +
                new Vector3(-1.0f, -1.0f, 1.0f) * shapeSize;
            Vector3 bottomRightBack = shapePosition +
                new Vector3(1.0f, -1.0f, 1.0f) * shapeSize;

            Vector3 frontNormal = new Vector3(0.0f, 0.0f, 1.0f) * shapeSize;
            Vector3 backNormal = new Vector3(0.0f, 0.0f, -1.0f) * shapeSize;
            Vector3 topNormal = new Vector3(0.0f, 1.0f, 0.0f) * shapeSize;
            Vector3 bottomNormal = new Vector3(0.0f, -1.0f, 0.0f) * shapeSize;
            Vector3 leftNormal = new Vector3(-1.0f, 0.0f, 0.0f) * shapeSize;
            Vector3 rightNormal = new Vector3(1.0f, 0.0f, 0.0f) * shapeSize;

            Vector2 textureTopLeft = new Vector2(0.5f * shapeSize.X, 0.0f * shapeSize.Y);
            Vector2 textureTopRight = new Vector2(0.0f * shapeSize.X, 0.0f * shapeSize.Y);
            Vector2 textureBottomLeft = new Vector2(0.5f * shapeSize.X, 0.5f * shapeSize.Y);
            Vector2 textureBottomRight = new Vector2(0.0f * shapeSize.X, 0.5f * shapeSize.Y);

            // Front face.
            shapeVertices[0] = new VertexPositionNormalTexture(
                topLeftFront, frontNormal, textureTopLeft);
            shapeVertices[1] = new VertexPositionNormalTexture(
                bottomLeftFront, frontNormal, textureBottomLeft);
            shapeVertices[2] = new VertexPositionNormalTexture(
                topRightFront, frontNormal, textureTopRight);
            shapeVertices[3] = new VertexPositionNormalTexture(
                bottomLeftFront, frontNormal, textureBottomLeft);
            shapeVertices[4] = new VertexPositionNormalTexture(
                bottomRightFront, frontNormal, textureBottomRight);
            shapeVertices[5] = new VertexPositionNormalTexture(
                topRightFront, frontNormal, textureTopRight);

            // Back face.
            shapeVertices[6] = new VertexPositionNormalTexture(
                topLeftBack, backNormal, textureTopRight);
            shapeVertices[7] = new VertexPositionNormalTexture(
                topRightBack, backNormal, textureTopLeft);
            shapeVertices[8] = new VertexPositionNormalTexture(
                bottomLeftBack, backNormal, textureBottomRight);
            shapeVertices[9] = new VertexPositionNormalTexture(
                bottomLeftBack, backNormal, textureBottomRight);
            shapeVertices[10] = new VertexPositionNormalTexture(
                topRightBack, backNormal, textureTopLeft);
            shapeVertices[11] = new VertexPositionNormalTexture(
                bottomRightBack, backNormal, textureBottomLeft);

            // Top face.
            shapeVertices[12] = new VertexPositionNormalTexture(
                topLeftFront, topNormal, textureBottomLeft);
            shapeVertices[13] = new VertexPositionNormalTexture(
                topRightBack, topNormal, textureTopRight);
            shapeVertices[14] = new VertexPositionNormalTexture(
                topLeftBack, topNormal, textureTopLeft);
            shapeVertices[15] = new VertexPositionNormalTexture(
                topLeftFront, topNormal, textureBottomLeft);
            shapeVertices[16] = new VertexPositionNormalTexture(
                topRightFront, topNormal, textureBottomRight);
            shapeVertices[17] = new VertexPositionNormalTexture(
                topRightBack, topNormal, textureTopRight);

            // Bottom face.
            shapeVertices[18] = new VertexPositionNormalTexture(
                bottomLeftFront, bottomNormal, textureTopLeft);
            shapeVertices[19] = new VertexPositionNormalTexture(
                bottomLeftBack, bottomNormal, textureBottomLeft);
            shapeVertices[20] = new VertexPositionNormalTexture(
                bottomRightBack, bottomNormal, textureBottomRight);
            shapeVertices[21] = new VertexPositionNormalTexture(
                bottomLeftFront, bottomNormal, textureTopLeft);
            shapeVertices[22] = new VertexPositionNormalTexture(
                bottomRightBack, bottomNormal, textureBottomRight);
            shapeVertices[23] = new VertexPositionNormalTexture(
                bottomRightFront, bottomNormal, textureTopRight);

            // Left face.
            shapeVertices[24] = new VertexPositionNormalTexture(
                topLeftFront, leftNormal, textureTopRight);
            shapeVertices[25] = new VertexPositionNormalTexture(
                bottomLeftBack, leftNormal, textureBottomLeft);
            shapeVertices[26] = new VertexPositionNormalTexture(
                bottomLeftFront, leftNormal, textureBottomRight);
            shapeVertices[27] = new VertexPositionNormalTexture(
                topLeftBack, leftNormal, textureTopLeft);
            shapeVertices[28] = new VertexPositionNormalTexture(
                bottomLeftBack, leftNormal, textureBottomLeft);
            shapeVertices[29] = new VertexPositionNormalTexture(
                topLeftFront, leftNormal, textureTopRight);

            // Right face.
            shapeVertices[30] = new VertexPositionNormalTexture(
                topRightFront, rightNormal, textureTopLeft);
            shapeVertices[31] = new VertexPositionNormalTexture(
                bottomRightFront, rightNormal, textureBottomLeft);
            shapeVertices[32] = new VertexPositionNormalTexture(
                bottomRightBack, rightNormal, textureBottomRight);
            shapeVertices[33] = new VertexPositionNormalTexture(
                topRightBack, rightNormal, textureTopRight);
            shapeVertices[34] = new VertexPositionNormalTexture(
                topRightFront, rightNormal, textureTopLeft);
            shapeVertices[35] = new VertexPositionNormalTexture(
                bottomRightBack, rightNormal, textureBottomRight);
        }

        public float Distf(Vector3 x, Vector3 y)
        {
            float dx = y.X - x.X;
            float dy = y.Y - x.Y;
            float dz = y.Z - x.Z;
            float dist = (float)(Math.Sqrt(dx * dx + dy * dy + dz * dz));
            return dist;
        }

        public void Render(GraphicsDevice graphicsDevice, GameTime gameTime)
        {
            RegenerateDirtyVertexLists();
           // graphicsDevice.Clear(Color.PaleGoldenrod);

            if (occActive)
            {
                sortedRegions.Clear();
                uint ourr = GetRegion((int)gameInstance.propertyBag.playerCamera.Position.X, (int)gameInstance.propertyBag.playerCamera.Position.Y, (int)gameInstance.propertyBag.playerCamera.Position.Z);
                int conflicts = 0;

                for (int rmx = 0; rmx < MAPSIZE; rmx += REGIONSIZE)
                    for (int rmy = 0; rmy < MAPSIZE; rmy += REGIONSIZE)
                        for (int rmz = 0; rmz < MAPSIZE; rmz += REGIONSIZE)
                        {
                            uint rm = GetRegion(rmx, rmy, rmz);
                            float key = Distf(gameInstance.propertyBag.playerCamera.Position, new Vector3(rmx + REGIONSIZE / 2, rmy + REGIONSIZE / 2, rmz + REGIONSIZE / 2));
                            if (rm == ourr)
                            {
                                // gameInstance.propertyBag.addChatMessage("r: " + key, ChatMessageType.SayAll, 10);
                                key = 0.0f;
                            }
                            while (sortedRegions.ContainsKey(key))
                            {
                                conflicts++;
                                key += 0.1f;
                            }
                            sortedRegions.Add(key, rm);
                        }

                bool[] complete = new bool[NUMREGIONS];
                //if(occComplete)
                occCount = 0;
                RegionOcc[ourr] = 1;
                //if(conflicts > 0)
                //gameInstance.propertyBag.addChatMessage("r: " + conflicts, ChatMessageType.SayAll, 10);
                foreach (uint o in sortedRegions.Values)
                {

                    //if (o != ourr)
                    //   return;
                    //if (RegionOcc[o] > 0)//region is visible, we wont need to put big black boxes all over the place
                    //    continue;
                    // gameInstance.propertyBag.addChatMessage("reg: " + o, ChatMessageType.SayAll, 10);

                    if (queryActive[o] == -1)
                    {
                        queryActive[o] = 3;
                        // query[o] = new OcclusionQuery(graphicsDevice);
                        query[o].Begin();
                        query[o].End();
                        //occComplete = true;
                        //this.gameInstance.propertyBag.addChatMessage("o:" + o, ChatMessageType.SayAll, 10);
                        // queryCount[o] = 1;
                        //start query again
                    }
                    //else if (query[o].IsComplete && queryCount[o] == 2)
                    //{
                    //    if (RegionOcc[o] == 0)
                    //        RegionOcc[o] = query[o].PixelCount;
                    //    //testing if stuff will appear

                    //    //process data
                    //    queryCount[o] = 3;
                    //}
                    //else 

                    //if (occComplete == true)
                    //{
                    if (query[o].IsComplete && queryActive[o] == 3)
                    {
                        queryActive[o] = 0;
                        RegionOcc[o] = query[o].PixelCount;
                        //query[o] = new OcclusionQuery(graphicsDevice);

                        if (o == ourr)
                        {
                            //   gameInstance.propertyBag.addChatMessage("pix: " + RegionOcc[o], ChatMessageType.SayAll, 10);
                            RegionOcc[o] = 1;
                        }
                        //RegionOcc[o] = 1;
                        //queryActive[o] = -1;//restart whole process
                        //occComplete = true;
                    }

                    if (RegionOcc[o] == 0)//THIS IS REALLY SLOW
                    {
                        //gameInstance.propertyBag.addChatMessage("pix: " + RegionOcc[o], ChatMessageType.SayAll, 10);

                        occCount++;
                        if (query[o].IsComplete && queryActive[o] == 0)
                        {
                            //query
                            //queryCount[o] = 1;

                            //int r = queryActive[o];

                            //if (RegionOcc[o] == 0)
                            //{
                            BoundingSphere regionBounds = new BoundingSphere(GetRegionCenter(o), REGIONSIZE);
                            BoundingFrustum boundingFrustum = new BoundingFrustum(gameInstance.propertyBag.playerCamera.ViewMatrix * gameInstance.propertyBag.playerCamera.ProjectionMatrix);

                            //while (boundingFrustum.Contains(regionBounds) == ContainmentType.Disjoint)
                            //{
                            //    RegionOcc[r] = 0;//it wasnt visible

                            //    r++;//check next region
                            //    if (r < NUMREGIONS)
                            //    {
                            //        regionBounds = new BoundingSphere(GetRegionCenter(r), REGIONSIZE);
                            //    }
                            //    else
                            //    {
                            //        break;
                            //    }
                            //}

                            //if (boundingFrustum.Contains(regionBounds) != ContainmentType.Disjoint)
                            //{//inside frustrum
                            Vector3 shapePosition = (regionBounds.Center - new Vector3(REGIONSIZE / 2, REGIONSIZE / 2, REGIONSIZE / 2));
                            Vector3 shapeSize = new Vector3(REGIONSIZE, REGIONSIZE, REGIONSIZE);
                            //VertexPositionNormalTexture[] boxVertices = new VertexPositionNormalTexture[36];
                            VertexPositionNormalTexture[] shapeVertices = new VertexPositionNormalTexture[36];

                            Vector3 topLeftFront = shapePosition +
                                new Vector3(-1.0f, 1.0f, -1.0f) * shapeSize;
                            Vector3 bottomLeftFront = shapePosition +
                                new Vector3(-1.0f, -1.0f, -1.0f) * shapeSize;
                            Vector3 topRightFront = shapePosition +
                                new Vector3(1.0f, 1.0f, -1.0f) * shapeSize;
                            Vector3 bottomRightFront = shapePosition +
                                new Vector3(1.0f, -1.0f, -1.0f) * shapeSize;
                            Vector3 topLeftBack = shapePosition +
                                new Vector3(-1.0f, 1.0f, 1.0f) * shapeSize;
                            Vector3 topRightBack = shapePosition +
                                new Vector3(1.0f, 1.0f, 1.0f) * shapeSize;
                            Vector3 bottomLeftBack = shapePosition +
                                new Vector3(-1.0f, -1.0f, 1.0f) * shapeSize;
                            Vector3 bottomRightBack = shapePosition +
                                new Vector3(1.0f, -1.0f, 1.0f) * shapeSize;

                            Vector3 frontNormal = new Vector3(0.0f, 0.0f, 1.0f) * shapeSize;
                            Vector3 backNormal = new Vector3(0.0f, 0.0f, -1.0f) * shapeSize;
                            Vector3 topNormal = new Vector3(0.0f, 1.0f, 0.0f) * shapeSize;
                            Vector3 bottomNormal = new Vector3(0.0f, -1.0f, 0.0f) * shapeSize;
                            Vector3 leftNormal = new Vector3(-1.0f, 0.0f, 0.0f) * shapeSize;
                            Vector3 rightNormal = new Vector3(1.0f, 0.0f, 0.0f) * shapeSize;

                            Vector2 textureTopLeft = new Vector2(0.5f * shapeSize.X, 0.0f * shapeSize.Y);
                            Vector2 textureTopRight = new Vector2(0.0f * shapeSize.X, 0.0f * shapeSize.Y);
                            Vector2 textureBottomLeft = new Vector2(0.5f * shapeSize.X, 0.5f * shapeSize.Y);
                            Vector2 textureBottomRight = new Vector2(0.0f * shapeSize.X, 0.5f * shapeSize.Y);

                            // Front face.
                            shapeVertices[0] = new VertexPositionNormalTexture(
                                topLeftFront, frontNormal, textureTopLeft);
                            shapeVertices[1] = new VertexPositionNormalTexture(
                                bottomLeftFront, frontNormal, textureBottomLeft);
                            shapeVertices[2] = new VertexPositionNormalTexture(
                                topRightFront, frontNormal, textureTopRight);
                            shapeVertices[3] = new VertexPositionNormalTexture(
                                bottomLeftFront, frontNormal, textureBottomLeft);
                            shapeVertices[4] = new VertexPositionNormalTexture(
                                bottomRightFront, frontNormal, textureBottomRight);
                            shapeVertices[5] = new VertexPositionNormalTexture(
                                topRightFront, frontNormal, textureTopRight);

                            // Back face.
                            shapeVertices[6] = new VertexPositionNormalTexture(
                                topLeftBack, backNormal, textureTopRight);
                            shapeVertices[7] = new VertexPositionNormalTexture(
                                topRightBack, backNormal, textureTopLeft);
                            shapeVertices[8] = new VertexPositionNormalTexture(
                                bottomLeftBack, backNormal, textureBottomRight);
                            shapeVertices[9] = new VertexPositionNormalTexture(
                                bottomLeftBack, backNormal, textureBottomRight);
                            shapeVertices[10] = new VertexPositionNormalTexture(
                                topRightBack, backNormal, textureTopLeft);
                            shapeVertices[11] = new VertexPositionNormalTexture(
                                bottomRightBack, backNormal, textureBottomLeft);

                            // Top face.
                            shapeVertices[12] = new VertexPositionNormalTexture(
                                topLeftFront, topNormal, textureBottomLeft);
                            shapeVertices[13] = new VertexPositionNormalTexture(
                                topRightBack, topNormal, textureTopRight);
                            shapeVertices[14] = new VertexPositionNormalTexture(
                                topLeftBack, topNormal, textureTopLeft);
                            shapeVertices[15] = new VertexPositionNormalTexture(
                                topLeftFront, topNormal, textureBottomLeft);
                            shapeVertices[16] = new VertexPositionNormalTexture(
                                topRightFront, topNormal, textureBottomRight);
                            shapeVertices[17] = new VertexPositionNormalTexture(
                                topRightBack, topNormal, textureTopRight);

                            // Bottom face.
                            shapeVertices[18] = new VertexPositionNormalTexture(
                                bottomLeftFront, bottomNormal, textureTopLeft);
                            shapeVertices[19] = new VertexPositionNormalTexture(
                                bottomLeftBack, bottomNormal, textureBottomLeft);
                            shapeVertices[20] = new VertexPositionNormalTexture(
                                bottomRightBack, bottomNormal, textureBottomRight);
                            shapeVertices[21] = new VertexPositionNormalTexture(
                                bottomLeftFront, bottomNormal, textureTopLeft);
                            shapeVertices[22] = new VertexPositionNormalTexture(
                                bottomRightBack, bottomNormal, textureBottomRight);
                            shapeVertices[23] = new VertexPositionNormalTexture(
                                bottomRightFront, bottomNormal, textureTopRight);

                            // Left face.
                            shapeVertices[24] = new VertexPositionNormalTexture(
                                topLeftFront, leftNormal, textureTopRight);
                            shapeVertices[25] = new VertexPositionNormalTexture(
                                bottomLeftBack, leftNormal, textureBottomLeft);
                            shapeVertices[26] = new VertexPositionNormalTexture(
                                bottomLeftFront, leftNormal, textureBottomRight);
                            shapeVertices[27] = new VertexPositionNormalTexture(
                                topLeftBack, leftNormal, textureTopLeft);
                            shapeVertices[28] = new VertexPositionNormalTexture(
                                bottomLeftBack, leftNormal, textureBottomLeft);
                            shapeVertices[29] = new VertexPositionNormalTexture(
                                topLeftFront, leftNormal, textureTopRight);

                            // Right face.
                            shapeVertices[30] = new VertexPositionNormalTexture(
                                topRightFront, rightNormal, textureTopLeft);
                            shapeVertices[31] = new VertexPositionNormalTexture(
                                bottomRightFront, rightNormal, textureBottomLeft);
                            shapeVertices[32] = new VertexPositionNormalTexture(
                                bottomRightBack, rightNormal, textureBottomRight);
                            shapeVertices[33] = new VertexPositionNormalTexture(
                                topRightBack, rightNormal, textureTopRight);
                            shapeVertices[34] = new VertexPositionNormalTexture(
                                topRightFront, rightNormal, textureTopLeft);
                            shapeVertices[35] = new VertexPositionNormalTexture(
                                bottomRightBack, rightNormal, textureBottomRight);

                            //BuildShape(regionBounds.Center - new Vector3(REGIONSIZE / 2, REGIONSIZE / 2, REGIONSIZE / 2), new Vector3(REGIONSIZE, REGIONSIZE, REGIONSIZE));


                            queryActive[o] = 3;
                            query[o].Begin();
                            //render a region-size square
                            VertexBuffer shapeBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionNormalTexture), shapeVertices.Length, BufferUsage.WriteOnly);
                            shapeBuffer.SetData(shapeVertices);

                            //basicEffect.CurrentTechnique = basicEffect.Techniques["Occ"];
                            //basicEffect.Parameters["xWorld"].SetValue(Matrix.Identity);
                            //basicEffect.Parameters["xView"].SetValue(gameInstance.propertyBag.playerCamera.ViewMatrix);
                            //basicEffect.Parameters["xProjection"].SetValue(gameInstance.propertyBag.playerCamera.ProjectionMatrix);
                            //basicEffect.Begin();
                            //foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
                            //{
                            //    pass.Begin();
                            graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
                            graphicsDevice.BlendState = new BlendState
                            {
                                ColorWriteChannels = ColorWriteChannels.None
                            };
                            graphicsDevice.RasterizerState = RasterizerState.CullNone;
                            graphicsDevice.SetVertexBuffer(shapeBuffer);
                            graphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, 12);
                            graphicsDevice.RasterizerState = RasterizerState.CullNone;
                            graphicsDevice.DepthStencilState = DepthStencilState.Default;
                            graphicsDevice.BlendState = BlendState.Opaque;
                            //    pass.End();
                            //}
                            //basicEffect.End();

                            //  basicEffect.CurrentTechnique = basicEffect.Techniques["Occ"];
                            //  basicEffect.Parameters["xWorld"].SetValue(Matrix.Identity);
                            //  basicEffect.Parameters["xView"].SetValue(gameInstance.propertyBag.playerCamera.ViewMatrix);
                            //  basicEffect.Parameters["xProjection"].SetValue(gameInstance.propertyBag.playerCamera.ProjectionMatrix);
                            // // basicEffect.Parameters["xTexture"].SetValue(blockTextures[(byte)BlockTexture.Barrel].Texture);
                            //  basicEffect.Parameters["xLODColor"].SetValue(new Vector3(1.0f, 1.0f, 1.0f));//lodColor.ToVector3());
                            ////  basicEffect.Parameters["xLight"].SetValue(1.0f);

                            //  basicEffect.Begin();
                            //  //foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
                            //  //{
                            //  //    pass.Begin();
                            //     // graphicsDevice.RenderState.DepthBufferEnable = true;//seems to work without
                            //      graphicsDevice.RenderState.DepthBufferWriteEnable = false;
                            //      graphicsDevice.RenderState.ColorWriteChannels = ColorWriteChannels.None;
                            //      graphicsDevice.RenderState.CullMode = CullMode.None; //CullMode.CullCounterClockwiseFace;
                            //      graphicsDevice.VertexDeclaration = vertexDeclaration;
                            //      graphicsDevice.Vertices[0].SetSource(shapeBuffer, 0, VertexPositionNormalTexture.SizeInBytes);
                            //      graphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, 12);
                            //      graphicsDevice.RenderState.CullMode = CullMode.None;
                            //      graphicsDevice.RenderState.DepthBufferWriteEnable = true;
                            //      graphicsDevice.RenderState.ColorWriteChannels = ColorWriteChannels.All;
                            //      //graphicsDevice.VertexDeclaration = new VertexDeclaration(
                            //      // graphicsDevice, VertexPositionNormalTexture.VertexElements);
                            //      //graphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, 12);
                            //  //    pass.End();
                            //  //}


                            //  basicEffect.End();
                            // if (queryActive[o] == 1)
                            //{
                            //   queryActive[o] = 3;
                            query[o].End();
                            //}
                            //RegionOcc[r] = 1;//visible on frustrum for now
                            //}
                            //else//render normally
                            //{

                            //}

                            //queryActive[o]++;
                        }
                    }

                    if (RegionOcc[o] > 0)
                        for (BlockTexture blockTexture = BlockTexture.None + 1; blockTexture < BlockTexture.MAXIMUM; blockTexture++)
                        {
                            // Figure out if we should be rendering translucently.
                            bool renderTranslucent = false;

                            if (blockTexture == BlockTexture.TransRed || blockTexture == BlockTexture.TransBlue || blockTexture == BlockTexture.Water || (gameInstance.propertyBag.playerTeam == PlayerTeam.Red && blockTexture == BlockTexture.StealthBlockR) || (gameInstance.propertyBag.playerTeam == PlayerTeam.Blue && blockTexture == BlockTexture.StealthBlockB))
                                renderTranslucent = true;

                            // If this is empty, don't render it.
                            DynamicVertexBuffer regionBuffer = vertexBuffers[(byte)blockTexture, o];
                            if (regionBuffer == null)
                                continue;

                            // If this isn't in our view frustum, don't render it. //double frustum check unnecessary?
                            BoundingSphere regionBounds = new BoundingSphere(GetRegionCenter(o), REGIONSIZE);
                            BoundingFrustum boundingFrustum = new BoundingFrustum(gameInstance.propertyBag.playerCamera.ViewMatrix * gameInstance.propertyBag.playerCamera.ProjectionMatrix);
                            if (boundingFrustum.Contains(regionBounds) == ContainmentType.Disjoint)
                            {
                                // RegionOcc[r] = -1;
                                RegionOcc[o] = 0;//might not be accurate
                                continue;
                            }
                            //RegionOcc[r] = 0;
                            // Make sure our vertex buffer is clean.
                            if (vertexListDirty[(byte)blockTexture, o])
                                continue;

                            // Actually render.
                            if (query[o].IsComplete && queryActive[o] == 0)
                            {
                                queryActive[o] = 3;
                                query[o].Begin();
                                RenderVertexList(graphicsDevice, regionBuffer, blockTextures[(byte)blockTexture].Texture, blockTextures[(byte)blockTexture].LODColor, renderTranslucent, blockTexture, (float)gameTime.TotalGameTime.TotalSeconds);
                                query[o].End();
                            }
                            else
                            {
                                RenderVertexList(graphicsDevice, regionBuffer, blockTextures[(byte)blockTexture].Texture, blockTextures[(byte)blockTexture].LODColor, renderTranslucent, blockTexture, (float)gameTime.TotalGameTime.TotalSeconds);
                            }
                        }
                }
            }
            else//no occ rendering path
            {

                for (BlockTexture blockTexture = BlockTexture.None + 1; blockTexture < BlockTexture.MAXIMUM; blockTexture++)
                    for (uint r = 0; r < NUMREGIONS; r++)
                    {
                        // Figure out if we should be rendering translucently.
                        bool renderTranslucent = false;

                        if (blockTexture == BlockTexture.TransRed || blockTexture == BlockTexture.TransBlue || blockTexture == BlockTexture.Water || (gameInstance.propertyBag.playerTeam == PlayerTeam.Red && blockTexture == BlockTexture.StealthBlockR) || (gameInstance.propertyBag.playerTeam == PlayerTeam.Blue && blockTexture == BlockTexture.StealthBlockB))
                            renderTranslucent = true;

                        // If this is empty, don't render it.
                        DynamicVertexBuffer regionBuffer = vertexBuffers[(byte)blockTexture, r];
                        if (regionBuffer == null)
                            continue;

                        // If this isn't in our view frustum, don't render it.
                        BoundingSphere regionBounds = new BoundingSphere(GetRegionCenter(r), REGIONSIZE);
                        BoundingFrustum boundingFrustum = new BoundingFrustum(gameInstance.propertyBag.playerCamera.ViewMatrix * gameInstance.propertyBag.playerCamera.ProjectionMatrix);
                        if (boundingFrustum.Contains(regionBounds) == ContainmentType.Disjoint)
                            continue;

                        // Make sure our vertex buffer is clean.
                        if (vertexListDirty[(byte)blockTexture, r])
                            continue;

                        // Actually render.
                        RenderVertexList(graphicsDevice, regionBuffer, blockTextures[(byte)blockTexture].Texture, blockTextures[(byte)blockTexture].LODColor, renderTranslucent, blockTexture, (float)gameTime.TotalGameTime.TotalSeconds);
                    }
            }
            // Apply posteffects.
            if (bloomPosteffect != null)
                bloomPosteffect.Draw(graphicsDevice);
        }

        public void DrawLine(GraphicsDevice graphicsDevice, Vector3 posStart, Vector3 posEnd, Color color, short points)
        {

            //BasicEffect basicEffect;
            //basicEffect.VertexColorEnabled = true;

            VertexPositionColor[] pointList = new VertexPositionColor[2];
            pointList[0] = new VertexPositionColor(posStart, Color.Red);
            pointList[1] = new VertexPositionColor(posEnd, Color.Red);
            int[] lineListIndices = new int[2];
            lineListIndices = new int[2];
            lineListIndices[0] = 0;
            lineListIndices[1] = 1;

            // Populate the array with references to indices in the vertex buffer
            //for (int i = 0; i < points - 1; i++)
            //{
            //    lineListIndices[i * 2] = (short)(i);
            //    lineListIndices[(i * 2) + 1] = (short)(i + 1);
            //}

            //lineListIndices = new short[14]{ 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7 };

            graphicsDevice.DrawUserIndexedPrimitives<VertexPositionColor>(PrimitiveType.LineList, pointList, 0, 2, lineListIndices, 0, 1);
        }
        private void RenderVertexListOcc(GraphicsDevice graphicsDevice, DynamicVertexBuffer vertexBuffer, int region)
        {

            if (vertexBuffer == null)
                return;

            basicEffect.CurrentTechnique = basicEffect.Techniques["Occ"];

            basicEffect.Parameters["xWorld"].SetValue(Matrix.Identity);
            basicEffect.Parameters["xView"].SetValue(gameInstance.propertyBag.playerCamera.ViewMatrix);
            basicEffect.Parameters["xProjection"].SetValue(gameInstance.propertyBag.playerCamera.ProjectionMatrix);

            basicEffect.CurrentTechnique.Passes[0].Apply();
            graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
            graphicsDevice.SetVertexBuffer(vertexBuffer);
            graphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, vertexBuffer.VertexCount / 3);
            graphicsDevice.RasterizerState = RasterizerState.CullNone;

        }

        private void RenderVertexList(GraphicsDevice graphicsDevice, DynamicVertexBuffer vertexBuffer, Texture2D blockTexture, Color lodColor, bool renderTranslucent, BlockTexture blocktex, float elapsedTime)
        {
            
            if (vertexBuffer == null)
                return;

            if (blocktex == BlockTexture.Lava || blocktex == BlockTexture.ForceR || blocktex == BlockTexture.ForceB)
            {
                basicEffect.CurrentTechnique = basicEffect.Techniques["LavaBlock"];
                basicEffect.Parameters["xTime"].SetValue(elapsedTime % 5);
            }
            else
            {
                basicEffect.CurrentTechnique = basicEffect.Techniques["Block"];
            }
            basicEffect.Parameters["xWorld"].SetValue(Matrix.Identity);
            basicEffect.Parameters["xView"].SetValue(gameInstance.propertyBag.playerCamera.ViewMatrix);
            basicEffect.Parameters["xProjection"].SetValue(gameInstance.propertyBag.playerCamera.ProjectionMatrix);
            basicEffect.Parameters["xTexture"].SetValue(blockTexture);
            basicEffect.Parameters["xLODColor"].SetValue(lodColor.ToVector3());
            basicEffect.Parameters["xLight"].SetValue(1.0f);
            
            foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();

                if (renderTranslucent)
                {
                    // TODO: Make translucent blocks look like we actually want them to look!
                    // We probably also want to pull this out to be rendered AFTER EVERYTHING ELSE IN THE GAME.
                    graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
                    graphicsDevice.BlendState = BlendState.AlphaBlend;
                    graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
                }
                else if (blocktex == BlockTexture.GlassR || blocktex == BlockTexture.GlassB || blocktex == BlockTexture.ForceR || blocktex == BlockTexture.ForceB)
                {
                    graphicsDevice.BlendState = new BlendState
                    {
                        AlphaSourceBlend = Blend.One,
                        AlphaDestinationBlend = Blend.Zero,
                        ColorSourceBlend = Blend.SourceAlpha,
                        ColorDestinationBlend = Blend.InverseSourceAlpha,
                        AlphaBlendFunction = BlendFunction.Add,
                        ColorBlendFunction = BlendFunction.Add
                    };
                    graphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
                }
                else
                {
                    graphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
                }
                graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
                graphicsDevice.SetVertexBuffer(vertexBuffer);
                graphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, vertexBuffer.VertexCount / 3);
                graphicsDevice.RasterizerState = RasterizerState.CullNone;

                if (renderTranslucent)
                {
                    graphicsDevice.DepthStencilState = DepthStencilState.Default;
                    graphicsDevice.BlendState = BlendState.Opaque;
                }
                else if (blocktex == BlockTexture.GlassR || blocktex == BlockTexture.GlassB || blocktex == BlockTexture.ForceR || blocktex == BlockTexture.ForceB)
                {
                    graphicsDevice.BlendState = BlendState.Opaque;
                }
            }
       
        }

        public void RegenerateDirtyVertexLists()
        {
            for (BlockTexture blockTexture = BlockTexture.None+1; blockTexture < BlockTexture.MAXIMUM; blockTexture++)
                for (int r = 0; r < NUMREGIONS; r++)
                    if (vertexListDirty[(byte)blockTexture, r])
                    {
                        vertexListDirty[(byte)blockTexture, r] = false;
                        Dictionary<uint, bool> faceList = faceMap[(byte)blockTexture, r];
                        vertexBuffers[(byte)blockTexture, r] = CreateVertexBufferFromFaceList(faceList, (byte)blockTexture, r);
                    }
        }

        public struct DynamicVertexBufferTag
        {
            public BlockEngine blockEngine;
            public int texture, region;
            public DynamicVertexBufferTag(BlockEngine blockEngine, int texture, int region)
            {
                this.blockEngine = blockEngine;
                this.texture = texture;
                this.region = region;
            }
        }

        // Create a dynamic vertex buffer. The arguments texture and region are used to flag a content reload if the device is lost.
        private DynamicVertexBuffer CreateVertexBufferFromFaceList(Dictionary<uint, bool> faceList, int texture, int region)
        {
            if (faceList.Count == 0)
                return null;

            VertexPositionTextureShade[] vertexList = new VertexPositionTextureShade[faceList.Count * 6];
            ulong vertexPointer = 0;
            foreach (uint faceInfo in faceList.Keys)
            {
                BuildFaceVertices(ref vertexList, vertexPointer, faceInfo, texture == (int)BlockTexture.Spikes);
                vertexPointer += 6;            
            }
            DynamicVertexBuffer vertexBuffer = new DynamicVertexBuffer(gameInstance.GraphicsDevice, typeof(VertexPositionTextureShade), vertexList.Length, BufferUsage.WriteOnly);
            vertexBuffer.Tag = new DynamicVertexBufferTag(this, texture, region);
            vertexBuffer.SetData(vertexList);
            return vertexBuffer;
        }

        // ContentLost event no longer exists in MonoGame, so this method is unused
        /*
        void vertexBuffer_ContentLost(object sender, EventArgs e)
        {
            DynamicVertexBuffer dvb = sender as DynamicVertexBuffer;
            if (dvb != null)
            {
                DynamicVertexBufferTag tag = (DynamicVertexBufferTag)dvb.Tag;
                tag.blockEngine.MakeRegionDirty(tag.texture, tag.region);
            }
        }
        */

        public void ShadowRay(int x, int yy, int z)//, ref BlockFaceDirection f)
        {
            bool first = true;
            uint reg = 0;
            for (int y = yy; y > 2; y--)
            {
                if (blockList[x, y, z] == BlockType.None || blockList[x, y, z] == BlockType.Water || first)
                {
                    if (first)
                    {
                        first = false;

                        Light[x, y, z, 0] = 0.7;
                        Light[x + 1, y, z, 1] = 0.7;
                        reg = GetRegion(x + 1, y, z);
                        vertexListDirty[(byte)blockTexList[x + 1, y, z], reg] = true;
                        //for (int d = 1; d < (int)BlockTexture.MAXIMUM; d++)
                        //{
                        //    vertexListDirty[d, reg] = true;
                        //}

                        Light[x, y, z, 1] = 0.7;
                        Light[x - 1, y, z, 0] = 0.7;
                        reg = GetRegion(x - 1, y, z);
                        vertexListDirty[(byte)blockTexList[x - 1, y, z], reg] = true;
                        //for (int d = 1; d < (int)BlockTexture.MAXIMUM; d++)
                        //{
                        //    vertexListDirty[d, reg] = true;
                        //}

                        Light[x, y, z, 2] = 0.7;
                        Light[x, y + 1, z, 3] = 0.7;
                        reg = GetRegion(x, y + 1, z);
                        vertexListDirty[(byte)blockTexList[x, y + 1, z], reg] = true;
                        //for (int d = 1; d < (int)BlockTexture.MAXIMUM; d++)
                        //{
                        //    vertexListDirty[d, reg] = true;
                        //}

                        Light[x, y, z, 3] = 0.7;
                        Light[x, y - 1, z, 2] = 0.7;
                        reg = GetRegion(x, y - 1, z);
                        vertexListDirty[(byte)blockTexList[x, y - 1, z], reg] = true;
                        //for (int d = 1; d < (int)BlockTexture.MAXIMUM; d++)
                        //{
                        //    vertexListDirty[d, reg] = true;
                        //}

                        Light[x, y, z, 4] = 0.7;
                        Light[x, y, z + 1, 5] = 0.7;
                        reg = GetRegion(x, y, z + 1);
                        vertexListDirty[(byte)blockTexList[x, y, z + 1], reg] = true;
                        //for (int d = 1; d < (int)BlockTexture.MAXIMUM; d++)
                        //{
                        //    vertexListDirty[d, reg] = true;
                        //}

                        Light[x, y, z, 5] = 0.7;
                        Light[x, y, z - 1, 4] = 0.7;
                        reg = GetRegion(x, y, z - 1);
                        vertexListDirty[(byte)blockTexList[x, y, z - 1], reg] = true;
                        //for (int d = 1; d < (int)BlockTexture.MAXIMUM; d++)
                        //{
                        //    vertexListDirty[d, reg] = true;
                        //}
                    }
                    else
                    {
                        Light[x, y, z, 0] = 0.7;
                        Light[x + 1, y, z, 1] = 0.7;
                        reg = GetRegion(x + 1, y, z);
                        vertexListDirty[(byte)blockTexList[x + 1, y, z], reg] = true;
                        //for (int d = 1; d < (int)BlockTexture.MAXIMUM; d++)
                        //{
                        //    vertexListDirty[d, reg] = true;
                        //}

                        Light[x, y, z, 1] = 0.7;
                        Light[x - 1, y, z, 0] = 0.7;
                        reg = GetRegion(x - 1, y, z);
                        vertexListDirty[(byte)blockTexList[x - 1, y, z], reg] = true;
                        //for (int d = 1; d < (int)BlockTexture.MAXIMUM; d++)
                        //{
                        //    vertexListDirty[d, reg] = true;
                        //}

                        Light[x, y, z, 2] = 0.7;
                        Light[x, y + 1, z, 3] = 0.7;
                        reg = GetRegion(x, y + 1, z);
                        vertexListDirty[(byte)blockTexList[x, y + 1, z], reg] = true;
                        //for (int d = 1; d < (int)BlockTexture.MAXIMUM; d++)
                        //{
                        //    vertexListDirty[d, reg] = true;
                        //}

                        Light[x, y, z, 3] = 0.7;
                        Light[x, y - 1, z, 2] = 0.7;
                        reg = GetRegion(x, y - 1, z);
                        vertexListDirty[(byte)blockTexList[x, y - 1, z], reg] = true;
                        //for (int d = 1; d < (int)BlockTexture.MAXIMUM; d++)
                        //{
                        //    vertexListDirty[d, reg] = true;
                        //}

                        Light[x, y, z, 4] = 0.7;
                        Light[x, y, z + 1, 5] = 0.7;
                        reg = GetRegion(x, y, z + 1);
                        vertexListDirty[(byte)blockTexList[x, y, z + 1], reg] = true;
                        //for (int d = 1; d < (int)BlockTexture.MAXIMUM; d++)
                        //{
                        //    vertexListDirty[d, reg] = true;
                        //}

                        Light[x, y, z, 5] = 0.7;
                        Light[x, y, z - 1, 4] = 0.7;
                        reg = GetRegion(x, y, z - 1);
                        vertexListDirty[(byte)blockTexList[x, y, z - 1], reg] = true;
                        //for (int d = 1; d < (int)BlockTexture.MAXIMUM; d++)
                        //{
                        //    vertexListDirty[d, reg] = true;
                        //}
                    }
                }
                else
                {
                   
                    Light[x, y, z, 3] = 0.7f;
                    reg = GetRegion(x, y, z);
                    vertexListDirty[(byte)blockTexList[x, y, z], reg] = true;
                    //for (int d = 1; d < (int)BlockTexture.MAXIMUM; d++)
                    //{
                    //    vertexListDirty[d, reg] = true;
                    //}
                    return;
                }
            }
        }

        public void LightRay(int x, int yy, int z)//, ref BlockFaceDirection f)
        {
            uint reg = 0;
            for (int y = yy; y > 2; y--)
            {
                if (blockList[x, y, z] == BlockType.None || blockList[x, y, z] == BlockType.Water || blockList[x, y, z] == BlockType.GlassR || blockList[x, y, z] == BlockType.GlassB)
                {
                    /*
                     * Light[x, y, z, 0] = 1.0f;
                           if(x+1 < MAPSIZE)
                           Light[x + 1, y, z, 1] = 1.0f;

                           Light[x, y, z, 1] = 1.0f;
                           if (x - 1 > 0)
                           Light[x - 1, y, z, 0] = 1.0f;

                           Light[x, y, z, 2] = 1.0f;
                           if (y + 1 < MAPSIZE)
                           Light[x, y + 1, z, 3] = 1.0f;

                           Light[x, y, z, 3] = 1.0f;
                           if (y - 1 > 0)
                           Light[x, y - 1, z, 2] = 1.0f;

                           Light[x, y, z, 4] = 1.0f;
                           if (z + 1 < MAPSIZE)
                           Light[x, y, z + 1, 5] = 1.0f;

                           Light[x, y, z, 5] = 1.0f;
                           if (z - 1 > 0)
                           Light[x, y, z - 1, 4] = 1.0f;
                     * */
                    Light[x, y, z, 0] = 1.2;
                    if (x + 1 < MAPSIZE)
                    {
                        Light[x + 1, y, z, 1] = 1.2;
                        reg = GetRegion(x + 1, y, z);
                        vertexListDirty[(byte)blockTexList[x + 1, y, z], reg] = true;
                    }
                    //for (int d = 1; d < (int)BlockTexture.MAXIMUM; d++)
                    //{
                    //    vertexListDirty[d, reg] = true;
                    //}

                    Light[x, y, z, 1] = 1.2;
                    if (x - 1 > 0)
                    {
                        Light[x - 1, y, z, 0] = 1.2;
                        reg = GetRegion(x - 1, y, z);
                        vertexListDirty[(byte)blockTexList[x - 1, y, z], reg] = true;
                    }
                    //for (int d = 1; d < (int)BlockTexture.MAXIMUM; d++)
                    //{
                    //    vertexListDirty[d, reg] = true;
                    //}

                    Light[x, y, z, 2] = 1.2;
                    if (y + 1 < MAPSIZE)
                    {
                        Light[x, y + 1, z, 3] = 1.2;
                        reg = GetRegion(x, y + 1, z);
                        vertexListDirty[(byte)blockTexList[x, y + 1, z], reg] = true;
                    }
                    //for (int d = 1; d < (int)BlockTexture.MAXIMUM; d++)
                    //{
                    //    vertexListDirty[d, reg] = true;
                    //}

                    Light[x, y, z, 3] = 1.2;
                    if (y - 1 > 0)
                    {
                        Light[x, y - 1, z, 2] = 1.2;
                        reg = GetRegion(x, y - 1, z);
                        vertexListDirty[(byte)blockTexList[x, y - 1, z], reg] = true;
                    }
                    //for (int d = 1; d < (int)BlockTexture.MAXIMUM; d++)
                    //{
                    //    vertexListDirty[d, reg] = true;
                    //}

                    Light[x, y, z, 4] = 1.2;
                    if (z + 1 < MAPSIZE)
                    {
                        Light[x, y, z + 1, 5] = 1.2;
                        reg = GetRegion(x, y, z + 1);
                        vertexListDirty[(byte)blockTexList[x, y, z + 1], reg] = true;
                    }
                    //for (int d = 1; d < (int)BlockTexture.MAXIMUM; d++)
                    //{
                    //    vertexListDirty[d, reg] = true;
                    //}

                    Light[x, y, z, 5] = 1.2;
                    if (z - 1 > 0)
                    {
                        Light[x, y, z - 1, 4] = 1.2;
                        reg = GetRegion(x, y, z - 1);
                        vertexListDirty[(byte)blockTexList[x, y, z - 1], reg] = true;
                    }
                    //for (int d = 1; d < (int)BlockTexture.MAXIMUM; d++)
                    //{
                    //    vertexListDirty[d, reg] = true;
                    //}
                    
                    //vertexListDirty[(byte)blockTexList[x,y,z], region] = true;

                }
                else
                {
                    return;
                }
            }
        }
        public void CalculateLight()
        {
            for(int x = 0;x < MAPSIZE;x++)
               for(int z = 0;z < MAPSIZE;z++)
                   for (int y = MAPSIZE-1; y > 0; y--)
                   {
                       if (gameInstance.RenderLight)
                       {
                           if (blockList[x, y, z] == BlockType.None || blockList[x, y, z] == BlockType.Water || blockList[x, y, z] == BlockType.GlassR || blockList[x, y, z] == BlockType.GlassB)
                           {
                               //case BlockFaceDirection.XIncreasing: 0

                               //  case BlockFaceDirection.XDecreasing: 1

                               //  case BlockFaceDirection.YIncreasing: 2

                               //  case BlockFaceDirection.YDecreasing: 3

                               //  case BlockFaceDirection.ZIncreasing: 4

                               //  case BlockFaceDirection.ZDecreasing: 5

                               //need to check boundaries
                               Light[x, y, z, 0] = 1.2f;
                               if (x + 1 < MAPSIZE)
                                   Light[x + 1, y, z, 1] = 1.2f;

                               Light[x, y, z, 1] = 1.2f;
                               if (x - 1 > 0)
                                   Light[x - 1, y, z, 0] = 1.2f;

                               Light[x, y, z, 2] = 1.2f;
                               if (y + 1 < MAPSIZE)
                                   Light[x, y + 1, z, 3] = 1.2f;

                               Light[x, y, z, 3] = 1.2f;
                               if (y - 1 > 0)
                                   Light[x, y - 1, z, 2] = 1.2f;

                               Light[x, y, z, 4] = 1.2f;
                               if (z + 1 < MAPSIZE)
                                   Light[x, y, z + 1, 5] = 1.2f;

                               Light[x, y, z, 5] = 1.2f;
                               if (z - 1 > 0)
                                   Light[x, y, z - 1, 4] = 1.2f;
                           }
                           else
                           {
                               //  Light[x, y, z, 2] = 1.0f;//upwards
                               break;
                           }
                       }
                       else
                       {
                           Light[x, y, z, 0] = 1.2f;
                           Light[x, y, z, 1] = 1.2f;
                           Light[x, y, z, 2] = 1.2f;
                           Light[x, y, z, 3] = 1.2f;
                           Light[x, y, z, 4] = 1.2f;
                           Light[x, y, z, 5] = 1.2f;
                       }
                   }
        }
        public double GetLighting(ref ushort x, ref ushort y, ref ushort z, ref BlockFaceDirection face)
        {
            return Light[x, y, z, (byte)face];
            //light = 1.0f - ((MAPSIZE-10) - y) * 0.05f;
           // return light;//Light[x, y, z, face];//
        }

        private void BuildFaceVertices(ref VertexPositionTextureShade[] vertexList, ulong vertexPointer, uint faceInfo, bool isShockBlock)
        {
            // Decode the face information.
            ushort x = 0, y = 0, z = 0;
            BlockFaceDirection faceDir = BlockFaceDirection.MAXIMUM;
            DecodeBlockFace(faceInfo, ref x, ref y, ref z, ref faceDir);
            //lighting
            double modifier = 1.0f;// GetLighting(ref x, ref y, ref z);// 1.0f - (MAPSIZE - y) * 0.05f;
            // Insert the vertices.
            switch (faceDir)
            {
                case BlockFaceDirection.XIncreasing:
                    {
                        modifier = GetLighting(ref x, ref y, ref z, ref faceDir);
                        vertexList[vertexPointer + 0] = new VertexPositionTextureShade(new Vector3(x + 1, y + 1, z + 1), new Vector2(0, 0), 0.6 * modifier);
                        vertexList[vertexPointer + 1] = new VertexPositionTextureShade(new Vector3(x + 1, y + 1, z), new Vector2(1, 0), 0.6 * modifier);
                        vertexList[vertexPointer + 2] = new VertexPositionTextureShade(new Vector3(x + 1, y, z + 1), new Vector2(0, 1), 0.6 * modifier);
                        vertexList[vertexPointer + 3] = new VertexPositionTextureShade(new Vector3(x + 1, y, z + 1), new Vector2(0, 1), 0.6 * modifier);
                        vertexList[vertexPointer + 4] = new VertexPositionTextureShade(new Vector3(x + 1, y + 1, z), new Vector2(1, 0), 0.6 * modifier);
                        vertexList[vertexPointer + 5] = new VertexPositionTextureShade(new Vector3(x + 1, y, z), new Vector2(1, 1), 0.6 * modifier);
                    }
                    break;


                case BlockFaceDirection.XDecreasing:
                    {
                        modifier = GetLighting(ref x, ref y, ref z, ref faceDir);
                        vertexList[vertexPointer + 0] = new VertexPositionTextureShade(new Vector3(x, y + 1, z), new Vector2(0, 0), 0.6 * modifier);
                        vertexList[vertexPointer + 1] = new VertexPositionTextureShade(new Vector3(x, y + 1, z + 1), new Vector2(1, 0), 0.6 * modifier);
                        vertexList[vertexPointer + 2] = new VertexPositionTextureShade(new Vector3(x, y, z + 1), new Vector2(1, 1), 0.6 * modifier);
                        vertexList[vertexPointer + 3] = new VertexPositionTextureShade(new Vector3(x, y + 1, z), new Vector2(0, 0), 0.6 * modifier);
                        vertexList[vertexPointer + 4] = new VertexPositionTextureShade(new Vector3(x, y, z + 1), new Vector2(1, 1), 0.6 * modifier);
                        vertexList[vertexPointer + 5] = new VertexPositionTextureShade(new Vector3(x, y, z), new Vector2(0, 1), 0.6 * modifier);
                    }
                    break;

                case BlockFaceDirection.YIncreasing:
                    {
                        modifier = GetLighting(ref x, ref y, ref z, ref faceDir);
                        vertexList[vertexPointer + 0] = new VertexPositionTextureShade(new Vector3(x, y + 1, z), new Vector2(0, 1), 0.8 * modifier);
                        vertexList[vertexPointer + 1] = new VertexPositionTextureShade(new Vector3(x + 1, y + 1, z), new Vector2(0, 0), 0.8 * modifier);
                        vertexList[vertexPointer + 2] = new VertexPositionTextureShade(new Vector3(x + 1, y + 1, z + 1), new Vector2(1, 0), 0.8 * modifier);
                        vertexList[vertexPointer + 3] = new VertexPositionTextureShade(new Vector3(x, y + 1, z), new Vector2(0, 1), 0.8 * modifier);
                        vertexList[vertexPointer + 4] = new VertexPositionTextureShade(new Vector3(x + 1, y + 1, z + 1), new Vector2(1, 0), 0.8 * modifier);
                        vertexList[vertexPointer + 5] = new VertexPositionTextureShade(new Vector3(x, y + 1, z + 1), new Vector2(1, 1), 0.8 * modifier);
                    }
                    break;

                case BlockFaceDirection.YDecreasing:
                    {
                        if (isShockBlock)
                        {
                            vertexList[vertexPointer + 0] = new VertexPositionTextureShade(new Vector3(x + 1, y, z + 1), new Vector2(0, 0), 1.5);
                            vertexList[vertexPointer + 1] = new VertexPositionTextureShade(new Vector3(x + 1, y, z), new Vector2(1, 0), 1.5);
                            vertexList[vertexPointer + 2] = new VertexPositionTextureShade(new Vector3(x, y, z + 1), new Vector2(0, 1), 1.5);
                            vertexList[vertexPointer + 3] = new VertexPositionTextureShade(new Vector3(x, y, z + 1), new Vector2(0, 1), 1.5);
                            vertexList[vertexPointer + 4] = new VertexPositionTextureShade(new Vector3(x + 1, y, z), new Vector2(1, 0), 1.5);
                            vertexList[vertexPointer + 5] = new VertexPositionTextureShade(new Vector3(x, y, z), new Vector2(1, 1), 1.5);
                        }
                        else
                        {
                            modifier = GetLighting(ref x, ref y, ref z, ref faceDir);
                            vertexList[vertexPointer + 0] = new VertexPositionTextureShade(new Vector3(x + 1, y, z + 1), new Vector2(0, 0), 0.2 * modifier);
                            vertexList[vertexPointer + 1] = new VertexPositionTextureShade(new Vector3(x + 1, y, z), new Vector2(1, 0), 0.2 * modifier);
                            vertexList[vertexPointer + 2] = new VertexPositionTextureShade(new Vector3(x, y, z + 1), new Vector2(0, 1), 0.2 * modifier);
                            vertexList[vertexPointer + 3] = new VertexPositionTextureShade(new Vector3(x, y, z + 1), new Vector2(0, 1), 0.2 * modifier);
                            vertexList[vertexPointer + 4] = new VertexPositionTextureShade(new Vector3(x + 1, y, z), new Vector2(1, 0), 0.2 * modifier);
                            vertexList[vertexPointer + 5] = new VertexPositionTextureShade(new Vector3(x, y, z), new Vector2(1, 1), 0.2 * modifier);
                        }
                    }
                    break;

                case BlockFaceDirection.ZIncreasing:
                    {
                        modifier = GetLighting(ref x, ref y, ref z, ref faceDir);
                        vertexList[vertexPointer + 0] = new VertexPositionTextureShade(new Vector3(x, y + 1, z + 1), new Vector2(0, 0), 0.5 * modifier);
                        vertexList[vertexPointer + 1] = new VertexPositionTextureShade(new Vector3(x + 1, y + 1, z + 1), new Vector2(1, 0), 0.5 * modifier);
                        vertexList[vertexPointer + 2] = new VertexPositionTextureShade(new Vector3(x + 1, y, z + 1), new Vector2(1, 1), 0.5 * modifier);
                        vertexList[vertexPointer + 3] = new VertexPositionTextureShade(new Vector3(x, y + 1, z + 1), new Vector2(0, 0), 0.5 * modifier);
                        vertexList[vertexPointer + 4] = new VertexPositionTextureShade(new Vector3(x + 1, y, z + 1), new Vector2(1, 1), 0.5 * modifier);
                        vertexList[vertexPointer + 5] = new VertexPositionTextureShade(new Vector3(x, y, z + 1), new Vector2(0, 1), 0.5 * modifier);
                    }
                    break;

                case BlockFaceDirection.ZDecreasing:
                    {
                        modifier = GetLighting(ref x, ref y, ref z, ref faceDir);
                        vertexList[vertexPointer + 0] = new VertexPositionTextureShade(new Vector3(x + 1, y + 1, z), new Vector2(0, 0), 0.5 * modifier);
                        vertexList[vertexPointer + 1] = new VertexPositionTextureShade(new Vector3(x, y + 1, z), new Vector2(1, 0), 0.5 * modifier);
                        vertexList[vertexPointer + 2] = new VertexPositionTextureShade(new Vector3(x + 1, y, z), new Vector2(0, 1), 0.5 * modifier);
                        vertexList[vertexPointer + 3] = new VertexPositionTextureShade(new Vector3(x + 1, y, z), new Vector2(0, 1), 0.5 * modifier);
                        vertexList[vertexPointer + 4] = new VertexPositionTextureShade(new Vector3(x, y + 1, z), new Vector2(1, 0), 0.5 * modifier);
                        vertexList[vertexPointer + 5] = new VertexPositionTextureShade(new Vector3(x, y, z), new Vector2(1, 1), 0.5 * modifier);
                    }
                    break;
            }
        }

        private void _AddBlock(ushort x, ushort y, ushort z, BlockFaceDirection dir, BlockType type, int x2, int y2, int z2, BlockFaceDirection dir2)
        {
            BlockType type2 = blockTexList[x2, y2, z2];
            if (type2 != BlockType.None && type != BlockType.ForceR && type != BlockType.ForceB && type2 != BlockType.ForceR && type2 != BlockType.ForceB && type != BlockType.GlassR && type != BlockType.GlassB && type2 != BlockType.GlassR && type2 != BlockType.GlassB && type2 != BlockType.TrapB && type != BlockType.TrapB && type != BlockType.TrapR && type2 != BlockType.TrapR && type != BlockType.TransRed && type != BlockType.TransBlue && type != BlockType.Water && type != BlockType.StealthBlockB && type != BlockType.StealthBlockR && type2 != BlockType.TransRed && type2 != BlockType.TransBlue && type2 != BlockType.Water && type2 != BlockType.StealthBlockB && type2 != BlockType.StealthBlockR)
                HideQuad((ushort)x2, (ushort)y2, (ushort)z2, dir2, type2);
            else
                ShowQuad(x, y, z, dir, type);
        }

        public void AddBlock(ushort x, ushort y, ushort z, BlockType blockType)
        {
            if (x <= 0 || y <= 0 || z <= 0 || (int)x >= MAPSIZE - 1 || (int)y >= MAPSIZE - 1 || (int)z >= MAPSIZE - 1)
                return;

            blockList[x, y, z] = blockType;
            blockTexList[x, y, z] = blockType;

            if (y > 0)
            if (gameInstance.RenderLight && blockType != BlockType.GlassB && blockType != BlockType.GlassR && blockType != BlockType.Water)
            {
                ShadowRay(x, y - 1, z);
            }

            _AddBlock(x, y, z, BlockFaceDirection.XIncreasing, blockType, x + 1, y, z, BlockFaceDirection.XDecreasing);
            _AddBlock(x, y, z, BlockFaceDirection.XDecreasing, blockType, x - 1, y, z, BlockFaceDirection.XIncreasing);
            _AddBlock(x, y, z, BlockFaceDirection.YIncreasing, blockType, x, y + 1, z, BlockFaceDirection.YDecreasing);
            _AddBlock(x, y, z, BlockFaceDirection.YDecreasing, blockType, x, y - 1, z, BlockFaceDirection.YIncreasing);
            _AddBlock(x, y, z, BlockFaceDirection.ZIncreasing, blockType, x, y, z + 1, BlockFaceDirection.ZDecreasing);
            _AddBlock(x, y, z, BlockFaceDirection.ZDecreasing, blockType, x, y, z - 1, BlockFaceDirection.ZIncreasing);
        }

        public void AddBlock(ushort x, ushort y, ushort z, BlockType blockType, BlockType blockTex)
        {
            if (x <= 0 || y <= 0 || z <= 0 || (int)x >= MAPSIZE - 1 || (int)y >= MAPSIZE - 1 || (int)z >= MAPSIZE - 1)
                return;

            blockList[x, y, z] = blockType;
            blockTexList[x, y, z] = blockTex;

            //if(Light[x, y, z, (byte)BlockFaceDirection.YIncreasing] > 0.6)
            //{
            if (gameInstance.RenderLight && blockType != BlockType.GlassB && blockType != BlockType.GlassR && blockType != BlockType.Water)
                ShadowRay(x, y - 1, z);
            //}
            _AddBlock(x, y, z, BlockFaceDirection.XIncreasing, blockTex, x + 1, y, z, BlockFaceDirection.XDecreasing);
            _AddBlock(x, y, z, BlockFaceDirection.XDecreasing, blockTex, x - 1, y, z, BlockFaceDirection.XIncreasing);
            _AddBlock(x, y, z, BlockFaceDirection.YIncreasing, blockTex, x, y + 1, z, BlockFaceDirection.YDecreasing);
            _AddBlock(x, y, z, BlockFaceDirection.YDecreasing, blockTex, x, y - 1, z, BlockFaceDirection.YIncreasing);
            _AddBlock(x, y, z, BlockFaceDirection.ZIncreasing, blockTex, x, y, z + 1, BlockFaceDirection.ZDecreasing);
            _AddBlock(x, y, z, BlockFaceDirection.ZDecreasing, blockTex, x, y, z - 1, BlockFaceDirection.ZIncreasing);
        }

        private void _RemoveBlock(ushort x, ushort y, ushort z, BlockFaceDirection dir, int x2, int y2, int z2, BlockFaceDirection dir2)
        {
            BlockType type = blockTexList[x, y, z];
            BlockType type2 = blockTexList[x2, y2, z2];
            if (type2 != BlockType.None && type2 != BlockType.GlassR && type2 != BlockType.GlassB && type2 != BlockType.ForceR && type2 != BlockType.ForceB && type != BlockType.GlassR && type != BlockType.GlassB && type != BlockType.ForceR && type != BlockType.ForceB && type2 != BlockType.TrapB && type != BlockType.TrapB && type != BlockType.TrapR && type2 != BlockType.TrapR && type != BlockType.TransRed && type != BlockType.TransBlue && type != BlockType.Water && type != BlockType.StealthBlockB && type != BlockType.StealthBlockR && type2 != BlockType.TransRed && type2 != BlockType.TransBlue && type2 != BlockType.Water && type2 != BlockType.StealthBlockB && type2 != BlockType.StealthBlockR)
                ShowQuad((ushort)x2, (ushort)y2, (ushort)z2, dir2, type2);
            else
                HideQuad(x, y, z, dir, type);
        }

        public void RemoveBlock(ushort x, ushort y, ushort z)
        {
            if (x <= 0 || y <= 0 || z <= 0 || x >= MAPSIZE - 1 || y >= MAPSIZE - 1 || z >= MAPSIZE - 1)
                return;

            _RemoveBlock(x, y, z, BlockFaceDirection.XIncreasing, x + 1, y, z, BlockFaceDirection.XDecreasing);
            _RemoveBlock(x, y, z, BlockFaceDirection.XDecreasing, x - 1, y, z, BlockFaceDirection.XIncreasing);
            _RemoveBlock(x, y, z, BlockFaceDirection.YIncreasing, x, y + 1, z, BlockFaceDirection.YDecreasing);
            _RemoveBlock(x, y, z, BlockFaceDirection.YDecreasing, x, y - 1, z, BlockFaceDirection.YIncreasing);
            _RemoveBlock(x, y, z, BlockFaceDirection.ZIncreasing, x, y, z + 1, BlockFaceDirection.ZDecreasing);
            _RemoveBlock(x, y, z, BlockFaceDirection.ZDecreasing, x, y, z - 1, BlockFaceDirection.ZIncreasing);

            blockList[x, y, z] = BlockType.None;
            blockTexList[x, y, z] = BlockType.None;

            if (Light[x, y, z, (byte)BlockFaceDirection.YIncreasing] > 0.7)
            {
                if(x > 0)
                if (Light[x-1, y, z, (byte)BlockFaceDirection.YIncreasing] > 0.7)
                LightRay(x-1, y, z);

                if(x < MAPSIZE)
                if (Light[x+1, y, z, (byte)BlockFaceDirection.YIncreasing] > 0.7)
                LightRay(x+1, y, z);

                if (z > 0)
                if (Light[x, y, z-1, (byte)BlockFaceDirection.YIncreasing] > 0.7)
                LightRay(x, y, z-1);

                if (z < MAPSIZE)
                if (Light[x, y, z+1, (byte)BlockFaceDirection.YIncreasing] > 0.7)
                LightRay(x, y, z+1);

                LightRay(x, y, z);//y//cast from the top?
            }
            //else if (Light[x, y + 1, z, (byte)BlockFaceDirection.YDecreasing] < 0.8)
            //{
            //    ShadowRay(x, y, z);
            //    this.gameInstance.propertyBag.addChatMessage("shadow", ChatMessageType.SayAll, 10);
            //}

        }

        private uint EncodeBlockFace(ushort x, ushort y, ushort z, BlockFaceDirection faceDir)
        {
            //TODO: OPTIMIZE BY HARD CODING VALUES IN
            return (uint)(x + y * MAPSIZE + z * MAPSIZE * MAPSIZE + (byte)faceDir * MAPSIZE * MAPSIZE * MAPSIZE);
        }

        private void DecodeBlockFace(uint faceCode, ref ushort x, ref ushort y, ref ushort z, ref BlockFaceDirection faceDir)
        {
            x = (ushort)(faceCode % MAPSIZE);
            faceCode = (faceCode - x) / MAPSIZE;
            y = (ushort)(faceCode % MAPSIZE);
            faceCode = (faceCode - y) / MAPSIZE;
            z = (ushort)(faceCode % MAPSIZE);
            faceCode = (faceCode - z) / MAPSIZE;
            faceDir = (BlockFaceDirection)faceCode;
        }

        // Returns the region that a block at (x,y,z) should belong in.
        public uint GetRegion(ushort x, ushort y, ushort z)
        {
            return (uint)(x / REGIONSIZE + (y / REGIONSIZE) * REGIONRATIO + (z / REGIONSIZE) * REGIONRATIO * REGIONRATIO);
        }
        public uint GetRegion(int x, int y, int z)
        {
            return (uint)(x / REGIONSIZE + (y / REGIONSIZE) * REGIONRATIO + (z / REGIONSIZE) * REGIONRATIO * REGIONRATIO);
        }
        private Vector3 GetRegionCenter(uint regionNumber)
        {
            uint x, y, z;
            x = regionNumber % REGIONRATIO;
            regionNumber = (regionNumber - x) / REGIONRATIO;
            y = regionNumber % REGIONRATIO;
            regionNumber = (regionNumber - y) / REGIONRATIO;
            z = regionNumber;
            return new Vector3(x * REGIONSIZE + REGIONSIZE / 2, y * REGIONSIZE + REGIONSIZE / 2, z * REGIONSIZE + REGIONSIZE / 2);            
        }
        private Vector3 GetRegionCenter(int regionNumber)
        {
            int x, y, z;
            x = regionNumber % REGIONRATIO;
            regionNumber = (regionNumber - x) / REGIONRATIO;
            y = regionNumber % REGIONRATIO;
            regionNumber = (regionNumber - y) / REGIONRATIO;
            z = regionNumber;
            return new Vector3(x * REGIONSIZE + REGIONSIZE / 2, y * REGIONSIZE + REGIONSIZE / 2, z * REGIONSIZE + REGIONSIZE / 2);
        }

        private Vector3 GetRegionPosition(int regionNumber)
        {
            int x, y, z;
            x = regionNumber % REGIONRATIO;
            regionNumber = (regionNumber - x) / REGIONRATIO;
            y = regionNumber % REGIONRATIO;
            regionNumber = (regionNumber - y) / REGIONRATIO;
            z = regionNumber;
            return new Vector3(x * REGIONSIZE, y * REGIONSIZE, z * REGIONSIZE);
        }
        private void ShowQuad(ushort x, ushort y, ushort z, BlockFaceDirection faceDir, BlockType blockType)
        {
            BlockTexture blockTexture = blockTextureMap[(byte)blockType, (byte)faceDir];
            uint blockFace = EncodeBlockFace(x, y, z, faceDir);
            uint region = GetRegion(x, y, z);
            if (!faceMap[(byte)blockTexture, region].ContainsKey(blockFace))
                faceMap[(byte)blockTexture, region].Add(blockFace, true);
            vertexListDirty[(byte)blockTexture, region] = true;
        }

        private void HideQuad(ushort x, ushort y, ushort z, BlockFaceDirection faceDir, BlockType blockType)
        {
            BlockTexture blockTexture = blockTextureMap[(byte)blockType, (byte)faceDir];
            uint blockFace = EncodeBlockFace(x, y, z, faceDir);
            uint region = GetRegion(x, y, z);
            if (faceMap[(byte)blockTexture, region].ContainsKey(blockFace))
                faceMap[(byte)blockTexture, region].Remove(blockFace);
            vertexListDirty[(byte)blockTexture, region] = true;
        }
    }
}
