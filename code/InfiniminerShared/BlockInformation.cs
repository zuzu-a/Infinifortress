using System;
using System.Collections.Generic;

using System.Text;

namespace Infiniminer
{
    public enum ItemType : byte
    {
        None,
        Gold,
        Ore,
        Artifact,
        Diamond,
        Bomb,
        Rope,
        Static,
        Mushroom,
        Spikes,
        Target,
        DirtBomb,
        MAXIMUM
    }

    public enum BlockType : byte
    {
        None,
        Dirt,
        Mud,
        Grass,
        Sand,
        Ore,
        Gold,
        Diamond,
        Rock,
        Ladder,
        Explosive,
        Jump,
        Shock,
        ArtCaseR,
        ArtCaseB,
        BankRed,
        BankBlue,
        BaseRed,
        BaseBlue,
        ResearchR,
        ResearchB,
        InhibitorR,
        InhibitorB,
        BeaconRed,
        BeaconBlue,
        Road,
        SolidRed,
        SolidBlue,
        SolidRed2,
        SolidBlue2,
        Metal,
        DirtSign,
        Lava,
        Generator,
        Controller,
        Pump,
        Barrel,
        Pipe,
        TransRed,
        TransBlue,
        Water,
        Spring,
        MagmaVent,
        MagmaBurst,
        Fire,
        Vacuum,
        TrapB,
        TrapR,
        StealthBlockR,
        StealthBlockB,
        Magma,
        Lever,
        Plate,
        RadarRed,
        RadarBlue,
        Hinge,
        MedicalR,
        MedicalB,
        Refinery,
        Maintenance,
        Highlight,//purely for particle effect
        ConstructionR,//a temporary block for difficult-to-make blocks
        ConstructionB,
        GlassR,
        GlassB,
        ForceR,
        ForceB,
        MAXIMUM
    }

    public enum BlockTexture : byte
    {
        None,
        Dirt,
        Mud,
        Grass,
        GrassSide,
        Sand,
        Ore,
        Gold,
        Diamond,
        Rock,
        Jump,
        JumpTop,
        Ladder,
        LadderTop,
        Explosive,
        Spikes,
        HomeRed,
        ForgeSide,
        Forge,
        HomeBlue,
        BankTopRed,
        BankTopBlue,
        BankFrontRed,
        BankFrontBlue,
        BankLeftRed,
        BankLeftBlue,
        BankRightRed,
        BankRightBlue,
        BankBackRed,
        BankBackBlue,
        BaseTopRed,
        BaseTopBlue,
        BaseFrontRed,
        BaseFrontBlue,
        BaseLeftRed,
        BaseLeftBlue,
        BaseRightRed,
        BaseRightBlue,
        BaseBackRed,
        BaseBackBlue,
        TeleTop,
        TeleBottom,
        TeleSideA,
        TeleSideB,
        SolidRed,
        SolidBlue,
        SolidRed2,
        SolidBlue2,
        Metal,
        DirtSign,
        Lava,
        Generator,
        Controller,
        Pump,
        Barrel,
        BarrelTop,
        Pipe,
        Road,
        RoadTop,
        RoadBottom,
        BeaconRed,
        BeaconBlue,
        Spring,
        MagmaVent,
        MagmaBurst,
        Fire,
        Magma,
        TrapR,
        TrapB,
        TrapVis,
        Trap,
        StealthBlockR,
        StealthBlockB,
        Lever,
        Plate,
        RadarRed,
        RadarBlue,
        InhibitorR,
        InhibitorB,
        ResearchR,
        ResearchB,
        MedicalR,
        MedicalB,
        Refinery,
        RefinerySide,
        Maintenance,
        Hinge,
        ConstructionR,
        ConstructionB,
        GlassR,
        GlassB,
        ArtCaseR,
        ArtCaseB,
        ForceR,
        ForceB,
        TransRed,   // THESE MUST BE THE LAST TWO TEXTURES
        TransBlue,
        Water,
        MAXIMUM
    }

    public enum Research : byte
    {
        None,
        ImpHealth,
        Strength,
        Regen,
        Fortify,
        Destruction,
        MAXIMUM,//topics above this can be researched via station
        OreRefinery,//block boosted
        TMAXIMUM,//saving requirement
        Range,
        Blockcost,
        Acro,

