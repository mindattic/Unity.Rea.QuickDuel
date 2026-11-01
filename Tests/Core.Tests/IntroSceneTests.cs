using System;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;

namespace Unity.Rea.QuickDuel.Core.Tests
{
    // The intro room (UQD-T44): obj_cloud_parent's drifting clouds, obj_typewriter_intro's story and obj_fade_intro's
    // loading fade, stepped at the original's 60 a second.
    public class IntroSceneTests
    {
        private const string Story = "For centuries.\nThe end.";

        [Test]
        public void Twelve_clouds_are_placed_at_random_across_and_around_the_room()
        {
            for (var seed = 0; seed < 50; seed++)
            {
                var scene = new IntroScene(Story, new Random(seed));
                Assert.That(scene.Clouds.Select(c => c.Kind), Is.EqualTo(new[] { 0, 1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3 }));
                foreach (var cloud in scene.Clouds)
                {
                    Assert.That(cloud.X, Is.InRange(-128, 768), "irandom_range(-sprite_width / 2, room_width + sprite_width / 2)");
                    Assert.That(cloud.Y, Is.InRange(-24, 456), "irandom_range(-sprite_height / 2, room_height - sprite_height / 2)");
                }
            }
        }

        [Test]
        public void Clouds_drift_left_a_pixel_a_step_and_wrap_back_in_from_the_right()
        {
            var scene = new IntroScene(new string('x', 1000), new Random(3));
            var start = scene.Clouds.Select(c => c.X).ToArray();
            scene.Step();
            for (var i = 0; i < start.Length; i++)
                Assert.That(scene.Clouds[i].X, Is.EqualTo(start[i] - 1));

            var cloud = scene.Clouds[0];
            var wrapped = false;
            for (var step = 0; step < 2000 && !scene.Finished; step++)
            {
                var before = cloud.X;
                scene.Step();
                if (cloud.X > before)
                {
                    wrapped = true;
                    Assert.That(before, Is.EqualTo(-257), "the step it is found past -sprite_width");
                    Assert.That(cloud.X, Is.InRange(639, 1279), "irandom_range(room_width, room_width * 2), then moved");
                    Assert.That(cloud.Y, Is.InRange(-24, 456));
                    break;
                }
                Assert.That(cloud.X, Is.EqualTo(before - 1));
            }
            Assert.That(wrapped, Is.True);
        }

        [Test]
        public void The_room_opens_under_the_first_loading_image_fading_out_at_a_hundredth_a_step()
        {
            var scene = new IntroScene(Story, new Random(1));
            Assert.That(scene.Fade.Image, Is.Zero, "globals.gml's fade_asset = spr_loading1");
            Assert.That(scene.Fade.Alpha, Is.EqualTo(1));
            scene.Step();
            Assert.That(scene.Fade.Alpha, Is.EqualTo(0.99).Within(1e-12));
            scene.RunTo(50);
            Assert.That(scene.Fade.Alpha, Is.EqualTo(0.5).Within(1e-9));
            scene.RunTo(101);
            Assert.That(scene.Fade.Mode, Is.EqualTo(LoadingFadeMode.Off));
            Assert.That(scene.Fade.DrawnAlpha, Is.Zero);
        }

        [Test]
        public void The_story_types_a_character_every_four_steps_from_three_seconds_in()
        {
            var scene = new IntroScene(Story, new Random(1));
            scene.RunTo(179);
            Assert.That(scene.TypewriterShown, Is.False);
            scene.RunTo(180);
            Assert.That(scene.TypewriterShown, Is.True, "obj_intro's alarm[0] = 3 * room_speed");
            Assert.That(scene.Typed, Is.Zero);
            scene.RunTo(183);
            Assert.That(scene.Typed, Is.Zero);
            scene.RunTo(184);
            Assert.That(scene.TypedText, Is.EqualTo("F"));
            scene.RunTo(187);
            Assert.That(scene.Typed, Is.EqualTo(1));
            scene.RunTo(188);
            Assert.That(scene.TypedText, Is.EqualTo("Fo"));
            Assert.That(IntroScene.StepTyping(2), Is.EqualTo(188));

            scene.RunTo(IntroScene.StepTyping(Story.Length));
            Assert.That(scene.TypedText, Is.EqualTo(Story), "the line break is typed like any other character");
            Assert.That(scene.Complete, Is.False);
            scene.RunTo(IntroScene.StepTyping(Story.Length + 1));
            Assert.That(scene.Complete, Is.True);
        }

