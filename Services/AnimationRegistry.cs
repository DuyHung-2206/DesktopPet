using System;
using System.Collections.Generic;
using DesktopPet.Models;

namespace DesktopPet.Services
{
    public enum AnimationLoopMode
    {
        Loop,       // Loops indefinitely while state is active (Idle, Walk, Run, Sleep, Sit, Dirty, Sick, Sad)
        OneShot,    // Runs for a set number of cycles, then triggers completion event (Eat, Drink, Bath, WakeUp, Hurt, Play, Happy, Dance, Jump, Angry)
        Physics     // Driven by physical world (Fall continues until pet.Y >= groundY)
    }

    public enum StateDurationOwner
    {
        AI,         // AI timer decides when state transitions (Walk, Run, Sit, Idle, Dirty, Sick, Sad)
        Animation,  // Animation cycle playback completion decides transition (Eat, Drink, Bath, WakeUp, Hurt, Play, Happy, Dance, Jump, Angry)
        Physics,    // Physical gravity/landing simulation decides transition (Fall)
        User        // User interaction or external trigger (Sleep)
    }

    public record AnimationDefinition(
        PetState State,
        string SpriteFileName,
        int DefaultFrameCount,
        int FrameDurationMs,
        AnimationLoopMode LoopMode,
        StateDurationOwner DurationOwner,
        int RepeatCycles = 1,
        PetState? CompletionState = PetState.Idle
    )
    {
        public double TotalDurationSeconds => (DefaultFrameCount * FrameDurationMs * RepeatCycles) / 1000.0;
    }

    public static class AnimationRegistry
    {
        private static readonly Dictionary<PetState, AnimationDefinition> Definitions = new()
        {
            [PetState.Idle] = new(PetState.Idle, "Idle.png", 4, 250, AnimationLoopMode.Loop, StateDurationOwner.AI),
            [PetState.Walk] = new(PetState.Walk, "Walk.png", 6, 125, AnimationLoopMode.Loop, StateDurationOwner.AI),
            [PetState.Run] = new(PetState.Run, "Run.png", 6, 95, AnimationLoopMode.Loop, StateDurationOwner.AI),
            [PetState.Sleep] = new(PetState.Sleep, "Sleep.png", 5, 240, AnimationLoopMode.Loop, StateDurationOwner.User, CompletionState: PetState.WakeUp),
            [PetState.Sit] = new(PetState.Sit, "Sit.png", 4, 300, AnimationLoopMode.Loop, StateDurationOwner.AI),
            [PetState.Dirty] = new(PetState.Dirty, "Dirty.png", 4, 250, AnimationLoopMode.Loop, StateDurationOwner.AI),
            [PetState.Sick] = new(PetState.Sick, "Sick.png", 4, 350, AnimationLoopMode.Loop, StateDurationOwner.AI),
            [PetState.Sad] = new(PetState.Sad, "Sad.png", 4, 300, AnimationLoopMode.Loop, StateDurationOwner.AI),

            [PetState.Eat] = new(PetState.Eat, "Eat.png", 5, 1000, AnimationLoopMode.OneShot, StateDurationOwner.Animation, 1, PetState.Idle),
            [PetState.Drink] = new(PetState.Drink, "Drink.png", 5, 300, AnimationLoopMode.OneShot, StateDurationOwner.Animation, 2, PetState.Idle),
            [PetState.Bath] = new(PetState.Bath, "Bath.png", 6, 130, AnimationLoopMode.OneShot, StateDurationOwner.Animation, 1, PetState.Idle),
            [PetState.WakeUp] = new(PetState.WakeUp, "WakeUp.png", 5, 200, AnimationLoopMode.OneShot, StateDurationOwner.Animation, 1, PetState.Idle),
            [PetState.Hurt] = new(PetState.Hurt, "Hurt.png", 5, 150, AnimationLoopMode.OneShot, StateDurationOwner.Animation, 1, PetState.Idle),
            [PetState.Play] = new(PetState.Play, "Play.png", 6, 120, AnimationLoopMode.OneShot, StateDurationOwner.Animation, 4, PetState.Idle),
            [PetState.Happy] = new(PetState.Happy, "Happy.png", 6, 120, AnimationLoopMode.OneShot, StateDurationOwner.Animation, 4, PetState.Idle),
            [PetState.Dance] = new(PetState.Dance, "Dance.png", 6, 120, AnimationLoopMode.OneShot, StateDurationOwner.Animation, 4, PetState.Idle),
            [PetState.Jump] = new(PetState.Jump, "Jump.png", 5, 120, AnimationLoopMode.OneShot, StateDurationOwner.Animation, 1, PetState.Idle),
            [PetState.Angry] = new(PetState.Angry, "Angry.png", 4, 150, AnimationLoopMode.OneShot, StateDurationOwner.Animation, 3, PetState.Idle),

            [PetState.Fall] = new(PetState.Fall, "Fall.png", 4, 100, AnimationLoopMode.Physics, StateDurationOwner.Physics, CompletionState: PetState.Idle)
        };

        public static AnimationDefinition GetDefinition(PetState state)
        {
            if (Definitions.TryGetValue(state, out var def))
            {
                return def;
            }
            return Definitions[PetState.Idle];
        }

        public static int GetIntervalMs(PetState state)
        {
            return GetDefinition(state).FrameDurationMs;
        }

        public static double GetAuthoritativeDurationSeconds(PetState state, int actualFrameCount = -1)
        {
            var def = GetDefinition(state);
            int frames = actualFrameCount > 0 ? actualFrameCount : def.DefaultFrameCount;
            return (frames * def.FrameDurationMs * def.RepeatCycles) / 1000.0;
        }

        public static bool IsOneShot(PetState state)
        {
            return GetDefinition(state).LoopMode == AnimationLoopMode.OneShot;
        }

        public static IReadOnlyDictionary<PetState, AnimationDefinition> AllDefinitions => Definitions;
    }
}