        //MAXIMUM
    }

    public class ResearchInformation
    {
        static int[] researchCost = new int[10];
        
        static ResearchInformation()
        {
            for (int a = 0; a < 10; a++)
            {
                researchCost[a] = 50;
            }

            researchCost[(byte)Research.None] = 0;
            researchCost[(byte)Research.ImpHealth] = 30;
            researchCost[(byte)Research.Strength] = 50;
            researchCost[(byte)Research.Regen] = 50;
            researchCost[(byte)Research.Fortify] = 30;
            researchCost[(byte)Research.Destruction] = 100;
        }
        
        public static int GetCost(Research res)
        {
            return researchCost[(byte)res];
        }

        public static string GetName(Research res)
        {
            switch (res)
            {
                case Research.None:
                    return "Nothing";
                case Research.ImpHealth:
                    return "Improved health";
                case Research.Strength:
                    return "Strength";
                case Research.Regen:
                    return "Health regeneration";
                case Research.Fortify:
                    return "Fortification";
                case Research.Destruction:
                    return "Destruction";
                default:
                    return "Something!";
            }
        }

    }

    public class ItemInformation
    {
        static int[] itemCost = new int[40];

        static ItemInformation()
        {
            for (int a = 0; a < 40; a++)
            {
                itemCost[a] = 10;
            }

            itemCost[0] = 11;
            itemCost[1] = 12;
            itemCost[2] = 13;
            itemCost[9] = 200;
            itemCost[10] = 0;
            itemCost[11] = 30;
        }

        public static int GetCost(ItemType ite)
        {
            return itemCost[(byte)ite];
        }

        public static string GetName(ItemType res)
        {
            switch (res)
            {
                case ItemType.None:
                    return "Blank";
                case ItemType.Spikes:
                    return "Spikes";
                case ItemType.Target:
                    return "Target";
                case ItemType.DirtBomb:
                    return "Dirt bomb";
                default:
                    return "Something!";
            }
        }

    }

    public class ArtifactInformation
    {
      
        static ArtifactInformation()
        {
        }

        public static string GetName(int art)
        {
            switch (art)
            {
                case 0:
                    {
                        return "Powerless artifact";
                    }
                case 1:
                    {
                        return "Material artifact";
                    }
                case 2:
                    {
                        return "Vampiric artifact";
                    }
                case 3:
                    {
                        return "Regeneration artifact";
                    }
                case 4:
                    {
                        return "Aqua artifact";
                    }
                case 5:
                    {
                        return "Golden artifact";
                    }
                case 6:
                    {
                        return "Storm artifact";
                    }
                case 7:
                    {
                        return "Reflection artifact";
                    }
                case 8:
                    {
                        return "Medical artifact";
                    }
                case 9:
                    {
                        return "Stone artifact";
                    }
                case 10:
                    {
                        return "Tremor artifact";
                    }
                case 11:
                    {
                        return "Judgement artifact";
                    }
                case 12:
                    {
                        return "Bog artifact";
                    }
                case 13:
                    {
                        return "Explosive artifact";
                    }
                case 14:
                    {
                        return "Armor artifact";
                    }
                case 15:
                    {
                        return "Doom artifact";
                    }
                case 16:
                    {
                        return "Inferno artifact";
                    }
                case 18:
                    {
                        return "Wings artifact";
                    }
                default:
                    {
                        return "Unknown artifact";
                    }
            }
        }

    }

    public enum BlockFaceDirection : byte
    {
        XIncreasing,
        XDecreasing,
        YIncreasing,
        YDecreasing,
        ZIncreasing,
        ZDecreasing,
        MAXIMUM
    }

    public class BlockInformation
    {
        static int[] blockHP = new int[256];
        static int[] blockMaxHP = new int[256];