        [Test]
        public void The_whole_story_holds_three_hundred_steps_then_a_random_loading_image_fades_in_and_the_menu_follows()
        {
            var images = new bool[LoadingFade.Images];
            for (var seed = 0; seed < 200; seed++)
            {
                var scene = new IntroScene(Story, new Random(seed));
                var fadingIn = IntroScene.StepFadingIn(Story.Length);
                Assert.That(fadingIn, Is.EqualTo(180 + 4 * (Story.Length + 1) + 300));
                scene.RunTo(fadingIn - 1);
                Assert.That(scene.Fade.Mode, Is.EqualTo(LoadingFadeMode.Off));
                scene.RunTo(fadingIn);
                Assert.That(scene.Fade.Mode, Is.EqualTo(LoadingFadeMode.FadeIn));
                Assert.That(scene.Fade.Alpha, Is.EqualTo(0.01).Within(1e-12), "from nothing, 0.01 a step");
                images[scene.Fade.Image] = true;

                scene.RunTo(fadingIn + 99);
                Assert.That(scene.Fade.Alpha, Is.GreaterThanOrEqualTo(1), "a hundred hundredths");
                Assert.That(scene.Finished, Is.False);
                scene.RunTo(fadingIn + 100);
                Assert.That(scene.Finished, Is.True, "room_goto(mainmenu) on the step after it is full");
                Assert.That(scene.Steps, Is.EqualTo(fadingIn + 100));
                scene.RunTo(fadingIn + 500);
                Assert.That(scene.Steps, Is.EqualTo(fadingIn + 100), "nothing runs once it has left");
            }
            Assert.That(images, Is.All.True, "irandom_range(1, 14): every loading image comes up");
        }

        [Test]
        public void A_click_starts_the_fade_to_the_menu_on_the_next_step_from_any_moment()
        {
            foreach (var at in new long[] { 0, 30, 250, 500 })
            {
                var scene = new IntroScene(Story, new Random(5));
                scene.RunTo(at);
                scene.Skip();
                scene.Step();
                Assert.That(scene.Fade.Mode, Is.EqualTo(LoadingFadeMode.FadeIn), $"clicked after step {at}");
                Assert.That(scene.Fade.Alpha, Is.EqualTo(0.01).Within(1e-12), "a fresh image from nothing");
                var typed = scene.Typed;
                scene.RunTo(at + 101);
                Assert.That(scene.Finished, Is.True);
                Assert.That(scene.Typed, Is.LessThanOrEqualTo(Story.Length));
                Assert.That(scene.Typed, Is.GreaterThanOrEqualTo(typed), "the room plays on under the fade");
            }
        }

        [Test]
        public void A_second_click_or_the_hold_running_out_does_not_restart_the_fade()
        {
            var scene = new IntroScene("ab", new Random(2));
            scene.RunTo(IntroScene.StepFadingIn(2) - 50);
            scene.Skip();
            scene.RunTo(IntroScene.StepFadingIn(2) + 10);
            var alpha = scene.Fade.Alpha;
            Assert.That(alpha, Is.EqualTo(0.6).Within(1e-9), "60 steps in, the hold's own fadein changed nothing");
            scene.Skip();
            scene.Step();
            Assert.That(scene.Fade.Alpha, Is.EqualTo(alpha + 0.01).Within(1e-12));
        }

        [Test]
        public void Steps_run_by_a_clock_reading_count_the_rooms_first_step_before_its_first_draw()
        {
            Assert.That(LoadingFade.StepsAt(-1), Is.Zero);
            Assert.That(LoadingFade.StepsAt(0), Is.EqualTo(1));
            Assert.That(LoadingFade.StepsAt(1.0 / 60 - 1e-9), Is.EqualTo(1));
            Assert.That(LoadingFade.StepsAt(3), Is.EqualTo(181));
            Assert.That(LoadingFade.StepsAt(double.NaN), Is.Zero);
        }

        [Test]
        public void An_arriving_scene_fades_the_handed_image_away_and_never_fades_back_in_by_itself()
        {
            var fade = new LoadingFade(7);
            Assert.That(fade.Image, Is.EqualTo(7));
            for (var i = 0; i < 100; i++) fade.Step(null);
            Assert.That(fade.Mode, Is.EqualTo(LoadingFadeMode.FadeOut));
            Assert.That(fade.DrawnAlpha, Is.LessThan(1e-9));
            fade.Step(null);
            Assert.That(fade.Mode, Is.EqualTo(LoadingFadeMode.Off));
            for (var i = 0; i < 500; i++) fade.Step(null);
            Assert.That(fade.Alpha, Is.Zero);
            Assert.That(fade.Finished, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => new LoadingFade(14));
        }
    }
}
