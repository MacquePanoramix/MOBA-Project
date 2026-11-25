using System;
using System.Collections.Generic;
using UnityEngine;

namespace Moba.LaneSlice
{
    public enum ControlContext : byte
    {
        Global = 0,
        ConventionalMoba = 1,
        RtsMultiUnit = 2,
        FlightSteering = 3,
        TwinStick = 4,
    }

    public enum RtsControlMode : byte
    {
        Unavailable = 0,
        Optional = 1,
        Required = 2,
    }

    public enum ControlCommand
    {
        // Global UI & Camera
        ToggleShop = 0,
        ToggleHeroSheet = 1,
        ToggleSettings = 2,
        ToggleHelp = 3,
        ToggleCameraLock = 4,
        CenterCamera = 5,

        // Conventional MOBA Profile
        Move = 10,
        AttackOrder = 11,
        AttackMove = 12,
        Stop = 13,
        CastQ = 14,
        CastW = 15,
        CastE = 16,
        CastR = 17,

        // RTS Multi-Unit Selection & Control Groups
        RtsSelectHero = 30,
        RtsSelectAllUnits = 31,
        RtsCycleUnit = 32,
        RtsGroup1 = 33,
        RtsGroup2 = 34,
        RtsGroup3 = 35,
        RtsGroup4 = 36,

        // Flight Steering Profile (future hero architectural preview: Airplane Fighter)
        FlightCastQ = 50,
        FlightCastW = 51,
        FlightCastE = 52,
        FlightCastR = 53,

        // Twin-Stick Roguelike Profile (future hero architectural preview: Twin-Stick Shooter)
        TwinStickMoveUp = 70,
        TwinStickMoveDown = 71,
        TwinStickMoveLeft = 72,
        TwinStickMoveRight = 73,
        TwinStickShoot = 74,
        TwinStickAbility1 = 75,
        TwinStickAbility2 = 76,
        TwinStickAbility3 = 77,
        TwinStickAbility4 = 78,
    }

    public readonly struct ControlCommandMeta
    {
        public readonly ControlCommand Command;
        public readonly ControlContext Context;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly KeyCode DefaultKey;
        public readonly ControlCommand? PairedCommand;

        public ControlCommandMeta(
            ControlCommand command,
            ControlContext context,
            string displayName,
            string description,
            KeyCode defaultKey,
            ControlCommand? pairedCommand = null)
        {
            Command = command;
            Context = context;
            DisplayName = displayName;
            Description = description;
            DefaultKey = defaultKey;
            PairedCommand = pairedCommand;
        }
    }