        static BlockInformation()
        {
            for (int a = 0; a < 255; a++)
            {
                blockHP[a] = 10;
                blockMaxHP[a] = 30;
            }

            blockMaxHP[(byte)BlockType.Water] = 0;
            blockMaxHP[(byte)BlockType.Lava] = 0;
            blockMaxHP[(byte)BlockType.Spring] = 0;
            blockMaxHP[(byte)BlockType.MagmaVent] = 0;
            blockMaxHP[(byte)BlockType.MagmaBurst] = 0;
            blockMaxHP[(byte)BlockType.Magma] = 2;
            blockMaxHP[(byte)BlockType.Fire] = 0;
            blockMaxHP[(byte)BlockType.BaseBlue] = 0;
            blockMaxHP[(byte)BlockType.BaseRed] = 0;
            blockMaxHP[(byte)BlockType.Vacuum] = 0;

            blockHP[(byte)BlockType.Grass] = 1;
            blockMaxHP[(byte)BlockType.Grass] = 1;
            blockHP[(byte)BlockType.Sand] = 1;
            blockMaxHP[(byte)BlockType.Sand] = 1;
            blockHP[(byte)BlockType.Dirt] = 1;
            blockMaxHP[(byte)BlockType.Dirt] = 1;
            blockHP[(byte)BlockType.Ore] = 1;
            blockMaxHP[(byte)BlockType.Ore] = 1;
            blockHP[(byte)BlockType.Rock] = 200;
            blockMaxHP[(byte)BlockType.Rock] = 2;
            blockMaxHP[(byte)BlockType.ForceR] = 0;
            blockMaxHP[(byte)BlockType.ForceB] = 0;
            blockMaxHP[(byte)BlockType.TransRed] = 0;
            blockMaxHP[(byte)BlockType.TransBlue] = 0;
            blockHP[(byte)BlockType.Explosive] = 1;
            blockMaxHP[(byte)BlockType.Explosive] = 1;

            blockHP[(byte)BlockType.ConstructionR] = 100;
            blockMaxHP[(byte)BlockType.ConstructionR] = 200;
            blockHP[(byte)BlockType.ConstructionB] = 100;
            blockMaxHP[(byte)BlockType.ConstructionB] = 200;

            blockHP[(byte)BlockType.Gold] = 40;
            blockMaxHP[(byte)BlockType.Gold] = 40;

            blockHP[(byte)BlockType.Diamond] = 200;
            blockMaxHP[(byte)BlockType.Diamond] = 200;

            blockHP[(byte)BlockType.Metal] = 50;
            blockMaxHP[(byte)BlockType.Metal] = 1;//non-repairable

            blockMaxHP[(byte)BlockType.MedicalB] = 50;
            blockMaxHP[(byte)BlockType.MedicalR] = 50;
            blockMaxHP[(byte)BlockType.Refinery] = 50;
            blockMaxHP[(byte)BlockType.RadarRed] = 50;
            blockMaxHP[(byte)BlockType.Maintenance] = 50;
            blockMaxHP[(byte)BlockType.RadarBlue] = 50;
            blockMaxHP[(byte)BlockType.Plate] = 50;
            blockMaxHP[(byte)BlockType.Lever] = 50;
            blockMaxHP[(byte)BlockType.Hinge] = 100;
            blockMaxHP[(byte)BlockType.Ladder] = 50;
            blockMaxHP[(byte)BlockType.BankBlue] = 50;
            blockMaxHP[(byte)BlockType.BankRed] = 50;
            blockMaxHP[(byte)BlockType.BeaconBlue] = 50;
            blockMaxHP[(byte)BlockType.BeaconRed] = 50;
            blockMaxHP[(byte)BlockType.InhibitorB] = 100;
            blockMaxHP[(byte)BlockType.InhibitorR] = 100;
            blockMaxHP[(byte)BlockType.ResearchR] = 50;
            blockMaxHP[(byte)BlockType.ResearchB] = 50;
            blockMaxHP[(byte)BlockType.Shock] = 50;

            blockHP[(byte)BlockType.Barrel] = 30;
            blockMaxHP[(byte)BlockType.Barrel] = 30;

            blockHP[(byte)BlockType.GlassR] = 10;
            blockMaxHP[(byte)BlockType.GlassR] = 50;

            blockHP[(byte)BlockType.GlassB] = 10;
            blockMaxHP[(byte)BlockType.GlassB] = 50;

            blockHP[(byte)BlockType.ArtCaseR] = 50;
            blockMaxHP[(byte)BlockType.ArtCaseR] = 200;

            blockHP[(byte)BlockType.ArtCaseB] = 50;
            blockMaxHP[(byte)BlockType.ArtCaseB] = 200;

            blockMaxHP[(byte)BlockType.SolidRed] = 50;
            blockMaxHP[(byte)BlockType.SolidBlue] = 50;

            blockHP[(byte)BlockType.SolidRed2] = 50;
            blockMaxHP[(byte)BlockType.SolidRed2] = 100;

            blockHP[(byte)BlockType.SolidBlue2] = 50;
            blockMaxHP[(byte)BlockType.SolidBlue2] = 100;
        }
        
