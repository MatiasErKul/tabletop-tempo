using System;
using TabletopTempo.Core.Input;

namespace TabletopTempo.Core.Rhythm
{
    /// <summary>Every move the game can ask for (section 5 of the design).</summary>
    public enum MoveId
    {
        None = 0,
        Clap,
        PoundBoth,
        TapL,
        TapR,
        TapBoth,
        BackhandL,
        BackhandR,
        Raise,
        CupTapTop,
        CupLift,
        CupGrabFlip,
        CupPalmTap,
        CupSlapDown,
        CupPass,
    }

    /// <summary>Highway lane: Left hand · Both/Cup · Right hand.</summary>
    public enum Lane
    {
        Left = 0,
        Center = 1,
        Right = 2,
    }

    public static class Moves
    {
        /// <summary>Chart ids are the upper-case snake names used in the design doc ("CLAP", "TAP_L"…).</summary>
        public static MoveId Parse(string id)
        {
            switch ((id ?? "").Trim().ToUpperInvariant())
            {
                case "CLAP": return MoveId.Clap;
                case "POUND_BOTH": return MoveId.PoundBoth;
                case "TAP_L": return MoveId.TapL;
                case "TAP_R": return MoveId.TapR;
                case "TAP_BOTH": return MoveId.TapBoth;
                case "BACKHAND_L": return MoveId.BackhandL;
                case "BACKHAND_R": return MoveId.BackhandR;
                case "RAISE": return MoveId.Raise;
                case "CUP_TAP_TOP": return MoveId.CupTapTop;
                case "CUP_LIFT": return MoveId.CupLift;
                case "CUP_GRAB_FLIP": return MoveId.CupGrabFlip;
                case "CUP_PALM_TAP": return MoveId.CupPalmTap;
                case "CUP_SLAP_DOWN": return MoveId.CupSlapDown;
                case "CUP_PASS": return MoveId.CupPass;
                default: throw new FormatException($"Unknown move id '{id}'");
            }
        }

        public static string ToId(MoveId m)
        {
            switch (m)
            {
                case MoveId.Clap: return "CLAP";
                case MoveId.PoundBoth: return "POUND_BOTH";
                case MoveId.TapL: return "TAP_L";
                case MoveId.TapR: return "TAP_R";
                case MoveId.TapBoth: return "TAP_BOTH";
                case MoveId.BackhandL: return "BACKHAND_L";
                case MoveId.BackhandR: return "BACKHAND_R";
                case MoveId.Raise: return "RAISE";
                case MoveId.CupTapTop: return "CUP_TAP_TOP";
                case MoveId.CupLift: return "CUP_LIFT";
                case MoveId.CupGrabFlip: return "CUP_GRAB_FLIP";
                case MoveId.CupPalmTap: return "CUP_PALM_TAP";
                case MoveId.CupSlapDown: return "CUP_SLAP_DOWN";
                case MoveId.CupPass: return "CUP_PASS";
                default: return "NONE";
            }
        }

        /// <summary>Lane for a move; left-handed mode mirrors L/R (applied by the caller).</summary>
        public static Lane LaneOf(MoveId m)
        {
            switch (m)
            {
                case MoveId.TapL:
                case MoveId.BackhandL:
                    return Lane.Left;
                case MoveId.TapR:
                case MoveId.BackhandR:
                    return Lane.Right;
                default:
                    return Lane.Center;
            }
        }

        /// <summary>Short on-screen coaching line per move (always text, no audio needed).</summary>
        public static string Coach(MoveId m)
        {
            switch (m)
            {
                case MoveId.Clap: return "Clap your hands!";
                case MoveId.PoundBoth: return "Pound the table with both fists!";
                case MoveId.TapL: return "Tap the table with your left hand";
                case MoveId.TapR: return "Tap the table with your right hand";
                case MoveId.TapBoth: return "Tap with both hands together";
                case MoveId.BackhandL: return "Left hand: tap with the back of your hand";
                case MoveId.BackhandR: return "Right hand: tap with the back of your hand";
                case MoveId.Raise: return "Raise both hands and hold";
                case MoveId.CupTapTop: return "Tap the top of the cup";
                case MoveId.CupLift: return "Lift the cup and set it down";
                case MoveId.CupGrabFlip: return "Grab the cup, thumb down";
                case MoveId.CupPalmTap: return "Tap the cup into your other palm";
                case MoveId.CupSlapDown: return "Slap the cup down on the table";
                case MoveId.CupPass: return "Pass the cup and set it down";
                default: return "";
            }
        }
    }

    /// <summary>A recognised move, on the input (DSP) clock.</summary>
    public readonly struct MoveEvent
    {
        public readonly MoveId Move;
        /// <summary>Which hand did it, if the move is one-handed.</summary>
        public readonly HandSide? Hand;
        public readonly double Time;
        public readonly float Confidence;

        public MoveEvent(MoveId move, double time, float confidence = 1f, HandSide? hand = null)
        {
            Move = move;
            Time = time;
            Confidence = confidence;
            Hand = hand;
        }

        public override string ToString() => $"{Moves.ToId(Move)}{(Hand.HasValue ? "/" + Hand.Value : "")} @{Time:0.000} ({Confidence:0.00})";
    }
}
