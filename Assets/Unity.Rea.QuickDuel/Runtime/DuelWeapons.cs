using System;
using System.Collections.Generic;
using Unity.Rea.QuickDuel.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Rea.QuickDuel
{
    // The loser's weapon thrown from the cut (UQD-T41): when a strike cuts one side down, as gameclock_aftermath.gml's
    // spawn_weapons did, the loser's weapon (with the Myrmidon's shield or the Ronin's second sword behind it) flies from
    // the centre of the fighters' line, spinning, and lands by the ground line with one of the four hit-ground sounds,
    // lying there as its ground sprite until the next round (WeaponToss). Presentation only: it reads the verdict, the
    // fighters on the field and the clock, and decides nothing.
    //
    // Everything steps at the original's 60 a second counted off the controller's clock, not rendered frames. obj_weapon
    // sits at depth -1: over the text (0) and the loser, level with the attacking winner and drawn over it, and under the
    // splatters (-100), the flash and the letterbox.
    [RequireComponent(typeof(DuelController), typeof(DuelView))]
    public sealed class DuelWeapons : MonoBehaviour
    {
        // spr_weapon_myrmidon_shield and its ground sprite, and sfx_hitground1 to 4 in order. Written by DuelSceneBuilder;
        // each fighter's own weapon comes with its FighterLook.
        [SerializeField] private Sprite shield;
        [SerializeField] private Sprite shieldGround;
        [SerializeField] private List<AudioClip> hitGround = new List<AudioClip>();

        // The original's weapon sprites are all 128 x 128, their origin at the centre (Data/assets.json).
        private const float OriginalSpriteSize = 128f;

        private DuelController controller;
        private DuelView view;
        private VisualElement layer;
        private AudioSource sounds;
        private readonly List<VisualElement> elements = new List<VisualElement>();
        private readonly List<bool> heard = new List<bool>();
        private readonly System.Random random = new System.Random();
        private FighterLook loserLook;
        private long since;

        public void SetAssets(Sprite shieldSprite, Sprite shieldGroundSprite, IEnumerable<AudioClip> hitGroundClips)
        {
            shield = shieldSprite;
            shieldGround = shieldGroundSprite;
            hitGround = new List<AudioClip>(hitGroundClips ?? throw new ArgumentNullException(nameof(hitGroundClips)));
        }

        public Sprite Shield => shield;
        public Sprite ShieldGround => shieldGround;
        public IReadOnlyList<AudioClip> HitGround => hitGround;

        // This round's throw; null before the first cut and once the next round has begun.
        public WeaponToss Toss { get; private set; }

        // Every hit-ground sound played (0 to 3: sfx_hitground1 to 4), in order, for tests and debugging.
        public List<int> Played { get; } = new List<int>();

        // The drawn layer and each piece's element (back to front), for tests and screenshots.
        public VisualElement Layer => layer;
        public VisualElement ElementOf(int piece) => piece >= 0 && piece < elements.Count ? elements[piece] : null;

        // The sprite piece `piece` shows now: its weapon (or shield) in the air, the ground sprite once it has landed.
        public Sprite SpriteOf(int piece)
        {
            if (Toss == null || piece < 0 || piece >= Toss.Pieces.Count) return null;
            var thrown = Toss.Pieces[piece];
            if (thrown.Kind == WeaponPiece.Shield)
                return thrown.Landed ? shieldGround : shield;
            return thrown.Landed ? loserLook?.GroundWeapon : loserLook?.Weapon;
        }

        private void Start()
        {
            controller = GetComponent<DuelController>();
            view = GetComponent<DuelView>();
            sounds = gameObject.AddComponent<AudioSource>();
            sounds.playOnAwake = false;

            layer = new VisualElement { name = "Weapons", pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute;
            layer.style.left = 0;
            layer.style.top = 0;
            layer.style.right = 0;
            layer.style.bottom = 0;
            view.Stage.Add(layer);

            controller.RoundStarted += OnRoundStarted;
            controller.Resolved += OnResolved;
            fade = GetComponent<DuelFade>();
            if (fade != null) fade.BattleReset += OnRoundStarted;
        }

        private DuelFade fade;

        private void OnDestroy()
        {
            if (fade != null) fade.BattleReset -= OnRoundStarted;
            if (controller == null) return;
            controller.RoundStarted -= OnRoundStarted;
            controller.Resolved -= OnResolved;
        }

        // The loading fade (UQD-T42) destroys every obj_weapon under its full image, or the next round, begun first, does.
        private void OnRoundStarted()
        {
            Toss = null;
            loserLook = null;
            Draw();
        }

        private void OnResolved(DuelResult verdict)
        {
            if (!WeaponToss.Throws(verdict))
                return;
            var loser = verdict.Winner.Value == Side.Left ? Side.Right : Side.Left;
            loserLook = view.FighterIn(loser);
            Toss = new WeaponToss(loserLook?.Id, loser, random);
            since = TickNow;
            heard.Clear();
            for (var i = 0; i < Toss.Pieces.Count; i++) heard.Add(false);
            // Depth -1: over the text and the fighters, the winner DuelView has just brought to the front among them.
            layer.BringToFront();
            Advance();
            Draw();
        }

        private long TickNow => (long)Math.Floor(controller.Now * WeaponToss.TicksPerSecond);

        private void Update()
        {
            Advance();
            Draw();
        }

        // Runs the throw on to the clock's current step, playing each piece's hit-ground sound as it lands.
        private void Advance()
        {
            if (Toss == null) return;
            Toss.AdvanceTo((int)Math.Min(int.MaxValue, TickNow - since));
            for (var i = 0; i < Toss.Pieces.Count; i++)
            {
                var piece = Toss.Pieces[i];
                if (!piece.Landed || heard[i]) continue;
                heard[i] = true;
                Played.Add(piece.Sound);
                var clip = piece.Sound < hitGround.Count ? hitGround[piece.Sound] : null;
                if (clip != null) sounds.PlayOneShot(clip);
            }
        }

        // Each piece centred on its room position on the stage, the original's pixels scaled up as the fighters are,
        // mirrored for a right loser and turned to its angle (GameMaker's counter-clockwise degrees).
        private void Draw()
        {
            var count = Toss?.Pieces.Count ?? 0;
            var size = OriginalSpriteSize * DuelView.PixelScale;
            while (elements.Count < count)
            {
                var element = new VisualElement { name = "Weapon", pickingMode = PickingMode.Ignore };
                element.style.position = Position.Absolute;
                element.style.width = size;
                element.style.height = size;
                element.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
                layer.Add(element);
                elements.Add(element);
            }
            for (var i = 0; i < elements.Count; i++)
            {
                var element = elements[i];
                if (i >= count)
                {
                    element.style.display = DisplayStyle.None;
                    continue;
                }
                var piece = Toss.Pieces[i];
                element.style.display = DisplayStyle.Flex;
                element.style.left = DuelView.StageWidth / 2f + (piece.X - WeaponToss.OriginX) * DuelView.PixelScale - size / 2f;
                element.style.top = piece.Y * DuelView.PixelScale - size / 2f;
                element.style.scale = new Scale(new Vector2(piece.XScale, 1f));
                element.style.rotate = new Rotate(new Angle(-piece.Angle, AngleUnit.Degree));
                var sprite = SpriteOf(i);
                element.style.backgroundImage = sprite != null ? new StyleBackground(sprite) : new StyleBackground(StyleKeyword.None);
            }
        }
    }
}
