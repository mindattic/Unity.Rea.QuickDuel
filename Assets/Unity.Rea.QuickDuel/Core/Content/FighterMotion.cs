using System;

namespace Unity.Rea.QuickDuel.Core
{
    // What a fighter is doing (UQD-T35), as obj_player's `state` has it.
    public enum FighterPose
    {
        Idle,
        Running,
        Attacking,
        Defeated
    }

    // Which of a fighter's sprites is on screen: the single idle and run frames, or a frame of the attack or defeat strip.
    public enum FighterSprite
    {
        Idle,
        Run,
        Attack,
        Defeat
    }

    // One fighter's motion through a round (UQD-T35), as obj_player, the set_state_* scripts and obj_gameclock move it.
    // Engine-free; DuelView turns X and the sprite into its stage. Positions are in the original's 640-pixel-wide room,
    // and every figure is the original's own, read from BushidoCoda.gmx, not a tuning choice:
    //
    //   set_state_idle      x = left/right_position_idle (room_width / 4, room_width - room_width / 4), idle sprite
    //   set_state_running   x = left/right_position_run (room_width / 2 -/+ room_width / 10), run sprite; called for
    //                       both on the cue (gameclock_action's first pass)
    //   gameclock_action    player[left].x += 0.01, player[right].x -= 0.01 every step, at room_speed = actionFPS 1000
    //   determine_winner    set_state_attacking(winner), set_state_defeat(loser); nothing on a draw
    //   set_state_attacking x = room_width / 2, attack strip; obj_player then steps
    //                       x += 1 + 0.1 * (right_position_idle - x) while short of it (facing right)
    //   set_state_defeat    x = room_width / 2, defeat strip
    //   obj_player          image_speed = 0.1 at room_speed normalFPS 60; Animation End holds the strips on frame 3;
    //                       a defeated fighter with HP left is held on frame 0 every step (if(HP > 0) image_index = 0),
    //                       so only the killing blow plays the whole defeat strip (UQD-T64)
    //   obj_gameclock       alarm[0] = 3 * room_speed after the strike: the winner's sprite goes back to idle
    //
    // Everything is timed by the clock, never by the rendered frame (UQD-LAW-2): the creep runs at the original's
    // 10 pixels a second, and the dash and the strips count the original's 60 steps a second from when they began.
    public sealed class FighterMotion
    {
        public const double RoomWidth = FighterLineup.OriginalWidth;

        public const double LeftRunX = 0.4;
        public const double RightRunX = 0.6;

        // gameclock_action's creep, 0.01 pixel a step at actionFPS (1000 steps a second): 10 pixels a second.
        public const double CreepPerTick = 0.01;
        public const double ActionTicksPerSecond = 1000;
        public const double CreepPerSecond = CreepPerTick * ActionTicksPerSecond;

        // The aftermath runs at normalFPS again.
        public const double TicksPerSecond = FighterLineup.TicksPerSecond;

        // obj_player's dash: each step covers 1 + 0.1 of the gap still left.
        public const double DashStep = 1;
        public const double DashEase = 0.1;

        // obj_player's image_speed: a tenth of a frame a step, so each frame of a four-frame strip shows for 10 steps.
        public const double AnimationSpeed = 0.1;
        public const int StripFrames = 4;

        // 60 fps steps a strip takes to reach its Animation End: 4 frames at a tenth of a frame a step.
        public const int StripTicks = 40;

        // obj_gameclock's alarm[0]: three seconds after the strike the winner returns to its idle sprite.
        public const double WinnerIdleAfterSeconds = 3;

        public FighterMotion(Side side)
        {
            Side = side;
            Idle();
        }

        public Side Side { get; }
        public FighterPose Pose { get; private set; }

        // Defeated by the killing blow: the round that decides the match, standing in for the original's HP reaching 0
        // (best of 3 replaces the HP bars). Only then does the defeat strip play through; otherwise it holds frame 0.
        public bool KillingBlow { get; private set; }

        // Where the fighter's centre stands, in the original room's pixels (0 to 640).
        public double X { get; private set; }