        public static int GetHP(BlockType blockType)
        {
            return blockHP[(byte)blockType];
        }

        public static int GetMaxHP(BlockType blockType)
        {
            return blockMaxHP[(byte)blockType];
        }
        //    switch (blockType)
        //    {
        //        case BlockType.RadarRed:
        //        case BlockType.RadarBlue:
        //        case BlockType.BankRed:
        //        case BlockType.BankBlue:
        //        case BlockType.BeaconRed:
        //        case BlockType.BeaconBlue:
        //        case BlockType.Lever:
        //        case BlockType.Hinge:
        //        case BlockType.Water:
        //        case BlockType.Generator:
        //        case BlockType.Controller:
        //        case BlockType.Pump:
        //        case BlockType.Barrel:
        //        case BlockType.Lava:
        //        case BlockType.Dirt:
        //        case BlockType.Pipe:
        //        case BlockType.StealthBlockB:
        //        case BlockType.StealthBlockR:
        //        case BlockType.TrapB:
        //        case BlockType.TrapR:
        //        case BlockType.TransRed:
        //        case BlockType.TransBlue:
        //        case BlockType.Road:
        //        case BlockType.Jump:
        //        case BlockType.Ladder:
        //        case BlockType.Shock:
        //        case BlockType.Explosive:
        //            return 10;

        //        case BlockType.SolidRed:
        //        case BlockType.SolidBlue:
        //            return 10;

        //        case BlockType.SolidRed2:
        //        case BlockType.SolidBlue2:
        //            return 50;
        //    }

        //    return 10;
        //}
       
        public static uint GetCost(BlockType blockType)
        {
            switch (blockType)
            {
                case BlockType.RadarRed:
                case BlockType.RadarBlue:
                    return 260;
                case BlockType.InhibitorR:
                case BlockType.InhibitorB:
                    return 240;
                case BlockType.ArtCaseR:
                case BlockType.ArtCaseB:
                case BlockType.BankRed:
                case BlockType.BankBlue:
                case BlockType.ResearchR:
                case BlockType.ResearchB:
                case BlockType.Barrel:
                case BlockType.MedicalR:
                case BlockType.MedicalB:
                case BlockType.Maintenance:
                    return 200;
                case BlockType.BeaconRed:
                case BlockType.BeaconBlue:
                case BlockType.Jump:
                case BlockType.GlassR:
                case BlockType.GlassB:
                case BlockType.Refinery:
                case BlockType.Plate:
                    return 100;
                case BlockType.StealthBlockB:
                case BlockType.StealthBlockR:
                case BlockType.TrapB:
                case BlockType.TrapR:
                case BlockType.Shock:
                    return 50;
                case BlockType.Lever:
                case BlockType.SolidRed:
                case BlockType.SolidBlue:
                    return 10;
                case BlockType.Generator:
                case BlockType.Controller:
                case BlockType.Pump:
                case BlockType.Pipe:
                case BlockType.ConstructionR:
                case BlockType.ConstructionB:
                    return 10;
                case BlockType.Metal:
                case BlockType.TransRed:
                case BlockType.TransBlue:
                    return 10;

                case BlockType.Road:
                    return 10;
                case BlockType.Ladder:
                case BlockType.Hinge:
                    return 25;
                case BlockType.Explosive:
                    return 100;

                case BlockType.BaseBlue:
                case BlockType.BaseRed:
                    return 10000;

                case BlockType.Water:
                case BlockType.Ore:
                case BlockType.Lava:
                    return 1000;

                case BlockType.Dirt:
                    return 0;
            }

            return 0;
        }

        public static BlockTexture GetTexture(BlockType blockType, BlockFaceDirection faceDir)
        {
            return GetTexture(blockType, faceDir, BlockType.None);
        }