    /// <summary>
    /// Central registry of input-agnostic gameplay commands, default keys, human labels,
    /// and context-overlap rules so different hero control contexts (e.g. Conventional Stop vs.
    /// Twin-Stick Backward) can cleanly share physical keys without false conflicts.
    /// </summary>
    public static class ControlCommandCatalog
    {
        public static readonly ControlCommandMeta[] All = new[]
        {
            // Global UI & Camera
            new ControlCommandMeta(ControlCommand.ToggleShop, ControlContext.Global,
                "Fountain Shop", "Open/close the tiered Fountain Shop", KeyCode.B),
            new ControlCommandMeta(ControlCommand.ToggleHeroSheet, ControlContext.Global,
                "Hero Numbers & Scaling", "Open/close Hero Scaling cards and formulas", KeyCode.C),
            new ControlCommandMeta(ControlCommand.ToggleSettings, ControlContext.Global,
                "Personal Settings", "Open/close Personal Settings & Keybindings menu", KeyCode.F10),
            new ControlCommandMeta(ControlCommand.ToggleHelp, ControlContext.Global,
                "Quick Help Overlay", "Toggle controls reference overlay", KeyCode.F1),
            new ControlCommandMeta(ControlCommand.ToggleCameraLock, ControlContext.Global,
                "Camera Lock Toggle", "Switch between Locked and Free Camera", KeyCode.Y),
            new ControlCommandMeta(ControlCommand.CenterCamera, ControlContext.Global,
                "Center Camera", "Recenter camera on your hero", KeyCode.Space),

            // Conventional MOBA
            new ControlCommandMeta(ControlCommand.Move, ControlContext.ConventionalMoba,
                "Move", "Move selected unit(s) toward cursor", KeyCode.Mouse1, pairedCommand: ControlCommand.AttackOrder),
            new ControlCommandMeta(ControlCommand.AttackOrder, ControlContext.ConventionalMoba,
                "Basic Attack Target", "Attack hovered hostile unit/tower (when hero supports basics)", KeyCode.Mouse1, pairedCommand: ControlCommand.Move),
            new ControlCommandMeta(ControlCommand.AttackMove, ControlContext.ConventionalMoba,
                "Attack-Move", "Quick-cast move while engaging nearest enemy along path", KeyCode.A),
            new ControlCommandMeta(ControlCommand.Stop, ControlContext.ConventionalMoba,
                "Stop", "Cancel movement and cancelable attack wind-ups (respects committed actions)", KeyCode.S),
            new ControlCommandMeta(ControlCommand.CastQ, ControlContext.ConventionalMoba,
                "Ability Q", "Cast Q (or Ctrl+Q to level up)", KeyCode.Q),
            new ControlCommandMeta(ControlCommand.CastW, ControlContext.ConventionalMoba,
                "Ability W", "Cast W (or Ctrl+W to level up)", KeyCode.W),
            new ControlCommandMeta(ControlCommand.CastE, ControlContext.ConventionalMoba,
                "Ability E", "Cast E (or Ctrl+E to level up)", KeyCode.E),
            new ControlCommandMeta(ControlCommand.CastR, ControlContext.ConventionalMoba,
                "Ultimate R", "Cast R (or Ctrl+R to level up at Lvl 4+)", KeyCode.R),

            // RTS Multi-Unit
            new ControlCommandMeta(ControlCommand.RtsSelectHero, ControlContext.RtsMultiUnit,
                "Select Hero Only", "Select only your main hero", KeyCode.F2),
            new ControlCommandMeta(ControlCommand.RtsSelectAllUnits, ControlContext.RtsMultiUnit,
                "Select All Units", "Select all controllable friendly units you own", KeyCode.F3),
            new ControlCommandMeta(ControlCommand.RtsCycleUnit, ControlContext.RtsMultiUnit,
                "Cycle Controllable Unit", "Cycle selection through controllable friendly units", KeyCode.Tab),
            new ControlCommandMeta(ControlCommand.RtsGroup1, ControlContext.RtsMultiUnit,
                "Control Group 1", "Recall group 1 (Ctrl+key assigns; double-tap centers)", KeyCode.Alpha1),
            new ControlCommandMeta(ControlCommand.RtsGroup2, ControlContext.RtsMultiUnit,
                "Control Group 2", "Recall group 2 (Ctrl+key assigns; double-tap centers)", KeyCode.Alpha2),
            new ControlCommandMeta(ControlCommand.RtsGroup3, ControlContext.RtsMultiUnit,
                "Control Group 3", "Recall group 3 (Ctrl+key assigns; double-tap centers)", KeyCode.Alpha3),
            new ControlCommandMeta(ControlCommand.RtsGroup4, ControlContext.RtsMultiUnit,
                "Control Group 4", "Recall group 4 (Ctrl+key assigns; double-tap centers)", KeyCode.Alpha4),

            // Flight Steering (future archetype)
            new ControlCommandMeta(ControlCommand.FlightCastQ, ControlContext.FlightSteering,
                "Flight Q", "Trigger flight-profile Q ability", KeyCode.Q),
            new ControlCommandMeta(ControlCommand.FlightCastW, ControlContext.FlightSteering,
                "Flight W", "Trigger flight-profile W ability", KeyCode.W),
            new ControlCommandMeta(ControlCommand.FlightCastE, ControlContext.FlightSteering,
                "Flight E", "Trigger flight-profile E ability", KeyCode.E),
            new ControlCommandMeta(ControlCommand.FlightCastR, ControlContext.FlightSteering,
                "Flight R", "Trigger flight-profile R ultimate", KeyCode.R),

            // Twin-Stick Roguelike (future archetype)
            new ControlCommandMeta(ControlCommand.TwinStickMoveUp, ControlContext.TwinStick,
                "Move Forward / Up", "Twin-Stick upward movement (release key to stop)", KeyCode.W),
            new ControlCommandMeta(ControlCommand.TwinStickMoveDown, ControlContext.TwinStick,
                "Move Backward / Down", "Twin-Stick downward movement (release key to stop)", KeyCode.S),
            new ControlCommandMeta(ControlCommand.TwinStickMoveLeft, ControlContext.TwinStick,
                "Move Left", "Twin-Stick left strafe", KeyCode.A),
            new ControlCommandMeta(ControlCommand.TwinStickMoveRight, ControlContext.TwinStick,
                "Move Right", "Twin-Stick right strafe", KeyCode.D),
            new ControlCommandMeta(ControlCommand.TwinStickShoot, ControlContext.TwinStick,
                "Basic Shot", "Hold/click to fire slow round projectile toward cursor", KeyCode.Mouse0),
            new ControlCommandMeta(ControlCommand.TwinStickAbility1, ControlContext.TwinStick,
                "Acquired Ability 1", "First acquired roguelike ability (default RMB)", KeyCode.Mouse1),
            new ControlCommandMeta(ControlCommand.TwinStickAbility2, ControlContext.TwinStick,
                "Acquired Ability 2", "Second acquired roguelike ability (default Shift)", KeyCode.LeftShift),
            new ControlCommandMeta(ControlCommand.TwinStickAbility3, ControlContext.TwinStick,
                "Acquired Ability 3", "Third acquired roguelike ability (default Q)", KeyCode.Q),
            new ControlCommandMeta(ControlCommand.TwinStickAbility4, ControlContext.TwinStick,
                "Acquired Ultimate", "Fourth acquired roguelike ability — Ultimate form (default E)", KeyCode.E),
        };

