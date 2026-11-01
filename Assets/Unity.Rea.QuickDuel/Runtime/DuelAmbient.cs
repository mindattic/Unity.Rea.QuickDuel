using System;
using System.Collections.Generic;
using Unity.Rea.QuickDuel.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Rea.QuickDuel
{
    // The arena's ambient effect (UQD-T49), as setup_arena_properties.gml placed one emitter per arena: sand blowing
    // across the Wastes of Nod, butterflies over Pastel Fields, snow on the Frozen Tundra, fog drifting through the
    // Fumaroles and flies over the Arid Plains (AmbientField has every figure). The original's rain, leaf and firefly
    // emitters, which no arena created, are built too: an arena's data can name them, and Override plays any of the eight.
    // Presentation only: it reads the arena on screen and the clock and decides nothing.
    //
    // It steps at the original's 60 a second counted off the controller's clock, frozen while the duel waits on the first
    // strike (the original's action mode), and starts afresh whenever another arena takes the screen (under the loading
    // fade's full image, as the original's emitters began with the room). Each layer is one element that draws its
    // particles as textured quads (one mesh per sprite), turned and mirrored about each sprite's GameMaker origin, at the
    // original's pixel scale, spread over the real screen's width like the splatters and down the stage's height like the
    // corner arts. Layers keep the original's depths: sand, snow, fog, rain and leaves (depth -1) over the fighters (0) and
    // under the splatters (-100); butterflies, flies and fireflies (0) just behind the fighters, over the blood drops (1).
    //
    // It starts after the other layers' owners (DefaultExecutionOrder), so DuelBlood's splatters, which it puts straight
    // after the stage, are already there when the front layer goes in between them and the stage.
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(DuelController), typeof(DuelView))]
    public sealed class DuelAmbient : MonoBehaviour
    {
        // Every sprite the eight effects draw. Written by DuelSceneBuilder.
        [SerializeField] private List<AmbientLook> looks = new List<AmbientLook>();

        // A stall (a scene load) runs the field on by at most this many steps at once.
        private const int MaxSteps = 60;

        private readonly Dictionary<string, AmbientLook> looksById = new Dictionary<string, AmbientLook>();
        private readonly Dictionary<Sprite, List<AmbientParticle>> batches = new Dictionary<Sprite, List<AmbientParticle>>();
        private readonly System.Random random = new System.Random();
        private DuelController controller;
        private DuelView view;
        private ArenaBackdrop shownArena;
        private AmbientKind? shownOverride;
        private bool started;
        private long fieldTick;

        public void SetLooks(IEnumerable<AmbientLook> ambientLooks)
        {
            looks = new List<AmbientLook>(ambientLooks ?? throw new ArgumentNullException(nameof(ambientLooks)));
            looksById.Clear();
        }

        public IReadOnlyList<AmbientLook> Looks => looks;

        // The look for a sprite id; null when the scene carries none.
        public AmbientLook LookOf(string id)
        {
            if (looksById.Count != looks.Count)
            {
                looksById.Clear();
                foreach (var look in looks)
                    if (look != null && !string.IsNullOrEmpty(look.Id))
                        looksById[look.Id] = look;
            }
            return id != null && looksById.TryGetValue(id, out var found) ? found : null;
        }

        // Plays this effect instead of the arena's own (any of the eight, or None for still air); null plays the arena's.
        public AmbientKind? Override { get; set; }

        // The effect now playing; null before the first frame.
        public AmbientField Field { get; private set; }

        // The effect the arena on screen (or the override) asks for.
        public AmbientKind Wanted => Override ?? view?.Arena?.Ambient ?? AmbientKind.None;

        // Whether the field is frozen this frame: the duel waits on the first strike.
        public bool Frozen => controller != null && controller.Phase == DuelPhase.Strike;

        // The two layers, for tests and screenshots: behind the fighters (depth 0) and over them (depth -1).
        public VisualElement BackLayer { get; private set; }
        public VisualElement FrontLayer { get; private set; }

        // The particles each layer drew the last time it painted.
        public int BackDrawn { get; private set; }
        public int FrontDrawn { get; private set; }

        private void Start()
        {
            controller = GetComponent<DuelController>();
            view = GetComponent<DuelView>();

            BackLayer = Layer("AmbientBack", front: false);
            var fighter = view.FighterElement(Side.Left)?.parent;
            var at = fighter != null && fighter.parent == view.Stage ? view.Stage.IndexOf(fighter) : 0;
            view.Stage.Insert(at, BackLayer);

            FrontLayer = Layer("AmbientFront", front: true);
            view.Root.Insert(view.Root.IndexOf(view.Stage) + 1, FrontLayer);
            started = true;
            Advance();
        }

        private long TickNow => (long)Math.Floor(controller.Now * AmbientField.TicksPerSecond);

        private void Update()
        {
            if (!started) return;
            Advance();
            BackLayer.MarkDirtyRepaint();
            FrontLayer.MarkDirtyRepaint();
        }

        // Starts the arena's effect afresh when the arena (or the override) changes, then runs it on to the clock's step.
        private void Advance()
        {
            var now = TickNow;
            var arena = view.Arena;
            if (Field == null || !ReferenceEquals(arena, shownArena) || Override != shownOverride)
            {
                shownArena = arena;
                shownOverride = Override;
                Field = new AmbientField(Wanted, random);
                fieldTick = now;
                return;
            }
            var steps = now - fieldTick;
            fieldTick = now;
            if (steps > 0)
                Field.Advance((int)Math.Min(steps, MaxSteps), Frozen);
        }

        private VisualElement Layer(string name, bool front)
        {
            var layer = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute;
            layer.style.left = 0;
            layer.style.top = 0;
            layer.style.right = 0;
            layer.style.bottom = 0;
            layer.generateVisualContent += context =>
            {
                var drawn = Paint(context, layer, front);
                if (front) FrontDrawn = drawn;
                else BackDrawn = drawn;
            };
            return layer;
        }

        // Where a room position lands in panel space: spread over the screen's width, down the stage's height.
        public Vector2 PanelPosition(double x, double y)
        {
            var root = view.Root.layout;
            return new Vector2((float)(x / AmbientField.RoomWidth) * root.width,
                root.height / 2f - DuelView.StageHeight / 2f + (float)y * DuelView.PixelScale);
        }

        // Draws this layer's particles, one mesh per sprite; returns how many it drew.
        private int Paint(MeshGenerationContext context, VisualElement layer, bool front)
        {
            var field = Field;
            if (field == null || AmbientKinds.InFront(field.Kind) != front || field.Particles.Count == 0)
                return 0;

            foreach (var batch in batches.Values)
                batch.Clear();
            foreach (var particle in field.Particles)
            {
                var look = LookOf(particle.Sprite);
                if (look == null || look.Frames.Count == 0) continue;
                var sprite = look.Frames[particle.Frame % look.Frames.Count];
                if (sprite == null) continue;
                if (!batches.TryGetValue(sprite, out var batch))
                    batches[sprite] = batch = new List<AmbientParticle>();
                batch.Add(particle);
            }

            // Room positions are worked out in Root's space, then moved into the layer's own.
            var offset = view.Root.ChangeCoordinatesTo(layer, Vector2.zero);
            var drawn = 0;
            foreach (var pair in batches)
            {
                var particles = pair.Value;
                if (particles.Count == 0) continue;
                var sprite = pair.Key;
                var look = LookOf(particles[0].Sprite);
                var texture = sprite.texture;
                var rect = sprite.rect;
                var uv0 = new Vector2(rect.xMin / texture.width, rect.yMin / texture.height);
                var uv1 = new Vector2(rect.xMax / texture.width, rect.yMax / texture.height);
                var mesh = context.Allocate(particles.Count * 4, particles.Count * 6, texture);
                var region = mesh.uvRegion;
                var vertex = 0;
                foreach (var particle in particles)
                {
                    var at = PanelPosition(particle.X, particle.Y) + offset;
                    var tint = new Color32(255, 255, 255, (byte)Mathf.RoundToInt((float)particle.Alpha * 255f));
                    var radians = particle.Angle * Math.PI / 180d;
                    var cos = (float)Math.Cos(radians);
                    var sin = (float)Math.Sin(radians);
                    var mirror = particle.Mirrored ? -1f : 1f;
                    float left = -look.Origin.x, top = -look.Origin.y, right = look.Size.x - look.Origin.x, bottom = look.Size.y - look.Origin.y;
                    Corner(mesh, region, at, left, top, mirror, cos, sin, new Vector2(uv0.x, uv1.y), tint);
                    Corner(mesh, region, at, right, top, mirror, cos, sin, new Vector2(uv1.x, uv1.y), tint);
                    Corner(mesh, region, at, right, bottom, mirror, cos, sin, new Vector2(uv1.x, uv0.y), tint);
                    Corner(mesh, region, at, left, bottom, mirror, cos, sin, new Vector2(uv0.x, uv0.y), tint);
                    // Clockwise on screen; a mirrored quad turns the other way, so its triangles are wound back.
                    if (particle.Mirrored)
                        Indices(mesh, vertex, 0, 2, 1, 0, 3, 2);
                    else
                        Indices(mesh, vertex, 0, 1, 2, 0, 2, 3);
                    vertex += 4;
                    drawn++;
                }
            }
            return drawn;
        }

        // One corner of a sprite: (x, y) original pixels from its origin, mirrored, turned counter-clockwise by GameMaker's
        // angle, scaled to the stage and put at the particle's place.
        private static void Corner(MeshWriteData mesh, Rect region, Vector2 at, float x, float y, float mirror, float cos, float sin,
            Vector2 uv, Color32 tint)
        {
            x *= mirror;
            var turnedX = x * cos + y * sin;
            var turnedY = -x * sin + y * cos;
            mesh.SetNextVertex(new Vertex
            {
                position = new Vector3(at.x + turnedX * DuelView.PixelScale, at.y + turnedY * DuelView.PixelScale, Vertex.nearZ),
                tint = tint,
                uv = new Vector2(region.x + uv.x * region.width, region.y + uv.y * region.height),
            });
        }

        private static void Indices(MeshWriteData mesh, int first, int a, int b, int c, int d, int e, int f)
        {
            mesh.SetNextIndex((ushort)(first + a));
            mesh.SetNextIndex((ushort)(first + b));
            mesh.SetNextIndex((ushort)(first + c));
            mesh.SetNextIndex((ushort)(first + d));
            mesh.SetNextIndex((ushort)(first + e));
            mesh.SetNextIndex((ushort)(first + f));
        }
    }
}