        // How long it has held its pose.
        public double PoseSeconds { get; private set; }

        // The fraction of the room width X stands at.
        public double XFraction => X / RoomWidth;

        // The run mark `side` takes on the cue: 40% and 60% of the width.
        public static double RunX(Side side) => side == Side.Left ? LeftRunX : RightRunX;

        // set_state_idle: back on its idle mark in its idle sprite.
        public void Idle() => Enter(FighterPose.Idle, FighterLineup.X(Side) * RoomWidth);

        // set_state_running: onto its run mark in its run sprite.
        public void Run() => Enter(FighterPose.Running, RunX(Side) * RoomWidth);

        // set_state_attacking: to the middle, then dashing on through to where the loser stood.
        public void Attack() => Enter(FighterPose.Attacking, RoomWidth / 2);

        // set_state_defeat: to the middle in the defeat strip, played through only for the killing blow (the round that
        // decides the match) and otherwise held on its first frame, as obj_player held a loser with HP left.
        public void Defeat(bool killingBlow)
        {
            Enter(FighterPose.Defeated, RoomWidth / 2);
            KillingBlow = killingBlow;
        }

        // While the cue is up (gameclock_action) a running fighter creeps toward the middle, 0.01 pixel a step.
        public void Creep(double seconds)
        {
            if (Pose != FighterPose.Running || !(seconds > 0)) return;
            X += FighterLineup.Facing(Side) * CreepPerSecond * seconds;
        }

        // Moves the pose's clock on; the attacking fighter's dash follows it.
        public void Advance(double seconds)
        {
            if (!(seconds > 0)) return;
            PoseSeconds += seconds;
            if (Pose == FighterPose.Attacking)
                X = DashX(Side, PoseSeconds);
        }

        // The sprite on screen: the winner keeps its attack strip until alarm[0] returns it to idle.
        public FighterSprite Sprite
        {
            get
            {
                switch (Pose)
                {
                    case FighterPose.Running: return FighterSprite.Run;
                    case FighterPose.Attacking:
                        return PoseSeconds >= WinnerIdleAfterSeconds ? FighterSprite.Idle : FighterSprite.Attack;
                    case FighterPose.Defeated: return FighterSprite.Defeat;
                    default: return FighterSprite.Idle;
                }
            }
        }

        // The strip frame on screen (0 for the single-frame idle and run sprites).
        public int Frame
        {
            get
            {
                switch (Sprite)
                {
                    case FighterSprite.Attack: return StripFrame(PoseSeconds);
                    case FighterSprite.Defeat: return KillingBlow ? StripFrame(PoseSeconds) : 0;
                    default: return 0;
                }
            }
        }

        // A strip's frame `seconds` after it began: a new frame every 10 steps, held on the last.
        public static int StripFrame(double seconds) => Math.Min(StripFrames - 1, (int)(Ticks(seconds) * AnimationSpeed));

        // Where `side`'s attacking fighter stands `seconds` into its dash from the middle: stepping 1 + 0.1 of the gap
        // each 60 fps step until it reaches the loser's idle mark (the left winner may end a fraction past it, as the
        // original's last step does). The right winner mirrors the left; see the BIBLE's fixed defect.
        public static double DashX(Side side, double seconds)
        {
            var facing = FighterLineup.Facing(side);
            var target = FighterLineup.X(side == Side.Left ? Side.Right : Side.Left) * RoomWidth;
            var x = RoomWidth / 2;
            for (var tick = Ticks(seconds); tick > 0 && facing * (target - x) > 0; tick--)
                x += facing * (DashStep + DashEase * facing * (target - x));
            return x;
        }

        private static int Ticks(double seconds) =>
            double.IsNaN(seconds) || seconds <= 0 ? 0 : (int)Math.Min(int.MaxValue, Math.Floor(seconds * TicksPerSecond + 1e-9));

        private void Enter(FighterPose pose, double x)
        {
            Pose = pose;
            X = x;
            PoseSeconds = 0;
            KillingBlow = false;
        }
    }
}