        private static readonly Dictionary<ControlCommand, ControlCommandMeta> ByCommand;

        static ControlCommandCatalog()
        {
            ByCommand = new Dictionary<ControlCommand, ControlCommandMeta>(All.Length);
            for (int i = 0; i < All.Length; i++)
            {
                ByCommand[All[i].Command] = All[i];
            }
        }

        public static bool TryGetMeta(ControlCommand cmd, out ControlCommandMeta meta) => ByCommand.TryGetValue(cmd, out meta);

        public static ControlCommandMeta GetMeta(ControlCommand cmd)
        {
            if (ByCommand.TryGetValue(cmd, out ControlCommandMeta meta)) return meta;
            return new ControlCommandMeta(cmd, ControlContext.Global, cmd.ToString(), string.Empty, KeyCode.None);
        }

        /// <summary>
        /// True when two control contexts can be active simultaneously and therefore cannot
        /// unambiguously share a key (except explicit contextual pairs like RMB Move + Attack).
        /// Mutually exclusive hero primary contexts (e.g. ConventionalMoba vs. TwinStick or FlightSteering)
        /// never overlap and can reuse keys such as S = Stop vs. S = Move Down.
        /// </summary>
        public static bool ContextsCanOverlap(ControlContext a, ControlContext b)
        {
            if (a == b) return true;
            if (a == ControlContext.Global || b == ControlContext.Global) return true;
            if ((a == ControlContext.ConventionalMoba && b == ControlContext.RtsMultiUnit) ||
                (a == ControlContext.RtsMultiUnit && b == ControlContext.ConventionalMoba))
            {
                return true;
            }
            return false;
        }

        public static string FormatContextLabel(ControlContext ctx)
        {
            switch (ctx)
            {
                case ControlContext.Global: return "Global";
                case ControlContext.ConventionalMoba: return "Conventional MOBA";
                case ControlContext.RtsMultiUnit: return "RTS Multi-Unit";
                case ControlContext.FlightSteering: return "Flight Steering";
                case ControlContext.TwinStick: return "Twin-Stick Roguelike";
                default: return ctx.ToString();
            }
        }