        public static BlockTexture GetTexture(BlockType blockType, BlockFaceDirection faceDir, BlockType blockAbove)
        {
            switch (blockType)
            {
                case BlockType.Generator:
                    return BlockTexture.Generator;
                case BlockType.Controller:
                    return BlockTexture.Controller;
                case BlockType.Pump:
                    return BlockTexture.Pump;
                case BlockType.Barrel:
                    switch (faceDir)
                    {
                        case BlockFaceDirection.XIncreasing: return BlockTexture.Barrel;
                        case BlockFaceDirection.XDecreasing: return BlockTexture.Barrel;
                        case BlockFaceDirection.ZIncreasing: return BlockTexture.Barrel;
                        case BlockFaceDirection.ZDecreasing: return BlockTexture.Barrel;
                        case BlockFaceDirection.YDecreasing: return BlockTexture.LadderTop;
                        default: return BlockTexture.BarrelTop;
                    }
                case BlockType.Hinge:
                    return BlockTexture.Hinge;
                case BlockType.Pipe:
                    return BlockTexture.Pipe;
                case BlockType.Metal:
                    return BlockTexture.Metal;
                case BlockType.Dirt:
                    return BlockTexture.Dirt;
                case BlockType.Mud:
                    return BlockTexture.Mud;
                case BlockType.Grass:
                    switch (faceDir)
                    {
                        case BlockFaceDirection.XIncreasing: return BlockTexture.GrassSide;
                        case BlockFaceDirection.XDecreasing: return BlockTexture.GrassSide;
                        case BlockFaceDirection.ZIncreasing: return BlockTexture.GrassSide;
                        case BlockFaceDirection.ZDecreasing: return BlockTexture.GrassSide;
                        case BlockFaceDirection.YDecreasing: return BlockTexture.Dirt;
                        default: return BlockTexture.Grass;
                    }
                case BlockType.Sand:
                    return BlockTexture.Sand;
                case BlockType.Lava:
                    return BlockTexture.Lava;
                case BlockType.Water:
                    return BlockTexture.Water;
                case BlockType.Rock:
                    return BlockTexture.Rock;
                case BlockType.Spring:
                    return BlockTexture.Spring;
                case BlockType.MagmaVent:
                    return BlockTexture.MagmaVent;
                case BlockType.MagmaBurst:
                    return BlockTexture.MagmaBurst;
                case BlockType.Fire:
                    return BlockTexture.Fire;
                case BlockType.Ore:
                    return BlockTexture.Ore;
                case BlockType.Gold:
                    return BlockTexture.Gold;
                case BlockType.Diamond:
                    return BlockTexture.Diamond;
                case BlockType.Lever:
                    return BlockTexture.Lever;
                case BlockType.Plate:
                    return BlockTexture.Lever;
                case BlockType.DirtSign:
                    return BlockTexture.DirtSign;
                case BlockType.Magma:
                    return BlockTexture.Magma;
                case BlockType.StealthBlockR:
                    return BlockTexture.StealthBlockR;
                case BlockType.StealthBlockB:
                    return BlockTexture.StealthBlockB;
                case BlockType.TrapB:
                    return BlockTexture.TrapB;
                case BlockType.TrapR:
                   return BlockTexture.TrapR;
                case BlockType.ConstructionR:
                   return BlockTexture.ConstructionR;
                case BlockType.ConstructionB:
                   return BlockTexture.ConstructionB;
                case BlockType.GlassR:
                   return BlockTexture.GlassR;
                case BlockType.GlassB:
                   return BlockTexture.GlassB;
                case BlockType.ForceR:
                   return BlockTexture.ForceR;
                case BlockType.ForceB:
                   return BlockTexture.ForceB;
                case BlockType.ArtCaseR:
                   switch (faceDir)
                   {
                       case BlockFaceDirection.XIncreasing: return BlockTexture.ArtCaseR;
                       case BlockFaceDirection.XDecreasing: return BlockTexture.BankBackRed;
                       case BlockFaceDirection.ZIncreasing: return BlockTexture.BankLeftRed;
                       case BlockFaceDirection.ZDecreasing: return BlockTexture.BankRightRed;
                       default: return BlockTexture.BankTopRed;
                   }
                case BlockType.ArtCaseB:
                   switch (faceDir)
                   {
                       case BlockFaceDirection.XIncreasing: return BlockTexture.ArtCaseB;
                       case BlockFaceDirection.XDecreasing: return BlockTexture.BankBackBlue;
                       case BlockFaceDirection.ZIncreasing: return BlockTexture.BankLeftBlue;
                       case BlockFaceDirection.ZDecreasing: return BlockTexture.BankRightBlue;
                       default: return BlockTexture.BankTopBlue;
                   }
                case BlockType.BankRed:
                    switch (faceDir)
                    {
                        case BlockFaceDirection.XIncreasing: return BlockTexture.BankFrontRed;
                        case BlockFaceDirection.XDecreasing: return BlockTexture.BankBackRed;
                        case BlockFaceDirection.ZIncreasing: return BlockTexture.BankLeftRed;
                        case BlockFaceDirection.ZDecreasing: return BlockTexture.BankRightRed;
                        default: return BlockTexture.BankTopRed;
                    }

                case BlockType.BankBlue:
                    switch (faceDir)
                    {
                        case BlockFaceDirection.XIncreasing: return BlockTexture.BankFrontBlue;
                        case BlockFaceDirection.XDecreasing: return BlockTexture.BankBackBlue;
                        case BlockFaceDirection.ZIncreasing: return BlockTexture.BankLeftBlue;
                        case BlockFaceDirection.ZDecreasing: return BlockTexture.BankRightBlue;
                        default: return BlockTexture.BankTopBlue;
                    }

                case BlockType.BaseRed:
                    switch (faceDir)
                    {
                        case BlockFaceDirection.XIncreasing: return BlockTexture.Forge;
                        case BlockFaceDirection.XDecreasing: return BlockTexture.ForgeSide;
                        case BlockFaceDirection.ZIncreasing: return BlockTexture.ForgeSide;
                        case BlockFaceDirection.ZDecreasing: return BlockTexture.ForgeSide;
                        default: return BlockTexture.BankTopRed;
                    }

                case BlockType.BaseBlue:
                    switch (faceDir)
                    {
                        case BlockFaceDirection.XIncreasing: return BlockTexture.Forge;
                        case BlockFaceDirection.XDecreasing: return BlockTexture.ForgeSide;
                        case BlockFaceDirection.ZIncreasing: return BlockTexture.ForgeSide;
                        case BlockFaceDirection.ZDecreasing: return BlockTexture.ForgeSide;
                        default: return BlockTexture.BankTopBlue;
                    }

                case BlockType.RadarRed:
                case BlockType.RadarBlue:
                    switch (faceDir)
                    {
                        case BlockFaceDirection.YDecreasing:
                            return BlockTexture.LadderTop;
                        case BlockFaceDirection.YIncreasing:
                            return blockType == BlockType.RadarRed ? BlockTexture.RadarRed : BlockTexture.RadarBlue;
                        case BlockFaceDirection.XDecreasing:
                        case BlockFaceDirection.XIncreasing:
                            return BlockTexture.TeleSideA;
                        case BlockFaceDirection.ZDecreasing:
                        case BlockFaceDirection.ZIncreasing:
                            return BlockTexture.TeleSideB;
                    }
                    break;

                case BlockType.Refinery:
                    switch (faceDir)
                    {
                        case BlockFaceDirection.YDecreasing:
                            return BlockTexture.LadderTop;
                        case BlockFaceDirection.YIncreasing:
                            return BlockTexture.Refinery;
                        case BlockFaceDirection.XDecreasing:
                        case BlockFaceDirection.XIncreasing:
                            return BlockTexture.RefinerySide;
                        case BlockFaceDirection.ZDecreasing:
                        case BlockFaceDirection.ZIncreasing:
                            return BlockTexture.RefinerySide;
                    }
                    break;

                case BlockType.InhibitorR:
                    return BlockTexture.InhibitorR;
                case BlockType.InhibitorB:
                    return BlockTexture.InhibitorB;
                case BlockType.Maintenance:
                    switch (faceDir)
                    {
                        case BlockFaceDirection.YDecreasing:
                            return BlockTexture.TeleBottom;
                        case BlockFaceDirection.YIncreasing:
                            return BlockTexture.Maintenance;
                        case BlockFaceDirection.XDecreasing:
                        case BlockFaceDirection.XIncreasing:
                            return BlockTexture.Jump;
                        case BlockFaceDirection.ZDecreasing:
                        case BlockFaceDirection.ZIncreasing:
                            return BlockTexture.Jump;
                    }
                    break;

                case BlockType.ResearchR:
                    return BlockTexture.ResearchR;
                case BlockType.ResearchB:
                    return BlockTexture.ResearchB;

                case BlockType.MedicalR:
                case BlockType.MedicalB:
                    switch (faceDir)
                    {
                        case BlockFaceDirection.YDecreasing:
                            return blockType == BlockType.MedicalR ? BlockTexture.MedicalR : BlockTexture.MedicalB;
                        case BlockFaceDirection.YIncreasing:
                            return blockType == BlockType.MedicalR ? BlockTexture.MedicalR : BlockTexture.MedicalB;
                        case BlockFaceDirection.XDecreasing:
                        case BlockFaceDirection.XIncreasing:
                            return blockType == BlockType.MedicalR ? BlockTexture.MedicalR : BlockTexture.MedicalB;
                        case BlockFaceDirection.ZDecreasing:
                        case BlockFaceDirection.ZIncreasing:
                            return blockType == BlockType.MedicalR ? BlockTexture.MedicalR : BlockTexture.MedicalB;
                    }
                    break;

                case BlockType.BeaconRed:
                case BlockType.BeaconBlue:
                    switch (faceDir)
                    {
                        case BlockFaceDirection.YDecreasing:
                            return BlockTexture.LadderTop;
                        case BlockFaceDirection.YIncreasing:
                            return blockType == BlockType.BeaconRed ? BlockTexture.BeaconRed : BlockTexture.BeaconBlue;
                        case BlockFaceDirection.XDecreasing:
                        case BlockFaceDirection.XIncreasing:
                            return BlockTexture.TeleSideA;
                        case BlockFaceDirection.ZDecreasing:
                        case BlockFaceDirection.ZIncreasing:
                            return BlockTexture.TeleSideB;
                    }
                    break;

                case BlockType.Road:
                    if (faceDir == BlockFaceDirection.YIncreasing)
                        return BlockTexture.RoadTop;
                    else if (faceDir == BlockFaceDirection.YDecreasing||blockAbove!=BlockType.None) //Looks better but won't work with current graphics setup...
                        return BlockTexture.RoadBottom;
                    return BlockTexture.Road;

                case BlockType.Shock:
                    switch (faceDir)
                    {
                        case BlockFaceDirection.YDecreasing:
                            return BlockTexture.Spikes;
                        case BlockFaceDirection.YIncreasing:
                            return BlockTexture.TeleBottom;
                        case BlockFaceDirection.XDecreasing:
                        case BlockFaceDirection.XIncreasing:
                            return BlockTexture.TeleSideA;
                        case BlockFaceDirection.ZDecreasing:
                        case BlockFaceDirection.ZIncreasing:
                            return BlockTexture.TeleSideB;
                    }
                    break;

                case BlockType.Jump:
                    switch (faceDir)
                    {
                        case BlockFaceDirection.YDecreasing:
                            return BlockTexture.TeleBottom;
                        case BlockFaceDirection.YIncreasing:
                            return BlockTexture.JumpTop;
                        case BlockFaceDirection.XDecreasing:
                        case BlockFaceDirection.XIncreasing:
                            return BlockTexture.Jump;
                        case BlockFaceDirection.ZDecreasing:
                        case BlockFaceDirection.ZIncreasing:
                            return BlockTexture.Jump;
                    }
                    break;
                case BlockType.SolidRed:
                    return BlockTexture.SolidRed;
                case BlockType.SolidBlue:
                    return BlockTexture.SolidBlue;
                case BlockType.SolidRed2:
                    return BlockTexture.SolidRed2;
                case BlockType.SolidBlue2:
                    return BlockTexture.SolidBlue2;
                case BlockType.TransRed:
                    return BlockTexture.TransRed;
                case BlockType.TransBlue:
                    return BlockTexture.TransBlue;

                case BlockType.Ladder:
                    if (faceDir == BlockFaceDirection.YDecreasing || faceDir == BlockFaceDirection.YIncreasing)
                        return BlockTexture.LadderTop;
                    else
                        return BlockTexture.Ladder;

                case BlockType.Explosive:
                    return BlockTexture.Explosive;
            }

            return BlockTexture.None;
        }
    }
}