        public static string FormatKeyLabel(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.None: return "Unbound";
                case KeyCode.Mouse0: return "LMB";
                case KeyCode.Mouse1: return "RMB";
                case KeyCode.Mouse2: return "MMB";
                case KeyCode.Mouse3: return "Mouse4";
                case KeyCode.Mouse4: return "Mouse5";
                case KeyCode.Space: return "Space";
                case KeyCode.Return: return "Enter";
                case KeyCode.Escape: return "Esc";
                case KeyCode.Tab: return "Tab";
                case KeyCode.LeftShift:
                case KeyCode.RightShift: return "Shift";
                case KeyCode.LeftControl:
                case KeyCode.RightControl: return "Ctrl";
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt: return "Alt";
                case KeyCode.Alpha0: return "0";
                case KeyCode.Alpha1: return "1";
                case KeyCode.Alpha2: return "2";
                case KeyCode.Alpha3: return "3";
                case KeyCode.Alpha4: return "4";
                case KeyCode.Alpha5: return "5";
                case KeyCode.Alpha6: return "6";
                case KeyCode.Alpha7: return "7";
                case KeyCode.Alpha8: return "8";
                case KeyCode.Alpha9: return "9";
                default: return key.ToString();
            }
        }
    }

    /// <summary>
    /// Composable per-hero control specification separating physical bindings from abstract
    /// gameplay capabilities. Guarantees personal preferences can only enable supported optional
    /// controls and never override fundamental hero mechanics.
    /// </summary>
    public sealed class HeroControlProfile
    {
        private readonly HashSet<ControlCommand> requiredCommands;
        private readonly HashSet<ControlCommand> optionalCommands;
        private readonly HashSet<ControlCommand> unavailableCommands;
        private readonly Dictionary<LaneAbilitySlot, ControlCommand> abilitySlotCommands;
        private readonly Dictionary<ControlCommand, string> commandNotes;

        public string ProfileId { get; }
        public string DisplayName { get; }
        public string MovementSummary { get; }
        public ControlContext PrimaryContext { get; }
        public RtsControlMode RtsSupport { get; }
        public IReadOnlyCollection<ControlCommand> RequiredCommands => requiredCommands;
        public IReadOnlyCollection<ControlCommand> OptionalCommands => optionalCommands;
        public IReadOnlyCollection<ControlCommand> UnavailableCommands => unavailableCommands;
        public IReadOnlyDictionary<LaneAbilitySlot, ControlCommand> AbilitySlotCommands => abilitySlotCommands;
        public IReadOnlyDictionary<ControlCommand, string> CommandNotes => commandNotes;

        public bool SupportsStop =>
            !unavailableCommands.Contains(ControlCommand.Stop) &&
            (requiredCommands.Contains(ControlCommand.Stop) || optionalCommands.Contains(ControlCommand.Stop));

        public bool SupportsBasicAttack =>
            !unavailableCommands.Contains(ControlCommand.AttackOrder) &&
            !unavailableCommands.Contains(ControlCommand.AttackMove);

        public HeroControlProfile(
            string profileId,
            string displayName,
            string movementSummary,
            ControlContext primaryContext,
            RtsControlMode rtsSupport,
            IEnumerable<ControlCommand> required,
            IEnumerable<ControlCommand> optional,
            IEnumerable<ControlCommand> unavailable,
            IDictionary<LaneAbilitySlot, ControlCommand> abilitySlots,
            IDictionary<ControlCommand, string> notes)
        {
            ProfileId = profileId ?? "unknown";
            DisplayName = displayName ?? ProfileId;
            MovementSummary = movementSummary ?? string.Empty;
            PrimaryContext = primaryContext;
            RtsSupport = rtsSupport;
            requiredCommands = required != null ? new HashSet<ControlCommand>(required) : new HashSet<ControlCommand>();
            optionalCommands = optional != null ? new HashSet<ControlCommand>(optional) : new HashSet<ControlCommand>();
            unavailableCommands = unavailable != null ? new HashSet<ControlCommand>(unavailable) : new HashSet<ControlCommand>();
            abilitySlotCommands = abilitySlots != null
                ? new Dictionary<LaneAbilitySlot, ControlCommand>(abilitySlots)
                : new Dictionary<LaneAbilitySlot, ControlCommand>();
            commandNotes = notes != null
                ? new Dictionary<ControlCommand, string>(notes)
                : new Dictionary<ControlCommand, string>();
        }

        public bool IsUnavailable(ControlCommand cmd) => unavailableCommands.Contains(cmd);
        public bool IsOptional(ControlCommand cmd) => optionalCommands.Contains(cmd);
        public bool IsRequired(ControlCommand cmd) => requiredCommands.Contains(cmd);

        public bool IsRtsEnabled(PlayerSettingsData settings)
        {
            if (RtsSupport == RtsControlMode.Required) return true;
            if (RtsSupport == RtsControlMode.Unavailable) return false;
            return settings != null && settings.enableOptionalRtsControls;
        }

        public bool IsCommandAvailable(ControlCommand cmd, PlayerSettingsData settings)
        {
            if (unavailableCommands.Contains(cmd)) return false;

            ControlCommandMeta meta = ControlCommandCatalog.GetMeta(cmd);
            if (meta.Context == ControlContext.Global) return true;
            if (requiredCommands.Contains(cmd)) return true;

            if (meta.Context == ControlContext.RtsMultiUnit)
            {
                return (optionalCommands.Contains(cmd) || RtsSupport == RtsControlMode.Required) && IsRtsEnabled(settings);
            }

            return false;
        }

        public bool TryGetAbilityCommand(LaneAbilitySlot slot, out ControlCommand cmd) =>
            abilitySlotCommands.TryGetValue(slot, out cmd);

        public string GetCommandNote(ControlCommand cmd) =>
            commandNotes.TryGetValue(cmd, out string note) ? note : string.Empty;

        public HeroControlProfile Without(params ControlCommand[] commands)
        {
            var req = new HashSet<ControlCommand>(requiredCommands);
            var opt = new HashSet<ControlCommand>(optionalCommands);
            var unavail = new HashSet<ControlCommand>(unavailableCommands);
            for (int i = 0; i < commands.Length; i++)
            {
                req.Remove(commands[i]);
                opt.Remove(commands[i]);
                unavail.Add(commands[i]);
            }
            return new HeroControlProfile(ProfileId, DisplayName, MovementSummary, PrimaryContext, RtsSupport,
                req, opt, unavail, abilitySlotCommands, commandNotes);
        }

        public HeroControlProfile WithRtsSupport(RtsControlMode mode)
        {
            var req = new HashSet<ControlCommand>(requiredCommands);
            var opt = new HashSet<ControlCommand>(optionalCommands);
            var unavail = new HashSet<ControlCommand>(unavailableCommands);

            ControlCommand[] rtsCmds =
            {
                ControlCommand.RtsSelectHero,
                ControlCommand.RtsSelectAllUnits,
                ControlCommand.RtsCycleUnit,
                ControlCommand.RtsGroup1,
                ControlCommand.RtsGroup2,
                ControlCommand.RtsGroup3,
                ControlCommand.RtsGroup4,
            };

            for (int i = 0; i < rtsCmds.Length; i++)
            {
                ControlCommand c = rtsCmds[i];
                req.Remove(c);
                opt.Remove(c);
                unavail.Remove(c);

                if (mode == RtsControlMode.Required) req.Add(c);
                else if (mode == RtsControlMode.Optional) opt.Add(c);
                else unavail.Add(c);
            }

            return new HeroControlProfile(ProfileId, DisplayName, MovementSummary, PrimaryContext, mode,
                req, opt, unavail, abilitySlotCommands, commandNotes);
        }

        public HeroControlProfile WithNote(ControlCommand cmd, string note)
        {
            var nextNotes = new Dictionary<ControlCommand, string>(commandNotes)
            {
                [cmd] = note ?? string.Empty
            };
            return new HeroControlProfile(ProfileId, DisplayName, MovementSummary, PrimaryContext, RtsSupport,
                requiredCommands, optionalCommands, unavailableCommands, abilitySlotCommands, nextNotes);
        }

        public HeroControlProfile WithIdentity(string profileId, string displayName, string movementSummary = null)
        {
            return new HeroControlProfile(
                profileId,
                displayName,
                movementSummary ?? MovementSummary,
                PrimaryContext,
                RtsSupport,
                requiredCommands,
                optionalCommands,
                unavailableCommands,
                abilitySlotCommands,
                commandNotes);
        }
    }

    /// <summary>
    /// Reusable control-profile catalog for all active playable heroes plus future architectural
    /// archetypes (Kobold King, Airplane Fighter, Twin-Stick Shooter Roguelike).
    /// </summary>
    public static class HeroControlCatalog
    {
        private static readonly HeroControlProfile OgreProfile;
        private static readonly HeroControlProfile MiravelProfile;
        private static readonly HeroControlProfile SwordsmanProfile;
        private static readonly HeroControlProfile LaserProfile;

        private static readonly HeroControlProfile KoboldKingPreview;
        private static readonly HeroControlProfile AirplaneFighterPreview;
        private static readonly HeroControlProfile TwinStickPreview;

        static HeroControlCatalog()
        {
            HeroControlProfile conventionalBase = CreateConventionalMobaBase(
                "ConventionalMobaBase",
                "Conventional MOBA",
                "Click-to-Move • Basic Attack • Attack-Move • Stop");

            OgreProfile = conventionalBase
                .WithIdentity("Ogre", "Ogre", "Click-to-Move • No Basic Attacks • Club Ability Kit")
                .Without(ControlCommand.AttackOrder, ControlCommand.AttackMove)
                .WithNote(ControlCommand.AttackOrder, "Unavailable — Ogre cannot perform basic attacks (right-clicking an enemy moves toward them).")
                .WithNote(ControlCommand.AttackMove, "Unavailable — Attack-Move requires basic attacks.")
                .WithNote(ControlCommand.Stop, "Stops movement; committed club casts and [R] Rampage channel cannot be cancelled.")
                .WithNote(ControlCommand.CastQ, "Queues aimed Slam when pressed during [R] channel.")
                .WithNote(ControlCommand.CastW, "Queues aimed Sweep when pressed during [R] channel.");

            MiravelProfile = conventionalBase
                .WithIdentity("Miravel", "Miravel (Hero Luís)", "Click-to-Move • Ranged Staff • Orb & Decoy Kit")
                .WithNote(ControlCommand.Stop, "Cancels movement, basic wind-ups, and [R] Sanctum channel.")
                .WithNote(ControlCommand.RtsSelectAllUnits, "When RTS is enabled, lets you select & direct your [W] Shimmer Decoy.");

            SwordsmanProfile = conventionalBase
                .WithIdentity("Swordsman", "Old Man Swordsman", "Click-to-Move • Twin-Blade Basics • Execute Dash")
                .WithNote(ControlCommand.Stop, "Cancels movement and basic wind-ups; committed [Q] arc and [R] dash finish normally.");

            LaserProfile = conventionalBase
                .WithIdentity("LaserShooter", "Chain Laser Shooter", "Click-to-Move • Chain Laser Basics • Mobile [R]")
                .WithNote(ControlCommand.Stop, "Stops movement (even during mobile [R] beam) and cancels basic wind-ups without interrupting committed [Q] or [R] beam.")
                .WithNote(ControlCommand.RtsSelectAllUnits, "[E] Laser Decoy stays stationary and non-controllable even when RTS is enabled.");

            KoboldKingPreview = conventionalBase
                .WithIdentity("KoboldKing", "Kobold King (Future)", "Click-to-Move + Required RTS Servant Control")
                .WithRtsSupport(RtsControlMode.Required)
                .WithNote(ControlCommand.RtsSelectAllUnits, "Required by Kobold King — always enabled even if Optional RTS Controls are off.")
                .WithNote(ControlCommand.CastR, "Succession requires currently selecting one of your kobold servants.");

            AirplaneFighterPreview = new HeroControlProfile(
                profileId: "AirplaneFighter",
                displayName: "Airplane Fighter (Future)",
                movementSummary: "Continuous Cursor-Steered Flight (No Stop Command)",
                primaryContext: ControlContext.FlightSteering,
                rtsSupport: RtsControlMode.Unavailable,
                required: new[]
                {
                    ControlCommand.FlightCastQ,
                    ControlCommand.FlightCastW,
                    ControlCommand.FlightCastE,
                    ControlCommand.FlightCastR,
                },
                optional: Array.Empty<ControlCommand>(),
                unavailable: new[]
                {
                    ControlCommand.Stop,
                    ControlCommand.Move,
                    ControlCommand.AttackOrder,
                    ControlCommand.AttackMove,
                    ControlCommand.RtsSelectHero,
                    ControlCommand.RtsSelectAllUnits,
                    ControlCommand.RtsCycleUnit,
                    ControlCommand.RtsGroup1,
                    ControlCommand.RtsGroup2,
                    ControlCommand.RtsGroup3,
                    ControlCommand.RtsGroup4,
                },
                abilitySlots: new Dictionary<LaneAbilitySlot, ControlCommand>
                {
                    { LaneAbilitySlot.Q, ControlCommand.FlightCastQ },
                    { LaneAbilitySlot.W, ControlCommand.FlightCastW },
                    { LaneAbilitySlot.E, ControlCommand.FlightCastE },
                    { LaneAbilitySlot.R, ControlCommand.FlightCastR },
                },
                notes: new Dictionary<ControlCommand, string>
                {
                    { ControlCommand.Stop, "Unavailable — continuous flight cannot stop in mid-air (parks at base only)." },
                });

            TwinStickPreview = new HeroControlProfile(
                profileId: "TwinStickShooter",
                displayName: "Twin-Stick Shooter Roguelike (Future)",
                movementSummary: "WASD Movement + Mouse Aim Shoot (Release Keys to Stop)",
                primaryContext: ControlContext.TwinStick,
                rtsSupport: RtsControlMode.Unavailable,
                required: new[]
                {
                    ControlCommand.TwinStickMoveUp,
                    ControlCommand.TwinStickMoveDown,
                    ControlCommand.TwinStickMoveLeft,
                    ControlCommand.TwinStickMoveRight,
                    ControlCommand.TwinStickShoot,
                    ControlCommand.TwinStickAbility1,
                    ControlCommand.TwinStickAbility2,
                    ControlCommand.TwinStickAbility3,
                    ControlCommand.TwinStickAbility4,
                },
                optional: Array.Empty<ControlCommand>(),
                unavailable: new[]
                {
                    ControlCommand.Stop,
                    ControlCommand.Move,
                    ControlCommand.AttackOrder,
                    ControlCommand.AttackMove,
                    ControlCommand.RtsSelectHero,
                    ControlCommand.RtsSelectAllUnits,
                    ControlCommand.RtsCycleUnit,
                    ControlCommand.RtsGroup1,
                    ControlCommand.RtsGroup2,
                    ControlCommand.RtsGroup3,
                    ControlCommand.RtsGroup4,
                },
                abilitySlots: new Dictionary<LaneAbilitySlot, ControlCommand>
                {
                    { LaneAbilitySlot.Q, ControlCommand.TwinStickAbility1 },
                    { LaneAbilitySlot.W, ControlCommand.TwinStickAbility2 },
                    { LaneAbilitySlot.E, ControlCommand.TwinStickAbility3 },
                    { LaneAbilitySlot.R, ControlCommand.TwinStickAbility4 },
                },
                notes: new Dictionary<ControlCommand, string>
                {
                    { ControlCommand.Stop, "Unavailable — release WASD movement keys to stop moving." },
                    { ControlCommand.TwinStickMoveDown, "S moves backward in Twin-Stick context without conflicting with Conventional MOBA Stop." },
                });
        }

        public static HeroControlProfile CreateConventionalMobaBase(string id, string displayName, string movementSummary)
        {
            return new HeroControlProfile(
                profileId: id,
                displayName: displayName,
                movementSummary: movementSummary,
                primaryContext: ControlContext.ConventionalMoba,
                rtsSupport: RtsControlMode.Optional,
                required: new[]
                {
                    ControlCommand.Move,
                    ControlCommand.AttackOrder,
                    ControlCommand.AttackMove,
                    ControlCommand.Stop,
                    ControlCommand.CastQ,
                    ControlCommand.CastW,
                    ControlCommand.CastE,
                    ControlCommand.CastR,
                },
                optional: new[]
                {
                    ControlCommand.RtsSelectHero,
                    ControlCommand.RtsSelectAllUnits,
                    ControlCommand.RtsCycleUnit,
                    ControlCommand.RtsGroup1,
                    ControlCommand.RtsGroup2,
                    ControlCommand.RtsGroup3,
                    ControlCommand.RtsGroup4,
                },
                unavailable: Array.Empty<ControlCommand>(),
                abilitySlots: new Dictionary<LaneAbilitySlot, ControlCommand>
                {
                    { LaneAbilitySlot.Q, ControlCommand.CastQ },
                    { LaneAbilitySlot.W, ControlCommand.CastW },
                    { LaneAbilitySlot.E, ControlCommand.CastE },
                    { LaneAbilitySlot.R, ControlCommand.CastR },
                },
                notes: new Dictionary<ControlCommand, string>());
        }

        public static HeroControlProfile ForHero(LaneHeroId hero)
        {
            switch (hero)
            {
                case LaneHeroId.Ogre: return OgreProfile;
                case LaneHeroId.Miravel: return MiravelProfile;
                case LaneHeroId.Swordsman: return SwordsmanProfile;
                case LaneHeroId.LaserShooter: return LaserProfile;
                default: return MiravelProfile;
            }
        }

        public static HeroControlProfile ForFutureArchetype(string archetypeKey)
        {
            if (string.Equals(archetypeKey, "KoboldKing", StringComparison.OrdinalIgnoreCase)) return KoboldKingPreview;
            if (string.Equals(archetypeKey, "AirplaneFighter", StringComparison.OrdinalIgnoreCase)) return AirplaneFighterPreview;
            if (string.Equals(archetypeKey, "TwinStickShooter", StringComparison.OrdinalIgnoreCase)) return TwinStickPreview;
            return null;
        }
    }
}
